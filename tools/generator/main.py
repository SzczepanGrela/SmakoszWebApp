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
    HeroImagesPhase,
    IngredientsPhase,
    RestaurantsPhase,
    ReviewsPhase,
    SocialGraphPhase,
    SystemConfigPhase,
    TagsPhase,
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
    """
    Setup and register all generation phases.

    Returns PhaseRegistry with all 10 phases registered.
    This is the single source of truth for phase registration.
    """
    registry = PhaseRegistry()

    # Phase 0: System Configuration
    registry.register(SystemConfigPhase(blueprints_dir=blueprints_dir))

    # Phase 1: Core Definitions (parallel - no dependencies)
    registry.register(CitiesPhase(blueprints_dir=blueprints_dir))
    registry.register(CuisineTypesPhase(blueprints_dir=blueprints_dir))
    registry.register(HeroImagesPhase(blueprints_dir=blueprints_dir))
    registry.register(IngredientsPhase(blueprints_dir=blueprints_dir))
    registry.register(TagsPhase())

    # Phase 2: Restaurants (depends on Cities)
    registry.register(RestaurantsPhase(blueprints_dir=blueprints_dir))

    # Phase 3: Dishes (depends on Ingredients + Restaurants)
    registry.register(DishesPhase(blueprints_dir=blueprints_dir))

    # Phase 4: Users (depends on Cities)
    registry.register(UsersPhase(blueprints_dir=blueprints_dir))

    # Phase 5: Reviews (depends on Users + Restaurants + Dishes)
    registry.register(ReviewsPhase(blueprints_dir=blueprints_dir))

    # Phase 6: Social Graph (depends on Users)
    registry.register(SocialGraphPhase(blueprints_dir=blueprints_dir))

    return registry

def print_statistics(db: DatabaseConnection):
    """Print final database statistics."""
    logger = logging.getLogger(__name__)
    logger.info("\n" + "=" * 80)
    logger.info("FINAL DATABASE STATISTICS")
    logger.info("=" * 80)

    tables = [
        "system.config",
        "cities",
        "cuisine_types",
        "ingredients",
        "tags",
        "restaurants",
        "dishes",
        "users",
        "reviews",
        "user_follows",
        "review_likes",
        "notifications",
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
  %(prog)s --generate              Run full pipeline (Phase 0-6)
  %(prog)s --phase phase3_dishes   Run single phase
  %(prog)s --phases 0-3            Run phase range (0 through 3)
  %(prog)s --no-cleanup            Skip database cleanup
  %(prog)s -v --generate           Verbose logging
  %(prog)s --stats                 Dataset statistics only (no generation)
  %(prog)s --generate --stats      Generate + print NCF statistics
        """,
    )

    # Execution mode
    parser.add_argument("--generate", action="store_true", help="Run full generation pipeline")
    parser.add_argument("--phase", type=str, help="Run single phase (e.g., phase2_restaurants)")
    parser.add_argument("--phases", type=str, help="Run phase range (e.g., 0-3)")

    # Options
    parser.add_argument("--users", type=int, help="Override number of users to generate")
    parser.add_argument("--no-cleanup", action="store_true", help="Skip database cleanup")
    parser.add_argument("--stats", action="store_true", help="Print dataset statistics (NCF-oriented) after generation")

    # Logging
    parser.add_argument("--quiet", "-q", action="store_true", help="Only show warnings and errors")
    parser.add_argument("--verbose", "-v", action="store_true", help="Show detailed INFO logs")
    parser.add_argument("--debug", "-d", action="store_true", help="Show DEBUG logs (most verbose)")

    args = parser.parse_args()

    # Logging setup
    if args.debug:
        log_level = "DEBUG"
    elif args.verbose:
        log_level = "INFO"
    else:
        log_level = "INFO"

    LoggingConfig.setup(level=cast(Literal["DEBUG", "INFO", "WARNING", "ERROR"], log_level), quiet=args.quiet)
    logger = logging.getLogger(__name__)

    start_time = datetime.now()

    # Configuration
    connection_params = get_connection_params()
    config = dict(GENERATION_CONFIG)

    # Override config from CLI args
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
            # Setup orchestration
            registry = setup_phase_registry(blueprints_dir="blueprints")
            context = ExecutionContext(db=db, config=config, phase_registry=registry)

            pipeline_config = PipelineConfig(cleanup_before_run=not args.no_cleanup, continue_on_error=False)

            # Determine which phases to run
            phase_ids = None  # None = run all

            if args.phase:
                # Single phase
                phase_ids = [args.phase]
                logger.info(f"Running single phase: {args.phase}")
            elif args.phases:
                # Phase range (e.g., "0-3")
                try:
                    start_phase, end_phase = args.phases.split("-")
                    start_num = int(start_phase)
                    end_num = int(end_phase)

                    # Generate phase IDs for range
                    phase_ids = []
                    if start_num == 0:
                        phase_ids.append("phase0_config")
                        start_num = 1

                    # Map phase numbers to IDs (simplified)
                    phase_map = {
                        1: ["phase1_cities", "phase1_cuisines", "phase1_ingredients", "phase1_tags"],
                        2: ["phase2_restaurants"],
                        3: ["phase3_dishes"],
                        4: ["phase4_users"],
                        5: ["phase5_reviews"],
                        6: ["phase6_social"],
                    }

                    for phase_num in range(start_num, end_num + 1):
                        if phase_num in phase_map:
                            phase_ids.extend(phase_map[phase_num])

                    logger.info(f"Running phase range {args.phases}: {len(phase_ids)} phases")
                except ValueError:
                    logger.error(f"Invalid phase range format: {args.phases}. Use format '0-3'")
                    sys.exit(1)
            elif args.generate:
                # Full pipeline
                logger.info("Running full generation pipeline (Phase 0-6)")
            elif args.stats:
                # Stats-only mode (no generation)
                logger.info("Running dataset statistics on existing data...")
                ds = DatasetStatistics(db)
                ds.collect_all()
                ds.print_report()
                ds.save_json()
                return
            else:
                # No action specified
                parser.print_help()
                sys.exit(0)

            # Execute pipeline
            pipeline = DataGenerationPipeline(context, pipeline_config)
            result = pipeline.run(phase_ids=phase_ids)

            # Print statistics
            print_statistics(db)

            # Dataset statistics (NCF-oriented)
            if args.stats:
                ds = DatasetStatistics(db)
                ds.collect_all()
                ds.print_report()
                ds.save_json()

            # Final summary
            duration = datetime.now() - start_time

            logger.info("\n" + "=" * 80)
            if result.success:
                logger.info(f"✓ SUCCESS! Completed in {duration}")
                logger.info(f"Phases completed: {len(result.phase_results)}")

                # Show per-phase timings
                for phase_result in result.phase_results:
                    logger.info(
                        f"  - {phase_result.phase_id}: "
                        f"{phase_result.duration_seconds:.2f}s "
                        f"({phase_result.status.value})"
                    )
            else:
                logger.error(f"✗ FAILED after {duration}")
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
