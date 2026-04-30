import logging
import random
import time
from datetime import timedelta

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.date_generator import ensure_naive
from utils.db_connection import DatabaseConnection
from utils.faker_instance import fake

logger = logging.getLogger(__name__)

ADMIN_NOTE_TEMPLATES = [
    "Zatwierdzono po manualnej weryfikacji.",
    "Odrzucono - wulgaryzmy.",
    "Wymaga dodatkowej kontroli.",
    "Tresc niezgodna z regulaminem.",
    "Zatwierdzono - falszywy alarm AI.",
    "Odrzucono - spam reklamowy.",
]

VERDICT_LOG_MAP = {
    "approved": "approve",
    "rejected": "reject",
    "needs_review": "needs_review",
}


def _generate_ai_logs(db: DatabaseConnection) -> int:
    logger.info("Generating AI moderation logs...")

    rows = db.fetch_all(
        "SELECT entity_type, entity_id, scores, ai_verdict, processed_at "
        "FROM system.moderation_results"
    )

    if not rows:
        logger.warning("No moderation_results found, skipping ai_logs")
        return 0

    buffer = []
    for entity_type, entity_id, scores, verdict, processed_at in rows:
        if entity_type == "review":
            model_type = "text_moderation"
            model_name = "HerBERT-base-cased-pl"
            model_version = "1.0.0"
        elif entity_type == "photo":
            model_type = "image_moderation"
            model_name = "gemini-2.0-flash"
            model_version = "2.0"
        else:
            continue

        buffer.append({
            "model_type": model_type,
            "model_name": model_name,
            "model_version": model_version,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "scores": scores,
            "verdict": verdict,
            "processing_time_ms": random.randint(50, 500),
            "fallback": False,
            "created_at": ensure_naive(processed_at),
        })

        if len(buffer) >= 5000:
            db.insert_bulk("system.ai_logs", buffer)
            buffer.clear()

    if buffer:
        db.insert_bulk("system.ai_logs", buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.ai_logs") or 0
    logger.info(f"Generated {count:,} ai_logs entries")
    return count


def _generate_moderation_logs(db: DatabaseConnection) -> int:
    logger.info("Generating moderation logs (AI + admin override)...")

    rows = db.fetch_all(
        "SELECT entity_type, entity_id, ai_verdict, scores, processed_at "
        "FROM system.moderation_results "
        "WHERE entity_type IN ('review', 'photo')"
    )

    if not rows:
        logger.warning("No moderation_results found, skipping moderation_logs")
        return 0

    admin_ids = [r[0] for r in db.fetch_all(
        "SELECT user_id FROM users WHERE role = 'admin' AND is_deleted = false"
    )]
    if not admin_ids:
        admin_ids = [r[0] for r in db.fetch_all(
            "SELECT user_id FROM users WHERE is_deleted = false LIMIT 1"
        )]

    ai_buffer = []
    admin_buffer = []
    for entity_type, entity_id, verdict, scores, processed_at in rows:
        log_verdict = VERDICT_LOG_MAP.get(verdict, "needs_review")
        created_at = ensure_naive(processed_at)

        ai_buffer.append({
            "entity_type": entity_type,
            "entity_id": entity_id,
            "actor": "ai",
            "verdict": log_verdict,
            "reason_codes": [],
            "admin_note": None,
            "processed_by": None,
            "ai_scores": scores,
            "created_at": created_at,
        })

        if verdict == "needs_review" and random.random() < 0.25 and admin_ids:
            admin_verdict = "approve" if random.random() < 0.6 else "reject"
            admin_note = random.choice(ADMIN_NOTE_TEMPLATES) if random.random() < 0.4 else None
            admin_buffer.append({
                "entity_type": entity_type,
                "entity_id": entity_id,
                "actor": "admin",
                "verdict": admin_verdict,
                "reason_codes": [],
                "admin_note": admin_note,
                "processed_by": random.choice(admin_ids),
                "ai_scores": None,
                "created_at": created_at + timedelta(minutes=random.randint(1, 30)),
            })

        if len(ai_buffer) >= 5000:
            db.insert_bulk("system.moderation_logs", ai_buffer)
            ai_buffer.clear()
        if len(admin_buffer) >= 5000:
            db.insert_bulk("system.moderation_logs", admin_buffer)
            admin_buffer.clear()

    if ai_buffer:
        db.insert_bulk("system.moderation_logs", ai_buffer)
    if admin_buffer:
        db.insert_bulk("system.moderation_logs", admin_buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.moderation_logs") or 0
    logger.info(f"Generated {count:,} moderation_logs entries")
    return count


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
            counts["ai_logs"] = _generate_ai_logs(context.db)
            counts["moderation_logs"] = _generate_moderation_logs(context.db)

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
