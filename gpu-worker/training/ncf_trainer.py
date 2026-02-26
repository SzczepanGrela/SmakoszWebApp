import csv
import io
import json
import logging
import time
from datetime import datetime
from pathlib import Path

import httpx
import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset

from api.client import WorkerApiClient
from config import Settings
from models.model_manager import ModelManager
from training.export_onnx import export_to_onnx, upload_onnx_to_r2

logger = logging.getLogger(__name__)

class NcfModel(nn.Module):
    """Neural Collaborative Filtering model."""

    def __init__(self, num_users: int, num_dishes: int, embedding_dim: int):
        super().__init__()
        self.user_embedding = nn.Embedding(num_users, embedding_dim)
        self.dish_embedding = nn.Embedding(num_dishes, embedding_dim)

        self.mlp = nn.Sequential(
            nn.Linear(embedding_dim * 2, 128),
            nn.ReLU(),
            nn.Dropout(0.2),
            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Dropout(0.1),
            nn.Linear(64, 32),
            nn.ReLU(),
            nn.Linear(32, 1),
        )

    def forward(self, user_ids: torch.Tensor, dish_ids: torch.Tensor) -> torch.Tensor:
        user_emb = self.user_embedding(user_ids)
        dish_emb = self.dish_embedding(dish_ids)
        x = torch.cat([user_emb, dish_emb], dim=-1)
        return self.mlp(x).squeeze(-1)

class NcfTrainer:
    """Neural Collaborative Filtering - training + ONNX export."""

    def __init__(self, model_manager: ModelManager, settings: Settings, device: torch.device):
        self.model_manager = model_manager
        self.settings = settings
        self.device = device

    def _download_csv(self, csv_url: str) -> list[dict]:
        resp = httpx.get(csv_url, timeout=120.0, follow_redirects=True)
        resp.raise_for_status()
        reader = csv.DictReader(io.StringIO(resp.text))
        return list(reader)

    def _prepare_data(self, rows: list[dict]) -> tuple:
        user_ids_raw = [int(r["user_id"]) for r in rows]
        dish_ids_raw = [int(r["dish_id"]) for r in rows]
        ratings = [float(r["rating"]) for r in rows]

        unique_users = sorted(set(user_ids_raw))
        unique_dishes = sorted(set(dish_ids_raw))
        user_map = {uid: idx for idx, uid in enumerate(unique_users)}
        dish_map = {did: idx for idx, did in enumerate(unique_dishes)}

        user_ids = [user_map[u] for u in user_ids_raw]
        dish_ids = [dish_map[d] for d in dish_ids_raw]

        return (
            torch.tensor(user_ids, dtype=torch.long),
            torch.tensor(dish_ids, dtype=torch.long),
            torch.tensor(ratings, dtype=torch.float32),
            len(unique_users),
            len(unique_dishes),
        )

    def train(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        csv_url = payload["csv_url"]
        epochs = int(payload.get("epochs", 20))
        batch_size = int(payload.get("batch_size", 256))
        learning_rate = float(payload.get("learning_rate", 0.001))
        embedding_dim = int(payload.get("embedding_dim", 64))

        start_time = time.monotonic()
        job_id = job["jobId"]

        logger.info("Starting NCF training: epochs=%d, batch_size=%d, lr=%s", epochs, batch_size, learning_rate)

        rows = self._download_csv(csv_url)
        logger.info("Loaded %d training samples", len(rows))

        user_ids, dish_ids, ratings, num_users, num_dishes = self._prepare_data(rows)
        logger.info("Users: %d, Dishes: %d", num_users, num_dishes)

        # Train/validation split (90/10)
        n = len(ratings)
        indices = torch.randperm(n)
        split = int(n * 0.9)
        train_idx, val_idx = indices[:split], indices[split:]

        train_dataset = TensorDataset(user_ids[train_idx], dish_ids[train_idx], ratings[train_idx])
        val_dataset = TensorDataset(user_ids[val_idx], dish_ids[val_idx], ratings[val_idx])

        train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)
        val_loader = DataLoader(val_dataset, batch_size=batch_size)

        model = NcfModel(num_users, num_dishes, embedding_dim).to(self.device)
        optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate)
        criterion = nn.MSELoss()

        total_steps = epochs * len(train_loader)
        current_step = 0
        final_loss = 0.0
        val_accuracy = 0.0

        for epoch in range(1, epochs + 1):
            model.train()
            epoch_loss = 0.0

            for batch_users, batch_dishes, batch_ratings in train_loader:
                batch_users = batch_users.to(self.device)
                batch_dishes = batch_dishes.to(self.device)
                batch_ratings = batch_ratings.to(self.device)

                optimizer.zero_grad()
                predictions = model(batch_users, batch_dishes)
                loss = criterion(predictions, batch_ratings)
                loss.backward()
                optimizer.step()

                epoch_loss += loss.item()
                current_step += 1

            avg_loss = epoch_loss / len(train_loader)
            final_loss = avg_loss

            # Validation
            model.eval()
            val_errors = []
            with torch.no_grad():
                for batch_users, batch_dishes, batch_ratings in val_loader:
                    batch_users = batch_users.to(self.device)
                    batch_dishes = batch_dishes.to(self.device)
                    batch_ratings = batch_ratings.to(self.device)

                    preds = model(batch_users, batch_dishes)
                    errors = torch.abs(preds - batch_ratings)
                    val_errors.append(errors)

            all_errors = torch.cat(val_errors)
            val_accuracy = (all_errors < 0.5).float().mean().item()

            logger.info(
                "Epoch %d/%d - loss: %.4f, val_accuracy: %.4f",
                epoch,
                epochs,
                avg_loss,
                val_accuracy,
            )

            api.report_progress(
                job_id,
                epoch=epoch,
                loss=round(avg_loss, 4),
                accuracy=round(val_accuracy, 4),
                learningRate=learning_rate,
                currentStep=current_step,
                totalSteps=total_steps,
                message=f"Epoch {epoch}/{epochs} - loss: {avg_loss:.4f}",
            )

        # Export to ONNX
        version = datetime.utcnow().strftime("v%Y%m%d_%H%M%S")
        export_dir = Path("model_cache") / "ncf" / version
        onnx_path = export_to_onnx(model, export_dir, embedding_dim)

        # Upload to R2
        model_url = ""
        if self.model_manager._s3 is not None:
            try:
                key = upload_onnx_to_r2(
                    self.model_manager._s3,
                    self.settings.r2_bucket,
                    onnx_path,
                    version,
                )
                model_url = f"r2://{self.settings.r2_bucket}/{key}"
            except Exception:
                logger.exception("Failed to upload ONNX to R2")

        training_time = time.monotonic() - start_time

        return {
            "model_url": model_url,
            "training_time_seconds": round(training_time, 1),
            "final_loss": round(final_loss, 4),
            "validation_accuracy": round(val_accuracy, 4),
            "epochs_completed": epochs,
            "num_users": num_users,
            "num_dishes": num_dishes,
        }
