"""
Dish Selector - Wybór dania z menu restauracji
"""

import random
from typing import Dict, Any, List
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def select_dish_from_menu(user: Dict[str, Any],
                          restaurant_dishes: List[Dict[str, Any]]) -> Dict[str, Any]:
    """
    Wybiera danie z menu restauracji

    Zasady:
    - 95% czasu: wybór bazowany na preferencjach (enjoyed_archetypes)
    - 5% czasu: całkowicie losowy (eksploracja)
    - Unikaj składników których nie lubi (< 0.3 preference)

    Args:
        user: Dane użytkownika
        restaurant_dishes: Lista dań w restauracji

    Returns:
        Wybrane danie
    """
    if not restaurant_dishes:
        return None

    # 5% szans na całkowicie losowy wybór (eksploracja)
    if random.random() < 0.05:
        return random.choice(restaurant_dishes)

    # Preferencje użytkownika
    enjoyed_archetypes = user.get('secret_enjoyed_archetypes', {})
    ingredient_prefs = user.get('secret_ingredient_preferences', {})

    # Oblicz score dla każdego dania
    dish_scores = []

    for dish in restaurant_dishes:
        score = 0.0

        # 1. Affinity do archetypu (duży wpływ)
        archetype = dish.get('archetype', 'Unknown')
        archetype_affinity = enjoyed_archetypes.get(archetype, 0.5)
        score += archetype_affinity * 10

        # 2. Składniki
        dish_ingredients = dish.get('ingredients', [])
        disliked_count = 0

        for ingredient in dish_ingredients:
            pref = ingredient_prefs.get(ingredient, 0.5)
            if pref < 0.3:  # Nie lubi
                disliked_count += 1

        # Kara za nielubiane składniki
        score -= disliked_count * 2

        # 3. Popularność dania (z Zipf distribution)
        popularity = dish.get('popularity_factor', 0.1)
        score += popularity

        # 4. Losowy szum (małe odchylenie)
        score += random.uniform(-0.5, 0.5)

        dish_scores.append((dish, max(0, score)))

    # Wybierz danie z najwyższym score (z prawdopodobieństwem proporcjonalnym do score)
    dishes = [d[0] for d in dish_scores]
    scores = [d[1] for d in dish_scores]

    total_score = sum(scores)
    if total_score == 0:
        return random.choice(dishes)

    weights = [s / total_score for s in scores]

    return random.choices(dishes, weights=weights, k=1)[0]
