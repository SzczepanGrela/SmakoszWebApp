from unittest.mock import MagicMock, patch

from generators.phase2_restaurants import RestaurantsPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase2Metadata:

    def test_restaurants_phase_metadata(self):
        phase = RestaurantsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase2_restaurants"
        assert metadata.display_name == "Restaurants Generation"

        assert "phase1_cities" in metadata.dependencies

        assert "restaurants" in metadata.required_tables
        assert "restaurant_opening_hours" in metadata.required_tables
        assert "menu_sections" in metadata.required_tables

class TestPhase2Registration:

    def test_restaurants_phase_registers(self):
        registry = PhaseRegistry()
        phase = RestaurantsPhase()

        registry.register(phase)

        retrieved = registry.get("phase2_restaurants")
        assert retrieved is phase

    def test_phase2_dependency_resolution(self):
        from generators.phase1_definitions import CitiesPhase, CuisineTypesPhase, TagsPhase

        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(TagsPhase())
        registry.register(RestaurantsPhase())

        resolved = registry.resolve_dependencies(["phase2_restaurants"])

        assert len(resolved) == 4
        assert resolved[-1] == "phase2_restaurants"
        assert resolved.index("phase1_cities") < resolved.index("phase2_restaurants")
        assert resolved.index("phase1_cuisines") < resolved.index("phase2_restaurants")
        assert resolved.index("phase1_tags") < resolved.index("phase2_restaurants")

class TestPhase2DependencyValidation:

    def test_phase2_requires_cities(self):
        mock_db = MagicMock()

        mock_db.fetch_all.return_value = []

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_restaurants": 100}, phase_registry=PhaseRegistry())

        with patch("generators.phase2_restaurants.generate_restaurants") as mock_gen:
            mock_gen.side_effect = Exception("No cities available")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

class TestPhase2ExecutionStructure:

    def test_restaurants_phase_execute_structure(self):
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            50,
            150,
            350,
        ]

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_restaurants": 50}, phase_registry=PhaseRegistry())

        with patch("generators.phase2_restaurants.generate_restaurants"):
            result = phase.execute(context)

        assert result.phase_id == "phase2_restaurants"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        assert "restaurants" in result.entities_generated
        assert "menu_sections" in result.entities_generated
        assert "opening_hours" in result.entities_generated

        assert result.entities_generated["restaurants"] == 50
        assert result.entities_generated["menu_sections"] == 150
        assert result.entities_generated["opening_hours"] == 350

        assert result.error is None

class TestPhase2ErrorHandling:

    def test_restaurants_phase_handles_generation_failure(self):
        mock_db = MagicMock()

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase2_restaurants.generate_restaurants") as mock_gen:
            mock_gen.side_effect = RuntimeError("Database connection failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "Database connection failed" in str(result.error)
