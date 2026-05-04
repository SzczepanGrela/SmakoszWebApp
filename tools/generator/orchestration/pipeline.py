import logging
from dataclasses import dataclass
from datetime import datetime

from reporting.sync_counters import CounterSync

from .context import ExecutionContext
from .database_manager import DatabaseManager
from .phase import PhaseResult, PhaseStatus

logger = logging.getLogger(__name__)

@dataclass
class PipelineConfig:

    cleanup_before_run: bool = True
    continue_on_error: bool = False
    dry_run: bool = False
    selective_mode: bool = False

@dataclass
class PipelineResult:

    total_duration_seconds: float
    phase_results: list[PhaseResult]
    success: bool

    @property
    def succeeded_phases(self) -> list[str]:
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.COMPLETED]

    @property
    def failed_phases(self) -> list[str]:
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.FAILED]

    @property
    def skipped_phases(self) -> list[str]:
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.SKIPPED]

    def get_phase_result(self, phase_id: str) -> PhaseResult | None:
        for result in self.phase_results:
            if result.phase_id == phase_id:
                return result
        return None

class DataGenerationPipeline:

    def __init__(
        self,
        context: ExecutionContext,
        config: PipelineConfig,
    ):
        self.context = context
        self.config = config
        self.db_manager = DatabaseManager(context.db)

    def run(self, phase_ids: list[str] | None = None) -> PipelineResult:
        start_time = datetime.now()
        logger.info("\n" + "=" * 80)
        logger.info("MOCKDATAFACTORY PIPELINE - Starting")
        logger.info("=" * 80)

        if phase_ids is None:
            all_phases = self.context.phase_registry.get_all()
            phase_ids = [
                p.metadata.phase_id
                for p in sorted(
                    all_phases,
                    key=lambda p: int(p.metadata.phase_id.replace("phase", "").split("_")[0]),
                )
            ]
            logger.info(f"Running ALL phases: {len(phase_ids)} phases")
        else:
            logger.info(f"Running SELECTED phases: {phase_ids}")

        try:
            if self.config.selective_mode:
                sorted_phase_ids = self.context.phase_registry.sort_phases(phase_ids)
                logger.info(f"[Selective] Running: {sorted_phase_ids}")

                downstream = self.context.phase_registry.resolve_downstream(sorted_phase_ids)
                all_phases_to_clean = sorted_phase_ids + downstream
                logger.info(f"[Selective] Downstream phases (cleanup only): {downstream}")
            else:
                sorted_phase_ids = self.context.phase_registry.resolve_dependencies(phase_ids)
                all_phases_to_clean = None
            logger.info(f"Resolved execution order: {sorted_phase_ids}")
        except ValueError as e:
            logger.error(f"Dependency resolution failed: {e}")
            return PipelineResult(
                total_duration_seconds=0,
                phase_results=[],
                success=False,
            )

        if self.config.cleanup_before_run:
            logger.info("\n" + "=" * 80)
            logger.info("DATABASE CLEANUP")
            logger.info("=" * 80)
            try:
                if self.config.selective_mode:
                    cleanup_tables = self.context.phase_registry.get_cleanup_tables_for_phases(
                        all_phases_to_clean
                    )
                    self.db_manager.cleanup_selective(cleanup_tables)
                else:
                    self.db_manager.cleanup(auto_confirm=True)
            except Exception as e:
                logger.error(f"Database cleanup failed: {e}", exc_info=True)
                return PipelineResult(
                    total_duration_seconds=0,
                    phase_results=[],
                    success=False,
                )

        phase_results = []

        for idx, phase_id in enumerate(sorted_phase_ids, 1):
            logger.info(f"\n{'=' * 80}\nPhase {idx}/{len(sorted_phase_ids)}: {phase_id}\n{'=' * 80}")

            result = self._execute_phase(phase_id)
            phase_results.append(result)

            if result.status == PhaseStatus.FAILED:
                if not self.config.continue_on_error:
                    logger.error(f"Pipeline aborted due to failure in {phase_id}")
                    break
                else:
                    logger.warning(f"Phase {phase_id} failed but continuing (continue_on_error=True)")
            elif result.status == PhaseStatus.COMPLETED:
                self.context.mark_completed(phase_id)
                logger.info(f"[OK] Phase {phase_id} completed in {result.duration_seconds:.2f}s")
            elif result.status == PhaseStatus.SKIPPED:
                logger.info(f"[SKIP] Phase {phase_id} skipped")

        success = all(r.status == PhaseStatus.COMPLETED for r in phase_results)

        if not self.config.dry_run and success:
            logger.info("\n" + "=" * 80)
            logger.info("SYNCHRONIZING DENORMALIZED COUNTERS")
            logger.info("=" * 80)
            CounterSync(self.context.db).sync_all()

        if not self.config.dry_run:
            logger.info("\n" + "=" * 80)
            self.db_manager.print_statistics()
            logger.info("=" * 80)

        duration = datetime.now() - start_time

        logger.info("\n" + "=" * 80)
        logger.info("PIPELINE SUMMARY")
        logger.info("=" * 80)
        logger.info(f"Total duration: {duration}")
        logger.info(
            f"Phases completed: {len([r for r in phase_results if r.status == PhaseStatus.COMPLETED])}/{len(phase_results)}"
        )
        logger.info(f"Success: {success}")

        if not success:
            failed = [r.phase_id for r in phase_results if r.status == PhaseStatus.FAILED]
            logger.error(f"Failed phases: {', '.join(failed)}")

        return PipelineResult(
            total_duration_seconds=duration.total_seconds(),
            phase_results=phase_results,
            success=success,
        )

    def _execute_phase(self, phase_id: str) -> PhaseResult:
        phase = self.context.phase_registry.get(phase_id)

        if not phase:
            logger.error(f"Phase {phase_id} not found in registry")
            return PhaseResult(
                phase_id=phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=0,
                entities_generated={},
                error=ValueError(f"Phase {phase_id} not found"),
            )

        logger.info(f"Executing: {phase.metadata.display_name}")
        if phase.metadata.dependencies:
            logger.debug(f"Dependencies: {', '.join(phase.metadata.dependencies)}")

        phase_start = datetime.now()

        try:
            if not self.config.dry_run:
                logger.debug("Validating prerequisites...")
                phase.validate_prerequisites(self.context, selective=self.config.selective_mode)

            if self.config.dry_run:
                logger.info(f"[DRY RUN] Would execute {phase_id}")
                result = PhaseResult(
                    phase_id=phase_id,
                    status=PhaseStatus.SKIPPED,
                    duration_seconds=0,
                    entities_generated={},
                )
            else:
                result = phase.execute(self.context)

            duration = (datetime.now() - phase_start).total_seconds()

            if result.entities_generated:
                for entity_type, count in result.entities_generated.items():
                    logger.info(f"  Generated {count:,} {entity_type}")

            return result

        except Exception as e:
            duration = (datetime.now() - phase_start).total_seconds()
            logger.error(f"Phase {phase_id} failed after {duration:.2f}s: {e}", exc_info=True)

            return PhaseResult(
                phase_id=phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
