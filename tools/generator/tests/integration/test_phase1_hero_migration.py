from unittest.mock import MagicMock, patch

from generators.phase1_definitions import HeroImagesPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestHeroImagesPhaseMetadata:

    def test_hero_images_phase_metadata(self):
        phase = HeroImagesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_hero"
        assert metadata.display_name == "Hero Images Registration"

        assert metadata.dependencies == []

        assert "media_assets" in metadata.required_tables
        assert metadata.cleanup_tables == []

    def test_hero_images_phase_no_dependencies(self):
        phase = HeroImagesPhase()
        assert len(phase.metadata.dependencies) == 0

class TestHeroImagesPhaseRegistration:

    def test_hero_images_phase_registers(self):
        registry = PhaseRegistry()
        phase = HeroImagesPhase()

        registry.register(phase)

        retrieved = registry.get("phase1_hero")
        assert retrieved is phase

    def test_phase1_hero_dependency_resolution(self):
        registry = PhaseRegistry()
        registry.register(HeroImagesPhase())

        resolved = registry.resolve_dependencies(["phase1_hero"])

        assert resolved == ["phase1_hero"]

    def test_phase1_hero_parallel_with_cities(self):
        from generators.phase1_definitions import CitiesPhase

        registry = PhaseRegistry()
        registry.register(CitiesPhase())
        registry.register(HeroImagesPhase())

        hero_chain = registry.resolve_dependencies(["phase1_hero"])
        cities_chain = registry.resolve_dependencies(["phase1_cities"])

        assert hero_chain == ["phase1_hero"]
        assert cities_chain == ["phase1_cities"]

class TestHeroImagesPhaseExecution:

    def test_hero_images_phase_execute_structure(self):
        import json
        from unittest.mock import mock_open

        mock_db = MagicMock()
        fake_index = {"images": [{"filename": f"hero_{i:03d}.webp", "source": "pixabay"} for i in range(12)]}

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with (
            patch("generators.phase1_definitions.HERO_INDEX_PATH") as mock_path,
            patch("builtins.open", mock_open(read_data=json.dumps(fake_index))),
        ):
            mock_path.exists.return_value = True
            result = phase.execute(context)

        assert result.phase_id == "phase1_hero"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0
        assert "hero_images" in result.entities_generated
        assert result.entities_generated["hero_images"] == 12
        assert result.error is None

    def test_hero_images_phase_missing_index_returns_zero(self):
        mock_db = MagicMock()

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase1_definitions.HERO_INDEX_PATH") as mock_path:
            mock_path.exists.return_value = False
            result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["hero_images"] == 0

    def test_hero_images_phase_handles_failure(self):
        mock_db = MagicMock()
        mock_db.execute_query.side_effect = RuntimeError("DB connection lost")

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)

    def test_hero_images_cleanup_deletes_existing_rows(self):
        import json
        from unittest.mock import mock_open

        mock_db = MagicMock()
        fake_index = {"images": [{"filename": "hero_001.webp", "source": "pixabay"}]}

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with (
            patch("generators.phase1_definitions.HERO_INDEX_PATH") as mock_path,
            patch("builtins.open", mock_open(read_data=json.dumps(fake_index))),
        ):
            mock_path.exists.return_value = True
            phase.execute(context)

        delete_call = mock_db.execute_query.call_args[0][0]
        assert "DELETE FROM media_assets" in delete_call
        assert "hero" in delete_call
