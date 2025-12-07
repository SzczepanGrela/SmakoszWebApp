"""
Rating Engine V5 - "Factors & Failures" (Complete Rewrite - Final Version)

PHILOSOPHY:
- Non-linear, decision-tree-based rating model
- User baseline bias (read from database, not calculated)
- Veto rule: ANY failing component caps overall rating
- Subtractive penalty model for poor sensory match
- Sigmoid stretch for polarization (fat tails)

KEY FEATURES:
1. User Baseline: Read from secret_rating_baseline (15% Critics, 60% Realists, 25% Fans)
2. Veto Rule: If ANY component < 3.0, overall = component + 1.5 (HARD CAP)
3. Food Score: Technical quality ceiling - subtractive penalty if sensory_fit < 0.5
4. Sigmoid Stretch: Push ratings away from center (6) toward edges (2-3, 8-9)

TARGET DISTRIBUTION:
- Ratings 1-3: ~20% (veto rule, deal-breakers)
- Ratings 4-6: ~25% (mediocre, penalties)
- Ratings 7-10: ~55% (good restaurants + fans)
- Fat tails: Lots of 2s, 3s, 8s, 9s (NOT clustered at 6)
"""

import numpy as np
import random
import math
from typing import Dict, Any
from .preference_calculator import calculate_affinity, calculate_direct_affinity, DIMENSIONS

import logging
logger = logging.getLogger(__name__)
logger.warning("!!! RATING ENGINE V4 LOADED - BOTTLENECK MODEL (Factors & Failures) !!!")

_ARCHETYPE_METADATA_CACHE = None

def get_archetype_metadata(archetype: str) -> Dict[str, Dict[str, float]]:
    """
    Pobiera metadane archetypu zawierające zarówno bazowe charakterystyki, jak i wagi.

    Args:
        archetype: Nazwa archetypu (np. "Pizza", "Burger")

    Returns:
        Dictionary z dwoma kluczami:
        - 'base_characteristics': Bazowe cechy archetypu
        - 'base_weights': Domyślne wagi dla wymiarów
    """
    global _ARCHETYPE_METADATA_CACHE
    if _ARCHETYPE_METADATA_CACHE is None:
        import json
        from pathlib import Path
        blueprint_path = Path(__file__).parent.parent / 'blueprints' / 'variant_characteristics.json'
        with open(blueprint_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        _ARCHETYPE_METADATA_CACHE = {
            arch: {
                'base_characteristics': arch_data['archetype_base']['characteristics'],
                'base_weights': arch_data['archetype_base']['default_weights']
            }
            for arch, arch_data in data.items()
        }
    return _ARCHETYPE_METADATA_CACHE.get(
        archetype,
        {
            'base_characteristics': {},
            'base_weights': {"_default": 1.0}
        }
    )

def sigmoid_stretch(value: float, midpoint: float = 6.0, steepness: float = 0.8) -> float:
    """
    Apply sigmoid transformation to push values away from midpoint.

    Purpose: Transform narrow Gaussian into polarized distribution with fat tails.

    Args:
        value: Input score (1-10)
        midpoint: Center point to push away from (default 6.0)
        steepness: How aggressive the stretch is (higher = more polarization)

    Returns:
        Stretched value with fat tails

    Example:
        Input:  [6.0, 6.5, 5.5, 7.0, 5.0, 8.0, 4.0]
        Output: [6.0, 7.5, 4.5, 8.5, 3.5, 9.2, 2.8]  ← More spread!
    """
    # Normalize to [-1, 1] range centered at midpoint
    normalized = (value - midpoint) / (10.0 - midpoint)

    # Apply sigmoid: push values away from center
    # Positive values -> push toward 10
    # Negative values -> push toward 1
    stretched = midpoint + (10.0 - midpoint) * math.tanh(normalized * steepness * 2)

    return max(1.0, min(10.0, stretched))

def get_user_baseline(user_data: Dict) -> float:
    """
    Read user's baseline rating tendency from database.

    This value is pre-generated in Phase 4 with the following distribution:
    - 15% Critics: Baseline ~4.0 (harsh raters)
    - 60% Realists: Baseline ~6.0 (neutral raters)
    - 25% Fans: Baseline ~8.0 (enthusiastic raters)

    Args:
        user_data: User profile dictionary containing secret_rating_baseline

    Returns:
        User's baseline score (1.0-10.0)
    """
    # Read from database (generated in Phase 4)
    baseline = user_data.get('secret_rating_baseline', None)

    # Fallback if field doesn't exist (backward compatibility)
    if baseline is None:
        logger.warning("⚠️  secret_rating_baseline not found in user_data - using default 6.0")
        baseline = 6.0

    return float(baseline)

def calculate_food_score_polarized(user_data: Dict, dish: Dict, restaurant: Dict,
                                   contextual_target_vector: Dict[str, float] = None) -> float:
    """
    Calculate food score with POLARIZATION (not weighted average).

    New Logic:
    1. Technical quality (60%) + Sensory fit (40%)
    2. If sensory fit < 0.4 (bad match), apply HARSH penalty (0.5x multiplier)
    3. Add aggressive noise for variance

    Returns:
        Polarized food score (1-10) with fat tails
    """
    # 1. TECHNICAL QUALITY (0.0 - 1.0)
    d_qual = float(dish.get('secret_quality', 0.5))
    r_qual = float(restaurant.get('secret_overall_food_quality', 0.5))
    technical_quality = (d_qual * 0.6) + (r_qual * 0.4)

    # 2. SENSORY FIT (0.0 - 1.0)
    user_vector = user_data.get('secret_characteristics_vector', {})
    dish_vector = dish.get('secret_characteristics_vector', {})

    # Parse JSONB if needed
    if isinstance(user_vector, str):
        import json
        user_vector = json.loads(user_vector)
    if isinstance(dish_vector, str):
        import json
        dish_vector = json.loads(dish_vector)

    # Calculate sensory fit (use optimized path if available)
    if contextual_target_vector is not None:
        # OPTIMIZED PATH
        archetype = dish.get('secret_archetype', 'Inne')
        archetype_metadata = get_archetype_metadata(archetype)
        weight_vector = dish.get('secret_weights_vector', None)
        if isinstance(weight_vector, str):
            import json
            weight_vector = json.loads(weight_vector)
        if weight_vector is None:
            weight_vector = archetype_metadata['base_weights']

        sensory_fit = calculate_direct_affinity(
            target_vector=contextual_target_vector,
            dish_vector=dish_vector,
            user_vector=user_vector,
            weight_vector=weight_vector
        )
    else:
        # STANDARD PATH
        archetype = dish.get('secret_archetype', 'Inne')
        archetype_metadata = get_archetype_metadata(archetype)
        archetype_base = archetype_metadata['base_characteristics']
        archetype_weights = archetype_metadata['base_weights']
        weight_vector = dish.get('secret_weights_vector', None)
        if isinstance(weight_vector, str):
            import json
            weight_vector = json.loads(weight_vector)
        if weight_vector is None:
            weight_vector = archetype_weights

        sensory_fit = calculate_affinity(
            user_vector,
            dish_vector,
            weight_vector,
            archetype_base=archetype_base,
            archetype_weights=archetype_weights
        )

    # 3. BASE SCORE (Technical Quality ceiling)
    # Start from technical quality as the maximum achievable score
    base_score = technical_quality * 10.0  # Range: 1-10

    # 4. POLARIZATION: SUBTRACTIVE penalty for bad sensory match
    # NEW LOGIC: Sensory fit acts as a penalty, not a weighted average
    if sensory_fit < 0.5:
        # BAD MATCH: Subtract 2-4 points based on how bad the mismatch is
        # sensory_fit = 0.1 -> penalty = -3.6 points
        # sensory_fit = 0.4 -> penalty = -1.2 points
        penalty = (0.5 - sensory_fit) * 6.0  # Range: 0.6-3.0
        base_score -= penalty
        logger.debug(f"Low sensory fit ({sensory_fit:.2f}) -> penalty -{penalty:.1f} points")

    # 5. Category affinity (deal-breaker check)
    archetype = dish.get('secret_archetype', 'Inne')
    category_affinity = user_data.get('secret_enjoyed_archetypes', {}).get(archetype, 0.5)

    # If user hates this category, cap score low
    if category_affinity < 0.3:
        # Hate this archetype -> max score = 4.0
        base_score = min(base_score, 4.0)
        logger.debug(f"Category hate ({archetype}: {category_affinity:.2f}) -> capped at 4.0")

    # 6. Aggressive noise for variance
    noise = random.gauss(0, 1.5)
    final_score = base_score + noise

    # 7. Kitchen mishaps (5% chance of disaster)
    if random.random() < 0.05:
        mishap_penalty = random.uniform(2.0, 4.0)
        final_score -= mishap_penalty
        logger.debug(f"Kitchen mishap! -> penalty -{mishap_penalty:.1f} points")

    return max(1.0, min(10.0, final_score))

def calculate_value_score_polarized(user_data: Dict, dish: Dict) -> float:
    """
    Calculate value score with SIGMOID curve (not linear).

    New Logic:
    - Price ratio > 1.5 (overpriced) -> score 1-2 (HARSH)
    - Price ratio 0.8-1.2 (fair) -> score 5-7
    - Price ratio < 0.8 (bargain) -> score 8-10 (REWARD)

    Returns:
        Polarized value score (1-10)
    """
    user_vector = user_data.get('secret_characteristics_vector', {})
    if isinstance(user_vector, str):
        import json
        user_vector = json.loads(user_vector)

    # Get price sensitivity
    price_sensitivity_data = user_vector.get('context_price_sensitivity', {'value': 0.5})
    if isinstance(price_sensitivity_data, (int, float)):
        sensitivity = float(price_sensitivity_data)
    else:
        sensitivity = price_sensitivity_data.get('value', 0.5)

    actual_price = float(dish.get('price', 35.0))
    quality = float(dish.get('secret_quality', 0.5))
    fair_price = 20.0 + (quality * 60.0)

    price_ratio = actual_price / fair_price

    # SIGMOID-LIKE CURVE for polarization
    if price_ratio > 1.5:
        # Severe overpricing -> score 1-2
        base_score = 1.5
        penalty = (price_ratio - 1.5) * sensitivity * 2.0
        score = max(1.0, base_score - penalty)
    elif price_ratio > 1.2:
        # Mild overpricing -> score 3-5
        score = 5.0 - (price_ratio - 1.2) * 5.0
    elif price_ratio < 0.8:
        # Bargain! -> score 8-10
        bonus = (0.8 - price_ratio) * 10.0
        score = min(10.0, 8.0 + bonus)
    else:
        # Fair pricing -> score 5-7
        score = 6.0 + random.uniform(-1.0, 1.0)

    # Add noise
    noise = random.gauss(0, 0.5)
    return max(1.0, min(10.0, score + noise))

def calculate_service_score(user_data: Dict, restaurant: Dict) -> float:
    """Calculate service score (unchanged from original)"""
    base_quality = float(restaurant.get('secret_service_quality', 0.5))
    variance = np.random.normal(0, 0.12)
    score = (base_quality * 10.0) + (variance * 10.0)
    return max(1.0, min(10.0, score))

def calculate_cleanliness_score(user_data: Dict, restaurant: Dict) -> float:
    """Calculate cleanliness score (unchanged from original)"""
    base_quality = float(restaurant.get('secret_cleanliness_score', 0.5))

    cuisine_type = restaurant.get('cuisine_type', 'Casual')
    expected_score = float(user_data.get('secret_cleanliness_preference', {}).get(cuisine_type, 7.0))
    expected_quality = expected_score / 10.0

    score = base_quality * 10.0

    # Magnified penalty if below expectations
    if base_quality < expected_quality:
        penalty = (expected_quality - base_quality) * 15.0
        score -= penalty

    variance = np.random.normal(0, 0.05)
    score += (variance * 10.0)
    return max(1.0, min(10.0, score))

def calculate_ambiance_score(user_data: Dict, restaurant: Dict) -> float:
    """Calculate ambiance score (unchanged from original)"""
    base_quality = float(restaurant.get('secret_ambiance_quality', 0.5))
    res_type = restaurant.get('secret_ambiance_type', 'Casual')
    user_pref = user_data.get('secret_preferred_ambiance', 'Casual')

    score = base_quality * 10.0

    if res_type == user_pref:
        score += 1.5
    elif user_pref == 'Spokojny' and res_type == 'Energiczny':
        score -= 2.0
    else:
        score -= 0.5

    variance = np.random.normal(0, 0.15)
    score += (variance * 10.0)
    return max(1.0, min(10.0, score))

def calculate_review_ratings(user_data: Dict[str, Any],
                            dish: Dict[str, Any],
                            restaurant: Dict[str, Any],
                            user_variant_preference_vector: Dict[str, float] = None) -> Dict[str, float]:
    """
    Calculate all 6 ratings using BOTTLENECK MODEL (Factors & Failures).

    NEW PHILOSOPHY:
    1. Start from user baseline (not 0)
    2. Calculate independent components
    3. Check for DEAL-BREAKERS (any component < 3.0)
    4. If deal-breaker exists, CAP overall rating
    5. If no deal-breaker, apply sigmoid stretch for polarization

    This creates FAT TAILS (lots of 2s, 3s, 8s, 9s) instead of Gaussian spike.

    Args:
        user_data: User profile with secret attributes
        dish: Dish data with secret attributes
        restaurant: Restaurant data with secret attributes
        user_variant_preference_vector: Optional pre-calculated preferences

    Returns:
        Dictionary with 6 ratings (food, service, cleanliness, ambiance, value, overall)
    """

    # STEP 1: USER BASELINE (Read from database)
    baseline = get_user_baseline(user_data)

    # STEP 2: CALCULATE INDEPENDENT COMPONENTS
    food_score = calculate_food_score_polarized(
        user_data, dish, restaurant,
        contextual_target_vector=user_variant_preference_vector
    )

    service_score = calculate_service_score(user_data, restaurant)
    cleanliness_score = calculate_cleanliness_score(user_data, restaurant)
    ambiance_score = calculate_ambiance_score(user_data, restaurant)
    value_score = calculate_value_score_polarized(user_data, dish)

    # STEP 3: CHECK FOR DEAL-BREAKERS (Bottleneck Logic)
    components = {
        'food': food_score,
        'service': service_score,
        'cleanliness': cleanliness_score,
        'value': value_score
    }

    # Find the minimum component
    min_component_name = min(components, key=components.get)
    min_component_score = components[min_component_name]

    # VETO RULE: If ANY component < 3.0, cap overall rating
    if min_component_score < 3.0:
        # Deal-breaker detected!
        # Overall cannot exceed failing component + 1.5 (FIXED, not random)
        # Example: Dirty floor (cleanliness=2.0) -> overall max = 3.5
        overall_rating = min_component_score + 1.5

        logger.debug(f"VETO RULE: {min_component_name}={min_component_score:.1f} -> overall capped at {overall_rating:.1f}")

    else:
        # STEP 4: NO DEAL-BREAKER -> Calculate weighted mean
        weighted_mean = (
            food_score * 0.50 +
            service_score * 0.15 +
            cleanliness_score * 0.10 +
            ambiance_score * 0.10 +
            value_score * 0.15
        )

        # STEP 5: SIGMOID STRETCH (Push away from 6.0)
        # This creates fat tails (more 2s, 3s, 8s, 9s)
        overall_rating = sigmoid_stretch(weighted_mean, midpoint=6.0, steepness=0.8)

        # Blend with user baseline (20% baseline influence)
        overall_rating = overall_rating * 0.8 + baseline * 0.2

    # STEP 6: FINAL SMOOTHING NOISE
    smoothing_noise = random.gauss(0, 0.5)
    overall_rating += smoothing_noise

    # Clamp to valid range
    overall_rating = max(1.0, min(10.0, overall_rating))

    return {
        'food_score': round(food_score, 2),
        'service_score': round(service_score, 2),
        'cleanliness_score': round(cleanliness_score, 2),
        'ambiance_score': round(ambiance_score, 2),
        'value_for_money_score': round(value_score, 2),
        'overall_rating': round(overall_rating, 2)
    }
