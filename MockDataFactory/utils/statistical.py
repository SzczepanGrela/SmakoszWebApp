"""
Statistical Utilities - Funkcje do próbkowania z różnych rozkładów
"""

import numpy as np
import random
from typing import List, Any

def sample_normal(mean: float, stdev: float,
                  min_val: float = None, max_val: float = None) -> float:
    """
    Próbkuje wartość z rozkładu normalnego z opcjonalnym clampingiem

    Args:
        mean: Średnia
        stdev: Odchylenie standardowe
        min_val: Minimalna wartość (opcjonalnie)
        max_val: Maksymalna wartość (opcjonalnie)

    Returns:
        Wartość z rozkładu normalnego
    """
    value = np.random.normal(mean, stdev)

    if min_val is not None:
        value = max(value, min_val)
    if max_val is not None:
        value = min(value, max_val)

    return float(value)

def sample_beta(alpha: float, beta: float,
                min_val: float = 0.0, max_val: float = 1.0) -> float:
    """
    Próbkuje wartość z rozkładu beta (przydatne dla wartości 0-1)

    Args:
        alpha: Parametr α (kształt)
        beta: Parametr β (kształt)
        min_val: Minimalna wartość do skalowania
        max_val: Maksymalna wartość do skalowania

    Returns:
        Wartość z rozkładu beta przeskalowana do [min_val, max_val]

    Przykłady rozkładów:
        alpha=2, beta=5: Skośny w lewo (więcej małych wartości)
        alpha=5, beta=2: Skośny w prawo (więcej dużych wartości)
        alpha=2, beta=2: Symetryczny (kształt dzwonu)
    """
    value = np.random.beta(alpha, beta)

    # Skaluj z [0,1] do [min_val, max_val]
    scaled_value = min_val + value * (max_val - min_val)

    return float(scaled_value)

def weighted_choice(items: List[Any], weights: List[float]) -> Any:
    """
    Wybiera element z listy z uwzględnieniem wag

    Args:
        items: Lista elementów do wyboru
        weights: Wagi dla każdego elementu (nieznormalizowane)

    Returns:
        Wybrany element
    """
    if len(items) != len(weights):
        raise ValueError("Liczba items i weights musi być równa!")

    if not items:
        raise ValueError("Cannot choose from empty list")

    # Normalizacja wag
    total_weight = sum(weights)

    # FIXED: Jeśli wszystkie wagi są 0, użyj równego prawdopodobieństwa
    if total_weight == 0:
        return random.choice(items)

    normalized_weights = [w / total_weight for w in weights]

    return random.choices(items, weights=normalized_weights, k=1)[0]

def zipf_distribution(n: int, alpha: float = 1.5) -> List[float]:
    """
    Generuje rozkład Zipfa dla n elementów (do modelowania popularności)

    Args:
        n: Liczba elementów
        alpha: Parametr kształtu (większy = bardziej skośny)
               1.0 = standardowy Zipf
               1.5 = mocniej skośny (używany dla dań/restauracji)

    Returns:
        Lista n prawdopodobieństw (suma = 1.0)

    Zastosowanie:
        - Popularność dań (niektóre bardzo popularne, większość rzadka)
        - Częstotliwość wizyt w restauracjach
        - Aktywność użytkowników (power users vs casual users)
    """
    # FIXED: Obsłuż edge case n <= 0
    if n <= 0:
        return []

    # Oblicz surowe wartości Zipfa
    ranks = np.arange(1, n + 1)
    values = 1.0 / (ranks ** alpha)

    # Normalizuj do prawdopodobieństw
    probabilities = values / values.sum()

    return probabilities.tolist()

def truncated_normal(mean: float, stdev: float,
                     lower: float, upper: float) -> float:
    """
    Rozkład normalny obcięty do przedziału [lower, upper]
    (odrzuca wartości poza przedziałem i próbkuje ponownie)

    Args:
        mean: Średnia
        stdev: Odchylenie standardowe
        lower: Dolna granica
        upper: Górna granica

    Returns:
        Wartość z przedziału [lower, upper]
    """
    max_attempts = 1000
    for _ in range(max_attempts):
        value = np.random.normal(mean, stdev)
        if lower <= value <= upper:
            return float(value)

    # Jeśli nie uda się w 1000 próbach, zwróć clampowaną wartość
    value = np.random.normal(mean, stdev)
    return float(max(lower, min(upper, value)))

def sample_discrete_normal(mean: float, stdev: float,
                           min_val: int, max_val: int) -> int:
    """
    Dyskretny rozkład normalny (zwraca int)

    Args:
        mean: Średnia
        stdev: Odchylenie standardowe
        min_val: Minimalna wartość (int)
        max_val: Maksymalna wartość (int)

    Returns:
        Wartość całkowita z rozkładu normalnego
    """
    value = np.random.normal(mean, stdev)
    value = int(round(value))
    value = max(min_val, min(max_val, value))

    return value
