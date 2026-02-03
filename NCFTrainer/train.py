"""
CFTrainer - Main Training Script

Neural Collaborative Filtering training with:
- PyTorch + ROCm (AMD GPU)
- TensorBoard logging
- Early stopping
- Checkpointing
- ONNX export
"""

import os
import sys
import argparse
import logging
from pathlib import Path
from datetime import datetime
import json

import torch
import torch.nn as nn
from torch.optim import Adam
from torch.optim.lr_scheduler import CosineAnnealingLR, ReduceLROnPlateau
from torch.utils.tensorboard import SummaryWriter
from torch.cuda.amp import GradScaler, autocast
from tqdm import tqdm
import numpy as np

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent))

from config import (
    DATABASE_CONFIG, MODEL_CONFIG, TRAINING_CONFIG,
    DEVICE_CONFIG, LOGGING_CONFIG, EXPORT_CONFIG,
    CHECKPOINTS_DIR, LOGS_DIR, EXPORTS_DIR, DATA_DIR
)
from data.data_loader import load_data_from_db, CFDataManager
from models.ncf import create_ncf_model

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# RANKING METRICS

def hit_ratio_at_k(ranked_list: np.ndarray, positive_item: int, k: int = 5) -> float:
    """
    Calculate Hit Ratio @ K

    Hit occurs if the positive item is in the top-K recommendations.

    Args:
        ranked_list: Array of item indices sorted by predicted score (descending)
        positive_item: Index of the ground-truth positive item
        k: Number of top recommendations to consider

    Returns:
        1.0 if positive item is in top-K, else 0.0
    """
    return 1.0 if positive_item in ranked_list[:k] else 0.0

def ndcg_at_k(ranked_list: np.ndarray, positive_item: int, k: int = 5) -> float:
    """
    Calculate Normalized Discounted Cumulative Gain @ K

    NDCG measures the position of the positive item in the ranking,
    giving higher weight to items ranked higher.

    Args:
        ranked_list: Array of item indices sorted by predicted score (descending)
        positive_item: Index of the ground-truth positive item
        k: Number of top recommendations to consider

    Returns:
        NDCG score between 0.0 and 1.0
    """
    # Find position of positive item (0-indexed)
    try:
        position = np.where(ranked_list[:k] == positive_item)[0][0]
        # DCG formula: 1 / log2(position + 2)
        # +2 because positions are 0-indexed and log2(1) = 0
        dcg = 1.0 / np.log2(position + 2)
        # IDCG (ideal DCG) is when positive item is at position 0
        idcg = 1.0 / np.log2(2)  # = 1.0
        return dcg / idcg
    except IndexError:
        # Positive item not in top-K
        return 0.0

class Trainer:
    """NCF Model Trainer"""

    def __init__(self,
                 model: nn.Module,
                 train_loader,
                 val_loader,
                 test_loader,
                 data_manager: CFDataManager,
                 device: str = 'cuda',
                 config: dict = None,
                 val_dataset=None,
                 test_dataset=None):

        self.model = model.to(device)
        self.train_loader = train_loader
        self.val_loader = val_loader
        self.test_loader = test_loader
        self.data_manager = data_manager
        self.device = device
        self.config = config or TRAINING_CONFIG

        # Store datasets for ranking evaluation
        self.val_dataset = val_dataset
        self.test_dataset = test_dataset

        # Loss function
        self.criterion = nn.MSELoss()

        # Optimizer
        self.optimizer = Adam(
            model.parameters(),
            lr=self.config['learning_rate'],
            weight_decay=self.config['weight_decay']
        )

        # Scheduler
        if self.config['scheduler'] == 'cosine':
            self.scheduler = CosineAnnealingLR(
                self.optimizer,
                T_max=self.config['epochs'],
                eta_min=1e-6
            )
        elif self.config['scheduler'] == 'plateau':
            self.scheduler = ReduceLROnPlateau(
                self.optimizer,
                mode='min',
                patience=3,
                factor=0.5
            )
        else:
            self.scheduler = None

        # Mixed precision
        self.use_amp = DEVICE_CONFIG.get('mixed_precision', False) and device == 'cuda'
        self.scaler = GradScaler() if self.use_amp else None

        # TensorBoard
        run_name = datetime.now().strftime("%Y%m%d_%H%M%S")
        self.writer = SummaryWriter(LOGS_DIR / run_name)

        # Early stopping
        self.best_val_loss = float('inf')
        self.patience_counter = 0

        # Metrics history
        self.history = {
            'train_loss': [], 'val_loss': [], 'test_loss': [],
            'train_rmse': [], 'val_rmse': [], 'test_rmse': [],
            'train_mae': [], 'val_mae': [], 'test_mae': [],
            'val_hr@5': [], 'test_hr@5': [],
            'val_ndcg@5': [], 'test_ndcg@5': [],
            'lr': []
        }

    def train_epoch(self, epoch: int) -> dict:
        """Train for one epoch"""
        self.model.train()

        total_loss = 0
        total_mae = 0
        n_batches = 0

        pbar = tqdm(self.train_loader, desc=f"Epoch {epoch+1}")

        for user_ids, item_ids, ratings in pbar:
            user_ids = user_ids.to(self.device)
            item_ids = item_ids.to(self.device)
            ratings = ratings.to(self.device)

            self.optimizer.zero_grad()

            if self.use_amp:
                with autocast():
                    predictions = self.model(user_ids, item_ids)
                    loss = self.criterion(predictions, ratings)

                self.scaler.scale(loss).backward()
                self.scaler.step(self.optimizer)
                self.scaler.update()
            else:
                predictions = self.model(user_ids, item_ids)
                loss = self.criterion(predictions, ratings)
                loss.backward()
                self.optimizer.step()

            total_loss += loss.item()
            total_mae += torch.abs(predictions - ratings).mean().item()
            n_batches += 1

            pbar.set_postfix({
                'loss': f"{loss.item():.4f}",
                'rmse': f"{np.sqrt(loss.item()):.4f}"
            })

        avg_loss = total_loss / n_batches
        avg_mae = total_mae / n_batches
        avg_rmse = np.sqrt(avg_loss)

        return {'loss': avg_loss, 'rmse': avg_rmse, 'mae': avg_mae}

    @torch.no_grad()
    def evaluate(self, loader, name: str = "val") -> dict:
        """
        Evaluate model on a dataset using running averages

        Memory-efficient implementation that doesn't store individual predictions.
        Computes metrics (RMSE, MAE) using accumulated sums instead of collecting all values.
        """
        self.model.eval()

        total_loss = 0.0
        total_mae = 0.0
        total_samples = 0

        for user_ids, item_ids, ratings in loader:
            user_ids = user_ids.to(self.device)
            item_ids = item_ids.to(self.device)
            ratings = ratings.to(self.device)

            batch_size = ratings.size(0)
            predictions = self.model(user_ids, item_ids)

            # Accumulate batch metrics weighted by batch size
            batch_loss = self.criterion(predictions, ratings)
            batch_mae = torch.abs(predictions - ratings).sum()

            total_loss += batch_loss.item() * batch_size
            total_mae += batch_mae.item()
            total_samples += batch_size

        # Calculate averages
        avg_loss = total_loss / total_samples
        avg_mae = total_mae / total_samples
        avg_rmse = np.sqrt(avg_loss)

        return {
            'loss': avg_loss,
            'rmse': avg_rmse,
            'mae': avg_mae
        }

    @torch.no_grad()
    def evaluate_ranking(self, dataset: 'RatingsDataset', k: int = 5,
                        n_negative: int = 99, rating_threshold: float = 0.7) -> dict:
        """
        Evaluate ranking metrics (HitRatio@K, NDCG@K)

        Simulates a ranking scenario for each user:
        - Takes ground-truth positive items (high ratings >= threshold)
        - Samples n_negative items the user hasn't interacted with
        - Ranks all items by predicted score
        - Calculates HitRatio and NDCG

        Args:
            dataset: RatingsDataset to evaluate
            k: Top-K for metrics (default 5)
            n_negative: Number of negative samples per positive (default 99)
            rating_threshold: Normalized rating threshold for positive items (default 0.7)

        Returns:
            Dict with 'hit_ratio' and 'ndcg' metrics
        """
        self.model.eval()

        # Group ratings by user
        user_items = {}  # user_idx -> list of (item_idx, rating)

        for i in range(len(dataset)):
            user_idx = dataset.user_ids[i].item()
            item_idx = dataset.item_ids[i].item()
            rating = dataset.ratings[i].item()

            if user_idx not in user_items:
                user_items[user_idx] = []
            user_items[user_idx].append((item_idx, rating))

        total_hr = 0.0
        total_ndcg = 0.0
        n_tests = 0

        rng = np.random.RandomState(42)

        for user_idx, items in user_items.items():
            # Get positive items (high ratings)
            positive_items = [item_idx for item_idx, rating in items if rating >= rating_threshold]

            if len(positive_items) == 0:
                continue

            # For each positive item, create a ranking test
            for pos_item in positive_items:
                # Sample negative items
                neg_items = self.data_manager.sample_negative_items(
                    user_idx, n_samples=n_negative, rng=rng
                )

                # Combine positive and negative items
                test_items = np.concatenate([[pos_item], neg_items])

                # Predict scores for all test items
                user_tensor = torch.full((len(test_items),), user_idx, dtype=torch.long).to(self.device)
                item_tensor = torch.LongTensor(test_items).to(self.device)

                scores = self.model(user_tensor, item_tensor).cpu().numpy()

                # Rank items by score (descending)
                ranked_indices = np.argsort(-scores)
                ranked_items = test_items[ranked_indices]

                # Calculate metrics
                hr = hit_ratio_at_k(ranked_items, pos_item, k=k)
                ndcg = ndcg_at_k(ranked_items, pos_item, k=k)

                total_hr += hr
                total_ndcg += ndcg
                n_tests += 1

        if n_tests == 0:
            logger.warning(f"No test cases generated for ranking evaluation (threshold={rating_threshold})")
            return {'hit_ratio': 0.0, 'ndcg': 0.0, 'n_tests': 0}

        avg_hr = total_hr / n_tests
        avg_ndcg = total_ndcg / n_tests

        return {
            'hit_ratio': avg_hr,
            'ndcg': avg_ndcg,
            'n_tests': n_tests
        }

    def train(self, epochs: int = None):
        """Full training loop"""
        epochs = epochs or self.config['epochs']
        logger.info(f"Starting training for {epochs} epochs")
        logger.info(f"Device: {self.device}, AMP: {self.use_amp}")

        for epoch in range(epochs):
            # Train
            train_metrics = self.train_epoch(epoch)
            self.history['train_loss'].append(train_metrics['loss'])
            self.history['train_rmse'].append(train_metrics['rmse'])
            self.history['train_mae'].append(train_metrics['mae'])

            # Validate
            val_metrics = self.evaluate(self.val_loader, "val")
            self.history['val_loss'].append(val_metrics['loss'])
            self.history['val_rmse'].append(val_metrics['rmse'])
            self.history['val_mae'].append(val_metrics['mae'])

            # Ranking metrics (evaluate every N epochs to save time)
            eval_ranking_interval = self.config.get('eval_ranking_every_n_epochs', 5)
            if self.val_dataset is not None and (epoch + 1) % eval_ranking_interval == 0:
                logger.info("  Evaluating ranking metrics...")
                ranking_metrics = self.evaluate_ranking(self.val_dataset, k=5)
                self.history['val_hr@5'].append(ranking_metrics['hit_ratio'])
                self.history['val_ndcg@5'].append(ranking_metrics['ndcg'])

                # Log ranking metrics to TensorBoard
                self.writer.add_scalar('Ranking/val_hr@5', ranking_metrics['hit_ratio'], epoch)
                self.writer.add_scalar('Ranking/val_ndcg@5', ranking_metrics['ndcg'], epoch)

                logger.info(
                    f"  Ranking: HR@5={ranking_metrics['hit_ratio']:.4f}, "
                    f"NDCG@5={ranking_metrics['ndcg']:.4f} "
                    f"({ranking_metrics['n_tests']} tests)"
                )
            else:
                # Append None for epochs without ranking evaluation
                if (epoch + 1) % eval_ranking_interval != 0:
                    self.history['val_hr@5'].append(None)
                    self.history['val_ndcg@5'].append(None)

            # Learning rate
            current_lr = self.optimizer.param_groups[0]['lr']
            self.history['lr'].append(current_lr)

            # Log to TensorBoard
            self.writer.add_scalar('Loss/train', train_metrics['loss'], epoch)
            self.writer.add_scalar('Loss/val', val_metrics['loss'], epoch)
            self.writer.add_scalar('RMSE/train', train_metrics['rmse'], epoch)
            self.writer.add_scalar('RMSE/val', val_metrics['rmse'], epoch)
            self.writer.add_scalar('MAE/train', train_metrics['mae'], epoch)
            self.writer.add_scalar('MAE/val', val_metrics['mae'], epoch)
            self.writer.add_scalar('LR', current_lr, epoch)

            logger.info(
                f"Epoch {epoch+1}/{epochs} - "
                f"Train RMSE: {train_metrics['rmse']:.4f}, "
                f"Val RMSE: {val_metrics['rmse']:.4f}, "
                f"LR: {current_lr:.6f}"
            )

            # Scheduler step
            if self.scheduler:
                if isinstance(self.scheduler, ReduceLROnPlateau):
                    self.scheduler.step(val_metrics['loss'])
                else:
                    self.scheduler.step()

            # Early stopping
            if val_metrics['loss'] < self.best_val_loss - self.config['early_stopping_min_delta']:
                self.best_val_loss = val_metrics['loss']
                self.patience_counter = 0
                self.save_checkpoint(epoch, is_best=True)
            else:
                self.patience_counter += 1
                if self.patience_counter >= self.config['early_stopping_patience']:
                    logger.info(f"Early stopping at epoch {epoch+1}")
                    break

            # Periodic checkpointing
            if (epoch + 1) % self.config['save_every_n_epochs'] == 0:
                self.save_checkpoint(epoch)

        # Final evaluation on test set
        logger.info("\nFinal Test Evaluation:")
        test_metrics = self.evaluate(self.test_loader, "test")
        self.history['test_loss'].append(test_metrics['loss'])
        self.history['test_rmse'].append(test_metrics['rmse'])
        self.history['test_mae'].append(test_metrics['mae'])

        logger.info(f"  RMSE: {test_metrics['rmse']:.4f}")
        logger.info(f"  MAE: {test_metrics['mae']:.4f}")

        # Ranking metrics on test set
        if self.test_dataset is not None:
            logger.info("  Evaluating ranking metrics on test set...")
            test_ranking = self.evaluate_ranking(self.test_dataset, k=5)
            self.history['test_hr@5'].append(test_ranking['hit_ratio'])
            self.history['test_ndcg@5'].append(test_ranking['ndcg'])
            test_metrics['hit_ratio'] = test_ranking['hit_ratio']
            test_metrics['ndcg'] = test_ranking['ndcg']

            logger.info(f"  HR@5: {test_ranking['hit_ratio']:.4f}")
            logger.info(f"  NDCG@5: {test_ranking['ndcg']:.4f}")
            logger.info(f"  ({test_ranking['n_tests']} ranking tests)")

        self.writer.close()

        return test_metrics

    def save_checkpoint(self, epoch: int, is_best: bool = False):
        """Save model checkpoint"""
        checkpoint = {
            'epoch': epoch,
            'model_state_dict': self.model.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
            'best_val_loss': self.best_val_loss,
            'config': self.config,
            'model_config': MODEL_CONFIG,
            'n_users': self.data_manager.n_users,
            'n_items': self.data_manager.n_items,
            'history': self.history
        }

        filename = f"checkpoint_epoch_{epoch+1}.pt"
        filepath = CHECKPOINTS_DIR / filename
        torch.save(checkpoint, filepath)

        if is_best:
            best_path = CHECKPOINTS_DIR / "best_model.pt"
            torch.save(checkpoint, best_path)
            logger.info(f"Saved best model (val_loss: {self.best_val_loss:.4f})")

    def export_onnx(self, output_path: Path = None):
        """Export model to ONNX format"""
        if output_path is None:
            output_path = EXPORTS_DIR / "ncf_model.onnx"

        self.model.eval()
        self.model.to('cpu')

        # Dummy inputs
        dummy_user = torch.zeros(1, dtype=torch.long)
        dummy_item = torch.zeros(1, dtype=torch.long)

        # Export
        torch.onnx.export(
            self.model,
            (dummy_user, dummy_item),
            output_path,
            export_params=True,
            opset_version=EXPORT_CONFIG['onnx_opset'],
            do_constant_folding=True,
            input_names=['user_id', 'item_id'],
            output_names=['rating'],
            dynamic_axes={
                'user_id': {0: 'batch_size'},
                'item_id': {0: 'batch_size'},
                'rating': {0: 'batch_size'}
            } if EXPORT_CONFIG['onnx_dynamic_axes'] else None
        )

        logger.info(f"Exported ONNX model to {output_path}")

        # Move back to device
        self.model.to(self.device)

        return output_path

    def save_final_model(self):
        """Save final model for production"""
        # Save PyTorch model
        model_path = EXPORTS_DIR / "ncf_model.pt"
        torch.save({
            'model_state_dict': self.model.state_dict(),
            'model_config': MODEL_CONFIG,
            'n_users': self.data_manager.n_users,
            'n_items': self.data_manager.n_items
        }, model_path)

        # Save mappings
        self.data_manager.save_mappings(EXPORTS_DIR / "mappings.json")

        # Save training history
        with open(EXPORTS_DIR / "training_history.json", 'w') as f:
            json.dump(self.history, f, indent=2)

        # Export ONNX
        self.export_onnx()

        logger.info(f"Saved final model to {EXPORTS_DIR}")

def main():
    parser = argparse.ArgumentParser(description='Train NCF Model')
    parser.add_argument('--epochs', type=int, default=None, help='Number of epochs')
    parser.add_argument('--batch-size', type=int, default=None, help='Batch size')
    parser.add_argument('--lr', type=float, default=None, help='Learning rate')
    parser.add_argument('--device', type=str, default=None, help='Device (cuda/cpu)')
    parser.add_argument('--no-amp', action='store_true', help='Disable mixed precision')
    args = parser.parse_args()

    # Override config with args
    if args.epochs:
        TRAINING_CONFIG['epochs'] = args.epochs
    if args.batch_size:
        TRAINING_CONFIG['batch_size'] = args.batch_size
    if args.lr:
        TRAINING_CONFIG['learning_rate'] = args.lr

    device = args.device or DEVICE_CONFIG['device']
    if args.no_amp:
        DEVICE_CONFIG['mixed_precision'] = False

    # Check GPU
    if device == 'cuda':
        if torch.cuda.is_available():
            logger.info(f"GPU: {torch.cuda.get_device_name(0)}")
            logger.info(f"VRAM: {torch.cuda.get_device_properties(0).total_memory / 1e9:.1f} GB")
        else:
            logger.warning("CUDA not available, falling back to CPU")
            device = 'cpu'

    # Load data
    logger.info("Loading data from database...")
    train_loader, val_loader, test_loader, data_manager = load_data_from_db(
        DATABASE_CONFIG,
        test_size=TRAINING_CONFIG['test_size'],
        val_size=TRAINING_CONFIG['val_size'],
        batch_size=TRAINING_CONFIG['batch_size'],
        num_workers=DEVICE_CONFIG['num_workers'],
        random_seed=TRAINING_CONFIG['random_seed']
    )

    # Get datasets for ranking evaluation (already created by load_data_from_db)
    val_ds = data_manager.val_dataset
    test_ds = data_manager.test_dataset

    # Create model
    logger.info("Creating NCF model...")
    model = create_ncf_model(
        n_users=data_manager.n_users,
        n_items=data_manager.n_items,
        config=MODEL_CONFIG
    )

    # Create trainer
    trainer = Trainer(
        model=model,
        train_loader=train_loader,
        val_loader=val_loader,
        test_loader=test_loader,
        data_manager=data_manager,
        device=device,
        config=TRAINING_CONFIG,
        val_dataset=val_ds,
        test_dataset=test_ds
    )

    # Train
    logger.info("Starting training...")
    test_metrics = trainer.train()

    # Save final model
    trainer.save_final_model()

    logger.info("\nTraining complete!")
    logger.info(f"Final Test RMSE: {test_metrics['rmse']:.4f}")
    logger.info(f"Final Test MAE: {test_metrics['mae']:.4f}")
    logger.info(f"Models saved to: {EXPORTS_DIR}")
    logger.info(f"TensorBoard logs: {LOGS_DIR}")

if __name__ == "__main__":
    main()
