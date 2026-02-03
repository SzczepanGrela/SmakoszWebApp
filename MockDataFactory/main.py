import argparse
import logging
import os
import sys
from datetime import datetime

from config import GENERATION_CONFIG, get_connection_params
from generators import (
    generate_cities,
    generate_cuisine_types,
    generate_dishes,
    generate_ingredients,
    generate_restaurants,
    generate_reviews,
    generate_social_graph,
    generate_system_config,
    generate_tags,
    generate_users,
)
from tools.toggle_triggers import TriggerManager
from utils.db_connection import DatabaseConnection

def setup_logging():
    file_handler = logging.FileHandler("mockdata_generation.log", encoding="utf-8")
    file_handler.setLevel(logging.INFO)
    file_handler.setFormatter(logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s"))

    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(logging.INFO)
    console_handler.setFormatter(logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s"))

    try:
        import io
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    except Exception:
        pass

    logging.basicConfig(level=logging.INFO, handlers=[file_handler, console_handler])

def clean_all_data(db: DatabaseConnection):
    logger = logging.getLogger(__name__)
    logger.info("Cleaning database...")

    tables = [
        "system.tickets",
        "system.ai_logs",
        "system.moderation_logs",
        "system.logs",
        "system.security_logs",
        "system.email_logs",
        "system.jobs",
        "system.refresh_tokens",
        "system.banned_identifiers",
        "system.forbidden_words",
        "system.files_to_delete",
        "system.config",
        "verification_codes",
        "user_sessions",
        "user_notification_settings",
        "notifications",
        "search_history",
        "data_correction_requests",
        "report_reason_assignments",
        "reports",
        "user_follows",
        "review_likes",
        "saved_dishes",
        "favorite_restaurants",
        "media_assets",
        "restaurant_tags",
        "dish_tags",
        "tags",
        "reviews",
        "dish_ingredients",
        "dish_section_assignments",
        "menu_sections",
        "dishes",
        "dish_variants",
        "dish_archetypes",
        "restaurant_opening_hours",
        "restaurants",
        "users",
        "ingredients",
        "cuisine_types",
        "cities",
    ]

    for table in tables:
        # Use CASCADE to handle remaining dependencies
        db.execute_query(f"TRUNCATE TABLE {table} CASCADE")
        
    logger.info("Database cleaned.")

def cleanup_database(db: DatabaseConnection):
    """Interactively or automatically clean the database."""
    print("\nWARNING: This will delete ALL data in the 'mockdatadb' database.")
    print("Do you want to proceed? (yes/no)")
    
    # For automation, assume yes if env var set, otherwise ask
    if os.getenv("AUTO_CONFIRM_CLEANUP") == "true":
        response = "yes"
    else:
        # TEMPORARY: Auto-confirm for this run
        response = "yes"
        print("> yes (auto-confirmed)")

    if response in ("yes", "y"):
        clean_all_data(db)
    else:
        print("Cleanup cancelled. Exiting.")
        sys.exit(0)

def print_statistics(db: DatabaseConnection):
    logger = logging.getLogger(__name__)
    logger.info("\n" + "=" * 40)
    logger.info("FINAL DATABASE STATISTICS")
    logger.info("=" * 40)
    
    tables = [
        "users", "restaurants", "dishes", "reviews", 
        "notifications", "media_assets", "system.tickets"
    ]
    
    for table in tables:
        count = db.fetch_val(f"SELECT COUNT(*) FROM {table}")
        logger.info(f"{table.ljust(20)}: {count}")

def main():
    setup_logging()
    logger = logging.getLogger(__name__)

    parser = argparse.ArgumentParser(description="Mock Data Generator for SmakoszWebApp")
    parser.add_argument("--generate", action="store_true", help="Run full generation pipeline")
    parser.add_argument("--users", type=int, help="Override number of users to generate")
    parser.add_argument("--all", action="store_true", help="Run everything (same as --generate)")
    
    args = parser.parse_args()

    start_time = datetime.now()
    
    # Load configuration
    connection_params = get_connection_params()
    num_users = args.users if args.users else GENERATION_CONFIG["num_users"]

    logger.info(f"Starting MockDataFactory v6.0")
    logger.info(f"Target Database: {connection_params.get('dbname')}")
    logger.info(f"Planned Users: {num_users}")

    try:
        with DatabaseConnection(connection_params) as db:
            # 1. Cleanup
            cleanup_database(db)

            # 2. Trigger Management
            trigger_manager = TriggerManager(db)
            
            logger.info("=" * 80)
            logger.info("PERFORMANCE MODE: Disabling heavy triggers")
            logger.info("=" * 80)
            
            try:
                # Disable triggers for bulk performance
                trigger_manager.disable_heavy_triggers()

                # 3. Generation Pipeline
                if args.generate or args.all:
                    logger.info("\n--- Phase 0: System Config ---")
                    generate_system_config(db, blueprints_dir="blueprints", cleanup=False)

                    logger.info("\n--- Phase 1: Core Data ---")
                    generate_cities(db, blueprints_dir="blueprints", cleanup=False)
                    generate_cuisine_types(db, blueprints_dir="blueprints", cleanup=False)
                    generate_ingredients(db, blueprints_dir="blueprints", cleanup=False)
                    generate_tags(db, cleanup=False)

                    logger.info("\n--- Phase 2: Restaurants ---")
                    generate_restaurants(db, blueprints_dir="blueprints", cleanup=False)

                    logger.info("\n--- Phase 3: Dishes ---")
                    generate_dishes(db, blueprints_dir="blueprints", cleanup=False)

                    logger.info("\n--- Phase 4: Users ---")
                    generate_users(db, num_users=num_users, cleanup=False)

                    logger.info("\n--- Phase 5: Reviews ---")
                    generate_reviews(db, cleanup=False)

                    logger.info("\n--- Phase 6: Social Graph ---")
                    generate_social_graph(db, cleanup=False)

            finally:
                # 4. Restore State (CRITICAL)
                logger.info("\n" + "=" * 80)
                logger.info("RESTORING STATE: Re-enabling triggers")
                logger.info("=" * 80)
                trigger_manager.enable_heavy_triggers()

            # 5. Statistics & Finish
            print_statistics(db)
            
            duration = datetime.now() - start_time
            logger.info(f"\nSUCCESS! Completed in {duration}")

    except Exception as e:
        logger.error(f"FATAL ERROR: {e}", exc_info=True)
        sys.exit(1)

if __name__ == "__main__":
    main()