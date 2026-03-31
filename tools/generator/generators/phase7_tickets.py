import logging
import random
import time
from datetime import datetime, timedelta, timezone

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

def _generate_system_tickets(db: DatabaseConnection):
    """Generate system tickets from pending content across all entity types."""
    logger.info("Generating system tickets...")

    admin_users = db.fetch_all("SELECT user_id FROM users WHERE role = 'admin'")
    admin_ids = [u[0] for u in admin_users] if admin_users else []

    now = datetime.now(timezone.utc)
    tickets = []

    # 1. Tickets for pending reviews (content_status = 'pending')
    pending_reviews = db.fetch_all("""
        SELECT r.review_id, r.created_at
        FROM reviews r
        WHERE r.content_status = 'pending'
    """)
    for row in pending_reviews:
        review_id, created_at = row
        tickets.append(_make_ticket("review_content", review_id, 2, created_at or now))

    logger.info(f"  ReviewContent tickets: {len(pending_reviews)}")

    # 2. Tickets for pending photos (status = 'pending')
    pending_photos = db.fetch_all("""
        SELECT ma.asset_id, ma.created_at
        FROM media_assets ma
        WHERE ma.status = 'pending'
    """)
    for row in pending_photos:
        asset_id, created_at = row
        tickets.append(_make_ticket("photo", asset_id, 2, created_at or now))

    logger.info(f"  Photo tickets: {len(pending_photos)}")

    # 3. Tickets for pending reports
    pending_reports = db.fetch_all("""
        SELECT rp.report_id, rp.created_at
        FROM reports rp
        WHERE rp.status = 'pending'
    """)
    for row in pending_reports:
        report_id, created_at = row
        tickets.append(_make_ticket("report", report_id, 2, created_at or now))

    logger.info(f"  Report tickets: {len(pending_reports)}")

    # 4. Tickets for pending edit requests
    pending_edits = db.fetch_all("""
        SELECT re.request_id, re.created_at
        FROM restaurant_edit_requests re
        WHERE re.status = 'pending'
    """)
    for row in pending_edits:
        request_id, created_at = row
        tickets.append(_make_ticket("edit_request", request_id, 3, created_at or now))

    logger.info(f"  EditRequest tickets: {len(pending_edits)}")

    # 5. Tickets for pending data correction requests
    pending_corrections = db.fetch_all("""
        SELECT dcr.request_id, dcr.created_at
        FROM data_correction_requests dcr
        WHERE dcr.status = 'pending'
    """)
    for row in pending_corrections:
        request_id, created_at = row
        tickets.append(_make_ticket("data_correction", request_id, 3, created_at or now))

    logger.info(f"  DataCorrection tickets: {len(pending_corrections)}")

    # Randomly resolve some tickets (~30%) to simulate admin activity
    if admin_ids:
        num_to_resolve = int(len(tickets) * 0.3)
        resolve_indices = random.sample(range(len(tickets)), min(num_to_resolve, len(tickets)))
        for idx in resolve_indices:
            t = tickets[idx]
            t["status"] = "resolved"
            t["assigned_admin_id"] = random.choice(admin_ids)
            t["updated_at"] = t["created_at"] + timedelta(hours=random.randint(1, 72))

    if tickets:
        db.insert_bulk("system.tickets", tickets)
        db.commit()

    total = len(tickets)
    resolved = sum(1 for t in tickets if t["status"] == "resolved")
    logger.info(f"Generated {total} system tickets ({resolved} resolved, {total - resolved} open)")

def _make_ticket(ticket_type: str, reference_id: int, priority: int, created_at) -> dict:
    return {
        "ticket_type": ticket_type,
        "reference_id": reference_id,
        "status": "open",
        "priority": priority,
        "assigned_admin_id": None,
        "created_at": created_at,
        "updated_at": created_at,
        "version": 1,
    }

class TicketsPhase(BasePhase):

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase7_tickets",
            display_name="System Tickets Generation",
            dependencies=[
                "phase5_reviews",
                "phase6_social",
            ],
            required_tables=["system.tickets"],
            cleanup_tables=["system.tickets"],
            estimated_duration=30,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 7: System Tickets Generation")
        logger.info("=" * 60)

        try:
            _generate_system_tickets(context.db)

            tickets_count = context.db.fetch_val("SELECT COUNT(*) FROM system.tickets") or 0

            duration = time.time() - start_time

            logger.info(f"Phase 7 completed in {duration:.2f}s")
            logger.info(f"Generated: {tickets_count:,} system tickets")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={
                    "system_tickets": tickets_count,
                },
                error=None,
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 7 FAILED after {duration:.2f}s: {e}", exc_info=True)

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
