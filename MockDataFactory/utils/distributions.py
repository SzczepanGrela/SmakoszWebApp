import random
from typing import Any

import numpy as np

def sample_normal(mean: float, stdev: float, min_val: float | None = None, max_val: float | None = None) -> float:
    value = np.random.normal(mean, stdev)

    if min_val is not None:
        value = max(value, min_val)
    if max_val is not None:
        value = min(value, max_val)

    return float(value)

def sample_beta(alpha: float, beta: float, min_val: float = 0.0, max_val: float = 1.0) -> float:
    value = np.random.beta(alpha, beta)

    scaled_value = min_val + value * (max_val - min_val)

    return float(scaled_value)

def weighted_choice(items: list[Any], weights: list[float]) -> Any:
    if len(items) != len(weights):
        raise ValueError("Liczba items i weights musi być równa!")

    if not items:
        raise ValueError("Cannot choose from empty list")

    total_weight = sum(weights)

    if total_weight == 0:
        return random.choice(items)

    normalized_weights = [w / total_weight for w in weights]

    return random.choices(items, weights=normalized_weights, k=1)[0]

def zipf_distribution(n: int, alpha: float = 1.5) -> list[float]:
    if n <= 0:
        return []

    ranks = np.arange(1, n + 1)
    values = 1.0 / (ranks**alpha)

    probabilities = values / values.sum()

    return probabilities.tolist()

def truncated_normal(mean: float, stdev: float, lower: float, upper: float) -> float:
    max_attempts = 1000
    for _ in range(max_attempts):
        value = np.random.normal(mean, stdev)
        if lower <= value <= upper:
            return float(value)

    value = np.random.normal(mean, stdev)
    return float(max(lower, min(upper, value)))

def sample_discrete_normal(mean: float, stdev: float, min_val: int, max_val: int) -> int:
    value = np.random.normal(mean, stdev)
    value = int(round(value))
    value = max(min_val, min(max_val, value))

    return value
