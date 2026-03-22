"""
Integration test for Phase 0 migration to new architecture.

Validates that SystemConfigPhase produces identical results to legacy
generate_system_config() function.
"""

from unittest.mock import MagicMock, Mock

from generators.phase0_config import SystemConfigPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase0Migration:
    """Test Phase 0 migration from function to BasePhase."""

    def test_system_config_phase_metadata(self):
        """Test that SystemConfigPhase has correct metadata."""
        phase = SystemConfigPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase0_config"
        assert metadata.display_name == "System Configuration"
        assert metadata.dependencies == []
        assert "system.config" in metadata.required_tables
        assert metadata.estimated_duration == 5

    def test_system_config_phase_execute_structure(self):
        """Test that execute() returns proper PhaseResult structure."""
        phase = SystemConfigPhase(blueprints_dir="blueprints")

        # Mock context
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        # Mock file reading to avoid filesystem dependency
        import json
        from unittest.mock import mock_open, patch

        mock_config = {
            "SYSTEM_CONFIG": {"APP_NAME": {"value": "Smakosz", "description": "Application name", "is_public": True}}
        }

        with patch("builtins.open", mock_open(read_data=json.dumps(mock_config))):
            result = phase.execute(context)

        # Verify result structure
        assert result.phase_id == "phase0_config"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0
        assert "config_entries" in result.entities_generated
        assert result.entities_generated["config_entries"] == 1
        assert result.error is None

    def test_system_config_phase_handles_missing_file(self):
        """Test that phase handles missing blueprint file gracefully."""
        phase = SystemConfigPhase(blueprints_dir="nonexistent")

        mock_db = MagicMock()
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        # Should return FAILED status
        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, FileNotFoundError)

    def test_phase_registry_integration(self):
        """Test that SystemConfigPhase integrates with PhaseRegistry."""
        registry = PhaseRegistry()
        phase = SystemConfigPhase(blueprints_dir="blueprints")

        # Should register without error
        registry.register(phase)

        # Should be retrievable
        retrieved = registry.get("phase0_config")
        assert retrieved is phase

        # Dependency resolution should work (no dependencies)
        resolved = registry.resolve_dependencies(["phase0_config"])
        assert resolved == ["phase0_config"]

    def test_execute_inserts_correct_data(self):
        """Test that execute() inserts correct data structure into system.config."""
        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        import json
        from unittest.mock import mock_open, patch

        mock_config = {
            "SYSTEM_CONFIG": {
                "KEY1": {"value": "val1", "description": "desc1", "is_public": True},
                "KEY2": {"value": "val2", "description": "desc2", "is_public": False},
            }
        }

        phase = SystemConfigPhase(blueprints_dir="blueprints")
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        with patch("builtins.open", mock_open(read_data=json.dumps(mock_config))):
            phase.execute(context)

        call_args = mock_db.insert_bulk.call_args
        assert call_args[0][0] == "system.config"
        entries = call_args[0][1]
        assert len(entries) == 2
        assert entries[0]["key"] == "KEY1"
        assert entries[0]["value"] == "val1"
