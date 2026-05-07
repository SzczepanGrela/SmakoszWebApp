import logging
import time

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)

REPORT_REASONS = [
    {
        "reason_code": "spam",
        "label_pl": "Spam lub reklama",
        "description": "Treść promocyjna, reklamowa lub niezamówione komunikaty.",
        "severity_score": 1,
    },
    {
        "reason_code": "offensive",
        "label_pl": "Obraźliwa treść",
        "description": "Wulgaryzmy, mowa nienawiści, treści atakujące inne osoby.",
        "severity_score": 3,
    },
    {
        "reason_code": "fake",
        "label_pl": "Fałszywa recenzja",
        "description": "Recenzja sprawia wrażenie nieprawdziwej, zmanipulowanej lub kupionej.",
        "severity_score": 2,
    },
    {
        "reason_code": "harassment",
        "label_pl": "Nękanie lub hejt",
        "description": "Treść skierowana przeciwko konkretnej osobie, prześladowanie.",
        "severity_score": 4,
    },
    {
        "reason_code": "illegal",
        "label_pl": "Treść niezgodna z prawem",
        "description": "Treści naruszające polskie prawo (np. groźby, oszczerstwa).",
        "severity_score": 5,
    },
    {
        "reason_code": "duplicate",
        "label_pl": "Duplikat",
        "description": "Recenzja jest kopią wcześniej dodanej treści.",
        "severity_score": 1,
    },
    {
        "reason_code": "off_topic",
        "label_pl": "Niezwiązane z miejscem",
        "description": "Treść nie dotyczy ocenianej restauracji ani dania.",
        "severity_score": 1,
    },
    {
        "reason_code": "inappropriate_photo",
        "label_pl": "Nieodpowiednie zdjęcie",
        "description": "Zdjęcie narusza regulamin (treści nieprzyzwoite, niezwiązane z jedzeniem).",
        "severity_score": 3,
    },
]

class ReportReasonsPhase(BasePhase):

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase0_report_reasons",
            display_name="Report Reasons",
            dependencies=[],
            required_tables=["report_reason_definitions"],
            cleanup_tables=["report_reason_definitions"],
            estimated_duration=5,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Phase 0: Seeding report reasons...")

        try:
            for r in REPORT_REASONS:
                context.db.execute_query(
                    """
                    INSERT INTO report_reason_definitions (reason_code, label_pl, description, severity_score)
                    VALUES (%s, %s, %s, %s)
                    ON CONFLICT (reason_code) DO NOTHING
                    """,
                    (r["reason_code"], r["label_pl"], r["description"], r["severity_score"]),
                )
            context.db.commit()

            duration = time.time() - start_time
            logger.info(f"[OK] Seeded {len(REPORT_REASONS)} report reasons in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"report_reason_definitions": len(REPORT_REASONS)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 0 (report reasons) failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
