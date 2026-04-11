from unittest.mock import MagicMock, patch

from generators.phase3_dishes import DishesPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase3Metadata:

    def test_dishes_phase_metadata(self):
        phase = DishesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase3_dishes"
        assert metadata.display_name == "Dishes Generation"

        assert len(metadata.dependencies) == 3
        assert "phase1_ingredients" in metadata.dependencies
        assert "phase1_tags" in metadata.dependencies
        assert "phase2_restaurants" in metadata.dependencies

        assert "dishes" in metadata.required_tables
        assert "dish_variants" in metadata.required_tables
        assert "dish_ingredients" in metadata.required_tables

class TestPhase3Registration:

    def test_dishes_phase_registers(self):
        registry = PhaseRegistry()
        phase = DishesPhase()

        registry.register(phase)

        retrieved = registry.get("phase3_dishes")
        assert retrieved is phase

    def test_phase3_dual_dependency_resolution(self):
        from generators.phase1_definitions import CuisineTypesPhase, IngredientsPhase, TagsPhase
        from generators.phase2_restaurants import RestaurantsPhase

        registry = PhaseRegistry()

        from generators.phase1_definitions import CitiesPhase

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())

        resolved = registry.resolve_dependencies(["phase3_dishes"])

        assert len(resolved) >= 3

        ingredients_idx = resolved.index("phase1_ingredients")
        cities_idx = resolved.index("phase1_cities")
        restaurants_idx = resolved.index("phase2_restaurants")
        dishes_idx = resolved.index("phase3_dishes")

        assert cities_idx < restaurants_idx

        assert ingredients_idx < dishes_idx
        assert restaurants_idx < dishes_idx

class TestPhase3DependencyValidation:

    def test_phase3_requires_ingredients(self):
        mock_db = MagicMock()

        mock_db.fetch_all.return_value = []

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_dishes": 100}, phase_registry=PhaseRegistry())

        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            mock_gen.side_effect = Exception("No ingredients found")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

    def test_phase3_requires_restaurants(self):
        mock_db = MagicMock()

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            mock_gen.side_effect = Exception("No restaurants available")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert "restaurants" in str(result.error).lower()

class TestPhase3ExecutionStructure:

    def test_dishes_phase_execute_structure(self):
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            100,
            150,
            500,
        ]

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_dishes": 100}, phase_registry=PhaseRegistry())

        with patch("generators.phase3_dishes.generate_dishes"):
            result = phase.execute(context)

        assert result.phase_id == "phase3_dishes"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        assert "dishes" in result.entities_generated
        assert "dish_variants" in result.entities_generated
        assert "dish_ingredients" in result.entities_generated

        assert result.entities_generated["dishes"] == 100
        assert result.entities_generated["dish_variants"] == 150
        assert result.entities_generated["dish_ingredients"] == 500

        assert result.error is None

class TestPhase3ErrorHandling:

    def test_dishes_phase_handles_generation_failure(self):
        mock_db = MagicMock()

        phase = DishesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase3_dishes.generate_dishes") as mock_gen:
            mock_gen.side_effect = RuntimeError("Dish generation failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "Dish generation failed" in str(result.error)

class TestPhase3ComplexDependencies:

    def test_full_dependency_chain(self):
        from generators.phase1_definitions import CitiesPhase, CuisineTypesPhase, IngredientsPhase, TagsPhase
        from generators.phase2_restaurants import RestaurantsPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())
        registry.register(DishesPhase())

        resolved = registry.resolve_dependencies(["phase3_dishes"])

        assert "phase1_cities" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase1_tags" in resolved
        assert "phase2_restaurants" in resolved
        assert "phase3_dishes" in resolved

        assert resolved.index("phase1_cities") < resolved.index("phase2_restaurants")
        assert resolved.index("phase1_ingredients") < resolved.index("phase3_dishes")
        assert resolved.index("phase1_tags") < resolved.index("phase3_dishes")
        assert resolved.index("phase2_restaurants") < resolved.index("phase3_dishes")
