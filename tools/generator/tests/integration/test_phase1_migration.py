"""
Integration tests for Phase 1 migration to new architecture.

Validates that Phase 1 classes (Cities, Cuisines, Ingredients, Tags)
produce correct results and integrate with PhaseRegistry.
"""

from unittest.mock import MagicMock, Mock, patch

from generators.phase1_definitions import (
    CitiesPhase,
    CuisineTypesPhase,
    IngredientsPhase,
    TagsPhase,
)
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase1Metadata:
    """Test that all Phase 1 classes have correct metadata."""

    def test_cities_phase_metadata(self):
        """Test CitiesPhase metadata."""
        phase = CitiesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_cities"
        assert metadata.display_name == "Cities Generation"
        assert metadata.dependencies == []  # No dependencies
        assert "cities" in metadata.required_tables

    def test_cuisines_phase_metadata(self):
        """Test CuisineTypesPhase metadata."""
        phase = CuisineTypesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_cuisines"
        assert metadata.display_name == "Cuisine Types Generation"
        assert metadata.dependencies == []  # No dependencies
        assert "cuisine_types" in metadata.required_tables

    def test_ingredients_phase_metadata(self):
        """Test IngredientsPhase metadata."""
        phase = IngredientsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_ingredients"
        assert metadata.display_name == "Ingredients Generation"
        assert metadata.dependencies == []  # No dependencies
        assert "ingredients" in metadata.required_tables

    def test_tags_phase_metadata(self):
        """Test TagsPhase metadata."""
        phase = TagsPhase()
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_tags"
        assert metadata.display_name == "Tags Generation"
        assert metadata.dependencies == []  # No dependencies
        assert "tags" in metadata.required_tables

class TestPhase1Registration:
    """Test that Phase 1 classes integrate with PhaseRegistry."""

    def test_all_phase1_phases_register(self):
        """Test that all 4 Phase 1 classes can be registered."""
        registry = PhaseRegistry()

        # Register all Phase 1 phases
        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())

        # Should have 4 phases
        all_phases = registry.get_all()
        assert len(all_phases) == 4

        # Should be able to retrieve each
        assert registry.get("phase1_cities") is not None
        assert registry.get("phase1_cuisines") is not None
        assert registry.get("phase1_ingredients") is not None
        assert registry.get("phase1_tags") is not None

    def test_phase1_dependency_resolution(self):
        """Test that Phase 1 phases can run in parallel (no dependencies)."""
        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())

        # Resolve dependencies for all Phase 1 phases
        resolved = registry.resolve_dependencies(
            [
                "phase1_cities",
                "phase1_cuisines",
                "phase1_ingredients",
                "phase1_tags",
            ]
        )

        # Since no dependencies, order should match input
        # (or be a valid permutation - all are parallel)
        assert len(resolved) == 4
        assert "phase1_cities" in resolved
        assert "phase1_cuisines" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase1_tags" in resolved

class TestPhase1Execution:
    """Test Phase 1 execution."""

    def test_cities_phase_executes(self):
        """Test CitiesPhase executes successfully with mock."""
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        mock_blueprint = {
            "CITY_CONFIG": {
                "Warszawa": {},
                "Kraków": {},
                "Wrocław": {},
            }
        }

        with patch("generators.phase1_definitions.BlueprintLoader") as MockLoader:
            mock_loader = Mock()
            mock_loader.load_blueprint.return_value = mock_blueprint
            MockLoader.return_value = mock_loader

            phase = CitiesPhase(blueprints_dir="blueprints")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        # Verify result
        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["cities"] == 3
        assert result.error is None

        # Verify insert_bulk was called
        assert mock_db.insert_bulk.called
        call_args = mock_db.insert_bulk.call_args
        assert call_args[0][0] == "cities"
        assert len(call_args[0][1]) == 3  # 3 cities

    def test_cuisines_phase_executes(self):
        """Test CuisineTypesPhase executes successfully."""
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        mock_blueprint = {
            "RESTAURANT_THEMES": {
                "Pizzeria": {},
                "Burgerownia": {},
                "Sushi Bar": {},
            }
        }

        with patch("generators.phase1_definitions.BlueprintLoader") as MockLoader:
            mock_loader = Mock()
            mock_loader.load_blueprint.return_value = mock_blueprint
            MockLoader.return_value = mock_loader

            phase = CuisineTypesPhase(blueprints_dir="blueprints")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["cuisine_types"] == 3
        assert mock_db.insert_bulk.called

    def test_tags_phase_executes(self):
        """Test TagsPhase executes successfully."""
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        phase = TagsPhase()
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        # Verify result
        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["tags"] > 0  # Should have many tags
        assert result.error is None

        # Verify insert_bulk was called with tags
        assert mock_db.insert_bulk.called
        call_args = mock_db.insert_bulk.call_args
        assert call_args[0][0] == "tags"

    def test_ingredients_phase_executes(self):
        """Test IngredientsPhase executes successfully."""
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        mock_dishes = {"Pizza": {"variants": {"Margherita": {"ingredients": ["mozzarella", "pomidory", "bazylia"]}}}}

        mock_global_config = {
            "DIETARY_KEYWORDS": {
                "meat": ["kurczak", "wołowina"],
                "dairy": ["ser", "mleko"],
                "eggs": ["jajko"],
                "gluten": ["pszenica", "mąka"],
            }
        }

        with (
            patch("generators.phase1_definitions.BlueprintLoader") as MockLoader,
            patch("generators.phase1_definitions.PhotoPools") as MockPhotoP,
            patch("generators.phase1_definitions.tqdm", side_effect=lambda x, **kwargs: x),
        ):
            mock_loader = Mock()
            mock_loader.load_blueprint.side_effect = [
                mock_dishes,  # First call: dishes.json
                mock_global_config,  # Second call: global_config.json
            ]
            MockLoader.return_value = mock_loader

            mock_photo_pools = Mock()
            mock_photo_pools.get_ingredient_photo.return_value = {
                "url": "http://example.com/photo.jpg",
                "blurhash": "ABC123",
            }
            MockPhotoP.return_value = mock_photo_pools

            phase = IngredientsPhase(blueprints_dir="blueprints")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["ingredients"] == 3  # 3 unique ingredients
        assert mock_db.insert_bulk.called

class TestPhase1ErrorHandling:
    """Test Phase 1 error handling."""

    def test_cities_phase_handles_missing_blueprint(self):
        """Test CitiesPhase handles missing blueprint gracefully."""
        mock_db = MagicMock()

        with patch("generators.phase1_definitions.BlueprintLoader") as MockLoader:
            mock_loader = Mock()
            mock_loader.load_blueprint.side_effect = FileNotFoundError("Not found")
            MockLoader.return_value = mock_loader

            phase = CitiesPhase(blueprints_dir="nonexistent")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, FileNotFoundError)

    def test_cities_phase_handles_empty_config(self):
        """Test CitiesPhase handles empty CITY_CONFIG."""
        mock_db = MagicMock()

        with patch("generators.phase1_definitions.BlueprintLoader") as MockLoader:
            mock_loader = Mock()
            mock_loader.load_blueprint.return_value = {"CITY_CONFIG": {}}
            MockLoader.return_value = mock_loader

            phase = CitiesPhase(blueprints_dir="blueprints")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        # Empty dict triggers the "must contain CITY_CONFIG key" check
        assert "CITY_CONFIG" in str(result.error)
