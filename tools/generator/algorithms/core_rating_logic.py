import hashlib
import logging
import math
import random as _random
from typing import Any

from .preference_calculator import calculate_affinity

logger = logging.getLogger(__name__)

def get_review_rng(user_id: int, dish_id: int) -> _random.Random:
    seed_str = f"review_{user_id}_{dish_id}"
    seed_int = int.from_bytes(hashlib.md5(seed_str.encode()).digest()[:8], "little")
    return _random.Random(seed_int)

def get_archetype_metadata(archetype: str, vectors_data: dict[str, Any] | None = None) -> dict[str, dict[str, float]]:
    if vectors_data is None:
        logger.warning(
            f"No vectors_data provided to get_archetype_metadata for archetype '{archetype}'. Using default empty metadata."
        )
        return {"base_characteristics": {}, "base_weights": {"_default": 1.0}}

    archetype_data = vectors_data.get(archetype)
    if archetype_data is None:
        logger.warning(f"Archetype '{archetype}' not found in vectors_data. Using default empty metadata.")
        return {"base_characteristics": {}, "base_weights": {"_default": 1.0}}

    return {
        "base_characteristics": archetype_data["archetype_base"]["characteristics"],
        "base_weights": archetype_data["archetype_base"]["default_weights"],
    }

def sigmoid_stretch(value: float, midpoint: float = 6.0, steepness: float = 1.2) -> float:
    normalized = (value - midpoint) / (10.0 - midpoint)
    stretched = midpoint + (10.0 - midpoint) * math.tanh(normalized * steepness * 2)
    return max(1.0, min(10.0, stretched))

def get_user_baseline(user_data: dict) -> float:
    baseline = user_data.get("secret_rating_baseline")

    if baseline is None:
        logger.warning("secret_rating_baseline not found in user_data - using default 6.0")
        baseline = 6.0

    return float(baseline)

def calculate_food_score_polarized(
    user_data: dict,
    dish: dict,
    restaurant: dict,
    contextual_target_vector: dict[str, float] | None = None,
    vectors_data: dict[str, Any] | None = None,
    rng: _random.Random | None = None,
) -> float:
    if rng is None:
        rng = _random.Random()

    technical_quality = float(dish.get("secret_quality", 0.5))

    user_vector = user_data.get("secret_characteristics_vector", {})
    dish_vector = dish.get("secret_characteristics_vector", {})

    archetype = dish.get("secret_archetype", "Inne")
    archetype_metadata = get_archetype_metadata(archetype, vectors_data)
    adaptation_weights = archetype_metadata["base_weights"]
    base_characteristics = archetype_metadata["base_characteristics"]
    penalty_vector = dish.get("secret_penalty_vector")

    sensory_fit = calculate_affinity(
        user_vector=user_vector,
        dish_vector=dish_vector,
        adaptation_weights=adaptation_weights,
        base_characteristics=base_characteristics,
        penalty_weights=penalty_vector,
        contextual_targets=contextual_target_vector,
    )

    base_score = technical_quality * 10.0

    affinity_shift = (sensory_fit - 0.5) * 4.0
    base_score += affinity_shift

    category_affinity = user_data.get("secret_enjoyed_archetypes", {}).get(archetype, 0.5)
    if category_affinity < 0.3:
        base_score -= (0.3 - category_affinity) * 8.0  # up to -2.4
    elif category_affinity > 0.7:
        base_score += (category_affinity - 0.7) * 3.0  # up to +0.9

    baseline = float(user_data.get("secret_rating_baseline", 6.0))
    base_score += (baseline - 6.0) * 0.5  # ~±1.0 for typical users

    base_score += rng.gauss(0, 1.0)

    if rng.random() < 0.05:
        base_score -= rng.uniform(2.0, 4.0)

    return max(1.0, min(10.0, base_score))
