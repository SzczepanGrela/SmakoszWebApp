"""
CFTrainer Configuration
Neural Collaborative Filtering for Smakosz dish recommendations
"""

import os
from pathlib import Path

# Load .env file
try:
    from dotenv import load_dotenv
    env_path = Path(__file__).parent / '.env'
    if env_path.exists():
        load_dotenv(env_path)
except ImportError:
    pass  # python-dotenv not installed, using system env vars

# PATHS

BASE_DIR = Path(__file__).parent.absolute()
DATA_DIR = BASE_DIR / "data"
MODELS_DIR = BASE_DIR / "models"
CHECKPOINTS_DIR = BASE_DIR / "checkpoints"
LOGS_DIR = BASE_DIR / "logs"
EXPORTS_DIR = BASE_DIR / "exports"

# Create directories if not exist
for dir_path in [DATA_DIR, MODELS_DIR, CHECKPOINTS_DIR, LOGS_DIR, EXPORTS_DIR]:
    dir_path.mkdir(exist_ok=True)

# DATABASE CONFIG (PostgreSQL)

DATABASE_CONFIG = {
    'host': os.getenv('DB_HOST', 'localhost'),
    'port': os.getenv('DB_PORT', '5432'),
    'database': os.getenv('DB_NAME', 'mockdatadb'),
    'user': os.getenv('DB_USER', 'postgres'),
    'password': os.getenv('DB_PASSWORD', '')
}

# MODEL ARCHITECTURE (NCF)

MODEL_CONFIG = {
    # Embedding dimensions
    'embedding_dim': 64,           # User/Item embedding size
    'gmf_embedding_dim': 32,       # GMF branch embedding
    'mlp_embedding_dim': 32,       # MLP branch embedding

    # MLP layers
    'mlp_layers': [128, 64, 32],   # Hidden layer sizes
    'dropout': 0.2,                # Dropout rate

    # Output
    'output_range': (0.0, 1.0),    # Normalized rating scale [0, 1]

    # NCF variant
    'use_gmf': True,               # Use GMF branch
    'use_mlp': True,               # Use MLP branch
}

# TRAINING CONFIG

TRAINING_CONFIG = {
    # Data split
    'test_size': 0.1,              # 10% test
    'val_size': 0.1,               # 10% validation
    'random_seed': 42,

    # Training params
    'batch_size': 4096,            # Large batch for GPU
    'epochs': 50,
    'learning_rate': 0.001,
    'weight_decay': 1e-5,          # L2 regularization

    # Scheduler
    'scheduler': 'cosine',         # cosine, step, plateau
    'warmup_epochs': 3,

    # Early stopping
    'early_stopping_patience': 7,
    'early_stopping_min_delta': 0.001,

    # Checkpointing
    'save_every_n_epochs': 5,
    'keep_n_checkpoints': 3,

    # Ranking evaluation
    'eval_ranking_every_n_epochs': 5,  # Evaluate ranking metrics every N epochs
    'ranking_k': 5,                     # Top-K for HR@K and NDCG@K
    'ranking_threshold': 0.7,           # Normalized rating threshold for positive items
}

# DEVICE CONFIG (AMD ROCm)

DEVICE_CONFIG = {
    'device': 'cuda',              # cuda for ROCm
    'num_workers': 4,              # DataLoader workers
    'pin_memory': True,            # Faster GPU transfer
    'mixed_precision': True,       # FP16 training
}

# LOGGING CONFIG

LOGGING_CONFIG = {
    'tensorboard': True,
    'log_every_n_steps': 100,
    'eval_every_n_epochs': 1,
}

# EXPORT CONFIG

EXPORT_CONFIG = {
    'onnx_opset': 17,
    'onnx_dynamic_axes': True,
    'quantize': False,             # INT8 quantization
}

# EXPECTED METRICS

EXPECTED_METRICS = {
    'target_rmse': 1.0,            # Target RMSE
    'target_mae': 0.8,             # Target MAE
    'min_coverage': 0.95,          # Min item coverage
}

def print_config():
    """Display current configuration"""
    print("=" * 60)
    print("CFTrainer Configuration")
    print("=" * 60)

    print("\nDatabase:")
    print(f"  Host: {DATABASE_CONFIG['host']}:{DATABASE_CONFIG['port']}")
    print(f"  Database: {DATABASE_CONFIG['database']}")

    print("\nModel (NCF):")
    print(f"  Embedding dim: {MODEL_CONFIG['embedding_dim']}")
    print(f"  MLP layers: {MODEL_CONFIG['mlp_layers']}")
    print(f"  GMF: {MODEL_CONFIG['use_gmf']}, MLP: {MODEL_CONFIG['use_mlp']}")

    print("\nTraining:")
    print(f"  Batch size: {TRAINING_CONFIG['batch_size']}")
    print(f"  Epochs: {TRAINING_CONFIG['epochs']}")
    print(f"  Learning rate: {TRAINING_CONFIG['learning_rate']}")

    print("\nDevice:")
    print(f"  Device: {DEVICE_CONFIG['device']}")
    print(f"  Mixed precision: {DEVICE_CONFIG['mixed_precision']}")

    print("=" * 60)

if __name__ == "__main__":
    print_config()
