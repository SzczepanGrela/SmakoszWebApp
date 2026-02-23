"""
Integration test for Phase 2 migration to new architecture.

Validates that RestaurantsPhase works correctly and integrates with
PhaseRegistry, including proper dependency handling.
"""

from unittest.mock import MagicMock, patch

from generators.phase2_restaurants import RestaurantsPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase2Metadata:
    """Test RestaurantsPhase metadata."""

    def test_restaurants_phase_metadata(self):
        """Test RestaurantsPhase has correct metadata."""
        phase = RestaurantsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase2_restaurants"
        assert metadata.display_name == "Restaurants Generation"

        # Critical: Phase 2 depends on Phase 1 cities
        assert "phase1_cities" in metadata.dependencies

        assert "restaurants" in metadata.required_tables
        assert "restaurant_opening_hours" in metadata.required_tables
        assert "menu_sections" in metadata.required_tables

class TestPhase2Registration:
    """Test Phase 2 integration with PhaseRegistry."""

    def test_restaurants_phase_registers(self):
        """Test that RestaurantsPhase can be registered."""
        registry = PhaseRegistry()
        phase = RestaurantsPhase()

        registry.register(phase)

        retrieved = registry.get("phase2_restaurants")
        assert retrieved is phase

    def test_phase2_dependency_resolution(self):
        """Test that Phase 2 dependencies are resolved correctly."""
        from generators.phase1_definitions import CitiesPhase

        registry = PhaseRegistry()

        # Register Phase 1 cities and Phase 2
        registry.register(CitiesPhase())
        registry.register(RestaurantsPhase())

        # Resolve dependencies for Phase 2
        resolved = registry.resolve_dependencies(["phase2_restaurants"])

        # Should include phase1_cities before phase2_restaurants
        assert len(resolved) == 2
        assert resolved[0] == "phase1_cities"
        assert resolved[1] == "phase2_restaurants"

class TestPhase2DependencyValidation:
    """Test Phase 2 dependency validation."""

    def test_phase2_requires_cities(self):
        """Test that Phase 2 fails gracefully if cities not populated."""
        mock_db = MagicMock()

        # Mock that cities table exists but is empty
        mock_db.fetch_all.return_value = []  # No cities

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_restaurants": 100}, phase_registry=PhaseRegistry())

        # Should execute but handle empty cities gracefully
        # (The actual function might have its own error handling)
        with patch("generators.phase2_restaurants.generate_restaurants") as mock_gen:
            mock_gen.side_effect = Exception("No cities available")

            result = phase.execute(context)

        # Should fail with appropriate error
        assert result.status == PhaseStatus.FAILED
        assert result.error is not None

class TestPhase2ExecutionStructure:
    """Test Phase 2 execution structure."""

    def test_restaurants_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure."""
        mock_db = MagicMock()
        mock_db.fetch_val.side_effect = [
            50,  # restaurants count
            150,  # menu_sections count
            350,  # opening_hours count
        ]

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={"num_restaurants": 50}, phase_registry=PhaseRegistry())

        # Mock the generate_restaurants function
        with patch("generators.phase2_restaurants.generate_restaurants"):
            result = phase.execute(context)

        # Verify result structure
        assert result.phase_id == "phase2_restaurants"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0

        # Check entities generated
        assert "restaurants" in result.entities_generated
        assert "menu_sections" in result.entities_generated
        assert "opening_hours" in result.entities_generated

        assert result.entities_generated["restaurants"] == 50
        assert result.entities_generated["menu_sections"] == 150
        assert result.entities_generated["opening_hours"] == 350

        assert result.error is None

class TestPhase2ErrorHandling:
    """Test Phase 2 error handling."""

    def test_restaurants_phase_handles_generation_failure(self):
        """Test that RestaurantsPhase handles generation failures gracefully."""
        mock_db = MagicMock()

        phase = RestaurantsPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        # Mock generate_restaurants to raise an exception
        with patch("generators.phase2_restaurants.generate_restaurants") as mock_gen:
            mock_gen.side_effect = RuntimeError("Database connection failed")

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)
        assert "Database connection failed" in str(result.error)
