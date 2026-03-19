#!/usr/bin/env python3
"""
Run a single phase independently.

Usage:
    python scripts/run_phase.py phase2_restaurants
    python scripts/run_phase.py phase5_reviews --no-cleanup
    python scripts/run_phase.py phase3_dishes --verbose
"""

import argparse
import logging
import sys
from datetime import datetime
from pathlib import Path
from typing import Literal, cast

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent.parent))

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
from utils.db_connection import DatabaseConnection
from utils.logging_config import LoggingConfig

def setup_phase_registry() -> PhaseRegistry:
    """Setup and register all phases."""
    registry = PhaseRegistry()

    registry.register(SystemConfigPhase())
    registry.register(CitiesPhase())
    registry.register(CuisineTypesPhase())
    registry.register(HeroImagesPhase())
    registry.register(IngredientsPhase())
    registry.register(TagsPhase())
    registry.register(RestaurantsPhase())
    registry.register(DishesPhase())
    registry.register(UsersPhase())
    registry.register(ReviewsPhase())
    registry.register(SocialGraphPhase())

    return registry

def main():
    parser = argparse.ArgumentParser(
        description="Run a single phase independently",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Available phases:
  phase0_config        System configuration
  phase1_cities        Cities generation
  phase1_cuisines      Cuisine types generation
  phase1_hero          Hero images registration
  phase1_ingredients   Ingredients generation
  phase1_tags          Tags generation
  phase2_restaurants   Restaurants generation
  phase3_dishes        Dishes generation
  phase4_users         Users generation
  phase5_reviews       Reviews generation
  phase6_social        Social graph generation

Examples:
  %(prog)s phase3_dishes                  Run dishes phase
  %(prog)s phase5_reviews --no-cleanup    Run reviews without cleanup
  %(prog)s phase2_restaurants -v          Run with verbose logging
        """
    )

    parser.add_argument("phase_id", type=str, help="Phase ID to run (e.g., phase3_dishes)")
    parser.add_argument("--no-cleanup", action="store_true", help="Skip table cleanup")
    parser.add_argument("--keep-triggers", action="store_true", help="Don't disable triggers")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose logging")
    parser.add_argument("--debug", "-d", action="store_true", help="Debug logging")

    args = parser.parse_args()

    # Logging
    log_level = "DEBUG" if args.debug else ("INFO" if args.verbose else "WARNING")
    LoggingConfig.setup(level=cast(Literal["DEBUG", "INFO", "WARNING", "ERROR"], log_level))
    logger = logging.getLogger(__name__)

    # Validate phase ID
    valid_phases = [
        "phase0_config",
        "phase1_cities",
        "phase1_cuisines",
        "phase1_hero",
        "phase1_ingredients",
        "phase1_tags",
        "phase2_restaurants",
        "phase3_dishes",
        "phase4_users",
        "phase5_reviews",
        "phase6_social",
    ]

    if args.phase_id not in valid_phases:
        logger.error(f"Invalid phase ID: {args.phase_id}")
        logger.error(f"Valid phases: {', '.join(valid_phases)}")
        sys.exit(1)

    start_time = datetime.now()

    logger.info("=" * 80)
    logger.info(f"Running single phase: {args.phase_id}")
    logger.info("=" * 80)

    try:
        connection_params = get_connection_params()

        with DatabaseConnection(connection_params) as db:
            # Setup
            registry = setup_phase_registry()
            context = ExecutionContext(
                db=db,
                config=GENERATION_CONFIG,
                phase_registry=registry
            )

            pipeline_config = PipelineConfig(
                cleanup_before_run=not args.no_cleanup,
                disable_triggers=not args.keep_triggers,
                continue_on_error=False
            )

            # Execute single phase
            pipeline = DataGenerationPipeline(context, pipeline_config)
            result = pipeline.run(phase_ids=[args.phase_id])

            # Summary
            duration = datetime.now() - start_time

            logger.info("\n" + "=" * 80)
            if result.success:
                logger.info(f"✓ SUCCESS! Completed in {duration}")

                phase_result = result.phase_results[0]
                logger.info(f"Phase: {phase_result.phase_id}")
                logger.info(f"Status: {phase_result.status.value}")
                logger.info(f"Duration: {phase_result.duration_seconds:.2f}s")
                logger.info(f"Entities: {phase_result.entities_generated}")
            else:
                logger.error(f"✗ FAILED after {duration}")
                if result.phase_results:
                    phase_result = result.phase_results[0]
                    if phase_result.error:
                        logger.error(f"Error: {phase_result.error}")
                sys.exit(1)

            logger.info("=" * 80)

    except KeyboardInterrupt:
        logger.warning("\n\nInterrupted by user")
        sys.exit(130)
    except Exception as e:
        logger.error(f"\n\nFATAL ERROR: {e}", exc_info=True)
        sys.exit(1)

if __name__ == "__main__":
    main()
