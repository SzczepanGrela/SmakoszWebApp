"""
Integration test for Phase 1 Hero Images migration to new architecture.

Validates that HeroImagesPhase works correctly as an independent Phase 1
component and integrates with PhaseRegistry.
"""

from unittest.mock import MagicMock, patch

from generators.phase1_definitions import HeroImagesPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestHeroImagesPhaseMetadata:
    """Test HeroImagesPhase metadata."""

    def test_hero_images_phase_metadata(self):
        """Test HeroImagesPhase has correct metadata."""
        phase = HeroImagesPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase1_hero"
        assert metadata.display_name == "Hero Images Registration"

        # Phase 1 has no dependencies - runs in parallel with Cities, Cuisines, etc.
        assert metadata.dependencies == []

        # Only touches hero rows in media_assets (targeted DELETE, not TRUNCATE)
        assert "media_assets" in metadata.required_tables
        assert metadata.cleanup_tables == []

    def test_hero_images_phase_no_dependencies(self):
        """Confirm HeroImagesPhase has no inter-phase dependencies."""
        phase = HeroImagesPhase()
        assert len(phase.metadata.dependencies) == 0

class TestHeroImagesPhaseRegistration:
    """Test HeroImagesPhase integration with PhaseRegistry."""

    def test_hero_images_phase_registers(self):
        """Test that HeroImagesPhase can be registered."""
        registry = PhaseRegistry()
        phase = HeroImagesPhase()

        registry.register(phase)

        retrieved = registry.get("phase1_hero")
        assert retrieved is phase

    def test_phase1_hero_dependency_resolution(self):
        """Test that phase1_hero resolves with only itself (no upstream deps)."""
        registry = PhaseRegistry()
        registry.register(HeroImagesPhase())

        resolved = registry.resolve_dependencies(["phase1_hero"])

        assert resolved == ["phase1_hero"]

    def test_phase1_hero_parallel_with_cities(self):
        """Test that HeroImages and Cities can be registered together independently."""
        from generators.phase1_definitions import CitiesPhase

        registry = PhaseRegistry()
        registry.register(CitiesPhase())
        registry.register(HeroImagesPhase())

        # Each resolves independently - no ordering constraint between them
        hero_chain = registry.resolve_dependencies(["phase1_hero"])
        cities_chain = registry.resolve_dependencies(["phase1_cities"])

        assert hero_chain == ["phase1_hero"]
        assert cities_chain == ["phase1_cities"]

class TestHeroImagesPhaseExecution:
    """Test HeroImagesPhase execution structure."""

    def test_hero_images_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure when hero index exists."""
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
        """Test that missing hero_index.json returns COMPLETED with 0 images."""
        mock_db = MagicMock()

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("generators.phase1_definitions.HERO_INDEX_PATH") as mock_path:
            mock_path.exists.return_value = False
            result = phase.execute(context)

        assert result.status == PhaseStatus.COMPLETED
        assert result.entities_generated["hero_images"] == 0

    def test_hero_images_phase_handles_failure(self):
        """Test that HeroImagesPhase handles DB failures gracefully."""
        mock_db = MagicMock()
        mock_db.execute_query.side_effect = RuntimeError("DB connection lost")

        phase = HeroImagesPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, RuntimeError)

    def test_hero_images_cleanup_deletes_existing_rows(self):
        """Test that execute() issues targeted DELETE for hero rows before insert."""
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
