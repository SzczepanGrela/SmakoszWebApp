"""
Data Loader for CFTrainer
Exports data from PostgreSQL and creates PyTorch datasets

Supports both:
- In-memory datasets (RatingsDataset) for smaller datasets
- Streaming datasets (StreamingRatingsDataset) for large-scale training
"""

import numpy as np
import pandas as pd
import psycopg2
from pathlib import Path
from typing import Tuple, Dict, Optional, Iterator
import torch
from torch.utils.data import Dataset, DataLoader, IterableDataset
from sklearn.model_selection import train_test_split
import logging
import random
from collections import deque

logger = logging.getLogger(__name__)

class RatingsDataset(Dataset):
    """PyTorch Dataset for user-item ratings (in-memory)"""

    def __init__(self, user_ids: np.ndarray, item_ids: np.ndarray, ratings: np.ndarray):
        self.user_ids = torch.LongTensor(user_ids)
        self.item_ids = torch.LongTensor(item_ids)
        self.ratings = torch.FloatTensor(ratings)

    def __len__(self) -> int:
        return len(self.ratings)

    def __getitem__(self, idx: int) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        return self.user_ids[idx], self.item_ids[idx], self.ratings[idx]

class StreamingRatingsDataset(IterableDataset):
    """
    PyTorch IterableDataset for streaming user-item ratings from PostgreSQL

    Uses server-side cursors to avoid loading entire dataset into RAM.
    Implements shuffle buffer for approximate shuffling.
    """

    def __init__(self,
                 db_config: Dict[str, str],
                 user_mapping: Dict[int, int],
                 item_mapping: Dict[int, int],
                 split: str = 'train',
                 split_ratio: Tuple[float, float, float] = (0.8, 0.1, 0.1),
                 shuffle_buffer_size: int = 10000,
                 random_seed: int = 42,
                 fetch_size: int = 1000):
        """
        Args:
            db_config: PostgreSQL connection config
            user_mapping: Dict mapping original user_ids to continuous indices
            item_mapping: Dict mapping original dish_ids to continuous indices
            split: 'train', 'val', or 'test'
            split_ratio: (train, val, test) ratio
            shuffle_buffer_size: Size of shuffle buffer (0 = no shuffling)
            random_seed: Random seed for reproducibility
            fetch_size: Number of rows to fetch per cursor iteration
        """
        self.db_config = db_config
        self.user_mapping = user_mapping
        self.item_mapping = item_mapping
        self.split = split
        self.split_ratio = split_ratio
        self.shuffle_buffer_size = shuffle_buffer_size
        self.random_seed = random_seed
        self.fetch_size = fetch_size

    def _get_split_query(self) -> str:
        """Generate SQL query for data split using modulo hashing"""
        train_ratio, val_ratio, test_ratio = self.split_ratio

        # Use modulo 100 for deterministic splitting
        if self.split == 'train':
            condition = f"MOD(hashtext(user_id::text || dish_id::text)::bigint, 100) < {int(train_ratio * 100)}"
        elif self.split == 'val':
            train_pct = int(train_ratio * 100)
            val_pct = int((train_ratio + val_ratio) * 100)
            condition = f"MOD(hashtext(user_id::text || dish_id::text)::bigint, 100) >= {train_pct} AND MOD(hashtext(user_id::text || dish_id::text)::bigint, 100) < {val_pct}"
        else:  # test
            test_pct = int((train_ratio + val_ratio) * 100)
            condition = f"MOD(hashtext(user_id::text || dish_id::text)::bigint, 100) >= {test_pct}"

        query = f"""
            SELECT user_id, dish_id, dish_rating
            FROM reviews
            WHERE dish_rating IS NOT NULL
              AND {condition}
            ORDER BY review_id
        """
        return query

    def _stream_from_db(self) -> Iterator[Tuple[int, int, float]]:
        """Stream rows from PostgreSQL using server-side cursor"""
        connection = psycopg2.connect(
            host=self.db_config['host'],
            port=self.db_config['port'],
            dbname=self.db_config['database'],
            user=self.db_config['user'],
            password=self.db_config['password']
        )

        # Create named cursor for server-side execution
        cursor_name = f"ratings_cursor_{self.split}_{id(self)}"
        cursor = connection.cursor(name=cursor_name)
        cursor.itersize = self.fetch_size

        try:
            query = self._get_split_query()
            cursor.execute(query)

            for row in cursor:
                user_id, dish_id, rating = row

                # Skip if ID not in mapping (shouldn't happen but safety check)
                if user_id not in self.user_mapping or dish_id not in self.item_mapping:
                    continue

                # Map to continuous indices
                mapped_user_id = self.user_mapping[user_id]
                mapped_dish_id = self.item_mapping[dish_id]

                # Normalize rating from [1, 10] to [0, 1]
                normalized_rating = (float(rating) - 1.0) / 9.0

                yield (mapped_user_id, mapped_dish_id, normalized_rating)

        finally:
            cursor.close()
            connection.close()

    def _shuffle_buffer(self, iterator: Iterator) -> Iterator:
        """
        Implement shuffle buffer for approximate shuffling

        Maintains a buffer and randomly samples from it
        """
        if self.shuffle_buffer_size <= 0:
            yield from iterator
            return

        buffer = deque(maxlen=self.shuffle_buffer_size)
        rng = random.Random(self.random_seed)

        # Fill initial buffer
        for item in iterator:
            buffer.append(item)
            if len(buffer) >= self.shuffle_buffer_size:
                # Buffer full, start yielding
                idx = rng.randint(0, len(buffer) - 1)
                yield buffer[idx]
                del buffer[idx]

        # Drain remaining buffer
        buffer_list = list(buffer)
        rng.shuffle(buffer_list)
        yield from buffer_list

    def __iter__(self) -> Iterator[Tuple[torch.Tensor, torch.Tensor, torch.Tensor]]:
        """Iterate over dataset"""
        stream = self._stream_from_db()

        # Apply shuffle buffer for training
        if self.split == 'train' and self.shuffle_buffer_size > 0:
            stream = self._shuffle_buffer(stream)

        for user_id, item_id, rating in stream:
            yield (
                torch.tensor(user_id, dtype=torch.long),
                torch.tensor(item_id, dtype=torch.long),
                torch.tensor(rating, dtype=torch.float32)
            )

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

        # Build user-item interaction set for negative sampling
        self._build_interaction_matrix()

        logger.info(f"DataManager initialized:")
        logger.info(f"  Users: {self.n_users:,}")
        logger.info(f"  Items: {self.n_items:,}")
        logger.info(f"  Ratings: {self.n_ratings:,}")
        logger.info(f"  Sparsity: {100 * (1 - self.n_ratings / (self.n_users * self.n_items)):.3f}%")

    def _create_mapping(self, ids: np.ndarray) -> Dict[int, int]:
        """Create continuous ID mapping"""
        return {original_id: idx for idx, original_id in enumerate(sorted(ids))}

    def _build_interaction_matrix(self):
        """Build user-item interaction sets for negative sampling"""
        self.user_items = {}  # user_idx -> set of item_idx

        for _, row in self.ratings_df.iterrows():
            user_idx = self.user_mapping[row['user_id']]
            item_idx = self.item_mapping[row['dish_id']]

            if user_idx not in self.user_items:
                self.user_items[user_idx] = set()
            self.user_items[user_idx].add(item_idx)

        # Precompute all items set for faster negative sampling
        self.all_items = set(range(self.n_items))

        logger.info(f"  Interaction matrix built: {len(self.user_items)} users with interactions")

    def get_user_positive_items(self, user_idx: int) -> set:
        """Get set of items the user has interacted with"""
        return self.user_items.get(user_idx, set())

    def sample_negative_items(self, user_idx: int, n_samples: int = 99,
                             rng: Optional[np.random.RandomState] = None) -> np.ndarray:
        """
        Sample negative items for a user (items they haven't interacted with)

        Args:
            user_idx: User index
            n_samples: Number of negative samples
            rng: Random state for reproducibility

        Returns:
            Array of negative item indices
        """
        if rng is None:
            rng = np.random.RandomState()

        positive_items = self.user_items.get(user_idx, set())
        negative_items = list(self.all_items - positive_items)

        if len(negative_items) < n_samples:
            logger.warning(f"User {user_idx} has only {len(negative_items)} negative items, "
                         f"requested {n_samples}. Sampling with replacement.")
            return rng.choice(negative_items, size=n_samples, replace=True)

        return rng.choice(negative_items, size=n_samples, replace=False)

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
        # Normalize ratings from [1, 10] to [0, 1]
        ratings = (self.ratings_df['rating'].values.astype(np.float32) - 1.0) / 9.0

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

        # Store datasets for later access (e.g., ranking evaluation)
        self.train_dataset = train_dataset
        self.val_dataset = val_dataset
        self.test_dataset = test_dataset

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
    Complete data loading pipeline (in-memory)

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

def load_streaming_data_from_db(db_config: Dict[str, str],
                                 test_size: float = 0.1,
                                 val_size: float = 0.1,
                                 batch_size: int = 4096,
                                 num_workers: int = 0,  # IterableDataset typically uses 0 workers
                                 shuffle_buffer_size: int = 10000,
                                 random_seed: int = 42,
                                 fetch_size: int = 1000) -> Tuple[DataLoader, DataLoader, DataLoader, Dict]:
    """
    Complete data loading pipeline with streaming (memory-efficient)

    Uses IterableDataset with PostgreSQL server-side cursors to avoid
    loading entire dataset into RAM.

    Args:
        db_config: PostgreSQL connection config
        test_size: Test set ratio
        val_size: Validation set ratio
        batch_size: Batch size for DataLoader
        num_workers: Number of DataLoader workers (0 recommended for streaming)
        shuffle_buffer_size: Size of shuffle buffer for training data
        random_seed: Random seed
        fetch_size: Number of rows to fetch per cursor iteration

    Returns:
        Tuple of (train_loader, val_loader, test_loader, mappings_dict)
    """
    # First, we need to create ID mappings
    # We'll do a quick scan to get unique user/item IDs
    logger.info("Creating ID mappings...")
    connection = psycopg2.connect(
        host=db_config['host'],
        port=db_config['port'],
        dbname=db_config['database'],
        user=db_config['user'],
        password=db_config['password']
    )

    try:
        cursor = connection.cursor()

        # Get unique user IDs
        cursor.execute("SELECT DISTINCT user_id FROM reviews WHERE dish_rating IS NOT NULL ORDER BY user_id")
        user_ids = [row[0] for row in cursor.fetchall()]
        user_mapping = {uid: idx for idx, uid in enumerate(user_ids)}

        # Get unique dish IDs
        cursor.execute("SELECT DISTINCT dish_id FROM reviews WHERE dish_rating IS NOT NULL ORDER BY dish_id")
        dish_ids = [row[0] for row in cursor.fetchall()]
        item_mapping = {did: idx for idx, did in enumerate(dish_ids)}

        # Get total count for logging
        cursor.execute("SELECT COUNT(*) FROM reviews WHERE dish_rating IS NOT NULL")
        total_ratings = cursor.fetchone()[0]

        cursor.close()
    finally:
        connection.close()

    n_users = len(user_mapping)
    n_items = len(item_mapping)

    logger.info(f"Streaming dataset initialized:")
    logger.info(f"  Users: {n_users:,}")
    logger.info(f"  Items: {n_items:,}")
    logger.info(f"  Total ratings: {total_ratings:,}")
    logger.info(f"  Sparsity: {100 * (1 - total_ratings / (n_users * n_items)):.3f}%")

    # Calculate split ratios
    train_ratio = 1.0 - test_size - val_size
    split_ratio = (train_ratio, val_size, test_size)

    # Create streaming datasets
    train_dataset = StreamingRatingsDataset(
        db_config=db_config,
        user_mapping=user_mapping,
        item_mapping=item_mapping,
        split='train',
        split_ratio=split_ratio,
        shuffle_buffer_size=shuffle_buffer_size,
        random_seed=random_seed,
        fetch_size=fetch_size
    )

    val_dataset = StreamingRatingsDataset(
        db_config=db_config,
        user_mapping=user_mapping,
        item_mapping=item_mapping,
        split='val',
        split_ratio=split_ratio,
        shuffle_buffer_size=0,  # No shuffling for validation
        random_seed=random_seed,
        fetch_size=fetch_size
    )

    test_dataset = StreamingRatingsDataset(
        db_config=db_config,
        user_mapping=user_mapping,
        item_mapping=item_mapping,
        split='test',
        split_ratio=split_ratio,
        shuffle_buffer_size=0,  # No shuffling for test
        random_seed=random_seed,
        fetch_size=fetch_size
    )

    # Create DataLoaders
    # Note: num_workers should be 0 for IterableDataset to avoid複duplication
    train_loader = DataLoader(
        train_dataset,
        batch_size=batch_size,
        num_workers=num_workers,
        pin_memory=True
    )

    val_loader = DataLoader(
        val_dataset,
        batch_size=batch_size,
        num_workers=num_workers,
        pin_memory=True
    )

    test_loader = DataLoader(
        test_dataset,
        batch_size=batch_size,
        num_workers=num_workers,
        pin_memory=True
    )

    # Return mappings for model initialization and inference
    mappings = {
        'user_mapping': user_mapping,
        'item_mapping': item_mapping,
        'n_users': n_users,
        'n_items': n_items
    }

    logger.info(f"Streaming DataLoaders created:")
    logger.info(f"  Batch size: {batch_size}")
    logger.info(f"  Shuffle buffer size: {shuffle_buffer_size}")
    logger.info(f"  Fetch size: {fetch_size}")

    return train_loader, val_loader, test_loader, mappings

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
