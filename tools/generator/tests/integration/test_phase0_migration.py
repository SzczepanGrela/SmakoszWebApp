from unittest.mock import MagicMock, Mock

from generators.phase0_config import SystemConfigPhase
from orchestration import ExecutionContext, PhaseRegistry, PhaseStatus

class TestPhase0Migration:

    def test_system_config_phase_metadata(self):
        phase = SystemConfigPhase(blueprints_dir="blueprints")
        metadata = phase.metadata

        assert metadata.phase_id == "phase0_config"
        assert metadata.display_name == "System Configuration"
        assert metadata.dependencies == []
        assert "system.config" in metadata.required_tables
        assert metadata.estimated_duration == 5

    def test_system_config_phase_execute_structure(self):
        phase = SystemConfigPhase(blueprints_dir="blueprints")

        mock_db = MagicMock()
        mock_db.insert_bulk = Mock()

        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        import json
        from unittest.mock import mock_open, patch

        mock_config = {
            "SYSTEM_CONFIG": {"APP_NAME": {"value": "Smakosz", "description": "Application name", "is_public": True}}
        }

        with patch("builtins.open", mock_open(read_data=json.dumps(mock_config))):
            result = phase.execute(context)

        assert result.phase_id == "phase0_config"
        assert result.status == PhaseStatus.COMPLETED
        assert result.duration_seconds >= 0
        assert "config_entries" in result.entities_generated
        assert result.entities_generated["config_entries"] == 1
        assert result.error is None

    def test_system_config_phase_handles_missing_file(self):
        phase = SystemConfigPhase(blueprints_dir="nonexistent")

        mock_db = MagicMock()
        context = ExecutionContext(db=mock_db, config={}, phase_registry=PhaseRegistry())

        result = phase.execute(context)

        assert result.status == PhaseStatus.FAILED
        assert result.error is not None
        assert isinstance(result.error, FileNotFoundError)

    def test_phase_registry_integration(self):
        registry = PhaseRegistry()
        phase = SystemConfigPhase(blueprints_dir="blueprints")

        registry.register(phase)

        retrieved = registry.get("phase0_config")
        assert retrieved is phase

        resolved = registry.resolve_dependencies(["phase0_config"])
        assert resolved == ["phase0_config"]

    def test_execute_inserts_correct_data(self):
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
