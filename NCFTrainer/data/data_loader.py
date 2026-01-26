"""
Data Loader for CFTrainer
Exports data from PostgreSQL and creates PyTorch datasets
"""

import numpy as np
import pandas as pd
import psycopg2
from pathlib import Path
from typing import Tuple, Dict, Optional
import torch
from torch.utils.data import Dataset, DataLoader
from sklearn.model_selection import train_test_split
import logging

logger = logging.getLogger(__name__)

class RatingsDataset(Dataset):
    """PyTorch Dataset for user-item ratings"""

    def __init__(self, user_ids: np.ndarray, item_ids: np.ndarray, ratings: np.ndarray):
        self.user_ids = torch.LongTensor(user_ids)
        self.item_ids = torch.LongTensor(item_ids)
        self.ratings = torch.FloatTensor(ratings)

    def __len__(self) -> int:
        return len(self.ratings)

    def __getitem__(self, idx: int) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        return self.user_ids[idx], self.item_ids[idx], self.ratings[idx]

class DataExporter:
    """Exports rating data from PostgreSQL"""

    def __init__(self, db_config: Dict[str, str]):
        self.db_config = db_config
        self.connection = None

    def connect(self):
        """Connect to PostgreSQL"""
        self.connection = psycopg2.connect(
            host=self.db_config['host'],
            port=self.db_config['port'],
            dbname=self.db_config['database'],
            user=self.db_config['user'],
            password=self.db_config['password']
        )
        logger.info("Connected to PostgreSQL")

    def close(self):
        """Close connection"""
        if self.connection:
            self.connection.close()
            logger.info("Connection closed")

    def export_ratings(self) -> pd.DataFrame:
        """
        Export ratings from reviews table

        Returns:
            DataFrame with columns: user_id, dish_id, rating
        """
        query = """
            SELECT user_id, dish_id, dish_rating as rating
            FROM reviews
            WHERE dish_rating IS NOT NULL
            ORDER BY user_id, dish_id
        """

        self.connect()
        df = pd.read_sql(query, self.connection)
        self.close()

        logger.info(f"Exported {len(df):,} ratings")
        logger.info(f"  Users: {df['user_id'].nunique():,}")
        logger.info(f"  Items: {df['dish_id'].nunique():,}")
        logger.info(f"  Rating range: {df['rating'].min()}-{df['rating'].max()}")

        return df

    def export_to_csv(self, output_path: Path):
        """Export ratings to CSV file"""
        df = self.export_ratings()
        df.to_csv(output_path, index=False)
        logger.info(f"Saved to {output_path}")
        return df

class CFDataManager:
    """
    Manages data preparation for CF training

    Creates ID mappings and train/val/test splits
    """

    def __init__(self, ratings_df: pd.DataFrame):
        self.ratings_df = ratings_df.copy()

        # Create ID mappings (original_id -> continuous_index)
        self.user_mapping = self._create_mapping(ratings_df['user_id'].unique())
        self.item_mapping = self._create_mapping(ratings_df['dish_id'].unique())

        # Reverse mappings
        self.user_reverse = {v: k for k, v in self.user_mapping.items()}
        self.item_reverse = {v: k for k, v in self.item_mapping.items()}

        # Stats
        self.n_users = len(self.user_mapping)
        self.n_items = len(self.item_mapping)
        self.n_ratings = len(ratings_df)

        logger.info(f"DataManager initialized:")
        logger.info(f"  Users: {self.n_users:,}")
        logger.info(f"  Items: {self.n_items:,}")
        logger.info(f"  Ratings: {self.n_ratings:,}")
        logger.info(f"  Sparsity: {100 * (1 - self.n_ratings / (self.n_users * self.n_items)):.3f}%")

    def _create_mapping(self, ids: np.ndarray) -> Dict[int, int]:
        """Create continuous ID mapping"""
        return {original_id: idx for idx, original_id in enumerate(sorted(ids))}

    def prepare_data(self,
                     test_size: float = 0.1,
                     val_size: float = 0.1,
                     random_seed: int = 42) -> Tuple[RatingsDataset, RatingsDataset, RatingsDataset]:
        """
        Prepare train/val/test datasets

        Args:
            test_size: Test set ratio
            val_size: Validation set ratio
            random_seed: Random seed for reproducibility

        Returns:
            Tuple of (train_dataset, val_dataset, test_dataset)
        """
        # Map IDs to continuous indices
        user_ids = self.ratings_df['user_id'].map(self.user_mapping).values
        item_ids = self.ratings_df['dish_id'].map(self.item_mapping).values
        ratings = self.ratings_df['rating'].values.astype(np.float32)

        # Split: train -> val -> test
        X = np.column_stack([user_ids, item_ids])
        y = ratings

        # First split: train+val vs test
        X_trainval, X_test, y_trainval, y_test = train_test_split(
            X, y, test_size=test_size, random_state=random_seed
        )

        # Second split: train vs val
        val_ratio = val_size / (1 - test_size)
        X_train, X_val, y_train, y_val = train_test_split(
            X_trainval, y_trainval, test_size=val_ratio, random_state=random_seed
        )

        logger.info(f"Data split:")
        logger.info(f"  Train: {len(y_train):,} ({100*len(y_train)/len(y):.1f}%)")
        logger.info(f"  Val: {len(y_val):,} ({100*len(y_val)/len(y):.1f}%)")
        logger.info(f"  Test: {len(y_test):,} ({100*len(y_test)/len(y):.1f}%)")

        train_dataset = RatingsDataset(X_train[:, 0], X_train[:, 1], y_train)
        val_dataset = RatingsDataset(X_val[:, 0], X_val[:, 1], y_val)
        test_dataset = RatingsDataset(X_test[:, 0], X_test[:, 1], y_test)

        return train_dataset, val_dataset, test_dataset

    def get_dataloaders(self,
                        train_dataset: RatingsDataset,
                        val_dataset: RatingsDataset,
                        test_dataset: RatingsDataset,
                        batch_size: int = 4096,
                        num_workers: int = 4,
                        pin_memory: bool = True) -> Tuple[DataLoader, DataLoader, DataLoader]:
        """Create DataLoaders for training"""

        train_loader = DataLoader(
            train_dataset,
            batch_size=batch_size,
            shuffle=True,
            num_workers=num_workers,
            pin_memory=pin_memory
        )

        val_loader = DataLoader(
            val_dataset,
            batch_size=batch_size,
            shuffle=False,
            num_workers=num_workers,
            pin_memory=pin_memory
        )

        test_loader = DataLoader(
            test_dataset,
            batch_size=batch_size,
            shuffle=False,
            num_workers=num_workers,
            pin_memory=pin_memory
        )

        return train_loader, val_loader, test_loader

    def save_mappings(self, path: Path):
        """Save ID mappings for inference"""
        import json

        mappings = {
            'user_mapping': self.user_mapping,
            'item_mapping': self.item_mapping,
            'n_users': self.n_users,
            'n_items': self.n_items
        }

        # Convert keys to strings for JSON
        mappings['user_mapping'] = {str(k): v for k, v in self.user_mapping.items()}
        mappings['item_mapping'] = {str(k): v for k, v in self.item_mapping.items()}

        with open(path, 'w') as f:
            json.dump(mappings, f)

        logger.info(f"Saved mappings to {path}")

    @classmethod
    def load_mappings(cls, path: Path) -> Dict:
        """Load ID mappings"""
        import json

        with open(path, 'r') as f:
            mappings = json.load(f)

        # Convert keys back to int
        mappings['user_mapping'] = {int(k): v for k, v in mappings['user_mapping'].items()}
        mappings['item_mapping'] = {int(k): v for k, v in mappings['item_mapping'].items()}

        return mappings

def load_data_from_db(db_config: Dict[str, str],
                      test_size: float = 0.1,
                      val_size: float = 0.1,
                      batch_size: int = 4096,
                      num_workers: int = 4,
                      random_seed: int = 42) -> Tuple[DataLoader, DataLoader, DataLoader, CFDataManager]:
    """
    Complete data loading pipeline

    Returns:
        Tuple of (train_loader, val_loader, test_loader, data_manager)
    """
    # Export from DB
    exporter = DataExporter(db_config)
    ratings_df = exporter.export_ratings()

    # Create manager
    data_manager = CFDataManager(ratings_df)

    # Prepare datasets
    train_ds, val_ds, test_ds = data_manager.prepare_data(
        test_size=test_size,
        val_size=val_size,
        random_seed=random_seed
    )

    # Create loaders
    train_loader, val_loader, test_loader = data_manager.get_dataloaders(
        train_ds, val_ds, test_ds,
        batch_size=batch_size,
        num_workers=num_workers
    )

    return train_loader, val_loader, test_loader, data_manager

if __name__ == "__main__":
    # Test data loading
    import sys
    sys.path.append(str(Path(__file__).parent.parent))
    from config import DATABASE_CONFIG, DATA_DIR

    logging.basicConfig(level=logging.INFO)

    # Export to CSV
    exporter = DataExporter(DATABASE_CONFIG)
    df = exporter.export_to_csv(DATA_DIR / "ratings.csv")

    # Test data manager
    manager = CFDataManager(df)
    train_ds, val_ds, test_ds = manager.prepare_data()
    manager.save_mappings(DATA_DIR / "mappings.json")

    print(f"\nDataset sizes:")
    print(f"  Train: {len(train_ds)}")
    print(f"  Val: {len(val_ds)}")
    print(f"  Test: {len(test_ds)}")
