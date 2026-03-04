import sys
from pathlib import Path

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

    def test_clamp_within_range(self):
        assert clamp(0.5, 0.0, 1.0) == 0.5

    def test_clamp_below_min(self):
        assert clamp(-0.5, 0.0, 1.0) == 0.0

    def test_clamp_above_max(self):
        assert clamp(1.5, 0.0, 1.0) == 1.0

class TestDerivePenaltyWeights:

    def test_critical_dimension_gets_highest_boost(self):
        adaptation_weights = {"physics_temperature": 0.1, "_default": 1.0}
        base_characteristics = {"physics_temperature": 0.1}

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["physics_temperature"] == 50.0

    def test_important_dimension_gets_moderate_boost(self):
        adaptation_weights = {"flavor_sweetness": 0.4, "_default": 1.0}
        base_characteristics = {"flavor_sweetness": 0.9}

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_sweetness"] == 25.0

    def test_locked_dimension_gets_boost(self):
        adaptation_weights = {"flavor_spiciness": 1.0, "_default": 1.0}
        base_characteristics = {"flavor_spiciness": 0.1}

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_spiciness"] == 10.0

    def test_modifiable_dimension_no_boost(self):
        adaptation_weights = {"flavor_spiciness": 1.5, "_default": 1.0}
        base_characteristics = {"flavor_spiciness": 0.5}

        penalty_weights = derive_penalty_weights(adaptation_weights, base_characteristics)

        assert penalty_weights["flavor_spiciness"] == 1.5

class TestCalculateContextualVector:

    def test_locked_dimension_ignores_user_preference(self):
        user_vector = {"flavor_sweetness": {"value": 0.9, "tolerance": 0.1}}
        archetype_base = {"flavor_sweetness": 0.2}
        adaptation_weights = {"flavor_sweetness": 0.5, "_default": 1.0}

        contextual = calculate_contextual_vector(user_vector, archetype_base, adaptation_weights)

        assert contextual["flavor_sweetness"] < 0.5

    def test_modifiable_dimension_shifts_towards_user(self):
        user_vector = {"flavor_spiciness": {"value": 0.9, "tolerance": 0.1}}
        archetype_base = {"flavor_spiciness": 0.5}
        adaptation_weights = {"flavor_spiciness": 1.8, "_default": 1.0}

        contextual = calculate_contextual_vector(user_vector, archetype_base, adaptation_weights)

        assert contextual["flavor_spiciness"] > 0.5

    def test_variant_override_applied(self):
        user_vector = {}
        archetype_base = {"physics_richness": 0.5}
        adaptation_weights = {"_default": 1.0}
        variant_override = {"physics_richness": 0.8}

        contextual = calculate_contextual_vector(
            user_vector, archetype_base, adaptation_weights, variant_base_override=variant_override
        )

        assert contextual["physics_richness"] == 0.8

class TestCalculateAffinity:

    def test_perfect_match_high_affinity(self):
        user_vector = {dim: {"value": 0.5, "tolerance": 0.2} for dim in DIMENSIONS}
        dish_vector = dict.fromkeys(DIMENSIONS, 0.5)
        adaptation_weights = {"_default": 1.0}
        base_characteristics = dict.fromkeys(DIMENSIONS, 0.5)

        affinity = calculate_affinity(user_vector, dish_vector, adaptation_weights, base_characteristics)

        assert affinity > 0.95

    def test_critical_mismatch_low_affinity(self):
        user_vector = {dim: {"value": 0.5, "tolerance": 0.2} for dim in DIMENSIONS}

        dish_vector = dict.fromkeys(DIMENSIONS, 0.5)
        dish_vector["physics_temperature"] = 0.9

        adaptation_weights = {"physics_temperature": 0.1, "_default": 1.0}
        base_characteristics = {"physics_temperature": 0.1}

        affinity = calculate_affinity(user_vector, dish_vector, adaptation_weights, base_characteristics)

        assert affinity < 0.7

class TestMergeVectors:

    def test_merge_with_override(self):
        base = {"a": 0.5, "b": 0.5}
        override = {"b": 0.9, "c": 0.7}

        merged = merge_vectors(base, override)

        assert merged["a"] == 0.5
        assert merged["b"] == 0.9
        assert merged["c"] == 0.7

    def test_merge_with_none_override(self):
        base = {"a": 0.5}

        merged = merge_vectors(base, None)

        assert merged == base
