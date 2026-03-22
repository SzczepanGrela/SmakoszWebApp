"""
Integration test for Phase 3 migration to new architecture.

Validates that DishesPhase works correctly with dual dependencies
(ingredients + restaurants) and integrates with PhaseRegistry.
"""

from unittest.mock import MagicMock, patch

from generators.phase3_dishes import DishesPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase3Metadata:
    """Test DishesPhase metadata."""

    def test_dishes_phase_metadata(self):
        """Test DishesPhase has correct metadata."""
        phase = DishesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase3_dishes"
        assert metadata.display_name == "Dishes Generation"

        # Critical: Phase 3 has DUAL dependencies
        assert len(metadata.dependencies) == 2
        assert "phase1_ingredients" in metadata.dependencies
        assert "phase2_restaurants" in metadata.dependencies

        assert "dishes" in metadata.required_tables
        assert "dish_variants" in metadata.required_tables
        assert "dish_ingredients" in metadata.required_tables

class TestPhase3Registration:
    """Test Phase 3 integration with PhaseRegistry."""

    def test_dishes_phase_registers(self):
        """Test that DishesPhase can be registered."""
        registry = PhaseRegistry()
        phase = DishesPhase()

        registry.register(phase)

        retrieved = registry.get("phase3_dishes")
        assert retrieved is phase

    def test_phase3_dual_dependency_resolution(self):
        """Test that Phase 3 dual dependencies are resolved correctly."""
        from generators.phase1_definitions import IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase

        registry = PhaseRegistry()

        # Register all dependencies
        from generators.phase1_definitions import CitiesPhase

        registry.register(CitiesPhase())  # Required by RestaurantsPhase
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())

        # Resolve dependencies for Phase 3
        resolved = registry.resolve_dependencies(["phase3_dishes"])

        # Should include all dependencies in correct order
        assert len(resolved) >= 3

        # Ingredients and Cities should come before Restaurants
        ingredients_idx = resolved.index("phase1_ingredients")
        cities_idx = resolved.index("phase1_cities")
        restaurants_idx = resolved.index("phase2_restaurants")
        dishes_idx = resolved.index("phase3_dishes")

        # Cities must come before Restaurants (Restaurants depends on Cities)
        assert cities_idx < restaurants_idx

        # Both Ingredients and Restaurants must come before Dishes
        assert ingredients_idx < dishes_idx
        assert restaurants_idx < dishes_idx

class TestPhase3DependencyValidation:
    """Test Phase 3 dependency validation."""

    def test_phase3_requires_ingredients(self):
        """Test that Phase 3 validates ingredients dependency."""
        mock_db = MagicMock()

        # Mock that ingredients table is empty
        mock_db.fetch_all.return_value = []  # No ingredients

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_dishes": 100}, phase_registry=PhaseRegistry())

        # Should execute but might handle empty ingredients gracefully
        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            # Simulate error due to missing ingredients
            mock_gen.side_effect = Exception("No ingredients found")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

    def test_phase3_requires_restaurants(self):
        """Test that Phase 3 validates restaurants dependency."""
        mock_db = MagicMock()

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        # Simulate error due to missing restaurants
        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            mock_gen.side_effect = Exception("No restaurants available")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "restaurants" in str(result.error).lower()

class TestPhase3ExecutionStructure:
    """Test Phase 3 execution structure."""

    def test_dishes_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure."""
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            100,  # dishes count
            150,  # dish_variants count
            500,  # dish_ingredients count
        ]

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_dishes": 100}, phase_registry=PhaseRegistry())

        # Mock the generate_dishes function
        with patch("generators.phase3_dishes.generate_dishes"):
            result = phase.execute(context)

        # Verify result structure
        assert result.phase_id == "phase3_dishes"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        # Check entities generated
        assert "dishes" in result.entities_generated
        assert "dish_variants" in result.entities_generated
        assert "dish_ingredients" in result.entities_generated

        assert result.entities_generated["dishes"] == 100
        assert result.entities_generated["dish_variants"] == 150
        assert result.entities_generated["dish_ingredients"] == 500

        assert result.error is None

class TestPhase3ErrorHandling:
    """Test Phase 3 error handling."""

    def test_dishes_phase_handles_generation_failure(self):
        """Test that DishesPhase handles generation failures gracefully."""
        mock_db = MagicMock()

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        # Mock generate_dishes to raise an exception
        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            mock_gen.side_effect = RuntimeError("Dish generation failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "Dish generation failed" in str(result.error)

class TestPhase3ComplexDependencies:
    """Test Phase 3 complex dependency scenarios."""

    def test_full_dependency_chain(self):
        """Test complete dependency chain: Cities -> Restaurants -> Dishes."""
        from generators.phase1_definitions import CitiesPhase, IngredientsPhase
        from generators.phase2_restaurants import RestaurantsPhase

        registry = PhaseRegistry()

        # Register full chain
        registry.register(CitiesPhase())
        registry.register(IngredientsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())

        # Request only Phase 3
        resolved = registry.resolve_dependencies(["phase3_dishes"])

        # Should automatically include entire dependency tree
        assert "phase1_cities" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase2_restaurants" in resolved
        assert "phase3_dishes" in resolved

        # Verify correct order
        assert resolved.index("phase1_cities") < resolved.index("phase2_restaurants")
        assert resolved.index("phase1_ingredients") < resolved.index("phase3_dishes")
        assert resolved.index("phase2_restaurants") < resolved.index("phase3_dishes")
