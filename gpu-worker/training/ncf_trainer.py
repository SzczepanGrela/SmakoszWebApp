import csv
import io
import json
import logging
import time
from datetime import datetime
from pathlib import Path
from urllib.parse import urlparse

import httpx
import torch
import torch.nn as nn
from torch.utils.data import DataLoader, TensorDataset

from api.client import WorkerApiClient
from config import Settings
from handlers.protocol import JobMapping, ModelRequirement
from models.model_manager import ModelManager
from training.export_onnx import export_to_onnx, upload_onnx_to_r2, upload_mapping_to_r2

logger = logging.getLogger(__name__)

class NcfModel(nn.Module):
    def __init__(self, num_users: int, num_dishes: int, embedding_dim: int):
        super().__init__()
        self.user_embedding = nn.Embedding(num_users, embedding_dim)
        self.dish_embedding = nn.Embedding(num_dishes, embedding_dim)
        nn.init.xavier_uniform_(self.user_embedding.weight)
        nn.init.xavier_uniform_(self.dish_embedding.weight)

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
        for layer in self.mlp:
            if isinstance(layer, nn.Linear):
                nn.init.xavier_uniform_(layer.weight)
                nn.init.zeros_(layer.bias)

    def forward(self, user_ids: torch.Tensor, dish_ids: torch.Tensor) -> torch.Tensor:
        user_emb = self.user_embedding(user_ids)
        dish_emb = self.dish_embedding(dish_ids)
        x = torch.cat([user_emb, dish_emb], dim=-1)
        return self.mlp(x).squeeze(-1)

class NcfTrainer:
    PHASE_NAME = "loading_ncf"
    MODELS: list[ModelRequirement] = []
    JOB_MAPPINGS = [JobMapping("ncf_training", "train")]

    def __init__(self, model_manager: ModelManager, settings: Settings, device: torch.device):
        self.model_manager = model_manager
        self.settings = settings
        self.device = device

    def _download_csv(self, csv_url: str) -> list[dict]:
        if csv_url.startswith("s3://"):
            parsed = urlparse(csv_url)
            bucket = parsed.netloc
            key = parsed.path.lstrip("/")
            s3 = self.model_manager.s3_client
            if s3 is None:
                raise RuntimeError("S3 client not configured, cannot download from s3:// URL")
            response = s3.get_object(Bucket=bucket, Key=key)
            text = response["Body"].read().decode("utf-8")
        else:
            resp = httpx.get(csv_url, timeout=120.0, follow_redirects=True)
            resp.raise_for_status()
            text = resp.text
        reader = csv.DictReader(io.StringIO(text))
        return list(reader)

    def _prepare_data(self, rows: list[dict]) -> tuple:
        if not rows:
            raise ValueError("Training dataset is empty")

        required = {"user_id", "dish_id", "rating"}
        actual = set(rows[0].keys())
        missing = required - actual
        if missing:
            raise ValueError(f"CSV missing required columns: {missing}. Found: {actual}")

        try:
            user_ids_raw = [int(r["user_id"]) for r in rows]
            dish_ids_raw = [int(r["dish_id"]) for r in rows]
            ratings = [float(r["rating"]) for r in rows]
        except (ValueError, KeyError) as e:
            raise ValueError(f"Invalid data in CSV: {e}") from e

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
            user_map,
            dish_map,
        )

    def _compute_baselines(
        self,
        user_ids: torch.Tensor,
        dish_ids: torch.Tensor,
        ratings: torch.Tensor,
        train_idx: torch.Tensor,
        val_idx: torch.Tensor,
    ) -> dict:
        train_r = ratings[train_idx]
        val_r = ratings[val_idx]

        optimal_constant = train_r.median().item()
        global_mean = train_r.mean().item()

        user_mean_acc: dict[int, list[float]] = {}
        for u, r in zip(user_ids[train_idx].tolist(), train_r.tolist()):
            user_mean_acc.setdefault(u, []).append(r)
        user_mean = {u: sum(v) / len(v) for u, v in user_mean_acc.items()}

        dish_mean_acc: dict[int, list[float]] = {}
        for d, r in zip(dish_ids[train_idx].tolist(), train_r.tolist()):
            dish_mean_acc.setdefault(d, []).append(r)
        dish_mean = {d: sum(v) / len(v) for d, v in dish_mean_acc.items()}

        val_truth = val_r.tolist()
        val_u_list = user_ids[val_idx].tolist()
        val_d_list = dish_ids[val_idx].tolist()

        def metrics(preds: list[float]) -> dict[str, float]:
            n = len(preds)
            acc = sum(1 for p, t in zip(preds, val_truth) if abs(p - t) < 0.5) / n
            rmse = (sum((p - t) ** 2 for p, t in zip(preds, val_truth)) / n) ** 0.5
            return {"acc": acc, "rmse": rmse}

        return {
            "constant": metrics([optimal_constant] * len(val_truth)),
            "user_mean": metrics([user_mean.get(u, global_mean) for u in val_u_list]),
            "dish_mean": metrics([dish_mean.get(d, global_mean) for d in val_d_list]),
            "user_dish_avg": metrics([
                (user_mean.get(u, global_mean) + dish_mean.get(d, global_mean)) / 2
                for u, d in zip(val_u_list, val_d_list)
            ]),
        }

    def _compute_ranking_metrics(
        self,
        model: nn.Module,
        val_users: torch.Tensor,
        val_dishes: torch.Tensor,
        val_ratings: torch.Tensor,
        k: int = 10,
    ) -> dict | None:
        import math
        import random

        model.eval()
        user_to_items: dict[int, list[tuple[int, float]]] = {}
        for u, d, r in zip(val_users.tolist(), val_dishes.tolist(), val_ratings.tolist()):
            user_to_items.setdefault(u, []).append((d, r))

        eligible = [u for u, items in user_to_items.items() if len(items) >= 5]
        if len(eligible) < 100:
            return None

        sample_users = random.sample(eligible, min(500, len(eligible)))

        ndcg_scores: list[float] = []
        hr_scores: list[float] = []
        with torch.no_grad():
            for u in sample_users:
                items = user_to_items[u]
                d_ids = torch.tensor([d for d, _ in items], dtype=torch.long, device=self.device)
                u_ids = torch.full_like(d_ids, u)
                preds = model(u_ids, d_ids).cpu().tolist()
                scored = sorted(zip(items, preds), key=lambda x: -x[1])
                top_k = scored[:k]
                hr = sum(1 for ((_, true_r), _) in top_k if true_r >= 8) / len(top_k)
                ideal = sorted([true_r for (_, true_r), _ in scored], reverse=True)[:k]
                dcg = sum((2 ** r - 1) / math.log2(i + 2) for i, ((_, r), _) in enumerate(top_k))
                idcg = sum((2 ** r - 1) / math.log2(i + 2) for i, r in enumerate(ideal))
                ndcg = dcg / idcg if idcg > 0 else 0
                hr_scores.append(hr)
                ndcg_scores.append(ndcg)

        return {
            "ndcg_at_10": sum(ndcg_scores) / len(ndcg_scores),
            "hr_at_10": sum(hr_scores) / len(hr_scores),
            "n_users": len(sample_users),
        }

    def train(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        csv_url = payload["csv_url"]
        epochs = int(payload["epochs"])
        batch_size = int(payload["batch_size"])
        learning_rate = float(payload["learning_rate"])
        embedding_dim = int(payload["embedding_dim"])

        start_time = time.monotonic()
        job_id = job["jobId"]

        logger.info("Starting NCF training: epochs=%d, batch_size=%d, lr=%s", epochs, batch_size, learning_rate)

        rows = self._download_csv(csv_url)
        logger.info("Loaded %d training samples", len(rows))

        user_ids, dish_ids, ratings, num_users, num_dishes, user_map, dish_map = self._prepare_data(rows)
        logger.info("Users: %d, Dishes: %d", num_users, num_dishes)

        n = len(ratings)
        if n < 10:
            raise ValueError(f"Dataset too small for training: {n} rows (minimum: 10)")
        indices = torch.randperm(n)
        split = int(n * 0.9)
        train_idx, val_idx = indices[:split], indices[split:]

        train_dataset = TensorDataset(user_ids[train_idx], dish_ids[train_idx], ratings[train_idx])
        val_dataset = TensorDataset(user_ids[val_idx], dish_ids[val_idx], ratings[val_idx])

        train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)
        val_loader = DataLoader(val_dataset, batch_size=batch_size)

        model = NcfModel(num_users, num_dishes, embedding_dim).to(self.device)
        optimizer = torch.optim.Adam(model.parameters(), lr=learning_rate, weight_decay=1e-4)
        scheduler = torch.optim.lr_scheduler.ReduceLROnPlateau(
            optimizer, mode="min", factor=0.5, patience=3, min_lr=1e-6,
        )
        criterion = nn.MSELoss()

        baselines = self._compute_baselines(user_ids, dish_ids, ratings, train_idx, val_idx)
        for name, m in baselines.items():
            logger.info("Baseline %-13s val_rmse=%.4f val_acc=%.4f", name, m["rmse"], m["acc"])
        target_rmse = baselines["user_dish_avg"]["rmse"]

        total_steps = epochs * len(train_loader)
        current_step = 0
        final_loss = 0.0
        val_accuracy = 0.0
        val_rmse = float("inf")
        best_val_rmse = float("inf")
        patience_counter = 0
        PATIENCE = 10
        last_ranking: dict | None = None
        epochs_completed = 0

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

            model.eval()
            val_errors = []
            val_sq_errors = []
            with torch.no_grad():
                for batch_users, batch_dishes, batch_ratings in val_loader:
                    batch_users = batch_users.to(self.device)
                    batch_dishes = batch_dishes.to(self.device)
                    batch_ratings = batch_ratings.to(self.device)

                    preds = model(batch_users, batch_dishes)
                    val_errors.append(torch.abs(preds - batch_ratings))
                    val_sq_errors.append((preds - batch_ratings) ** 2)

            all_errors = torch.cat(val_errors)
            val_accuracy = (all_errors < 0.5).float().mean().item()
            val_rmse = torch.cat(val_sq_errors).mean().sqrt().item()
            val_loss = val_rmse ** 2

            scheduler.step(val_loss)
            current_lr = optimizer.param_groups[0]["lr"]
            epochs_completed = epoch

            logger.info(
                "Epoch %d/%d - train_loss=%.4f val_rmse=%.4f val_acc=%.4f (baseline_rmse=%.4f) lr=%.6f",
                epoch, epochs, avg_loss, val_rmse, val_accuracy, target_rmse, current_lr,
            )

            if epoch % 5 == 0 or epoch == epochs:
                ranking = self._compute_ranking_metrics(
                    model, user_ids[val_idx], dish_ids[val_idx], ratings[val_idx],
                )
                if ranking:
                    last_ranking = ranking
                    logger.info(
                        "Epoch %d ranking: NDCG@10=%.4f HR@10=%.4f (n=%d)",
                        epoch, ranking["ndcg_at_10"], ranking["hr_at_10"], ranking["n_users"],
                    )

            api.report_progress(
                job_id,
                epoch=epoch,
                loss=round(avg_loss, 4),
                accuracy=round(val_accuracy, 4),
                learningRate=current_lr,
                currentStep=current_step,
                totalSteps=total_steps,
                message=f"Epoch {epoch}/{epochs} - val_rmse: {val_rmse:.4f}",
            )

            if val_rmse < best_val_rmse - 0.001:
                best_val_rmse = val_rmse
                patience_counter = 0
            else:
                patience_counter += 1
                if patience_counter >= PATIENCE:
                    logger.info(
                        "Early stopping at epoch %d (val_rmse plateaued for %d epochs)",
                        epoch, PATIENCE,
                    )
                    break

        version = datetime.utcnow().strftime("v%Y%m%d_%H%M%S")
        export_dir = Path("model_cache") / "ncf" / version
        onnx_path = export_to_onnx(model, export_dir, embedding_dim)

        mapping = {
            "user_map": {str(k): v for k, v in user_map.items()},
            "dish_map": {str(k): v for k, v in dish_map.items()},
        }
        mapping_path = export_dir / "mapping.json"
        mapping_path.write_text(json.dumps(mapping))
        logger.info("ID mapping exported to %s", mapping_path)

        model_url = ""
        if self.model_manager.s3_client is not None:
            if not self.settings.r2_bucket:
                raise RuntimeError(
                    "R2 bucket is not configured (Settings.r2_bucket is empty) "
                    "but s3_client is initialized. Set R2_BUCKET env var on the worker container."
                )
            key = upload_onnx_to_r2(
                self.model_manager.s3_client,
                self.settings.r2_bucket,
                onnx_path,
                version,
            )
            model_url = f"r2://{self.settings.r2_bucket}/{key}"

            upload_mapping_to_r2(
                self.model_manager.s3_client,
                self.settings.r2_bucket,
                mapping_path,
                version,
            )

        training_time = time.monotonic() - start_time

        final_ranking = last_ranking or self._compute_ranking_metrics(
            model, user_ids[val_idx], dish_ids[val_idx], ratings[val_idx],
        )

        return {
            "model_version": version,
            "model_url": model_url,
            "training_time_seconds": round(training_time, 1),
            "final_train_loss": round(final_loss, 4),
            "final_val_rmse": round(val_rmse, 4),
            "final_val_accuracy": round(val_accuracy, 4),
            "baseline_user_dish_rmse": round(baselines["user_dish_avg"]["rmse"], 4),
            "baseline_user_dish_acc": round(baselines["user_dish_avg"]["acc"], 4),
            "ndcg_at_10": round(final_ranking["ndcg_at_10"], 4) if final_ranking else None,
            "hr_at_10": round(final_ranking["hr_at_10"], 4) if final_ranking else None,
            "model_beats_baseline_rmse": val_rmse < baselines["user_dish_avg"]["rmse"],
            "epochs_completed": epochs_completed,
            "num_users": num_users,
            "num_dishes": num_dishes,
        }
