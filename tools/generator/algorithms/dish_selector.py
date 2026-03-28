import math
import random
from typing import Any

from config import GENERATION_CONFIG

def select_dish_from_menu(user: dict[str, Any], restaurant_dishes: list[dict[str, Any]]) -> dict[str, Any] | None:
    if not restaurant_dishes:
        return None

    decision_roll = random.random()

    random_chance = user.get("secret_chance_pick_random_dish", 0.15)
    if decision_roll < random_chance:
        return random.choice(restaurant_dishes)

    popularity_bias_chance = 0.10
    if decision_roll < (random_chance + popularity_bias_chance):
        popularity_scores = [d.get("secret_popularity_factor", 0.1) for d in restaurant_dishes]
        total_pop = sum(popularity_scores)
        if total_pop > 0:
            pop_weights = [s / total_pop for s in popularity_scores]
            return random.choices(restaurant_dishes, weights=pop_weights, k=1)[0]
        else:
            return random.choice(restaurant_dishes)

    enjoyed_archetypes = user.get("secret_enjoyed_archetypes", {})
    ingredient_prefs = user.get("secret_ingredient_preferences", {})

    dish_scores = []

    for dish in restaurant_dishes:
        score = 0.0

        archetype = dish.get("secret_archetype", "Unknown")
        archetype_affinity = enjoyed_archetypes.get(archetype, 0.5)
        score += archetype_affinity * 4

        dish_ingredients = dish.get("ingredients", [])
        disliked_count = 0
        for ingredient in dish_ingredients:
            pref = ingredient_prefs.get(ingredient, 0.5)
            if pref < 0.3:
                disliked_count += 1

        score -= disliked_count * 1.5

        popularity = dish.get("secret_popularity_factor", 0.1)
        score += popularity * 2.5
        score += random.uniform(-1.8, 1.8)

        dish_scores.append((dish, max(0, score)))

    dishes = [d[0] for d in dish_scores]
    scores = [d[1] for d in dish_scores]

    total_score = sum(scores)
    if total_score == 0:
        return random.choice(dishes)

    temperature = float(GENERATION_CONFIG.get("dish_selection_temperature", 2.5))
    exp_scores = [math.exp(s / temperature) for s in scores]
    total_exp = sum(exp_scores)
    weights = [s / total_exp for s in exp_scores]

    return random.choices(dishes, weights=weights, k=1)[0]
