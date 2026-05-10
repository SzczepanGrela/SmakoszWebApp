import hashlib
import logging
import random
from typing import Any

from algorithms.preference_calculator import (
    DIMENSIONS,
    calculate_contextual_vector,
    clamp,
)

logger = logging.getLogger(__name__)

NOISE_STDEV = 0.03

def _get_deterministic_rng(username: str, variant_name: str) -> random.Random:
    seed_str = f"{username}_{variant_name}"
    hash_bytes = hashlib.md5(seed_str.encode("utf-8")).digest()
    seed_int = int.from_bytes(hash_bytes[:8], "little")
    return random.Random(seed_int)

def get_contextual_preferences(
    vectors_data: dict[str, Any], user: dict, dish: dict, variant_name: str, archetype: str
) -> dict[str, float]:
    username = str(user.get("username") or user.get("user_id", ""))

    blueprint = vectors_data.get(archetype, {})
    archetype_base = blueprint.get("archetype_base", {})

    base_chars = archetype_base.get("characteristics", {})
    adaptation_weights = archetype_base.get("default_weights", {"_default": 1.0})

    variant_data = blueprint.get("variants", {}).get(variant_name, {})
    variant_chars = variant_data.get("characteristics", {})
    variant_weights = variant_data.get("weights")

    user_vector_raw = user.get("secret_characteristics_vector", {})

    user_vector = {}
    for dim in DIMENSIONS:
        val = user_vector_raw.get(dim, 0.5)
        if isinstance(val, dict):
            user_vector[dim] = val
        else:
            user_vector[dim] = {"value": float(val), "tolerance": 0.2}

    target_vector = calculate_contextual_vector(
        user_vector=user_vector,
        archetype_base=base_chars,
        adaptation_weights=adaptation_weights,
        variant_base_override=variant_chars,
        variant_weights_override=variant_weights,
        damping_factor=0.8,
    )

    if not target_vector:
        target_vector = dict.fromkeys(DIMENSIONS, 0.5)

    rng = _get_deterministic_rng(username, variant_name)

    for dim in list(target_vector.keys()):
        noise = rng.gauss(0, NOISE_STDEV)
        target_vector[dim] = clamp(target_vector[dim] + noise, 0.0, 1.0)

    return target_vector
