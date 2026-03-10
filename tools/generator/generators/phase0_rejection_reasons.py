import logging
import time

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)

REJECTION_REASONS = [
    {
        "reason_code": "profanity",
        "category": "text",
        "admin_label": "Wulgarny język",
        "user_message_template": "Twoja treść zawiera niedozwolone słowa.",
        "is_active": True,
    },
    {
        "reason_code": "spam",
        "category": "text",
        "admin_label": "Spam / reklama",
        "user_message_template": "Treść została oznaczona jako spam.",
        "is_active": True,
    },
    {
        "reason_code": "off_topic",
        "category": "text",
        "admin_label": "Niezwiązane z daniem",
        "user_message_template": "Recenzja nie dotyczy ocenianego dania.",
        "is_active": True,
    },
    {
        "reason_code": "fake",
        "category": "text",
        "admin_label": "Fałszywa recenzja",
        "user_message_template": "Recenzja została uznana za nieautentyczną.",
        "is_active": True,
    },
    {
        "reason_code": "inappropriate",
        "category": "photo",
        "admin_label": "Nieodpowiednie zdjęcie",
        "user_message_template": "Zdjęcie narusza regulamin serwisu.",
        "is_active": True,
    },
    {
        "reason_code": "low_quality",
        "category": "photo",
        "admin_label": "Niska jakość",
        "user_message_template": "Zdjęcie nie spełnia wymagań jakościowych.",
        "is_active": True,
    },
    {
        "reason_code": "wrong_subject",
        "category": "photo",
        "admin_label": "Nieprawidłowy obiekt",
        "user_message_template": "Zdjęcie nie przedstawia właściwego dania/restauracji.",
        "is_active": True,
    },
    {
        "reason_code": "copyright",
        "category": "photo",
        "admin_label": "Naruszenie praw autorskich",
        "user_message_template": "Zdjęcie może naruszać prawa autorskie.",
        "is_active": True,
    },
]

class RejectionReasonsPhase(BasePhase):

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase0_rejection_reasons",
            display_name="Rejection Reasons",
            dependencies=[],
            required_tables=["rejection_reasons"],
            cleanup_tables=["rejection_reasons"],
            estimated_duration=5,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Phase 0: Seeding rejection reasons...")

        try:
            for r in REJECTION_REASONS:
                context.db.execute_query(
                    """
                    INSERT INTO rejection_reasons (reason_code, category, admin_label, user_message_template, is_active)
                    VALUES (%s, %s, %s, %s, %s)
                    ON CONFLICT (reason_code) DO NOTHING
                    """,
                    (r["reason_code"], r["category"], r["admin_label"], r["user_message_template"], r["is_active"]),
                )
            context.db.commit()

            duration = time.time() - start_time
            logger.info(f"[OK] Seeded {len(REJECTION_REASONS)} rejection reasons in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"rejection_reasons": len(REJECTION_REASONS)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 0 (rejection reasons) failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
