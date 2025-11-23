"""
Hyperparameter Tuning for NCF Model

Uses Optuna for automated hyperparameter optimization.
Finds optimal: embedding_dim, learning_rate, dropout, layer sizes, etc.
"""

import sys
import argparse
import logging
from pathlib import Path
from datetime import datetime
import json

import optuna
from optuna.trial import Trial
from optuna.samplers import TPESampler
from optuna.pruners import MedianPruner
import torch
import torch.nn as nn
from torch.optim import Adam
from torch.utils.tensorboard import SummaryWriter
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))

from config import (
    DATABASE_CONFIG, TRAINING_CONFIG, DEVICE_CONFIG,
    CHECKPOINTS_DIR, LOGS_DIR, DATA_DIR
)
from data.data_loader import load_data_from_db, CFDataManager
from models.ncf import NCF

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class NCFObjective:
    """
    Optuna objective function for NCF hyperparameter tuning
    """

    def __init__(self,
                 train_loader,
                 val_loader,
                 n_users: int,
                 n_items: int,
                 device: str = 'cuda',
                 n_epochs: int = 10,
                 early_stopping_patience: int = 3):

        self.train_loader = train_loader
        self.val_loader = val_loader
        self.n_users = n_users
        self.n_items = n_items
        self.device = device
        self.n_epochs = n_epochs
        self.early_stopping_patience = early_stopping_patience

    def __call__(self, trial: Trial) -> float:
        """
        Single trial: suggest hyperparameters, train, return validation RMSE
        """

        # Suggest hyperparameters
        params = {
            # Embedding dimensions
            'gmf_embedding_dim': trial.suggest_int('gmf_embedding_dim', 8, 64, step=8),
            'mlp_embedding_dim': trial.suggest_int('mlp_embedding_dim', 8, 64, step=8),

            # MLP architecture
            'n_layers': trial.suggest_int('n_layers', 2, 4),
            'layer_size_multiplier': trial.suggest_float('layer_size_multiplier', 1.5, 3.0),

            # Regularization
            'dropout': trial.suggest_float('dropout', 0.1, 0.5),

            # Optimizer
            'learning_rate': trial.suggest_float('learning_rate', 1e-4, 1e-2, log=True),
            'weight_decay': trial.suggest_float('weight_decay', 1e-6, 1e-4, log=True),

            # Architecture choices
            'use_gmf': trial.suggest_categorical('use_gmf', [True, False]),
            'use_mlp': True,  # Always use MLP
        }

        # Ensure at least one branch is used
        if not params['use_gmf']:
            params['use_mlp'] = True

        # Build MLP layers based on suggested params
        base_size = params['mlp_embedding_dim'] * 2
        mlp_layers = []
        current_size = base_size
        for i in range(params['n_layers']):
            next_size = max(16, int(current_size / params['layer_size_multiplier']))
            mlp_layers.append(next_size)
            current_size = next_size

        params['mlp_layers'] = mlp_layers

        logger.info(f"Trial {trial.number}: {params}")

        # Create model
        model = NCF(
            n_users=self.n_users,
            n_items=self.n_items,
            gmf_embedding_dim=params['gmf_embedding_dim'],
            mlp_embedding_dim=params['mlp_embedding_dim'],
            mlp_layers=params['mlp_layers'],
            dropout=params['dropout'],
            output_range=(1.0, 10.0),
            use_gmf=params['use_gmf'],
            use_mlp=params['use_mlp']
        ).to(self.device)

        # Optimizer
        optimizer = Adam(
            model.parameters(),
            lr=params['learning_rate'],
            weight_decay=params['weight_decay']
        )

        criterion = nn.MSELoss()

        # Training loop
        best_val_rmse = float('inf')
        patience_counter = 0

        for epoch in range(self.n_epochs):
            # Train
            model.train()
            train_loss = 0
            n_batches = 0

            for user_ids, item_ids, ratings in self.train_loader:
                user_ids = user_ids.to(self.device)
                item_ids = item_ids.to(self.device)
                ratings = ratings.to(self.device)

                optimizer.zero_grad()
                predictions = model(user_ids, item_ids)
                loss = criterion(predictions, ratings)
                loss.backward()
                optimizer.step()

                train_loss += loss.item()
                n_batches += 1

            avg_train_loss = train_loss / n_batches

            # Validate
            model.eval()
            val_loss = 0
            n_val_batches = 0

            with torch.no_grad():
                for user_ids, item_ids, ratings in self.val_loader:
                    user_ids = user_ids.to(self.device)
                    item_ids = item_ids.to(self.device)
                    ratings = ratings.to(self.device)

                    predictions = model(user_ids, item_ids)
                    loss = criterion(predictions, ratings)
                    val_loss += loss.item()
                    n_val_batches += 1

            avg_val_loss = val_loss / n_val_batches
            val_rmse = np.sqrt(avg_val_loss)

            # Report to Optuna for pruning
            trial.report(val_rmse, epoch)

            # Pruning check
            if trial.should_prune():
                raise optuna.TrialPruned()

            # Early stopping
            if val_rmse < best_val_rmse - 0.001:
                best_val_rmse = val_rmse
                patience_counter = 0
            else:
                patience_counter += 1
                if patience_counter >= self.early_stopping_patience:
                    break

            logger.info(f"  Epoch {epoch+1}: train_rmse={np.sqrt(avg_train_loss):.4f}, val_rmse={val_rmse:.4f}")

        # Cleanup
        del model
        torch.cuda.empty_cache()

        return best_val_rmse

def run_tuning(n_trials: int = 50,
               n_epochs_per_trial: int = 10,
               timeout_hours: float = None,
               study_name: str = None):
    """
    Run hyperparameter tuning

    Args:
        n_trials: Number of trials to run
        n_epochs_per_trial: Epochs per trial (fewer = faster but less accurate)
        timeout_hours: Optional timeout in hours
        study_name: Name for the study (for resuming)
    """

    device = DEVICE_CONFIG['device']
    if device == 'cuda' and not torch.cuda.is_available():
        logger.warning("CUDA not available, using CPU")
        device = 'cpu'

    logger.info("Loading data...")
    train_loader, val_loader, test_loader, data_manager = load_data_from_db(
        DATABASE_CONFIG,
        test_size=TRAINING_CONFIG['test_size'],
        val_size=TRAINING_CONFIG['val_size'],
        batch_size=TRAINING_CONFIG['batch_size'],
        num_workers=DEVICE_CONFIG['num_workers'],
        random_seed=TRAINING_CONFIG['random_seed']
    )

    # Create objective
    objective = NCFObjective(
        train_loader=train_loader,
        val_loader=val_loader,
        n_users=data_manager.n_users,
        n_items=data_manager.n_items,
        device=device,
        n_epochs=n_epochs_per_trial,
        early_stopping_patience=3
    )

    # Create study
    if study_name is None:
        study_name = f"ncf_tuning_{datetime.now().strftime('%Y%m%d_%H%M%S')}"

    study = optuna.create_study(
        study_name=study_name,
        direction='minimize',  # Minimize RMSE
        sampler=TPESampler(seed=42),
        pruner=MedianPruner(n_startup_trials=5, n_warmup_steps=3)
    )

    # Run optimization
    logger.info(f"Starting hyperparameter tuning: {n_trials} trials")
    logger.info(f"Study name: {study_name}")

    timeout = timeout_hours * 3600 if timeout_hours else None

    study.optimize(
        objective,
        n_trials=n_trials,
        timeout=timeout,
        show_progress_bar=True,
        gc_after_trial=True
    )

    # Results
    logger.info("\n" + "=" * 60)
    logger.info("TUNING COMPLETE")
    logger.info("=" * 60)

    logger.info(f"\nBest trial: {study.best_trial.number}")
    logger.info(f"Best RMSE: {study.best_value:.4f}")

    logger.info("\nBest hyperparameters:")
    for key, value in study.best_params.items():
        logger.info(f"  {key}: {value}")

    # Save results
    results = {
        'study_name': study_name,
        'n_trials': len(study.trials),
        'best_trial': study.best_trial.number,
        'best_rmse': study.best_value,
        'best_params': study.best_params,
        'all_trials': [
            {
                'number': t.number,
                'value': t.value,
                'params': t.params,
                'state': str(t.state)
            }
            for t in study.trials
        ]
    }

    results_path = LOGS_DIR / f"{study_name}_results.json"
    with open(results_path, 'w') as f:
        json.dump(results, f, indent=2)
    logger.info(f"\nResults saved to: {results_path}")

    # Generate config for best params
    best_config = generate_best_config(study.best_params)
    config_path = LOGS_DIR / f"{study_name}_best_config.json"
    with open(config_path, 'w') as f:
        json.dump(best_config, f, indent=2)
    logger.info(f"Best config saved to: {config_path}")

    return study, results

def generate_best_config(best_params: dict) -> dict:
    """Generate MODEL_CONFIG from best hyperparameters"""

    # Reconstruct MLP layers
    base_size = best_params['mlp_embedding_dim'] * 2
    mlp_layers = []
    current_size = base_size
    for i in range(best_params['n_layers']):
        next_size = max(16, int(current_size / best_params['layer_size_multiplier']))
        mlp_layers.append(next_size)
        current_size = next_size

    config = {
        'gmf_embedding_dim': best_params['gmf_embedding_dim'],
        'mlp_embedding_dim': best_params['mlp_embedding_dim'],
        'mlp_layers': mlp_layers,
        'dropout': best_params['dropout'],
        'output_range': [1.0, 10.0],
        'use_gmf': best_params['use_gmf'],
        'use_mlp': True,

        # Training params
        'learning_rate': best_params['learning_rate'],
        'weight_decay': best_params['weight_decay']
    }

    return config

def print_study_stats(study: optuna.Study):
    """Print detailed study statistics"""

    print("\n" + "=" * 60)
    print("STUDY STATISTICS")
    print("=" * 60)

    # Trial statistics
    completed = len([t for t in study.trials if t.state == optuna.trial.TrialState.COMPLETE])
    pruned = len([t for t in study.trials if t.state == optuna.trial.TrialState.PRUNED])
    failed = len([t for t in study.trials if t.state == optuna.trial.TrialState.FAIL])

    print(f"\nTrials: {len(study.trials)} total")
    print(f"  Completed: {completed}")
    print(f"  Pruned: {pruned}")
    print(f"  Failed: {failed}")

    # Best trials
    print(f"\nTop 5 trials:")
    sorted_trials = sorted(
        [t for t in study.trials if t.value is not None],
        key=lambda t: t.value
    )[:5]

    for i, trial in enumerate(sorted_trials):
        print(f"  {i+1}. Trial {trial.number}: RMSE={trial.value:.4f}")

    # Parameter importance
    if completed >= 10:
        try:
            importances = optuna.importance.get_param_importances(study)
            print(f"\nParameter importance:")
            for param, importance in sorted(importances.items(), key=lambda x: -x[1]):
                print(f"  {param}: {importance:.3f}")
        except Exception:
            pass

def main():
    parser = argparse.ArgumentParser(description='Hyperparameter Tuning for NCF')
    parser.add_argument('--n-trials', type=int, default=50, help='Number of trials')
    parser.add_argument('--epochs-per-trial', type=int, default=10, help='Epochs per trial')
    parser.add_argument('--timeout', type=float, default=None, help='Timeout in hours')
    parser.add_argument('--study-name', type=str, default=None, help='Study name (for resuming)')
    args = parser.parse_args()

    study, results = run_tuning(
        n_trials=args.n_trials,
        n_epochs_per_trial=args.epochs_per_trial,
        timeout_hours=args.timeout,
        study_name=args.study_name
    )

    print_study_stats(study)

    print("\n" + "=" * 60)
    print("NEXT STEPS")
    print("=" * 60)
    print("\n1. Copy best config to config.py:")
    print(f"   cat {LOGS_DIR}/{study.study_name}_best_config.json")
    print("\n2. Train with best hyperparameters:")
    print(f"   python train.py --epochs 50")
    print("=" * 60)

if __name__ == "__main__":
    main()
