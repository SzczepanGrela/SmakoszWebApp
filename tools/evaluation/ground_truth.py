import random
from contextlib import contextmanager
from typing import Any

import numpy as np

from algorithms.on_the_fly_calculator import get_contextual_preferences
from algorithms.rating_strategies import calculate_review_ratings

@contextmanager
def suppress_rating_noise():
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
    def __init__(self, vectors_data: dict[str, Any]):
        self.vectors_data = vectors_data

    def calculate_rating(
        self,
        user: dict,
        dish: dict,
        restaurant: dict,
    ) -> dict[str, float]:
        archetype = dish.get("secret_archetype", "")
        variant_name = dish.get("secret_variant_name", archetype)

        pref_vector = get_contextual_preferences(
            self.vectors_data, user, dish, variant_name, archetype
        )

        with suppress_rating_noise():
            result = calculate_review_ratings(
                user_data=user,
                dish=dish,
                restaurant=restaurant,
                user_variant_preference_vector=pref_vector,
                vectors_data=self.vectors_data,
            )

        return result
