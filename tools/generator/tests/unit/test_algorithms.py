"""
Unit tests for algorithm modules.

Tests preference calculation, rating strategies, and selectors.
"""

import sys
from pathlib import Path

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from algorithms.preference_calculator import (
    DIMENSIONS,
    calculate_affinity,
    calculate_contextual_vector,
    clamp,
    derive_penalty_weights,
    merge_vectors,
)

class TestClampFunction:
    """Tests for the clamp utility function."""

    def test_clamp_within_range(self):
        """Value within range should be unchanged."""
        assert clamp(0.5, 0.0, 1.0) == 0.5

    def test_clamp_below_min(self):
        """Value below min should be clamped to min."""
        assert clamp(-0.5, 0.0, 1.0) == 0.0

    def test_clamp_above_max(self):
        """Value above max should be clamped to max."""
        assert clamp(1.5, 0.0, 1.0) == 1.0

class TestDerivePenaltyWeights:
    """Tests for tiered penalty weight derivation."""

    def test_critical_dimension_gets_highest_boost(self):
        """Weight <= 0.3 with extreme base should get penalty 50.0."""
        adaptation_weights = {"physics_temperature": 0.1, "_default": 1.0}
        base_characteristics = {"physics_temperature": 0.1}  # Extreme low

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["physics_temperature"] == 50.0

    def test_important_dimension_gets_moderate_boost(self):
        """Weight <= 0.5 with extreme base should get penalty 25.0."""
        adaptation_weights = {"flavor_sweetness": 0.4, "_default": 1.0}
        base_characteristics = {"flavor_sweetness": 0.9}  # Extreme high

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_sweetness"] == 25.0

    def test_locked_dimension_gets_boost(self):
        """Weight <= 1.0 with extreme base should get penalty 10.0."""
        adaptation_weights = {"flavor_spiciness": 1.0, "_default": 1.0}
        base_characteristics = {"flavor_spiciness": 0.1}  # Extreme low

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_spiciness"] == 10.0

    def test_modifiable_dimension_no_boost(self):
        """Weight > 1.0 should not get penalty boost."""
        adaptation_weights = {"flavor_spiciness": 1.5, "_default": 1.0}
        base_characteristics = {"flavor_spiciness": 0.5}  # Not extreme

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_spiciness"] == 1.5

class TestCalculateContextualVector:
    """Tests for contextual vector calculation."""

    def test_locked_dimension_ignores_user_preference(self):
        """Dimension with weight <= 1.0 should not shift towards user preference."""
        user_vector = {"flavor_sweetness": {"value": 0.9, "tolerance": 0.1}}
        archetype_base = {"flavor_sweetness": 0.2}
        adaptation_weights = {"flavor_sweetness": 0.5, "_default": 1.0}  # Locked

        contextual = calculate_contextual_vector(user_vector, archetype_base, adaptation_weights)

        # Should stay near base (0.2), not shift to user (0.9)
        assert contextual["flavor_sweetness"] < 0.5

    def test_modifiable_dimension_shifts_towards_user(self):
        """Dimension with weight > 1.0 should shift towards user preference."""
        user_vector = {"flavor_spiciness": {"value": 0.9, "tolerance": 0.1}}
        archetype_base = {"flavor_spiciness": 0.5}
        adaptation_weights = {"flavor_spiciness": 1.8, "_default": 1.0}  # Modifiable

        contextual = calculate_contextual_vector(user_vector, archetype_base, adaptation_weights)

        # Should shift towards user preference (> 0.5)
        assert contextual["flavor_spiciness"] > 0.5

    def test_variant_override_applied(self):
        """Variant characteristics should override archetype base."""
        user_vector = {}
        archetype_base = {"physics_richness": 0.5}
        adaptation_weights = {"_default": 1.0}
        variant_override = {"physics_richness": 0.8}

        contextual = calculate_contextual_vector(
            user_vector, archetype_base, adaptation_weights, variant_base_override=variant_override
        )

        assert contextual["physics_richness"] == 0.8

class TestCalculateAffinity:
    """Tests for affinity calculation between user and dish."""

    def test_perfect_match_high_affinity(self):
        """User and dish with matching vectors should have high affinity."""
        user_vector = {dim: {"value": 0.5, "tolerance": 0.2} for dim in DIMENSIONS}
        dish_vector = dict.fromkeys(DIMENSIONS, 0.5)
        adaptation_weights = {"_default": 1.0}
        base_characteristics = dict.fromkeys(DIMENSIONS, 0.5)

        affinity = calculate_affinity(user_vector, dish_vector, adaptation_weights, base_characteristics)

        assert affinity > 0.95

    def test_critical_mismatch_low_affinity(self):
        """Critical dimension mismatch should result in lower affinity."""
        user_vector = {dim: {"value": 0.5, "tolerance": 0.2} for dim in DIMENSIONS}

        # Dish with wrong temperature (hot instead of cold)
        dish_vector = dict.fromkeys(DIMENSIONS, 0.5)
        dish_vector["physics_temperature"] = 0.9  # Hot

        # Archetype expects cold (base=0.1, weight=0.1 = CRITICAL)
        adaptation_weights = {"physics_temperature": 0.1, "_default": 1.0}
        base_characteristics = {"physics_temperature": 0.1}  # Cold expected

        affinity = calculate_affinity(user_vector, dish_vector, adaptation_weights, base_characteristics)

        # Should be significantly penalized
        assert affinity < 0.7

class TestMergeVectors:
    """Tests for vector merging utility."""

    def test_merge_with_override(self):
        """Override values should take precedence."""
        base = {"a": 0.5, "b": 0.5}
        override = {"b": 0.9, "c": 0.7}

        merged = merge_vectors(base, override)

        assert merged["a"] == 0.5
        assert merged["b"] == 0.9
        assert merged["c"] == 0.7

    def test_merge_with_none_override(self):
        """None override should return base unchanged."""
        base = {"a": 0.5}

        merged = merge_vectors(base, None)

        assert merged == base
