import argparse
import logging
import sys
from datetime import datetime
from typing import Literal, cast

from config import GENERATION_CONFIG, get_connection_params
from generators import (
    CitiesPhase,
    CuisineTypesPhase,
    DishesPhase,
    ForbiddenWordsPhase,
    HeroImagesPhase,
    IngredientsPhase,
    RejectionReasonsPhase,
    RestaurantsPhase,
    ReviewsPhase,
    SocialGraphPhase,
    SystemConfigPhase,
    SystemLogsPhase,
    TagsPhase,
    TicketsPhase,
    UsersPhase,
)
from orchestration import (
    DataGenerationPipeline,
    ExecutionContext,
    PhaseRegistry,
    PipelineConfig,
)
from reporting.stats import DatasetStatistics
from utils.db_connection import DatabaseConnection
from utils.logging_config import LoggingConfig

def setup_phase_registry(blueprints_dir: str = "blueprints") -> PhaseRegistry:
    registry = PhaseRegistry()

    registry.register(SystemConfigPhase(blueprints_dir=blueprints_dir))
    registry.register(ForbiddenWordsPhase(blueprints_dir=blueprints_dir))
    registry.register(RejectionReasonsPhase())

    registry.register(CitiesPhase(blueprints_dir=blueprints_dir))
    registry.register(CuisineTypesPhase(blueprints_dir=blueprints_dir))
    registry.register(HeroImagesPhase(blueprints_dir=blueprints_dir))
    registry.register(IngredientsPhase(blueprints_dir=blueprints_dir))
    registry.register(TagsPhase())

    registry.register(RestaurantsPhase(blueprints_dir=blueprints_dir))

    registry.register(DishesPhase(blueprints_dir=blueprints_dir))

    registry.register(UsersPhase(blueprints_dir=blueprints_dir))

    registry.register(ReviewsPhase(blueprints_dir=blueprints_dir))

    registry.register(SocialGraphPhase(blueprints_dir=blueprints_dir))

    registry.register(TicketsPhase())

    registry.register(SystemLogsPhase(blueprints_dir=blueprints_dir))

    return registry

def print_statistics(db: DatabaseConnection):
    logger = logging.getLogger(__name__)
    logger.info("\n" + "=" * 80)
    logger.info("FINAL DATABASE STATISTICS")
    logger.info("=" * 80)

    tables = [
        "system.config",
        "system.forbidden_words",
        "rejection_reasons",
        "cities",
        "cuisine_types",
        "ingredients",
        "tags",
        "restaurants",
        "dishes",
        "users",
        "reviews",
        "system.moderation_results",
        "user_follows",
        "review_likes",
        "notifications",
        "restaurant_edit_requests",
        "system.tickets",
    ]

    for table in tables:
        try:
            count = db.fetch_val(f"SELECT COUNT(*) FROM {table}")
            logger.info(f"{table.ljust(25)}: {count:,}")
        except Exception as e:
            logger.warning(f"{table.ljust(25)}: ERROR ({e})")

    logger.info("=" * 80)

def main():
    parser = argparse.ArgumentParser(
        description="Mock Data Generator for SmakoszWebApp",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --generate                          Run full pipeline (Phase 0-7)
  %(prog)s --phase phase7_tickets              Run ONLY phase7 (selective cleanup)
  %(prog)s --phase phase4_users --phase phase5  Multiple phases (selective)
  %(prog)s --from phase5_reviews               Run phase5 and all downstream
  %(prog)s --phases 0-3                        Run phase range (0 through 3)
  %(prog)s --phase phase7 --no-cleanup         Run phase7 without cleanup
  %(prog)s -v --generate                       Verbose logging
  %(prog)s --stats                             Dataset statistics only
        """,
    )

    parser.add_argument("--generate", action="store_true", help="Run full generation pipeline")
    parser.add_argument("--phase", type=str, action="append", help="Run specific phase selectively (repeatable)")
    parser.add_argument("--from", type=str, dest="from_phase", help="Run from phase X onwards (selective)")
    parser.add_argument("--phases", type=str, help="Run phase range (e.g., 0-3)")

    parser.add_argument("--users", type=int, help="Override number of users to generate")
    parser.add_argument("--no-cleanup", action="store_true", help="Skip database cleanup")
    parser.add_argument("--stats", action="store_true", help="Print dataset statistics (NCF-oriented) after generation")

    parser.add_argument("--quiet", "-q", action="store_true", help="Only show warnings and errors")
    parser.add_argument("--verbose", "-v", action="store_true", help="Show detailed INFO logs")
    parser.add_argument("--debug", "-d", action="store_true", help="Show DEBUG logs (most verbose)")

    args = parser.parse_args()

    exclusive_count = sum([
        args.generate,
        args.phase is not None,
        args.from_phase is not None,
        args.phases is not None,
    ])
    if exclusive_count > 1:
        parser.error("Flags --generate, --phase, --from, and --phases are mutually exclusive.")

    if args.debug:
        log_level = "DEBUG"
    elif args.verbose:
        log_level = "INFO"
    else:
        log_level = "INFO"

    LoggingConfig.setup(level=cast(Literal["DEBUG", "INFO", "WARNING", "ERROR"], log_level), quiet=args.quiet)
    logger = logging.getLogger(__name__)

    start_time = datetime.now()

    connection_params = get_connection_params()
    config = dict(GENERATION_CONFIG)

    if args.users:
        config["num_users"] = args.users

    logger.info("=" * 80)
    logger.info("MockDataFactory v7.0 - New Architecture")
    logger.info("=" * 80)
    logger.info(f"Target Database: {connection_params.get('dbname')}")
    logger.info(f"Planned Users: {config.get('num_users', 'N/A'):,}")
    logger.info(f"Cleanup: {'Disabled' if args.no_cleanup else 'Enabled'}")
    logger.debug(f"Connection: {connection_params.get('host')}:{connection_params.get('port')}")

    try:
        with DatabaseConnection(connection_params) as db:
            registry = setup_phase_registry(blueprints_dir="blueprints")
            context = ExecutionContext(db=db, config=config, phase_registry=registry)

            phase_ids = None
            is_selective = False

            if args.phase:
                phase_ids = args.phase
                is_selective = True
                logger.info(f"[Selective] Running phases: {phase_ids}")
            elif args.from_phase:
                downstream = registry.resolve_downstream([args.from_phase])
                phase_ids = registry.sort_phases([args.from_phase] + downstream)
                is_selective = True
                logger.info(f"[Selective] Running from {args.from_phase}: {phase_ids}")
            elif args.phases:
                try:
                    start_phase, end_phase = args.phases.split("-")
                    start_num = int(start_phase)
                    end_num = int(end_phase)

                    phase_ids = []
                    if start_num == 0:
                        phase_ids.append("phase0_config")
                        phase_ids.append("phase0_forbidden_words")
                        phase_ids.append("phase0_rejection_reasons")
                        start_num = 1

                    phase_map = {
                        1: ["phase1_cities", "phase1_cuisines", "phase1_ingredients", "phase1_tags"],
                        2: ["phase2_restaurants"],
                        3: ["phase3_dishes"],
                        4: ["phase4_users"],
                        5: ["phase5_reviews"],
                        6: ["phase6_social"],
                        7: ["phase7_tickets"],
                        8: ["phase8_logs"],
                    }

                    for phase_num in range(start_num, end_num + 1):
                        if phase_num in phase_map:
                            phase_ids.extend(phase_map[phase_num])

                    is_selective = True
                    logger.info(f"[Selective] Running phase range {args.phases}: {len(phase_ids)} phases")
                except ValueError:
                    logger.error(f"Invalid phase range format: {args.phases}. Use format '0-3'")
                    sys.exit(1)
            elif args.generate:
                logger.info("Running full generation pipeline (Phase 0-7)")
            elif args.stats:
                logger.info("Running dataset statistics on existing data...")
                ds = DatasetStatistics(db)
                ds.collect_all()
                ds.print_report()
                ds.save_json()
                return
            else:
                parser.print_help()
                sys.exit(0)

            pipeline_config = PipelineConfig(
                cleanup_before_run=not args.no_cleanup,
                continue_on_error=False,
                selective_mode=is_selective,
            )

            pipeline = DataGenerationPipeline(context, pipeline_config)
            result = pipeline.run(phase_ids=phase_ids)

            print_statistics(db)

            if args.stats:
                ds = DatasetStatistics(db)
                ds.collect_all()
                ds.print_report()
                ds.save_json()

            duration = datetime.now() - start_time

            logger.info("\n" + "=" * 80)
            if result.success:
                logger.info(f"[OK] SUCCESS! Completed in {duration}")
                logger.info(f"Phases completed: {len(result.phase_results)}")

                for phase_result in result.phase_results:
                    logger.info(
                        f"  - {phase_result.phase_id}: "
                        f"{phase_result.duration_seconds:.2f}s "
                        f"({phase_result.status.value})"
                    )
            else:
                logger.error(f"[FAIL] FAILED after {duration}")
                logger.error(f"Failed phases: {result.failed_phases}")
                for phase_result in result.phase_results:
                    if phase_result.error:
                        logger.error(f"  - {phase_result.phase_id}: {phase_result.error}")
                sys.exit(1)

            logger.info("=" * 80)

    except KeyboardInterrupt:
        logger.warning("\n\nInterrupted by user. Exiting...")
        sys.exit(130)
    except Exception as e:
        logger.error(f"\n\nFATAL ERROR: {e}", exc_info=True)
        sys.exit(1)

if __name__ == "__main__":
    main()
