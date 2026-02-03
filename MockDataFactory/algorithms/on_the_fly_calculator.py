"""
On-the-fly preference calculator using deterministic seeding.
Replaces materialized user_variant_preferences table with zero-IO calculation.
"""

import hashlib
import random
import logging
from typing import Any

from algorithms.preference_calculator import (
    calculate_contextual_vector,
    merge_vectors,
    DIMENSIONS,
    clamp,
)
from utils.helpers import safe_json_loads

logger = logging.getLogger(__name__)

def _get_deterministic_rng(user_id: int, dish_name: str, variant_name: str) -> random.Random:
    """
    Create reproducible RNG from user+dish combination.
    
    Uses MD5 hash to generate a stable seed that works across Python restarts.
    The same (user_id, dish_name, variant_name) tuple always produces the same RNG state.
    """
    seed_str = f"{user_id}_{dish_name}_{variant_name}"
    hash_bytes = hashlib.md5(seed_str.encode('utf-8')).digest()
    seed_int = int.from_bytes(hash_bytes[:8], 'little')
    return random.Random(seed_int)

class OnTheFlyCalculator:
    # Standard deviation for reproducible noise
    NOISE_STDEV = 0.03
    
    def __init__(self, vectors_data: dict[str, Any]):
        """
        Args:
            vectors_data: The loaded `vectors.json` or `dishes.json` blueprint.
        """
        self.vectors_data = vectors_data

    def get_contextual_preferences(
        self, 
        user: dict, 
        dish: dict, 
        variant_name: str, 
        archetype: str
    ) -> dict[str, float]:
        """
        Calculate preference vector on-the-fly using deterministic seeding.
        
        Uses weight-based relevance gating:
        - Dimensions with weight > 1.0 are MODIFIABLE (user preferences shift the target)
        - Dimensions with weight <= 1.0 are LOCKED (target stays at archetype base)
        
        Args:
            user: User data dict (must contain 'user_id' and 'secret_characteristics_vector').
            dish: Dish data dict (must contain 'dish_id' and 'dish_name').
            variant_name: Name of the variant (e.g., 'Margherita').
            archetype: Archetype name (e.g., 'Pizza').

        Returns:
            dict: The contextual target preference vector for this specific interaction.
        """
        user_id = user.get("user_id", 0)
        dish_name = dish.get("dish_name", variant_name)

        blueprint = self.vectors_data.get(archetype, {})
        archetype_base = blueprint.get("archetype_base", {})
        
        base_chars = archetype_base.get("characteristics", {})
        adaptation_weights = archetype_base.get("default_weights", {"_default": 1.0})
        
        variant_data = blueprint.get("variants", {}).get(variant_name, {})
        variant_chars = variant_data.get("characteristics", {})
        variant_weights = variant_data.get("weights")  # May be None

        user_vector_raw = user.get("secret_characteristics_vector", {})
        if isinstance(user_vector_raw, str):
            user_vector_raw = safe_json_loads(user_vector_raw, {})
        
        # Convert to format expected by calculate_contextual_vector
        # (dict[str, dict] with 'value' and 'tolerance' keys)
        user_vector = {}
        for dim in DIMENSIONS:
            val = user_vector_raw.get(dim, 0.5)
            if isinstance(val, dict):
                user_vector[dim] = val
            else:
                user_vector[dim] = {"value": float(val), "tolerance": 0.2}

        # Weights control how much user preferences matter
        target_vector = calculate_contextual_vector(
            user_vector=user_vector,
            archetype_base=base_chars,
            adaptation_weights=adaptation_weights,
            variant_base_override=variant_chars,
            variant_weights_override=variant_weights,
            damping_factor=0.8,
        )
        
        # Handle unknown archetypes (fallback to neutral)
        if not target_vector:
            target_vector = {dim: 0.5 for dim in DIMENSIONS}

        # Same user + same dish always gets same noise (deterministic)
        rng = _get_deterministic_rng(user_id, dish_name, variant_name)
        
        for dim in list(target_vector.keys()):
            noise = rng.gauss(0, self.NOISE_STDEV)
            target_vector[dim] = clamp(target_vector[dim] + noise, 0.0, 1.0)
        
        return target_vector
