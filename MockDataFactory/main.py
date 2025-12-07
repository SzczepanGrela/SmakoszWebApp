"""
Main Orchestrator - Punkt wejścia dla MockDataFactory
Wykonuje wszystkie 5 faz generacji danych
"""

import logging
import sys
import argparse
from datetime import datetime

from config import get_connection_params, GENERATION_CONFIG
from utils.db_connection import DatabaseConnection
from generators import (
    generate_cities,
    generate_ingredients,
    generate_tags,
    generate_ingredient_restrictions,
    generate_restaurants,
    generate_dishes,
    generate_users,
    generate_user_variant_preferences,
    generate_reviews,
    generate_social_graph
)

# NOTE: update_last_login is now handled automatically in Phase 5 (generate_reviews)
# from update_last_login import update_last_login_for_users

def setup_logging():
    """Konfiguracja logowania"""
    # Configure UTF-8 encoding for file handler
    file_handler = logging.FileHandler('mockdata_generation.log', encoding='utf-8')
    file_handler.setLevel(logging.INFO)
    file_handler.setFormatter(logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s'))

    # Configure UTF-8 encoding for console handler (Windows fix)
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(logging.INFO)
    console_handler.setFormatter(logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s'))

    # For Windows console: try to reconfigure stdout to UTF-8
    try:
        import io
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    except Exception:
        pass  # If reconfiguration fails, continue with default

    logging.basicConfig(
        level=logging.INFO,
        handlers=[file_handler, console_handler]
    )

def cleanup_database(db: DatabaseConnection):
    """
    Czyści wszystkie dane z bazy danych przed generacją.
    Pyta użytkownika o potwierdzenie.

    Args:
        db: Połączenie z bazą danych
    """
    logger = logging.getLogger(__name__)

    logger.warning("=" * 70)
    logger.warning("WARNING: DATABASE CLEANUP")
    logger.warning("=" * 70)
    logger.warning("This operation will DELETE ALL data from the following tables:")
    logger.warning("  - cities, ingredients, tags")
    logger.warning("  - restaurants, dishes")
    logger.warning("  - users, reviews, photos")
    logger.warning("  - All related tables")
    logger.warning("=" * 70)

    # Flush stdout to ensure prompt appears before input
    sys.stdout.flush()
    response = input("\nAre you sure you want to delete all records? (yes/no): ").strip().lower()

    if response not in ['yes', 'y', 'tak', 't']:
        logger.info("Cancelled database cleanup.")
        logger.info("To run without cleanup, use: python main.py --skip-cleanup")
        sys.exit(0)

    logger.info("Starting database cleanup...")

    # Lista tabel w kolejności zależności (od najbardziej zależnych do podstawowych)
    # TRUNCATE CASCADE automatycznie obsługuje foreign keys, ale zachowujemy kolejność dla przejrzystości
    tables = [
        'auth_tokens',
        'security_logs',
        'email_logs',
        'search_history',
        'data_correction_requests',
        'notifications',
        'user_follows',
        'review_likes',
        'restaurant_opening_hours',
        'reports',
        'pending_comments',
        'pending_user_photos',
        'user_photos',
        'saved_dishes',
        'user_variant_preferences',
        'reviews',
        'users',
        'photos',
        'restaurant_tags',
        'dish_tags',
        'dish_ingredients_link',
        'dishes',
        'restaurants',
        'ingredient_restrictions',
        'tags',
        'ingredients',
        'cities'
    ]

    try:
        # TRUNCATE RESTART IDENTITY CASCADE: usuwa dane + resetuje sekwencje + usuwa powiązane
        for table in tables:
            db.execute_query(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")
            # logger.info(f"  Cleared: {table}") # Reduced spam

        db.commit()
        logger.info("Database cleaned successfully!")

    except Exception as e:
        logger.error(f"Error during database cleanup: {e}")
        db.rollback()
        raise

def print_statistics(db: DatabaseConnection):
    """Wyświetla statystyki wygenerowanych danych"""
    logger = logging.getLogger(__name__)

    logger.info("\n" + "=" * 60)
    logger.info("=> STATYSTYKI WYGENEROWANYCH DANYCH")
    logger.info("=" * 60)

    tables = [
        "cities", "ingredients", "tags", "restaurants",
        "dishes", "users", "user_variant_preferences", "reviews", "photos"
    ]

    for table in tables:
        try:
            count = db.fetch_one(f"SELECT COUNT(*) FROM {table}")[0]
            logger.info(f"  {table}: {count:,}")
        except Exception as e:
            logger.error(f"  {table}: Błąd - {e}")

    # Oblicz metryki CF
    try:
        num_users = db.fetch_one("SELECT COUNT(*) FROM users")[0]
        num_dishes = db.fetch_one("SELECT COUNT(*) FROM dishes")[0]
        num_reviews = db.fetch_one("SELECT COUNT(*) FROM reviews")[0]

        if num_users > 0 and num_dishes > 0:
            sparsity = (1 - (num_reviews / (num_users * num_dishes))) * 100
            logger.info("\n" + "-" * 60)
            logger.info("=> METRYKI COLLABORATIVE FILTERING")
            logger.info("-" * 60)
            logger.info(f"  Sparsity: {sparsity:.3f}%")
            logger.info(f"  Średnia recenzji/użytkownik: {num_reviews / num_users:.1f}")
            logger.info(f"  Średnia recenzji/danie: {num_reviews / num_dishes:.1f}")

    except Exception as e:
        logger.error(f"  Błąd obliczania metryk: {e}")

    logger.info("=" * 60 + "\n")

def main():
    """Główna funkcja orkiestratora"""
    # Parse command-line arguments
    parser = argparse.ArgumentParser(description='MockDataFactory - Generator danych testowych dla systemu rekomendacji')
    parser.add_argument('--skip-cleanup', action='store_true',
                        help='Pomija czyszczenie bazy danych przed generacją')
    args = parser.parse_args()

    setup_logging()
    logger = logging.getLogger(__name__)

    start_time = datetime.now()

    logger.info("=" * 60)
    logger.info("=> MOCKDATAFACTORY - START")
    logger.info("=" * 60)
    logger.info(f"Start: {start_time.strftime('%Y-%m-%d %H:%M:%S')}")
    logger.info("")

    # Wczytaj konfigurację
    connection_params = get_connection_params()
    num_users = GENERATION_CONFIG['num_users']

    logger.info("=> KONFIGURACJA:")
    logger.info(f"  Użytkownicy: {num_users:,}")
    logger.info(f"  Restauracje: ~{GENERATION_CONFIG['num_restaurants']:,}")
    logger.info(f"  Dania: ~{GENERATION_CONFIG['num_dishes']:,}")
    logger.info(f"  Oczekiwane recenzje: ~{num_users * GENERATION_CONFIG['avg_reviews_per_user']:,}")
    logger.info("")

    try:
        # Połącz z bazą danych
        with DatabaseConnection(connection_params) as db:

            # ========================================
            # CLEANUP: Wyczyść bazę przed generacją (jeśli nie pominięto)
            # ========================================
            if not args.skip_cleanup:
                cleanup_database(db)
            else:
                logger.info("Skipped database cleanup (--skip-cleanup flag)")
                logger.info("")

            # ========================================
            # PHASE 1: Core (miasta, składniki, tagi)
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 1: Generowanie danych podstawowych")
            logger.info("=" * 60)

            generate_cities(db, blueprints_dir="blueprints")
            generate_ingredients(db, blueprints_dir="blueprints")
            generate_tags(db)
            generate_ingredient_restrictions(db)

            logger.info(" PHASE 1 zakończona")
            logger.info("")

            # ========================================
            # PHASE 2: Restaurants
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 2: Generowanie restauracji")
            logger.info("=" * 60)

            generate_restaurants(db, blueprints_dir="blueprints")

            logger.info(" PHASE 2 zakończona")
            logger.info("")

            # ========================================
            # PHASE 3: Dishes
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 3: Generowanie dań")
            logger.info("=" * 60)

            generate_dishes(db, blueprints_dir="blueprints")

            logger.info(" PHASE 3 zakończona")
            logger.info("")

            # ========================================
            # PHASE 4: Users
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 4: Generowanie użytkowników")
            logger.info("=" * 60)

            generate_users(db, num_users=num_users)

            logger.info(" PHASE 4 zakończona")
            logger.info("")

            # ========================================
            # PHASE 4b: User-Variant Preferences Materialization
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 4b: Pre-calculating user preferences")
            logger.info("=" * 60)

            generate_user_variant_preferences(db)

            logger.info(" PHASE 4b zakończona")
            logger.info("")

            # ========================================
            # PHASE 5: Reviews (NAJDŁUŻSZE!)
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 5: Generowanie recenzji (to zajmie ~10-15 minut)")
            logger.info("=" * 60)

            generate_reviews(db)

            logger.info(" PHASE 5 zakończona")
            logger.info("")

            # ========================================
            # PHASE 6: Social Graph (Likes, Follows, Notifications)
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 6: Generowanie grafu społecznościowego")
            logger.info("=" * 60)

            generate_social_graph(db)

            logger.info(" PHASE 6 zakończona")
            logger.info("")

            # ========================================
            # STATYSTYKI
            # ========================================
            print_statistics(db)

            # Oblicz czas trwania
            end_time = datetime.now()
            duration = end_time - start_time

            logger.info("=" * 60)
            logger.info(" MOCKDATAFACTORY - ZAKOŃCZONE POMYŚLNIE")
            logger.info("=" * 60)
            logger.info(f"Koniec: {end_time.strftime('%Y-%m-%d %H:%M:%S')}")
            logger.info(f"Czas trwania: {duration}")
            logger.info("=" * 60)

    except Exception as e:
        logger.error("=" * 60)
        logger.error(" BŁĄD KRYTYCZNY")
        logger.error("=" * 60)
        logger.error(f"Błąd: {e}", exc_info=True)
        sys.exit(1)

if __name__ == "__main__":
    main()
