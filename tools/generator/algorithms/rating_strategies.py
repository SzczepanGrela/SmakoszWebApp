import json
import random
import unicodedata
from abc import ABC, abstractmethod
from typing import Any

import numpy as np

from utils.blueprint_loader import BlueprintLoader

from .core_rating_logic import calculate_food_score_polarized, sigmoid_stretch

def _normalize_ingredient(name: str) -> str:
    name = name.strip().lower()
    name = unicodedata.normalize("NFD", name)
    name = "".join(c for c in name if unicodedata.category(c) != "Mn")
    return unicodedata.normalize("NFC", name)

class RatingComponentStrategy(ABC):

    @abstractmethod
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
        pass

class ServiceRatingStrategy(RatingComponentStrategy):
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
        base_quality = float(restaurant.get("secret_service_quality", 0.5))
        weights = context.get("scoring_weights", {})

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

        if isinstance(user_expectations, str):
            user_expectations = json.loads(user_expectations)

        expected_score = float(user_expectations.get(tier_key, expected_baseline * 10.0))
        expected_quality = expected_score / 10.0

        score = base_quality * 10.0

        penalty_mult = weights.get("service_failure_penalty_multiplier", 12.0)
        bonus_mult = weights.get("service_exceed_bonus_multiplier", 5.0)

        if base_quality < expected_quality:
            penalty = (expected_quality - base_quality) * penalty_mult
            score -= penalty

        if base_quality > expected_quality + 0.1:
            score += (base_quality - expected_quality) * bonus_mult

        variance = np.random.normal(0, 0.12)
        score += variance * 10.0
        return max(1.0, min(10.0, score))

class CleanlinessRatingStrategy(RatingComponentStrategy):
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
        base_quality = float(restaurant.get("secret_cleanliness_score", 0.5))
        weights = context.get("scoring_weights", {})

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
        if isinstance(user_expectations, str):
            user_expectations = json.loads(user_expectations)

        expected_score = float(user_expectations.get(tier_key, expected_baseline * 10.0))
        expected_quality = expected_score / 10.0

        score = base_quality * 10.0

        penalty_mult = weights.get("cleanliness_failure_penalty_multiplier", 15.0)

        if base_quality < expected_quality:
            penalty = (expected_quality - base_quality) * penalty_mult
            score -= penalty

        variance = np.random.normal(0, 0.05)
        score += variance * 10.0
        return max(1.0, min(10.0, score))

class AmbianceRatingStrategy(RatingComponentStrategy):
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
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

        variance = np.random.normal(0, 0.15)
        score += variance * 10.0
        return max(1.0, min(10.0, score))

class ValueRatingStrategy(RatingComponentStrategy):
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
        user_vector = user_data.get("secret_characteristics_vector", {})
        if isinstance(user_vector, str):
            user_vector = json.loads(user_vector)

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
            score = 6.0 + random.uniform(-1.0, 1.0)

        noise = random.gauss(0, 0.5)
        return max(1.0, min(10.0, score + noise))

class FoodRatingStrategy(RatingComponentStrategy):
    def calculate(
        self, user_data: dict[str, Any], dish: dict[str, Any], restaurant: dict[str, Any], context: dict[str, Any]
    ) -> float:
        contextual_target = context.get("user_variant_preference_vector")
        vectors_data = context.get("vectors_data")
        weights = context.get("scoring_weights", {})

        base_score = calculate_food_score_polarized(user_data, dish, restaurant, contextual_target, vectors_data)

        ingredients_raw = dish.get("ingredients_json", [])
        if isinstance(ingredients_raw, str):
            try:
                ingredients = json.loads(ingredients_raw)
            except json.JSONDecodeError:
                ingredients = []
        else:
            ingredients = ingredients_raw

        user_prefs_raw = user_data.get("secret_ingredient_preferences", {})
        if isinstance(user_prefs_raw, str):
            try:
                user_prefs = json.loads(user_prefs_raw)
            except json.JSONDecodeError:
                user_prefs = {}
        else:
            user_prefs = user_prefs_raw

        ingredient_modifier = 0.0
        if ingredients and user_prefs:
            bonus_love = weights.get("ingredient_love_bonus", 1.5)
            penalty_hate = weights.get("ingredient_hate_penalty", 2.0)
            penalty_minor = weights.get("ingredient_minor_penalty", 0.5)

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

            cap_min = weights.get("ingredient_score_cap_min", -3.0)
            cap_max = weights.get("ingredient_score_cap_max", 2.0)
            ingredient_modifier = max(cap_min, min(cap_max, ingredient_modifier))

        final_score = base_score + ingredient_modifier

        cleanliness_score = float(restaurant.get("secret_cleanliness_score", 0.5))
        cleanliness_threshold = 0.3

        if cleanliness_score < cleanliness_threshold:
            cleanliness_penalty = (cleanliness_threshold - cleanliness_score) * 10.0
            final_score -= cleanliness_penalty

        return max(1.0, min(10.0, final_score))

class RatingAggregator:
    def __init__(self):
        self.strategies: dict[str, RatingComponentStrategy] = {
            "food": FoodRatingStrategy(),
            "service": ServiceRatingStrategy(),
            "cleanliness": CleanlinessRatingStrategy(),
            "ambiance": AmbianceRatingStrategy(),
            "value": ValueRatingStrategy(),
        }
        self.weights = {"food": 0.50, "service": 0.15, "cleanliness": 0.10, "ambiance": 0.10, "value": 0.15}

        try:
            loader = BlueprintLoader("blueprints")
            global_config = loader.load_blueprint("global_config.json")
            self.scoring_weights = global_config.get("SCORING_WEIGHTS", {})
        except Exception:
            self.scoring_weights = {}

    def calculate_all(
        self,
        user_data: dict[str, Any],
        dish: dict[str, Any],
        restaurant: dict[str, Any],
        user_variant_preference_vector: dict[str, float] | None = None,
        vectors_data: dict[str, Any] | None = None,
    ) -> dict[str, float]:
        context = {
            "user_variant_preference_vector": user_variant_preference_vector,
            "vectors_data": vectors_data,
            "scoring_weights": self.scoring_weights,
        }

        components = {}
        for name, strategy in self.strategies.items():
            components[name] = strategy.calculate(user_data, dish, restaurant, context)

        baseline = float(user_data.get("secret_rating_baseline", 6.0))

        min_component_name = min(components, key=lambda k: components[k])
        min_component_score = components[min_component_name]

        if min_component_score < 3.0:
            overall_rating = min_component_score + 1.5
        else:
            weighted_mean = sum(components[name] * self.weights.get(name, 0.0) for name in components)

            overall_rating = sigmoid_stretch(weighted_mean, midpoint=6.0, steepness=1.2)
            overall_rating = overall_rating * 0.8 + baseline * 0.2

        smoothing_noise = random.gauss(0, 0.5)
        overall_rating += smoothing_noise
        overall_rating = max(1.0, min(10.0, overall_rating))

        result = {f"{name}_score": round(val, 2) for name, val in components.items()}
        result["value_for_money_score"] = result.pop("value_score")
        result["overall_rating"] = round(overall_rating, 2)

        return result
