import random
from typing import Any

from utils.distributions import zipf_distribution

def select_restaurants_for_user(
    user: dict[str, Any], all_restaurants: list[dict[str, Any]], city_id: int, count: int
) -> list[int]:
    city_restaurants = [r for r in all_restaurants if r["city_id"] == city_id]
    if not city_restaurants:
        return []

    zipf_probs = zipf_distribution(len(city_restaurants), alpha=1.5)
    for i, res in enumerate(city_restaurants):
        res["popularity"] = zipf_probs[i]

    city_restaurants.sort(key=lambda x: x["popularity"], reverse=True)

    is_power_user = user.get("secret_total_review_count", 35) >= 100

    if is_power_user:
        top_percentage = 0.30
        top_visit_rate = 0.80
    else:
        top_percentage = 0.20
        top_visit_rate = 0.40

    top_count = max(1, int(len(city_restaurants) * top_percentage))
    top_restaurants = city_restaurants[:top_count]
    other_restaurants = city_restaurants[top_count:]

    selected = []
    enjoyed_themes = user.get("secret_enjoyed_archetypes", {})
    random_chance = user.get("secret_chance_dine_random", 0.1)

    for _ in range(count):
        restaurant: dict[str, Any] | None = None
        if random.random() < random_chance:
            if all_restaurants:
                restaurant = random.choice(city_restaurants)
            else:
                restaurant = None
        elif random.random() < top_visit_rate and top_restaurants:
            restaurant = _select_with_theme_preference(top_restaurants, enjoyed_themes)
        elif other_restaurants:
            restaurant = _select_with_theme_preference(other_restaurants, enjoyed_themes)
        else:
            restaurant = _select_with_theme_preference(top_restaurants, enjoyed_themes)

        if restaurant and restaurant["restaurant_id"] not in selected:
            selected.append(restaurant["restaurant_id"])

    return selected[:count]

def _select_with_theme_preference(restaurants: list[dict], enjoyed_themes: dict[str, float]) -> dict[str, Any] | None:
    if not restaurants:
        return None

    weights = []
    for restaurant in restaurants:
        theme = restaurant.get("cuisine_type", "Unknown")
        affinity = enjoyed_themes.get(theme, 0.5)
        weight = affinity + restaurant.get("popularity", 0.1)
        weights.append(weight)

    total_weight = sum(weights)
    if total_weight == 0:
        return random.choice(restaurants)

    normalized_weights = [w / total_weight for w in weights]
    return random.choices(restaurants, weights=normalized_weights, k=1)[0]
