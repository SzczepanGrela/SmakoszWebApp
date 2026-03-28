from unittest.mock import MagicMock, Mock, patch

from generators.phase1_definitions import (
    CitiesPhase,
    CuisineTypesPhase,
    IngredientsPhase,
    TagsPhase,
)
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase1Metadata:

    def test_cities_phase_metadata(self):
        phase = CitiesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_cities"
        assert metadata.display_name == "Cities Generation"
        assert metadata.dependencies == []
        assert "cities" in metadata.required_tables

    def test_cuisines_phase_metadata(self):
        phase = CuisineTypesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_cuisines"
        assert metadata.display_name == "Cuisine Types Generation"
        assert metadata.dependencies == []
        assert "cuisine_types" in metadata.required_tables

    def test_ingredients_phase_metadata(self):
        phase = IngredientsPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_ingredients"
        assert metadata.display_name == "Ingredients Generation"
        assert metadata.dependencies == []
        assert "ingredients" in metadata.required_tables

    def test_tags_phase_metadata(self):
        phase = TagsPhase()
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_tags"
        assert metadata.display_name == "Tags Generation"
        assert metadata.dependencies == []
        assert "tags" in metadata.required_tables

class TestPhase1Registration:

    def test_all_phase1_phases_register(self):
        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())

        all_phases = registry.get_all()
        assert len(all_phases) == 4

        assert registry.get("phase1_cities") is not None
        assert registry.get("phase1_cuisines") is not None
        assert registry.get("phase1_ingredients") is not None
        assert registry.get("phase1_tags") is not None

    def test_phase1_dependency_resolution(self):
        registry = PhaseRegistry()

        registry.register(CitiesPhase())
        registry.register(CuisineTypesPhase())
        registry.register(IngredientsPhase())
        registry.register(TagsPhase())

        resolved = registry.resolve_dependencies(
            [
                "phase1_cities",
                "phase1_cuisines",
                "phase1_ingredients",
                "phase1_tags",
            ]
        )

        assert len(resolved) == 4
        assert "phase1_cities" in resolved
        assert "phase1_cuisines" in resolved
        assert "phase1_ingredients" in resolved
        assert "phase1_tags" in resolved

class TestPhase1Execution:

    def test_cities_phase_executes(self):
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

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["cities"] == 3
        assert result.error is None

        assert mock_db.insert_bulk.called
        call_args = mock_db.insert_bulk.call_args
        assert call_args[0][0] == "cities"
        assert len(call_args[0][1]) == 3

    def test_cuisines_phase_executes(self):
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        mock_themes = [
            {"name": "Pizzeria", "display_name": "Pizzeria", "icon": "icon1"},
            {"name": "Burgerownia", "display_name": "Burgerownia", "icon": "icon2"},
            {"name": "Sushi Bar", "display_name": "Sushi Bar", "icon": "icon3"},
        ]

        with patch("generators.phase1_definitions.BlueprintDB") as MockBDB:
            mock_bdb = MagicMock()
            mock_bdb.get_themes.return_value = mock_themes
            MockBDB.return_value = mock_bdb

            phase = CuisineTypesPhase(blueprints_dir="blueprints")
            context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

            result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["cuisine_types"] == 3
        assert mock_db.insert_bulk.called

    def test_tags_phase_executes(self):
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        phase = TagsPhase()
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["tags"] > 0
        assert result.error is None

        assert mock_db.insert_bulk.called
        call_args = mock_db.insert_bulk.call_args
        assert call_args[0][0] == "tags"

    def test_ingredients_phase_executes(self):
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        mock_ingredients = [
            {"name": "mozzarella", "is_meat": 0, "is_dairy": 1, "is_egg": 0, "is_gluten": 0},
            {"name": "pomidory", "is_meat": 0, "is_dairy": 0, "is_egg": 0, "is_gluten": 0},
            {"name": "bazylia", "is_meat": 0, "is_dairy": 0, "is_egg": 0, "is_gluten": 0},
        ]

        with (
            patch("generators.phase1_definitions.BlueprintDB") as MockBDB,
            patch("generators.phase1_definitions.PhotoPools") as MockPhotoP,
            patch("generators.phase1_definitions.tqdm", side_effect=lambda x, **kwargs: x),
        ):
            mock_bdb = MagicMock()
            mock_bdb.get_all_ingredients.return_value = mock_ingredients
            MockBDB.return_value = mock_bdb

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
        assert result.entities_generated["ingredients"] == 3
        assert mock_db.insert_bulk.called

class TestPhase1ErrorHandling:

    def test_cities_phase_handles_missing_blueprint(self):
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
        assert "CITY_CONFIG" in str(result.error)
