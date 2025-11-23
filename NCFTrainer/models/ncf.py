"""
Neural Collaborative Filtering (NCF) Model

Combines GMF (Generalized Matrix Factorization) and MLP (Multi-Layer Perceptron)
for collaborative filtering recommendations.

Paper: He et al., "Neural Collaborative Filtering" (WWW 2017)
"""

import torch
import torch.nn as nn
from typing import List, Tuple, Optional

class GMF(nn.Module):
    """
    Generalized Matrix Factorization

    Element-wise product of user and item embeddings
    """

    def __init__(self, n_users: int, n_items: int, embedding_dim: int):
        super().__init__()

        self.user_embedding = nn.Embedding(n_users, embedding_dim)
        self.item_embedding = nn.Embedding(n_items, embedding_dim)

        # Initialize embeddings
        nn.init.normal_(self.user_embedding.weight, std=0.01)
        nn.init.normal_(self.item_embedding.weight, std=0.01)

    def forward(self, user_ids: torch.Tensor, item_ids: torch.Tensor) -> torch.Tensor:
        user_emb = self.user_embedding(user_ids)
        item_emb = self.item_embedding(item_ids)

        # Element-wise product
        return user_emb * item_emb

class MLP(nn.Module):
    """
    Multi-Layer Perceptron for learning non-linear interactions

    Concatenates user and item embeddings, then passes through MLP layers
    """

    def __init__(self,
                 n_users: int,
                 n_items: int,
                 embedding_dim: int,
                 layers: List[int],
                 dropout: float = 0.2):
        super().__init__()

        self.user_embedding = nn.Embedding(n_users, embedding_dim)
        self.item_embedding = nn.Embedding(n_items, embedding_dim)

        # Initialize embeddings
        nn.init.normal_(self.user_embedding.weight, std=0.01)
        nn.init.normal_(self.item_embedding.weight, std=0.01)

        # Build MLP layers
        mlp_layers = []
        input_dim = embedding_dim * 2  # Concatenated embeddings

        for hidden_dim in layers:
            mlp_layers.append(nn.Linear(input_dim, hidden_dim))
            mlp_layers.append(nn.ReLU())
            mlp_layers.append(nn.BatchNorm1d(hidden_dim))
            mlp_layers.append(nn.Dropout(dropout))
            input_dim = hidden_dim

        self.mlp = nn.Sequential(*mlp_layers)
        self.output_dim = layers[-1] if layers else embedding_dim * 2

    def forward(self, user_ids: torch.Tensor, item_ids: torch.Tensor) -> torch.Tensor:
        user_emb = self.user_embedding(user_ids)
        item_emb = self.item_embedding(item_ids)

        # Concatenate
        x = torch.cat([user_emb, item_emb], dim=-1)

        return self.mlp(x)

class NCF(nn.Module):
    """
    Neural Collaborative Filtering

    Combines GMF and MLP branches for hybrid recommendations.
    Predicts rating on scale [min_rating, max_rating].
    """

    def __init__(self,
                 n_users: int,
                 n_items: int,
                 gmf_embedding_dim: int = 32,
                 mlp_embedding_dim: int = 32,
                 mlp_layers: List[int] = None,
                 dropout: float = 0.2,
                 output_range: Tuple[float, float] = (1.0, 10.0),
                 use_gmf: bool = True,
                 use_mlp: bool = True):
        super().__init__()

        if mlp_layers is None:
            mlp_layers = [128, 64, 32]

        self.use_gmf = use_gmf
        self.use_mlp = use_mlp
        self.output_range = output_range

        # GMF branch
        if use_gmf:
            self.gmf = GMF(n_users, n_items, gmf_embedding_dim)
            gmf_output_dim = gmf_embedding_dim
        else:
            gmf_output_dim = 0

        # MLP branch
        if use_mlp:
            self.mlp = MLP(n_users, n_items, mlp_embedding_dim, mlp_layers, dropout)
            mlp_output_dim = self.mlp.output_dim
        else:
            mlp_output_dim = 0

        # Final prediction layer
        combined_dim = gmf_output_dim + mlp_output_dim
        self.prediction = nn.Sequential(
            nn.Linear(combined_dim, 16),
            nn.ReLU(),
            nn.Linear(16, 1)
        )

        # Store dimensions for ONNX export
        self.n_users = n_users
        self.n_items = n_items

    def forward(self, user_ids: torch.Tensor, item_ids: torch.Tensor) -> torch.Tensor:
        outputs = []

        if self.use_gmf:
            gmf_out = self.gmf(user_ids, item_ids)
            outputs.append(gmf_out)

        if self.use_mlp:
            mlp_out = self.mlp(user_ids, item_ids)
            outputs.append(mlp_out)

        # Concatenate branches
        x = torch.cat(outputs, dim=-1)

        # Predict rating
        rating = self.prediction(x).squeeze(-1)

        # Scale to output range using sigmoid
        min_rating, max_rating = self.output_range
        rating = torch.sigmoid(rating) * (max_rating - min_rating) + min_rating

        return rating

    def predict(self, user_ids: torch.Tensor, item_ids: torch.Tensor) -> torch.Tensor:
        """Inference mode prediction"""
        self.eval()
        with torch.no_grad():
            return self.forward(user_ids, item_ids)

    def get_user_embedding(self, user_ids: torch.Tensor) -> torch.Tensor:
        """Get user embeddings for similarity computation"""
        embeddings = []
        if self.use_gmf:
            embeddings.append(self.gmf.user_embedding(user_ids))
        if self.use_mlp:
            embeddings.append(self.mlp.user_embedding(user_ids))
        return torch.cat(embeddings, dim=-1)

    def get_item_embedding(self, item_ids: torch.Tensor) -> torch.Tensor:
        """Get item embeddings for similarity computation"""
        embeddings = []
        if self.use_gmf:
            embeddings.append(self.gmf.item_embedding(item_ids))
        if self.use_mlp:
            embeddings.append(self.mlp.item_embedding(item_ids))
        return torch.cat(embeddings, dim=-1)

def create_ncf_model(n_users: int,
                     n_items: int,
                     config: dict) -> NCF:
    """
    Factory function to create NCF model from config

    Args:
        n_users: Number of users
        n_items: Number of items
        config: Model configuration dict

    Returns:
        NCF model instance
    """
    model = NCF(
        n_users=n_users,
        n_items=n_items,
        gmf_embedding_dim=config.get('gmf_embedding_dim', 32),
        mlp_embedding_dim=config.get('mlp_embedding_dim', 32),
        mlp_layers=config.get('mlp_layers', [128, 64, 32]),
        dropout=config.get('dropout', 0.2),
        output_range=config.get('output_range', (1.0, 10.0)),
        use_gmf=config.get('use_gmf', True),
        use_mlp=config.get('use_mlp', True)
    )

    # Count parameters
    total_params = sum(p.numel() for p in model.parameters())
    trainable_params = sum(p.numel() for p in model.parameters() if p.requires_grad)

    print(f"NCF Model created:")
    print(f"  Users: {n_users:,}, Items: {n_items:,}")
    print(f"  Total parameters: {total_params:,}")
    print(f"  Trainable parameters: {trainable_params:,}")

    return model

if __name__ == "__main__":
    # Test model creation
    n_users = 25000
    n_items = 20000

    config = {
        'gmf_embedding_dim': 32,
        'mlp_embedding_dim': 32,
        'mlp_layers': [128, 64, 32],
        'dropout': 0.2,
        'output_range': (1.0, 10.0),
        'use_gmf': True,
        'use_mlp': True
    }

    model = create_ncf_model(n_users, n_items, config)

    # Test forward pass
    batch_size = 64
    user_ids = torch.randint(0, n_users, (batch_size,))
    item_ids = torch.randint(0, n_items, (batch_size,))

    ratings = model(user_ids, item_ids)
    print(f"\nTest forward pass:")
    print(f"  Input: user_ids {user_ids.shape}, item_ids {item_ids.shape}")
    print(f"  Output: ratings {ratings.shape}")
    print(f"  Rating range: [{ratings.min():.2f}, {ratings.max():.2f}]")
