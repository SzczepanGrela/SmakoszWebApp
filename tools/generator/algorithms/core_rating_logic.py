import json
import logging
import math
import random
from typing import Any

from .preference_calculator import calculate_affinity, calculate_direct_affinity

logger = logging.getLogger(__name__)

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
) -> float:
    technical_quality = float(dish.get("secret_quality", 0.5))

    user_vector = user_data.get("secret_characteristics_vector", {})
    dish_vector = dish.get("secret_characteristics_vector", {})

    if isinstance(user_vector, str):
        user_vector = json.loads(user_vector)
    if isinstance(dish_vector, str):
        dish_vector = json.loads(dish_vector)

    if contextual_target_vector is not None:
        archetype = dish.get("secret_archetype", "Inne")
        archetype_metadata = get_archetype_metadata(archetype, vectors_data)
        adaptation_weights = archetype_metadata["base_weights"]
        base_characteristics = archetype_metadata["base_characteristics"]
        penalty_vector = dish.get("secret_penalty_vector")
        if isinstance(penalty_vector, str):
            penalty_vector = json.loads(penalty_vector)

        sensory_fit = calculate_direct_affinity(
            target_vector=contextual_target_vector,
            dish_vector=dish_vector,
            user_vector=user_vector,
            adaptation_weights=adaptation_weights,
            base_characteristics=base_characteristics,
            penalty_weights=penalty_vector,
        )
    else:
        archetype = dish.get("secret_archetype", "Inne")
        archetype_metadata = get_archetype_metadata(archetype, vectors_data)
        base_characteristics = archetype_metadata["base_characteristics"]
        adaptation_weights = archetype_metadata["base_weights"]
        penalty_vector = dish.get("secret_penalty_vector")
        if isinstance(penalty_vector, str):
            penalty_vector = json.loads(penalty_vector)

        sensory_fit = calculate_affinity(
            user_vector=user_vector,
            dish_vector=dish_vector,
            adaptation_weights=adaptation_weights,
            base_characteristics=base_characteristics,
            penalty_weights=penalty_vector,
        )

    base_score = technical_quality * 10.0

    if sensory_fit < 0.5:
        penalty = (0.5 - sensory_fit) * 6.0
        base_score -= penalty

    archetype = dish.get("secret_archetype", "Inne")
    category_affinity = user_data.get("secret_enjoyed_archetypes", {}).get(archetype, 0.5)
    if category_affinity < 0.3:
        base_score = min(base_score, 4.0)

    noise = random.gauss(0, 1.5)
    final_score = base_score + noise

    if random.random() < 0.05:
        mishap_penalty = random.uniform(2.0, 4.0)
        final_score -= mishap_penalty

    return max(1.0, min(10.0, final_score))
