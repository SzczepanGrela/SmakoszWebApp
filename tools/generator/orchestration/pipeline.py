"""
Data Generation Pipeline Orchestrator

Main orchestration logic for MockDataFactory:
- Phase execution coordination
- Error handling and recovery
- Progress reporting
"""

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
    """Configuration for pipeline execution."""

    cleanup_before_run: bool = True
    continue_on_error: bool = False
    dry_run: bool = False

@dataclass
class PipelineResult:
    """Result of entire pipeline execution."""

    total_duration_seconds: float
    phase_results: list[PhaseResult]
    success: bool

    @property
    def succeeded_phases(self) -> list[str]:
        """Get list of successfully completed phase IDs."""
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.COMPLETED]

    @property
    def failed_phases(self) -> list[str]:
        """Get list of failed phase IDs."""
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.FAILED]

    @property
    def skipped_phases(self) -> list[str]:
        """Get list of skipped phase IDs."""
        return [r.phase_id for r in self.phase_results if r.status == PhaseStatus.SKIPPED]

    def get_phase_result(self, phase_id: str) -> PhaseResult | None:
        """Get result for specific phase."""
        for result in self.phase_results:
            if result.phase_id == phase_id:
                return result
        return None

class DataGenerationPipeline:
    """
    Main orchestrator for data generation pipeline.

    Responsibilities (SOLID SRP):
    - Phase dependency resolution
    - Error handling and rollback
    - Progress reporting

    Does NOT handle:
    - Data generation logic (delegated to phases)
    - Database cleanup (delegated to DatabaseManager)
    - Phase registration (handled by PhaseRegistry)
    """

    def __init__(
        self,
        context: ExecutionContext,
        config: PipelineConfig,
    ):
        """
        Initialize pipeline.

        Args:
            context: Execution context with DB, config, registry
            config: Pipeline configuration
        """
        self.context = context
        self.config = config
        self.db_manager = DatabaseManager(context.db, strategy="query_based")

    def run(self, phase_ids: list[str] | None = None) -> PipelineResult:
        """
        Run the data generation pipeline.

        Args:
            phase_ids: List of phase IDs to run. If None, runs all phases.

        Returns:
            PipelineResult with execution summary

        Example:
            >>> pipeline = DataGenerationPipeline(context, config)
            >>> result = pipeline.run(["phase2_restaurants"])
            >>> print(f"Success: {result.success}")
        """
        start_time = datetime.now()
        logger.info("\n" + "=" * 80)
        logger.info("MOCKDATAFACTORY PIPELINE - Starting")
        logger.info("=" * 80)

        # 1. Determine phases to run
        if phase_ids is None:
            # Run all phases in order
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

        # 2. Resolve dependencies (topological sort)
        try:
            sorted_phase_ids = self.context.phase_registry.resolve_dependencies(phase_ids)
            logger.info(f"Resolved execution order: {sorted_phase_ids}")
        except ValueError as e:
            logger.error(f"Dependency resolution failed: {e}")
            return PipelineResult(
                total_duration_seconds=0,
                phase_results=[],
                success=False,
            )

        # 3. Database cleanup
        if self.config.cleanup_before_run:
            logger.info("\n" + "=" * 80)
            logger.info("DATABASE CLEANUP")
            logger.info("=" * 80)
            try:
                self.db_manager.cleanup(auto_confirm=True)
            except Exception as e:
                logger.error(f"Database cleanup failed: {e}", exc_info=True)
                return PipelineResult(
                    total_duration_seconds=0,
                    phase_results=[],
                    success=False,
                )

        phase_results = []

        # 4. Disable triggers for bulk insert performance
        #    session_replication_role = 'replica' bypasses all user-defined triggers and FK checks.
        #    Counters are synced in bulk after all phases (step 6).
        if not self.config.dry_run:
            logger.info("Disabling triggers for bulk insert (session_replication_role = 'replica')")
            self.context.db.execute_query("SET session_replication_role = 'replica';")

        try:
            # 5. Execute phases
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
                    logger.info(f"✓ Phase {phase_id} completed in {result.duration_seconds:.2f}s")
                elif result.status == PhaseStatus.SKIPPED:
                    logger.info(f"⊘ Phase {phase_id} skipped")
        finally:
            # ALWAYS restore triggers (even if a phase fails)
            if not self.config.dry_run:
                logger.info("Re-enabling triggers (session_replication_role = 'origin')")
                self.context.db.execute_query("SET session_replication_role = 'origin';")

        # 6. Check success
        success = all(r.status == PhaseStatus.COMPLETED for r in phase_results)

        # 7. Synchronize denormalized counters
        if not self.config.dry_run and success:
            logger.info("\n" + "=" * 80)
            logger.info("SYNCHRONIZING DENORMALIZED COUNTERS")
            logger.info("=" * 80)
            CounterSync(self.context.db).sync_all()

        # 8. Final statistics
        if not self.config.dry_run:
            logger.info("\n" + "=" * 80)
            self.db_manager.print_statistics()
            logger.info("=" * 80)

        # 9. Summary
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
        """
        Execute a single phase with error handling.

        Args:
            phase_id: ID of phase to execute

        Returns:
            PhaseResult with status and statistics
        """
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
            # Validate prerequisites
            if not self.config.dry_run:
                logger.debug("Validating prerequisites...")
                phase.validate_prerequisites(self.context)

            # Execute
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

            # Log entity counts
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
