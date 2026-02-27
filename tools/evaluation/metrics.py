"""
Evaluation metrics for NCF recommendation quality.
"""

import numpy as np

def rmse(predicted: list[float], actual: list[float]) -> float:
    """Root Mean Squared Error between predicted and actual ratings."""
    if not predicted:
        return 0.0
    pred = np.array(predicted)
    act = np.array(actual)
    return float(np.sqrt(np.mean((pred - act) ** 2)))

def mae(predicted: list[float], actual: list[float]) -> float:
    """Mean Absolute Error between predicted and actual ratings."""
    if not predicted:
        return 0.0
    pred = np.array(predicted)
    act = np.array(actual)
    return float(np.mean(np.abs(pred - act)))

def hit_rate_at_k(
    recommended_ids: list[int],
    relevant_ids: set[int],
    k: int,
) -> float:
    """
    Hit Rate @ K - 1.0 if at least one relevant item in top-K, else 0.0.

    A relevant item is one whose ground truth rating >= threshold (determined by caller).
    """
    top_k = recommended_ids[:k]
    return 1.0 if any(did in relevant_ids for did in top_k) else 0.0

def ndcg_at_k(
    recommended_ids: list[int],
    relevant_ids: set[int],
    k: int,
) -> float:
    """
    Normalized Discounted Cumulative Gain @ K.

    Binary relevance: 1 if dish_id in relevant_ids, 0 otherwise.
    """
    top_k = recommended_ids[:k]

    dcg = 0.0
    for i, did in enumerate(top_k):
        rel = 1.0 if did in relevant_ids else 0.0
        dcg += rel / np.log2(i + 2)  # i+2 because rank starts at 1

    # Ideal DCG: all relevant items at the top
    n_relevant = min(len(relevant_ids), k)
    idcg = sum(1.0 / np.log2(i + 2) for i in range(n_relevant))

    return float(dcg / idcg) if idcg > 0 else 0.0

def coverage(
    all_recommended_ids: set[int],
    total_dish_count: int,
) -> float:
    """Fraction of the catalog that appears in at least one recommendation."""
    if total_dish_count == 0:
        return 0.0
    return len(all_recommended_ids) / total_dish_count
