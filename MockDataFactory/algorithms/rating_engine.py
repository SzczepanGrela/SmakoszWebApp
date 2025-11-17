"""
Rating Engine - Algorytm oceniania z 30+ czynnikami (KLUCZOWY DLA CF!)
"""

import numpy as np
import random
from typing import Dict, Any

def calculate_review_ratings(user_data: Dict[str, Any],
                            dish: Dict[str, Any],
                            restaurant: Dict[str, Any]) -> Dict[str, float]:
    """
    Oblicza wszystkie 6 ocen na podstawie dopasowania preferencji użytkownika

    Args:
        user_data: Dane użytkownika z secret attributes
        dish: Dane dania z secret attributes
        restaurant: Dane restauracji z secret attributes

    Returns:
        {
            'food_score': 8.5,
            'service_score': 7.0,
            'cleanliness_score': 8.0,
            'ambiance_score': 6.5,
            'value_for_money_score': 7.5,
            'overall_rating': 7.5
        }
    """

    # 1. FOOD SCORE (40% wpływu na overall)
    food_score = calculate_food_score(user_data, dish, restaurant)

    # 2. SERVICE SCORE (15% wpływu)
    service_score = calculate_service_score(user_data, restaurant)

    # 3. CLEANLINESS SCORE (15% wpływu)
    cleanliness_score = calculate_cleanliness_score(user_data, restaurant)

    # 4. AMBIANCE SCORE (10% wpływu)
    ambiance_score = calculate_ambiance_score(user_data, restaurant)

    # 5. VALUE FOR MONEY SCORE (10% wpływu)
    value_score = calculate_value_score(user_data, dish)

    # 6. CROSS-IMPACT (Efekt halo - 10% wpływu, ZOPTYMALIZOWANY: 0.02)
    cross_impact_factor = user_data.get('secret_cross_impact_factor', 0.02)
    service_score, cleanliness_score, ambiance_score = apply_cross_impact(
        food_score, service_score, cleanliness_score, ambiance_score, cross_impact_factor
    )

    # 7. OVERALL RATING (ważona średnia)
    overall = (
        food_score * 0.40 +
        service_score * 0.15 +
        cleanliness_score * 0.15 +
        ambiance_score * 0.10 +
        value_score * 0.10 +
        (food_score * cross_impact_factor * 10) * 0.10  # Cross-impact contribution
    )

    overall = max(1.0, min(10.0, overall))

    return {
        'food_score': round(food_score, 2),
        'service_score': round(service_score, 2),
        'cleanliness_score': round(cleanliness_score, 2),
        'ambiance_score': round(ambiance_score, 2),
        'value_for_money_score': round(value_score, 2),
        'overall_rating': round(overall, 2)
    }


def calculate_food_score(user_data: Dict, dish: Dict, restaurant: Dict) -> float:
    """
    Kalkulacja oceny jedzenia (30+ czynników)

    Składowe:
    - Jakość (30%): dish quality + restaurant food quality
    - Ostrość (10%): dopasowanie do preferencji
    - Bogactwo (10%): dopasowanie richness
    - Tekstura (10%): dopasowanie texture
    - Składniki (15%): preferencje składnikowe
    - Typ kuchni/archetype (15%): affinity
    - Nastrój (10%): ZOPTYMALIZOWANY mood_propensity = 0.3
    """
    score = 5.0  # Neutralny start

    # 1. JAKOŚĆ (30%)
    dish_quality = dish.get('secret_quality', 0.7)
    restaurant_quality = restaurant.get('secret_overall_food_quality', 0.7)

    score += dish_quality * 3.0
    score += restaurant_quality * 2.0

    # 2. OSTROŚĆ (10%)
    dish_spiciness = dish.get('secret_spiciness', 0.0)
    user_spice_pref = user_data.get('secret_spice_preference', 5.0)

    spice_diff = abs(dish_spiciness - user_spice_pref)
    score -= spice_diff * 0.4

    # 3. BOGACTWO (10%)
    dish_richness = dish.get('secret_richness', 0.5)
    user_richness_pref = user_data.get('secret_richness_preference', 0.5)

    richness_diff = abs(dish_richness - user_richness_pref)
    score -= richness_diff * 0.8

    # 4. TEKSTURA (10%)
    dish_texture = dish.get('secret_texture_score', 0.7)
    user_texture_pref = user_data.get('secret_texture_preference', 0.7)

    texture_diff = abs(dish_texture - user_texture_pref)
    score -= texture_diff * 0.6

    # 5. SKŁADNIKI (15%)
    ingredient_prefs = user_data.get('secret_ingredient_preferences', {})
    dish_ingredients = dish.get('ingredients', [])

    for ingredient in dish_ingredients:
        pref = ingredient_prefs.get(ingredient, 0.5)
        if pref > 0.7:  # Uwielbia
            score += 0.3
        elif pref < 0.3:  # Nie lubi
            score -= 0.5

    # 6. TYP KUCHNI / ARCHETYPE (15%)
    enjoyed_archetypes = user_data.get('secret_enjoyed_archetypes', {})
    dish_archetype = dish.get('archetype', 'Unknown')

    archetype_affinity = enjoyed_archetypes.get(dish_archetype, 0.5)
    score += (archetype_affinity - 0.5) * 2.0

    # 7. NASTRÓJ (10% - ZOPTYMALIZOWANE!)
    mood_propensity = user_data.get('secret_mood_propensity', 0.3)  # 0.3 średnio
    mood_variance = np.random.normal(0, 1.0 * mood_propensity)
    score += mood_variance

    # Clamp do 1-10
    return max(1.0, min(10.0, score))


def calculate_service_score(user_data: Dict, restaurant: Dict) -> float:
    """Kalkulacja oceny obsługi (15% wpływu)"""
    base_service = restaurant.get('secret_service_quality', 0.7)

    # Konwertuj z [0,1] na [1,10]
    service_score = base_service * 10

    # Losowa wariancja dnia
    service_score += random.uniform(-0.5, 0.5)

    return max(1.0, min(10.0, service_score))


def calculate_cleanliness_score(user_data: Dict, restaurant: Dict) -> float:
    """Kalkulacja oceny czystości (15% wpływu)"""
    base_cleanliness = restaurant.get('secret_cleanliness_score', 7.0)

    # Oczekiwania użytkownika (zależne od typu restauracji)
    restaurant_theme = restaurant.get('theme', 'Casual')
    cleanliness_expectations = user_data.get('secret_cleanliness_preference', {})
    user_expectation = cleanliness_expectations.get(restaurant_theme, 7.0)

    cleanliness_score = base_cleanliness

    # Kara jeśli poniżej oczekiwań
    if cleanliness_score < user_expectation:
        cleanliness_score -= (user_expectation - cleanliness_score) * 0.5

    return max(1.0, min(10.0, cleanliness_score))


def calculate_ambiance_score(user_data: Dict, restaurant: Dict) -> float:
    """Kalkulacja oceny atmosfery (10% wpływu)"""
    base_ambiance = restaurant.get('secret_ambiance_quality', 0.7)

    # Konwertuj z [0,1] na [1,10]
    ambiance_score = base_ambiance * 10

    # Czy typ atmosfery pasuje do preferencji?
    restaurant_ambiance_type = restaurant.get('secret_ambiance_type', 'Casual')
    user_preferred_ambiance = user_data.get('secret_preferred_ambiance', 'Spokojny')

    if restaurant_ambiance_type == user_preferred_ambiance:
        ambiance_score += 1.0  # Bonus
    else:
        ambiance_score -= 0.5  # Lekka kara

    return max(1.0, min(10.0, ambiance_score))


def calculate_value_score(user_data: Dict, dish: Dict) -> float:
    """Kalkulacja oceny wartości za pieniądze (10% wpływu)"""
    expected_price = user_data.get('secret_price_preference_range', {}).get('mean', 35.0)
    actual_price = dish.get('public_price', 35.0)

    tolerance_above = user_data.get('secret_price_preference_range', {}).get('tolerance_above', 2.0)
    tolerance_below = user_data.get('secret_price_preference_range', {}).get('tolerance_below', 0.5)

    value_score = 5.0  # Neutralny start

    if actual_price <= expected_price * tolerance_below:
        value_score += 2.0  # Tania przekąska!
    elif actual_price > expected_price * tolerance_above:
        value_score -= 3.0  # Za drogie!
    else:
        # W przedziale - liniowa interpolacja
        ratio = actual_price / expected_price
        value_score += (1 - ratio) * 2

    return max(1.0, min(10.0, value_score))


def apply_cross_impact(food_score: float, service_score: float,
                      cleanliness_score: float, ambiance_score: float,
                      cross_impact_factor: float) -> tuple:
    """
    Efekt halo - jeśli jedzenie świetne, wszystko inne wydaje się lepsze
    ZOPTYMALIZOWANY: cross_impact_factor = 0.02 (było 0.05)

    Args:
        food_score: Ocena jedzenia
        service_score: Ocena obsługi
        cleanliness_score: Ocena czystości
        ambiance_score: Ocena atmosfery
        cross_impact_factor: Siła efektu (0.02 średnio)

    Returns:
        Tuple (service_score, cleanliness_score, ambiance_score) po zastosowaniu cross-impact
    """
    if food_score > 7:
        boost = (food_score - 7) * cross_impact_factor * 0.5
        service_score = min(10.0, service_score + boost)
        cleanliness_score = min(10.0, cleanliness_score + boost)
        ambiance_score = min(10.0, ambiance_score + boost)

    return service_score, cleanliness_score, ambiance_score
