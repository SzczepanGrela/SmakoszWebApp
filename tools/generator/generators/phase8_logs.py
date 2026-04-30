import logging
import time

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)


class SystemLogsPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase8_logs",
            display_name="System Logs and Sessions",
            dependencies=["phase4_users", "phase5_reviews", "phase6_social"],
            required_tables=[
                "system.ai_logs",
                "system.moderation_logs",
                "system.email_logs",
                "system.security_logs",
                "system.nodes",
                "audit_logs",
                "user_sessions",
            ],
            cleanup_tables=[
                "system.ai_logs",
                "system.moderation_logs",
                "system.email_logs",
                "system.security_logs",
                "system.nodes",
                "audit_logs",
                "user_sessions",
            ],
            estimated_duration=180,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 8: System Logs and Sessions")
        logger.info("=" * 60)

        try:
            counts: dict[str, int] = {}

            duration = time.time() - start_time
            logger.info(f"Phase 8 completed in {duration:.2f}s")
            logger.info(f"Generated: {counts}")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated=counts,
                error=None,
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 8 FAILED after {duration:.2f}s: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
