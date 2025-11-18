"""
Restaurant Selector - Wybór restauracji przez użytkownika
"""

import random
from typing import List, Dict, Any
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.statistical import zipf_distribution


def select_restaurants_for_user(user: Dict[str, Any],
                                 all_restaurants: List[Dict[str, Any]],
                                 city_id: int,
                                 count: int) -> List[int]:
    """
    Wybiera restauracje które użytkownik odwiedzi

    Zasady:
    - 40% z TOP 20% najpopularniejszych w mieście (anchor items dla CF)
    - 60% losowo z reszty
    - Power users: 80% z TOP 30%
    - Preferencja dla enjoyed_themes

    Args:
        user: Dane użytkownika
        all_restaurants: Lista wszystkich restauracji
        city_id: ID miasta
        count: Liczba restauracji do wyboru

    Returns:
        Lista restaurant_id
    """
    # Filtruj restauracje w danym mieście
    city_restaurants = [r for r in all_restaurants if r['city_id'] == city_id]

    if not city_restaurants:
        return []

    # Sortuj według popularności (Zipf distribution)
    zipf_probs = zipf_distribution(len(city_restaurants), alpha=1.5)

    # Przypisz prawdopodobieństwa
    for i, restaurant in enumerate(city_restaurants):
        restaurant['popularity'] = zipf_probs[i]

    # Sortuj według popularności
    city_restaurants.sort(key=lambda x: x['popularity'], reverse=True)

    # Określ czy user jest power userem
    is_power_user = user.get('secret_total_review_count', 35) >= 100

    if is_power_user:
        # Power users: 80% wizyt w TOP 30%
        top_percentage = 0.30
        top_visit_rate = 0.80
    else:
        # Zwykli users: 40% wizyt w TOP 20%
        top_percentage = 0.20
        top_visit_rate = 0.40

    # Wyznacz TOP restauracje
    top_count = max(1, int(len(city_restaurants) * top_percentage))
    top_restaurants = city_restaurants[:top_count]
    other_restaurants = city_restaurants[top_count:]

    selected = []
    enjoyed_themes = user.get('secret_enjoyed_archetypes', {})

    for _ in range(count):
        # Czy wybieramy z TOP?
        if random.random() < top_visit_rate and top_restaurants:
            # Wybierz z TOP, z preferencją dla enjoyed themes
            restaurant = _select_with_theme_preference(top_restaurants, enjoyed_themes)
        elif other_restaurants:
            # Wybierz losowo z pozostałych
            restaurant = _select_with_theme_preference(other_restaurants, enjoyed_themes)
        else:
            # Jeśli brak innych, wybierz z TOP
            restaurant = _select_with_theme_preference(top_restaurants, enjoyed_themes)

        if restaurant and restaurant['restaurant_id'] not in selected:
            selected.append(restaurant['restaurant_id'])

    return selected[:count]


def _select_with_theme_preference(restaurants: List[Dict],
                                  enjoyed_themes: Dict[str, float]) -> Dict:
    """
    Wybiera restaurację z preferencją dla lubianych motywów

    Args:
        restaurants: Lista restauracji do wyboru
        enjoyed_themes: Słownik {theme: affinity}

    Returns:
        Wybrana restauracja
    """
    if not restaurants:
        return None

    # Oblicz wagi dla każdej restauracji
    weights = []
    for restaurant in restaurants:
        theme = restaurant.get('theme', 'Unknown')
        affinity = enjoyed_themes.get(theme, 0.5)

        # Waga = affinity + popularity
        weight = affinity + restaurant.get('popularity', 0.1)
        weights.append(weight)

    # Wybierz z wagami
    total_weight = sum(weights)
    if total_weight == 0:
        return random.choice(restaurants)

    normalized_weights = [w / total_weight for w in weights]

    return random.choices(restaurants, weights=normalized_weights, k=1)[0]
