import json
import logging
import time
from pathlib import Path

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)

class SystemConfigPhase(BasePhase):
    """
    Phase 0: System Configuration

    Populates system.config table with default system settings from blueprint.

    Dependencies: None (first phase)
    Required Tables: system.config
    Estimated Duration: ~5 seconds
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """
        Initialize SystemConfigPhase.

        Args:
            blueprints_dir: Directory containing blueprint JSON files
        """
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase0_config",
            display_name="System Configuration",
            dependencies=[],  # No dependencies - first phase
            required_tables=["system.config"],
            cleanup_tables=["system.config"],
            estimated_duration=5,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """
        Execute system configuration generation.

        Args:
            context: Execution context with DB connection and config

        Returns:
            PhaseResult with status and statistics
        """
        start_time = time.time()
        logger.info("Phase 0: Initializing System Configuration...")

        try:
            # Load blueprint
            config_path = Path(self.blueprints_dir) / "system_config.json"

            try:
                with open(config_path, encoding="utf-8") as f:
                    data = json.load(f)
                    config_items = data.get("SYSTEM_CONFIG", {})
            except FileNotFoundError:
                error_msg = f"Config file not found: {config_path}"
                logger.error(error_msg)
                return PhaseResult(
                    phase_id=self.metadata.phase_id,
                    status=PhaseStatus.FAILED,
                    duration_seconds=time.time() - start_time,
                    entities_generated={},
                    error=FileNotFoundError(error_msg),
                )

            # Prepare insert data
            insert_data = []
            for key, details in config_items.items():
                insert_data.append(
                    {
                        "key": key,
                        "value": details.get("value"),
                        "description": details.get("description"),
                        "is_secret": False,  # Default, can be overridden if needed
                        "is_public": details.get("is_public", False),
                    }
                )

            # Insert into database
            if insert_data:
                context.db.insert_bulk("system.config", insert_data)

            duration = time.time() - start_time
            logger.info(f"✓ Initialized {len(insert_data)} system settings in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"config_entries": len(insert_data)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Phase 0 failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
