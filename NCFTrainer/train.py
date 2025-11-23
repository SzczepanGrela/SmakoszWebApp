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

class Trainer:
    """NCF Model Trainer"""

    def __init__(self,
                 model: nn.Module,
                 train_loader,
                 val_loader,
                 test_loader,
                 data_manager: CFDataManager,
                 device: str = 'cuda',
                 config: dict = None):

        self.model = model.to(device)
        self.train_loader = train_loader
        self.val_loader = val_loader
        self.test_loader = test_loader
        self.data_manager = data_manager
        self.device = device
        self.config = config or TRAINING_CONFIG

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
        """Evaluate model on a dataset"""
        self.model.eval()

        total_loss = 0
        total_mae = 0
        n_batches = 0
        all_predictions = []
        all_ratings = []

        for user_ids, item_ids, ratings in loader:
            user_ids = user_ids.to(self.device)
            item_ids = item_ids.to(self.device)
            ratings = ratings.to(self.device)

            predictions = self.model(user_ids, item_ids)
            loss = self.criterion(predictions, ratings)

            total_loss += loss.item()
            total_mae += torch.abs(predictions - ratings).mean().item()
            n_batches += 1

            all_predictions.extend(predictions.cpu().numpy())
            all_ratings.extend(ratings.cpu().numpy())

        avg_loss = total_loss / n_batches
        avg_mae = total_mae / n_batches
        avg_rmse = np.sqrt(avg_loss)

        return {
            'loss': avg_loss,
            'rmse': avg_rmse,
            'mae': avg_mae,
            'predictions': np.array(all_predictions),
            'ratings': np.array(all_ratings)
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
        test_metrics = self.evaluate(self.test_loader, "test")
        self.history['test_loss'].append(test_metrics['loss'])
        self.history['test_rmse'].append(test_metrics['rmse'])
        self.history['test_mae'].append(test_metrics['mae'])

        logger.info(f"\nFinal Test Results:")
        logger.info(f"  RMSE: {test_metrics['rmse']:.4f}")
        logger.info(f"  MAE: {test_metrics['mae']:.4f}")

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
        config=TRAINING_CONFIG
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
