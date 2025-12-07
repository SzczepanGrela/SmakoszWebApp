"""
Dish Selector - Wybór dania z menu restauracji (UPDATED - Realistic Human Behavior)
"""

import random
import math
from typing import Dict, Any, List

def select_dish_from_menu(user: Dict[str, Any],
                          restaurant_dishes: List[Dict[str, Any]]) -> Dict[str, Any]:
    """
    Wybiera danie z menu restauracji - SYMULACJA REALISTYCZNYCH ZACHOWAŃ

    Decision Modes (Realistic Human Dining Behavior):
    - 15% chance: Purely Random (experimentation, "Today's Special", friend recommendation)
    - 10% chance: Popularity-Driven (visual bias, "everyone is ordering this", Instagram-worthy)
    - 75% chance: Preference-Based (but with FLATTENED weights to avoid perfect matches)

    OLD BEHAVIOR: Always picked dishes with ~0.95 sensory fit (too perfect)
    NEW BEHAVIOR: Average sensory fit 0.60-0.95 (realistic variance)

    Args:
        user: Dane użytkownika
        restaurant_dishes: Lista dań w restauracji

    Returns:
        Wybrane danie
    """
    if not restaurant_dishes:
        return None

    # Decision Mode Selection
    decision_roll = random.random()

    # MODE 1: PURE RANDOMNESS (15% - increased from 5%)
    # Simulates: "I'll try the special", "My friend recommended this", "Feeling adventurous"
    random_chance = user.get('secret_chance_pick_random_dish', 0.15)
    if decision_roll < random_chance:
        return random.choice(restaurant_dishes)

    # MODE 2: POPULARITY BIAS (10%)
    # Simulates: "Everyone is ordering this", "Waiter recommended", "Looks good on Instagram"
    popularity_bias_chance = 0.10
    if decision_roll < (random_chance + popularity_bias_chance):
        # Weight heavily by popularity instead of personal preference
        popularity_scores = [d.get('secret_popularity_factor', 0.1) for d in restaurant_dishes]
        total_pop = sum(popularity_scores)
        if total_pop > 0:
            pop_weights = [s / total_pop for s in popularity_scores]
            return random.choices(restaurant_dishes, weights=pop_weights, k=1)[0]
        else:
            return random.choice(restaurant_dishes)

    # MODE 3: PREFERENCE-BASED (78% - but with FLATTENED weights)
    # This is where we fix the "perfect match" problem

    # Preferencje użytkownika
    enjoyed_archetypes = user.get('secret_enjoyed_archetypes', {})
    ingredient_prefs = user.get('secret_ingredient_preferences', {})

    # Oblicz score dla każdego dania
    dish_scores = []

    for dish in restaurant_dishes:
        score = 0.0

        # 1. Affinity do archetypu (REDUCED weight to prevent dominance)
        # OLD: * 10 (too strong, always picked max)
        # NEW: * 4 (still influential but not overwhelming)
        archetype = dish.get('secret_archetype', 'Unknown')
        archetype_affinity = enjoyed_archetypes.get(archetype, 0.5)
        score += archetype_affinity * 4

        # 2. Składniki (slightly reduced penalty)
        dish_ingredients = dish.get('ingredients', [])
        disliked_count = 0

        for ingredient in dish_ingredients:
            pref = ingredient_prefs.get(ingredient, 0.5)
            if pref < 0.3:  # Nie lubi
                disliked_count += 1

        # Kara za nielubiane składniki (reduced from 2 to 1.5)
        score -= disliked_count * 1.5

        # 3. Popularność dania (BOOSTED influence for visual bias)
        # OLD: + popularity (0.1-1.0 range, negligible)
        # NEW: * 2.5 (now 0.25-2.5 range, meaningful)
        popularity = dish.get('secret_popularity_factor', 0.1)
        score += popularity * 2.5

        # 4. Losowy szum (INCREASED variance to flatten distribution)
        # OLD: ±0.5 (too small to matter)
        # NEW: ±1.8 (significant impact, can swing close decisions)
        score += random.uniform(-1.8, 1.8)

        dish_scores.append((dish, max(0, score)))

    # Softmax-style selection with temperature for flattened probability curve
    dishes = [d[0] for d in dish_scores]
    scores = [d[1] for d in dish_scores]

    total_score = sum(scores)
    if total_score == 0:
        return random.choice(dishes)

    # Apply temperature scaling (softmax) to flatten the distribution
    # Temperature > 1.0 makes selection more uniform (less greedy)
    # This prevents the top-scoring dish from dominating
    temperature = 2.5
    exp_scores = [math.exp(s / temperature) for s in scores]
    total_exp = sum(exp_scores)
    weights = [s / total_exp for s in exp_scores]

    return random.choices(dishes, weights=weights, k=1)[0]
