import json
import logging
import time
from datetime import datetime, timezone
from pathlib import Path

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)

CATEGORY_MAP: dict[str, tuple[int, bool]] = {
    # section_name -> (category_enum_value, is_regex)
    "reserved": (1, False),        # ForbiddenWordCategory.Reserved
    "profanity_pl": (0, False),    # ForbiddenWordCategory.Profanity
    "profanity_en": (0, False),    # ForbiddenWordCategory.Profanity
    "offensive": (2, False),       # ForbiddenWordCategory.Offensive
    "spam_patterns": (0, True),    # ForbiddenWordCategory.Profanity, IsRegex
}

class ForbiddenWordsPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase0_forbidden_words",
            display_name="Forbidden Words",
            dependencies=[],
            required_tables=["system.forbidden_words"],
            cleanup_tables=["system.forbidden_words"],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Phase 0: Seeding Forbidden Words...")

        try:
            path = Path(self.blueprints_dir) / "forbidden_words.json"

            try:
                with open(path, encoding="utf-8") as f:
                    data = json.load(f)
                    sections = data.get("FORBIDDEN_WORDS", {})
            except FileNotFoundError:
                error_msg = f"Blueprint not found: {path}"
                logger.error(error_msg)
                return PhaseResult(
                    phase_id=self.metadata.phase_id,
                    status=PhaseStatus.FAILED,
                    duration_seconds=time.time() - start_time,
                    entities_generated={},
                    error=FileNotFoundError(error_msg),
                )

            insert_data = []
            now = datetime.now(timezone.utc)
            for section_name, (category, is_regex) in CATEGORY_MAP.items():
                words = sections.get(section_name, [])
                for word in words:
                    insert_data.append({
                        "word": word,
                        "category": category,
                        "is_regex": is_regex,
                        "created_at": now,
                    })

            if insert_data:
                context.db.insert_bulk("system.forbidden_words", insert_data)

            duration = time.time() - start_time
            logger.info(f"[OK] Seeded {len(insert_data)} forbidden words in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"forbidden_words": len(insert_data)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Phase 0 (forbidden words) failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
