import random as _random_module
import unicodedata
from typing import Any

from utils.blueprint_loader import BlueprintLoader

from .core_rating_logic import calculate_food_score_polarized, get_review_rng

def _normalize_ingredient(name: str) -> str:
    name = name.strip().lower()
    name = unicodedata.normalize("NFD", name)
    name = "".join(c for c in name if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", name)

_scoring_weights: dict | None = None

def _get_scoring_weights() -> dict:
    global _scoring_weights
    if _scoring_weights is None:
        try:
            loader = BlueprintLoader("blueprints")
            global_config = loader.load_blueprint("global_config.json")
            _scoring_weights = global_config.get("SCORING_WEIGHTS", {})
        except Exception:
            _scoring_weights = {}
    return _scoring_weights

def calculate_service_rating(
    user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any],
    scoring_weights: dict, rng: _random_module.Random | None = None,
) -> float:
    if rng is None:
        rng = _random_module.Random()

    base_quality = float(restaurant.get("secret_service_quality", 0.5))

    price_level = int(restaurant.get("price_level", 2))
    if price_level == 1:
        tier_key = "Fast casual"
        expected_baseline = 0.5
    elif price_level == 2:
        tier_key = "Casual"
        expected_baseline = 0.7
    else:
        tier_key = "Fine dining"
        expected_baseline = 0.85

    user_expectations = user_data.get("secret_cleanliness_preference", {})
    expected_score = float(user_expectations.get(tier_key, expected_baseline * 10.0))
    expected_quality = expected_score / 10.0

    score = base_quality * 10.0

    penalty_mult = scoring_weights.get("service_failure_penalty_multiplier", 8.0)
    bonus_mult = scoring_weights.get("service_exceed_bonus_multiplier", 4.0)

    if base_quality < expected_quality:
        penalty = (expected_quality - base_quality) * penalty_mult
        score -= penalty

    if base_quality > expected_quality + 0.1:
        score += (base_quality - expected_quality) * bonus_mult

    baseline = float(user_data.get("secret_rating_baseline", 6.0))
    score += (baseline - 6.0) * 0.3

    score += rng.gauss(0, 0.8)
    return max(1.0, min(10.0, score))

def calculate_cleanliness_rating(
    user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any],
    scoring_weights: dict, rng: _random_module.Random | None = None,
) -> float:
    if rng is None:
        rng = _random_module.Random()

    base_quality = float(restaurant.get("secret_cleanliness_score", 0.5))

    price_level = int(restaurant.get("price_level", 2))
    if price_level == 1:
        tier_key = "Fast casual"
        expected_baseline = 0.5
    elif price_level == 2:
        tier_key = "Casual"
        expected_baseline = 0.7
    else:
        tier_key = "Fine dining"
        expected_baseline = 0.9

    user_expectations = user_data.get("secret_cleanliness_preference", {})
    expected_score = float(user_expectations.get(tier_key, expected_baseline * 10.0))
    expected_quality = expected_score / 10.0

    score = base_quality * 10.0

    penalty_mult = scoring_weights.get("cleanliness_failure_penalty_multiplier", 10.0)

    if base_quality < expected_quality:
        penalty = (expected_quality - base_quality) * penalty_mult
        score -= penalty

    baseline = float(user_data.get("secret_rating_baseline", 6.0))
    score += (baseline - 6.0) * 0.3

    score += rng.gauss(0, 0.6)
    return max(1.0, min(10.0, score))

def calculate_ambiance_rating(
    user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any],
    scoring_weights: dict, rng: _random_module.Random | None = None,
) -> float:
    if rng is None:
        rng = _random_module.Random()

    base_quality = float(restaurant.get("secret_ambiance_quality", 0.5))
    res_type = restaurant.get("secret_ambiance_type", "Casual")
    user_pref = user_data.get("secret_preferred_ambiance", "Casual")

    score = base_quality * 10.0

    if res_type == user_pref:
        score += 1.5
    elif user_pref == "Spokojny" and res_type == "Energiczny":
        score -= 2.0
    else:
        score -= 0.5

    baseline = float(user_data.get("secret_rating_baseline", 6.0))
    score += (baseline - 6.0) * 0.3

    score += rng.gauss(0, 1.5)
    return max(1.0, min(10.0, score))

def calculate_value_rating(
    user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any],
    scoring_weights: dict, rng: _random_module.Random | None = None,
) -> float:
    if rng is None:
        rng = _random_module.Random()

    user_vector = user_data.get("secret_characteristics_vector", {})
    price_sensitivity_data = user_vector.get("context_price_sensitivity", {"value": 0.5})
    if isinstance(price_sensitivity_data, (int, float)):
        sensitivity = float(price_sensitivity_data)
    else:
        sensitivity = price_sensitivity_data.get("value", 0.5)

    actual_price = float(dish.get("price", 35.0))
    quality = float(dish.get("secret_quality", 0.5))
    fair_price = 20.0 + (quality * 60.0)
    price_ratio = actual_price / fair_price

    if price_ratio > 1.5:
        base_score = 1.5
        penalty = (price_ratio - 1.5) * sensitivity * 2.0
        score = max(1.0, base_score - penalty)
    elif price_ratio > 1.2:
        score = 5.0 - (price_ratio - 1.2) * 5.0
    elif price_ratio < 0.8:
        bonus = (0.8 - price_ratio) * 10.0
        score = min(10.0, 8.0 + bonus)
    else:
        score = 6.0 + rng.uniform(-1.0, 1.0)

    noise = rng.gauss(0, 0.5)
    return max(1.0, min(10.0, score + noise))

def calculate_food_rating(
    user_data: dict[str, Any],
    dish: dict[str, Any],
    restaurant: dict[str, Any],
    scoring_weights: dict,
    user_variant_preference_vector: dict[str, float] | None = None,
    vectors_data: dict[str, Any] | None = None,
    rng: _random_module.Random | None = None,
) -> float:
    base_score = calculate_food_score_polarized(
        user_data, dish, restaurant, user_variant_preference_vector, vectors_data, rng=rng,
    )

    ingredients = dish.get("ingredients_json", dish.get("ingredients", []))
    user_prefs = user_data.get("secret_ingredient_preferences", {})

    ingredient_modifier = 0.0
    if ingredients and user_prefs:
        bonus_love = scoring_weights.get("ingredient_love_bonus", 1.5)
        penalty_hate = scoring_weights.get("ingredient_hate_penalty", 2.0)
        penalty_minor = scoring_weights.get("ingredient_minor_penalty", 0.5)

        for ing in ingredients:
            ing_name = ing if isinstance(ing, str) else ing.get("name", "")
            ing_normalized = _normalize_ingredient(ing_name)

            pref_value = None
            for k, v in user_prefs.items():
                if _normalize_ingredient(k) == ing_normalized:
                    pref_value = v
                    break

            if pref_value is not None:
                if pref_value > 0.8:
                    ingredient_modifier += bonus_love
                elif pref_value < 0.2:
                    ingredient_modifier -= penalty_hate
                elif pref_value < 0.4:
                    ingredient_modifier -= penalty_minor

        cap_min = scoring_weights.get("ingredient_score_cap_min", -3.0)
        cap_max = scoring_weights.get("ingredient_score_cap_max", 2.0)
        ingredient_modifier = max(cap_min, min(cap_max, ingredient_modifier))

    final_score = base_score + ingredient_modifier

    cleanliness_score = float(restaurant.get("secret_cleanliness_score", 0.5))
    cleanliness_threshold = 0.3

    if cleanliness_score < cleanliness_threshold:
        cleanliness_penalty = (cleanliness_threshold - cleanliness_score) * 10.0
        final_score -= cleanliness_penalty

    return max(1.0, min(10.0, final_score))

COMPONENT_WEIGHTS = {"food": 0.50, "service": 0.15, "cleanliness": 0.10, "ambiance": 0.10, "value": 0.15}

COMPONENT_FUNCTIONS = {
    "food": calculate_food_rating,
    "service": calculate_service_rating,
    "cleanliness": calculate_cleanliness_rating,
    "ambiance": calculate_ambiance_rating,
    "value": calculate_value_rating,
}

def calculate_review_ratings(
    user_data: dict[str, Any],
    dish: dict[str, Any],
    restaurant: dict[str, Any],
    user_variant_preference_vector: dict[str, float] | None = None,
    vectors_data: dict[str, Any] | None = None,
) -> dict[str, float]:
    sw = _get_scoring_weights()

    username = str(user_data.get("username") or user_data.get("user_id", ""))
    variant_name = str(dish.get("secret_variant_name") or dish.get("dish_name", ""))
    rng = get_review_rng(username, variant_name)

    components = {}
    for name, fn in COMPONENT_FUNCTIONS.items():
        if name == "food":
            components[name] = fn(
                user_data, dish, restaurant, sw,
                user_variant_preference_vector=user_variant_preference_vector,
                vectors_data=vectors_data,
                rng=rng,
            )
        else:
            components[name] = fn(user_data, dish, restaurant, sw, rng=rng)

    weighted_mean = sum(components[name] * COMPONENT_WEIGHTS.get(name, 0.0) for name in components)
    overall_rating = weighted_mean + rng.gauss(0, 0.3)
    overall_rating = max(1.0, min(10.0, overall_rating))

    result = {f"{name}_score": round(val, 2) for name, val in components.items()}
    result["value_for_money_score"] = result.pop("value_score")
    result["overall_rating"] = round(overall_rating, 2)

    return result
