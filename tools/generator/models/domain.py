from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

@dataclass(slots=True)
class UserForReview:
    user_id: int
    city_id: int
    secret_total_review_count: int
    travel_propensity: float
    secret_enjoyed_archetypes: dict
    secret_ingredient_preferences: dict
    secret_cleanliness_preference: dict
    secret_preferred_ambiance: str
    secret_mood_propensity: float
    secret_cross_impact_factor: float
    secret_chance_dine_random: float
    secret_chance_pick_random_dish: float
    join_date: datetime | None
    secret_characteristics_vector: dict
    secret_rating_baseline: float
    secret_spice_preference: float
    secret_richness_preference: float
    secret_texture_preference: float
    secret_price_preference_range: float = 35.0
    secret_price_tolerance_above: float = 2.0
    secret_price_tolerance_below: float = 0.5

@dataclass(slots=True)
class UserForSocial:
    user_id: int
    username: str
    secret_home_city_id: int
    secret_is_influencer: bool

@dataclass(slots=True)
class RestaurantForReview:
    restaurant_id: int
    city_id: int
    cuisine_type: str
    created_at: datetime | None
    secret_price_multiplier: float
    secret_overall_food_quality: float
    secret_service_quality: float
    secret_cleanliness_score: float
    secret_ambiance_type: str
    secret_ambiance_quality: float

@dataclass(slots=True)
class RestaurantForDishes:
    restaurant_id: int
    secret_menu_blueprint: str
    secret_price_multiplier: float
    secret_archetype_modifiers: dict
    status: str
    created_at: str | None

@dataclass(slots=True)
class DishForReview:
    dish_id: int
    dish_name: str
    secret_archetype: str
    price: float
    secret_base_price: float
    secret_quality: float
    secret_popularity_factor: float
    secret_characteristics_vector: dict
    secret_penalty_vector: dict | None
    secret_variant_name: str
    ingredients: list[str]

@dataclass(slots=True)
class CityInfo:
    city_id: int
    city_name: str
