"""
Ground truth calculation with noise suppression.

suppress_rating_noise() patches global random/numpy functions so that
RatingAggregator and strategies produce deterministic (noise-free) scores.
OnTheFlyCalculator is NOT affected - it uses instance-level random.Random(seed).
"""

import random
from contextlib import contextmanager
from typing import Any

import numpy as np

from algorithms.on_the_fly_calculator import OnTheFlyCalculator
from algorithms.rating_strategies import RatingAggregator

@contextmanager
def suppress_rating_noise():
    """
    Temporarily patch global RNG functions to eliminate rating noise.

    Patches:
      random.gauss(mu, sigma) -> mu          (food 1.5, value 0.5, aggregator 0.5)
      random.uniform(a, b)    -> (a+b)/2     (value fair-price range)
      random.random()         -> 1.0         (blocks food mishap: random() < 0.05 = False)
      np.random.normal(0, σ)  -> 0           (service 0.12, cleanliness 0.05, ambiance 0.15)
    """
    orig_gauss = random.gauss
    orig_uniform = random.uniform
    orig_random = random.random
    orig_normal = np.random.normal

    random.gauss = lambda mu, sigma: mu
    random.uniform = lambda a, b: (a + b) / 2.0
    random.random = lambda: 1.0
    np.random.normal = lambda loc=0.0, scale=1.0, size=None: (
        np.zeros(size) if size is not None else 0.0
    )

    try:
        yield
    finally:
        random.gauss = orig_gauss
        random.uniform = orig_uniform
        random.random = orig_random
        np.random.normal = orig_normal

class GroundTruthCalculator:
    """Calculates noise-free ground truth ratings using generator algorithms."""

    def __init__(self, vectors_data: dict[str, Any]):
        self.on_the_fly = OnTheFlyCalculator(vectors_data)
        self.aggregator = RatingAggregator()

    def calculate_rating(
        self,
        user: dict,
        dish: dict,
        restaurant: dict,
    ) -> dict[str, float]:
        """
        Compute deterministic ground truth rating for a (user, dish) pair.

        Returns the full result dict from RatingAggregator.calculate_all(),
        including component scores and overall_rating.
        """
        archetype = dish.get("secret_archetype", "")
        variant_name = dish.get("secret_variant_name", archetype)

        # Contextual preference vector (deterministic - uses instance RNG)
        pref_vector = self.on_the_fly.get_contextual_preferences(
            user, dish, variant_name, archetype
        )

        # Noise-free rating calculation
        with suppress_rating_noise():
            result = self.aggregator.calculate_all(
                user_data=user,
                dish=dish,
                restaurant=restaurant,
                user_variant_preference_vector=pref_vector,
                vectors_data=self.on_the_fly.vectors_data,
            )

        return result
