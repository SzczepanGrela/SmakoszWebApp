import random

DIMENSIONS: list[str] = [
    "flavor_sweetness",
    "flavor_bitterness",
    "flavor_spiciness",
    "flavor_umami",
    "flavor_sourness",
    "flavor_saltiness",
    "texture_crispy",
    "texture_creamy",
    "texture_chewy",
    "physics_richness",
    "physics_temperature",
    "physics_freshness",
    "context_price_sensitivity",
    "context_portion_preference",
]

def clamp(value: float, min_val: float = 0.0, max_val: float = 1.0) -> float:
    """Clamps value to specified range."""
    return max(min_val, min(value, max_val))

def derive_penalty_weights(
    adaptation_weights: dict[str, float],
    base_characteristics: dict[str, float],
    default_adaptation_weight: float = 1.0,
    extreme_threshold_low: float = 0.15,
    extreme_threshold_high: float = 0.85,
) -> dict[str, float]:
    """
    Derives penalty weights from adaptation weights to catch culinary aberrations.

    Uses tiered penalty boost based on how critical the dimension is:
    - adapt_weight <= 0.3 AND extreme base -> CRITICAL (penalty 50.0)
    - adapt_weight <= 0.5 AND extreme base -> Very Important (penalty 25.0)
    - adapt_weight <= 1.0 AND extreme base -> Important (penalty 10.0)
    - otherwise -> use adaptation weight as penalty

    Returns penalty weights used for affinity calculation weighted averaging.
    """
    penalty_weights = {}

    for dim in DIMENSIONS:
        adapt_weight = adaptation_weights.get(dim, adaptation_weights.get("_default", default_adaptation_weight))
        base_value = base_characteristics.get(dim, 0.5)
        is_extreme = (base_value <= extreme_threshold_low) or (base_value >= extreme_threshold_high)

        if is_extreme:
            if adapt_weight <= 0.3:
                # CRITICAL dimension - violation destroys rating
                penalty_weights[dim] = 50.0
            elif adapt_weight <= 0.5:
                # Very important - major penalty
                penalty_weights[dim] = 25.0
            elif adapt_weight <= 1.0:
                # Important - moderate penalty
                penalty_weights[dim] = 10.0
            else:
                # Modifiable dimension - user preference matters
                penalty_weights[dim] = adapt_weight
        else:
            penalty_weights[dim] = max(1.0, adapt_weight)

    if "_default" in adaptation_weights:
        penalty_weights["_default"] = adaptation_weights["_default"]

    return penalty_weights

def calculate_direct_affinity(
    target_vector: dict[str, float],
    dish_vector: dict[str, float],
    user_vector: dict[str, dict[str, float]],
    adaptation_weights: dict[str, float],
    base_characteristics: dict[str, float],
    penalty_weights: dict[str, float] | None = None,
    default_weight: float = 1.0,
) -> float:
    """
    Calculates affinity using pre-calculated contextual targets (optimization path).

    Skips contextual vector calculation and relevance gating. Used when targets
    are already materialized in user_variant_preferences table.
    """
    if penalty_weights is None:
        penalty_weights = derive_penalty_weights(
            adaptation_weights=adaptation_weights,
            base_characteristics=base_characteristics,
            default_adaptation_weight=default_weight,
        )

    weighted_score_sum = 0.0
    total_weight = 0.0

    for dim in DIMENSIONS:
        user_pref = user_vector.get(dim, {"value": 0.5, "tolerance": 0.2})

        if isinstance(user_pref, (int, float)):
            tolerance = 0.2
        else:
            tolerance = user_pref.get("tolerance", 0.2)

        contextual_target = target_vector.get(dim, 0.5)
        dish_value = dish_vector.get(dim, 0.5)
        penalty_weight = penalty_weights.get(dim, default_weight)

        # Penalty weight from mask (no runtime override - trust the blueprint)

        diff = abs(contextual_target - dish_value)

        if diff <= tolerance:
            penalty = 0.0
        else:
            excess = diff - tolerance
            max_excess = 1.0 - tolerance
            penalty = (excess / max_excess) ** 2 if max_excess > 0 else 0.0

        dimension_score = clamp(1.0 - penalty, 0.0, 1.0)
        weighted_score_sum += dimension_score * penalty_weight
        total_weight += penalty_weight

    return clamp(weighted_score_sum / total_weight, 0.0, 1.0) if total_weight > 0 else 0.5

def calculate_contextual_vector(
    user_vector: dict[str, dict[str, float]],
    archetype_base: dict[str, float],
    adaptation_weights: dict[str, float],
    variant_base_override: dict[str, float] | None = None,
    variant_weights_override: dict[str, float] | None = None,
    damping_factor: float = 0.8,
) -> dict[str, float]:
    """
    Calculates contextualized target vector for user-variant combination.

    Implements relevance-gated preference logic where adaptation weights control
    how much user preferences influence the archetype base characteristics.
    Weight > 1.0 indicates relevant dimension where user preferences matter.
    """
    merged_base = {**archetype_base}
    if variant_base_override:
        merged_base.update(variant_base_override)

    merged_weights = {**adaptation_weights}
    if variant_weights_override:
        merged_weights.update(variant_weights_override)

    contextual_vector = {}

    for dim in DIMENSIONS:
        user_pref = user_vector.get(dim, {"value": 0.5, "tolerance": 0.2})

        if isinstance(user_pref, (int, float)):
            user_pref_value = float(user_pref)
        else:
            user_pref_value = user_pref.get("value", 0.5)

        user_bias = user_pref_value - 0.5
        category_weight = merged_weights.get(dim, merged_weights.get("_default", 1.0))

        if category_weight <= 1.0:
            relevance_factor = 0.0
        else:
            relevance_factor = clamp(category_weight - 1.0, 0.0, 1.0)

        base_value = merged_base.get(dim, 0.5)
        shift = user_bias * relevance_factor * damping_factor
        contextual_target = clamp(base_value + shift, 0.0, 1.0)

        contextual_vector[dim] = contextual_target

    return contextual_vector

def calculate_affinity(
    user_vector: dict[str, dict[str, float]],
    dish_vector: dict[str, float],
    adaptation_weights: dict[str, float],
    base_characteristics: dict[str, float],
    penalty_weights: dict[str, float] | None = None,
    default_weight: float = 1.0,
    archetype_base: dict[str, float] | None = None,
    adaptation_weights_legacy: dict[str, float] | None = None,
    contextual_targets_override: dict[str, float] | None = None,
) -> float:
    """
    Calculates affinity between user preferences and dish characteristics.

    Uses Quadratic Penalty Function with user tolerance zones and relevance-gated
    preference logic. Supports dual-weight system (adaptation vs penalty).

    If contextual_targets_override provided, skips contextual calculation (optimization).
    """
    # Backward compatibility
    if archetype_base is not None and base_characteristics == {}:
        base_characteristics = archetype_base
    if adaptation_weights_legacy is not None and adaptation_weights == {}:
        adaptation_weights = adaptation_weights_legacy

    if penalty_weights is None:
        penalty_weights = derive_penalty_weights(
            adaptation_weights=adaptation_weights,
            base_characteristics=base_characteristics,
            default_adaptation_weight=default_weight,
        )

    weighted_score_sum = 0.0
    total_weight = 0.0

    if contextual_targets_override is not None:
        contextual_targets = contextual_targets_override
    else:
        contextual_targets = calculate_contextual_vector(
            user_vector=user_vector,
            archetype_base=base_characteristics,
            adaptation_weights=adaptation_weights,
            variant_base_override=None,
            variant_weights_override=None,
            damping_factor=0.8,
        )

    for dim in DIMENSIONS:
        user_pref = user_vector.get(dim, {"value": 0.5, "tolerance": 0.2})

        if isinstance(user_pref, (int, float)):
            tolerance = 0.2
        else:
            tolerance = user_pref.get("tolerance", 0.2)

        contextual_target = contextual_targets.get(dim, 0.5)
        dish_value = dish_vector.get(dim, 0.5)
        penalty_weight = penalty_weights.get(dim, default_weight)

        # Penalty weight from mask (no runtime override - trust the blueprint)

        diff = abs(contextual_target - dish_value)

        if diff <= tolerance:
            penalty = 0.0
        else:
            excess = diff - tolerance
            max_excess = 1.0 - tolerance
            penalty = (excess / max_excess) ** 2 if max_excess > 0 else 0.0

        dimension_score = clamp(1.0 - penalty, 0.0, 1.0)
        weighted_score_sum += dimension_score * penalty_weight
        total_weight += penalty_weight

    return clamp(weighted_score_sum / total_weight, 0.0, 1.0) if total_weight > 0 else 0.5

def merge_vectors(base: dict[str, float], override: dict[str, float] | None) -> dict[str, float]:
    """Merges two vectors with override taking precedence."""
    return {**base, **(override or {})}

def apply_restaurant_bias(
    dish_vector: dict[str, float], archetype: str, restaurant_modifiers: dict[str, dict[str, float]]
) -> dict[str, float]:
    """
    Applies restaurant-specific modifiers to dish characteristics.
    Models restaurant style influence (e.g., saltier, richer preparation).
    """
    if archetype not in restaurant_modifiers:
        return dish_vector

    modifiers = restaurant_modifiers[archetype]
    result = dish_vector.copy()

    for dim, offset in modifiers.items():
        current_val = result.get(dim, 0.5)
        result[dim] = clamp(current_val + offset, 0.0, 1.0)

    return result

def add_dish_variance(dish_vector: dict[str, float], variance: float = 0.05) -> dict[str, float]:
    """Adds Gaussian noise to dish characteristics to simulate natural variation."""
    result = {}
    for dim, value in dish_vector.items():
        noise = random.gauss(0, variance)
        result[dim] = clamp(value + noise, 0.0, 1.0)

    return result
