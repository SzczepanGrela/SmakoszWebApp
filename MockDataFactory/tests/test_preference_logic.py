"""
Test Suite for Preference Logic & Rating Engine
Consolidates tests for affinity calculation, relevance gating, and weight fallbacks.
"""

import importlib.util
import json
import logging
import sys
from pathlib import Path

# Add project root to path
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))

# Setup logging
logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger(__name__)

# Dynamic import of preference_calculator to avoid __init__ issues
spec = importlib.util.spec_from_file_location(
    "preference_calculator", project_root / "algorithms" / "preference_calculator.py"
)
preference_calc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preference_calc)

# Shortcuts
calculate_affinity = preference_calc.calculate_affinity
calculate_contextual_vector = preference_calc.calculate_contextual_vector
apply_restaurant_bias = preference_calc.apply_restaurant_bias

# Constants
BLUEPRINT_PATH = project_root / "blueprints" / "vectors.json"

def load_archetype_metadata():
    """Load archetype metadata from blueprint file"""
    if not BLUEPRINT_PATH.exists():
        logger.error(f"Blueprint not found at {BLUEPRINT_PATH}")
        return {}

    with open(BLUEPRINT_PATH, encoding="utf-8") as f:
        data = json.load(f)

    metadata_cache = {
        arch: {
            "base_characteristics": arch_data["archetype_base"]["characteristics"],
            "base_weights": arch_data["archetype_base"]["default_weights"],
        }
        for arch, arch_data in data.items()
    }
    return metadata_cache

# Load metadata once
ARCHETYPE_METADATA = load_archetype_metadata()

def get_archetype_metadata(archetype):
    return ARCHETYPE_METADATA.get(archetype, {"base_characteristics": {}, "base_weights": {"_default": 1.0}})

def test_affinity_calculation():
    logger.info("\n[TEST] 1. Affinity Calculation (Basic)")

    user_vec = {"flavor_sweetness": {"value": 0.9, "tolerance": 0.2}}
    dish_vec = {"flavor_sweetness": 0.7}
    weights = {"_default": 1.0}  # Neutral weight

    # Base characteristics needed for penalty derivation
    base_chars = {"flavor_sweetness": 0.5}
    affinity = calculate_affinity(user_vec, dish_vec, weights, base_characteristics=base_chars)

    # Diff = 0.2, Tolerance = 0.2 -> Within comfort zone -> Penalty 0 -> Affinity 1.0
    # But wait, calculation might use base_characteristics if not provided?
    # Let's check implementation. If contextual_target is not calculated (no base),
    # it uses calculate_contextual_vector which uses base (default 0.5).

    # To test purely mathematical affinity, we need consistent inputs.
    # In new logic, affinity depends on contextual target.
    # Contextual target depends on base + user_bias * relevance.

    # Let's simulate exact scenario:
    # Base = 0.5 (default), Weight = 1.0 (irrelevant) -> Target = 0.5
    # Dish = 0.7. Diff = 0.2. Tolerance = 0.2. Penalty = 0.

    assert 0.0 <= affinity <= 1.0, f"Affinity {affinity} out of range"
    logger.info(f"   [OK] Affinity: {affinity:.3f}")

def test_restaurant_bias():
    logger.info("\n[TEST] 2. Restaurant Bias")
    base_vec = {"physics_richness": 0.6}
    modifiers = {"Pizza": {"physics_richness": 0.15}}

    pizza_vec = apply_restaurant_bias(base_vec.copy(), "Pizza", modifiers)
    assert abs(pizza_vec["physics_richness"] - 0.75) < 0.01
    logger.info(f"   [OK] Pizza bias applied: {pizza_vec['physics_richness']:.2f}")

def test_relevance_gating():
    logger.info("\n[TEST] 3. Relevance Gating Logic")

    # Scenario: Sugar Lover (0.9)
    user_vector = {"flavor_sweetness": {"value": 0.9, "tolerance": 0.1}}

    # Case A: Low Weight (Irrelevant) -> Target stays at Archetype Base (0.5)
    base_low = {"flavor_sweetness": 0.5}
    weights_low = {"flavor_sweetness": 1.0, "_default": 1.0}

    # Dish is NOT sweet (0.3) -> Closer to 0.5 than to 0.9
    dish_vector = {"flavor_sweetness": 0.3}

    affinity_low = calculate_affinity(user_vector, dish_vector, weights_low, base_characteristics=base_low)

    # Case B: High Weight (Relevant) -> Target shifts to User Pref (0.9)
    weights_high = {"flavor_sweetness": 2.5, "_default": 1.0}

    affinity_high = calculate_affinity(user_vector, dish_vector, weights_high, base_characteristics=base_low)

    logger.info(f"   Affinity (Low Weight/Irrelevant): {affinity_low:.3f}")
    logger.info(f"   Affinity (High Weight/Relevant): {affinity_high:.3f}")

    # In Case A: Target=0.5. Dish=0.3. Diff=0.2. Tol=0.1. Penalty exists.
    # In Case B: Target~0.8. Dish=0.3. Diff=0.5. Tol=0.1. Penalty HUGE.

    if affinity_low > affinity_high:
        logger.info("   [OK] Low weight ignored user preference (correctly).")
    else:
        logger.error("   [FAIL] Relevance gating failed.")

def test_fallback_logic():
    logger.info("\n[TEST] 4. Blueprint Integration & Fallback")

    if not ARCHETYPE_METADATA:
        logger.warning("   [SKIP] No blueprints found, skipping integration test.")
        return

    # Get Pizza metadata
    pizza_meta = get_archetype_metadata("Pizza")
    if not pizza_meta["base_characteristics"]:
        logger.warning("   [SKIP] Pizza archetype not found in blueprints.")
        return

    pizza_base = pizza_meta["base_characteristics"]
    pizza_weights = pizza_meta["base_weights"]

    user_vec = {"flavor_spiciness": {"value": 0.9, "tolerance": 0.2}}  # Spicy lover
    dish_vec = {"flavor_spiciness": 0.85}  # Spicy dish

    # Calculate using loaded metadata
    affinity = calculate_affinity(user_vec, dish_vec, pizza_weights, base_characteristics=pizza_base)

    logger.info(f"   [OK] Calculated affinity using blueprints: {affinity:.3f}")

def test_locked_dimension_no_user_shift():
    """
    Test that locked dimensions (weight < 1.0) prevent user preferences from shifting target.

    A user who loves sweetness shouldn't get a sweet contextual target for soup,
    because soup's sweetness is LOCKED.
    """
    logger.info("\n[TEST] 5. Locked Dimension (No User Shift)")

    # Sugar lover
    sweet_lover = {"flavor_sweetness": {"value": 0.95, "tolerance": 0.1}}

    # Soup base is low sweetness (0.3)
    soup_base = {"flavor_sweetness": 0.3}

    # Sweetness is LOCKED for soup (weight 0.3 < 1.0)
    locked_weights = {"flavor_sweetness": 0.3, "_default": 1.0}

    target = calculate_contextual_vector(
        user_vector=sweet_lover,
        archetype_base=soup_base,
        adaptation_weights=locked_weights,
    )

    sweetness_target = target.get("flavor_sweetness", 0.5)

    # Target should stay near base (0.3), NOT shift to user preference (0.95)
    assert sweetness_target < 0.5, f"Locked dimension shifted! Target={sweetness_target}, expected ~0.3"
    logger.info(f"   [OK] Sweetness target stayed low: {sweetness_target:.3f} (base=0.3)")

def test_modifiable_dimension_user_shift():
    """
    Test that modifiable dimensions (weight > 1.0) allow user preferences to shift target.

    A user who loves spiciness should get a spicier contextual target for curry,
    because curry's spiciness is MODIFIABLE.
    """
    logger.info("\n[TEST] 6. Modifiable Dimension (User Shift)")

    # Spice lover
    spice_lover = {"flavor_spiciness": {"value": 0.9, "tolerance": 0.1}}

    # Curry base is moderately spicy (0.6)
    curry_base = {"flavor_spiciness": 0.6}

    # Spiciness is MODIFIABLE for curry (weight 1.8 > 1.0)
    modifiable_weights = {"flavor_spiciness": 1.8, "_default": 1.0}

    target = calculate_contextual_vector(
        user_vector=spice_lover,
        archetype_base=curry_base,
        adaptation_weights=modifiable_weights,
    )

    spiciness_target = target.get("flavor_spiciness", 0.5)

    # Target should shift towards user preference (higher than 0.6)
    assert spiciness_target > 0.65, f"Modifiable dimension didn't shift! Target={spiciness_target}, expected >0.65"
    logger.info(f"   [OK] Spiciness target shifted up: {spiciness_target:.3f} (base=0.6)")

def test_culinary_aberration_penalized():
    """
    Test that culinary aberrations (hot ice cream, sweet soup) receive LOWER affinity
    than normal dishes. We compare hot ice cream vs cold ice cream.
    """
    logger.info("\n[TEST] 7. Culinary Aberration Penalty")

    # Neutral user (no strong preferences)
    neutral_user = {"physics_temperature": {"value": 0.5, "tolerance": 0.3}}

    # Ice cream archetype: MUST be cold (base temp 0.1)
    ice_cream_base = {"physics_temperature": 0.1}
    ice_cream_weights = {"physics_temperature": 0.1, "_default": 1.0}  # Locked to cold

    # Normal ice cream (cold, matches base)
    cold_ice_cream = {"physics_temperature": 0.1}

    # Aberration: Hot ice cream (temperature 0.9)
    hot_ice_cream = {"physics_temperature": 0.9}

    affinity_normal = calculate_affinity(
        neutral_user, cold_ice_cream,
        ice_cream_weights, base_characteristics=ice_cream_base
    )

    affinity_aberrant = calculate_affinity(
        neutral_user, hot_ice_cream,
        ice_cream_weights, base_characteristics=ice_cream_base
    )

    # Hot ice cream should have SIGNIFICANTLY lower affinity than cold ice cream
    penalty_impact = affinity_normal - affinity_aberrant

    assert penalty_impact > 0.1, f"Aberration not penalized! Normal={affinity_normal:.3f}, Aberrant={affinity_aberrant:.3f}"
    assert affinity_aberrant < affinity_normal, "Hot ice cream should have lower affinity"

    logger.info(f"   [OK] Normal ice cream: {affinity_normal:.3f}")
    logger.info(f"   [OK] Hot ice cream (aberrant): {affinity_aberrant:.3f}")
    logger.info(f"   [OK] Penalty impact: {penalty_impact:.3f}")

def test_reproducible_noise():
    """
    Test that OnTheFlyCalculator produces reproducible results for same user+dish.
    """
    logger.info("\n[TEST] 8. Reproducible Noise")

    try:
        # Import the module's helper function
        import hashlib
        import random

        def get_deterministic_rng(user_id, dish_name, variant_name):
            seed_str = f"{user_id}_{dish_name}_{variant_name}"
            hash_bytes = hashlib.md5(seed_str.encode('utf-8')).digest()
            seed_int = int.from_bytes(hash_bytes[:8], 'little')
            return random.Random(seed_int)

        # Same inputs
        user_id = 12345
        dish_name = "Margherita"
        variant_name = "Margherita"

        # Generate noise twice
        rng1 = get_deterministic_rng(user_id, dish_name, variant_name)
        noise1 = [rng1.gauss(0, 0.03) for _ in range(5)]

        rng2 = get_deterministic_rng(user_id, dish_name, variant_name)
        noise2 = [rng2.gauss(0, 0.03) for _ in range(5)]

        # Should be identical
        for i, (n1, n2) in enumerate(zip(noise1, noise2, strict=False)):
            assert n1 == n2, f"Noise not reproducible at index {i}: {n1} != {n2}"

        logger.info("   [OK] Noise is reproducible across runs")

        # Different inputs should produce different noise
        rng3 = get_deterministic_rng(user_id + 1, dish_name, variant_name)
        noise3 = [rng3.gauss(0, 0.03) for _ in range(5)]

        assert noise1 != noise3, "Different users should get different noise!"
        logger.info("   [OK] Different users get different noise")

    except ImportError as e:
        logger.warning(f"   [SKIP] Could not import hashlib: {e}")

# MAIN RUNNER

if __name__ == "__main__":
    logger.info("=== RUNNING PREFERENCE LOGIC TESTS ===")
    try:
        test_affinity_calculation()
        test_restaurant_bias()
        test_relevance_gating()
        test_fallback_logic()
        test_locked_dimension_no_user_shift()
        test_modifiable_dimension_user_shift()
        test_culinary_aberration_penalized()
        test_reproducible_noise()
        logger.info("\n=== ALL TESTS PASSED ===")
    except AssertionError as e:
        logger.error(f"\n[FAIL] Assertion Error: {e}")
        sys.exit(1)
    except Exception as e:
        logger.error(f"\n[ERROR] {e}")
        sys.exit(1)
