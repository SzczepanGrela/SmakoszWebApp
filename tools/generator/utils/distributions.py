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

def zipf_distribution(n: int, alpha: float = 1.5) -> list[float]:
    if n <= 0:
        return []

    ranks = np.arange(1, n + 1)
    values = 1.0 / (ranks**alpha)

    probabilities = values / values.sum()

    return probabilities.tolist()
