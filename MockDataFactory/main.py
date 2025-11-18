"""
Main Orchestrator - Punkt wejścia dla MockDataFactory
Wykonuje wszystkie 5 faz generacji danych
"""

import logging
import sys
from datetime import datetime

from config import get_connection_string, GENERATION_CONFIG
from utils.db_connection import DatabaseConnection
from generators import (
    generate_cities,
    generate_ingredients,
    generate_tags,
    generate_ingredient_restrictions,
    generate_restaurants,
    generate_dishes,
    generate_users,
    generate_reviews
)


def setup_logging():
    """Konfiguracja logowania"""
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.FileHandler('mockdata_generation.log'),
            logging.StreamHandler(sys.stdout)
        ]
    )


def print_statistics(db: DatabaseConnection):
    """Wyświetla statystyki wygenerowanych danych"""
    logger = logging.getLogger(__name__)

    logger.info("\n" + "=" * 60)
    logger.info("=> STATYSTYKI WYGENEROWANYCH DANYCH")
    logger.info("=" * 60)

    tables = [
        "Cities", "Ingredients", "Tags", "Restaurants",
        "Dishes", "Users", "Reviews", "Photos"
    ]

    for table in tables:
        try:
            count = db.fetch_one(f"SELECT COUNT(*) FROM {table}")[0]
            logger.info(f"  {table}: {count:,}")
        except Exception as e:
            logger.error(f"  {table}: Błąd - {e}")

    # Oblicz metryki CF
    try:
        num_users = db.fetch_one("SELECT COUNT(*) FROM Users")[0]
        num_dishes = db.fetch_one("SELECT COUNT(*) FROM Dishes")[0]
        num_reviews = db.fetch_one("SELECT COUNT(*) FROM Reviews")[0]

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
    setup_logging()
    logger = logging.getLogger(__name__)

    start_time = datetime.now()

    logger.info("=" * 60)
    logger.info("=> MOCKDATAFACTORY - START")
    logger.info("=" * 60)
    logger.info(f"Start: {start_time.strftime('%Y-%m-%d %H:%M:%S')}")
    logger.info("")

    # Wczytaj konfigurację
    connection_string = get_connection_string()
    num_users = GENERATION_CONFIG['num_users']

    logger.info("=> KONFIGURACJA:")
    logger.info(f"  Użytkownicy: {num_users:,}")
    logger.info(f"  Restauracje: ~{GENERATION_CONFIG['num_restaurants']:,}")
    logger.info(f"  Dania: ~{GENERATION_CONFIG['num_dishes']:,}")
    logger.info(f"  Oczekiwane recenzje: ~{num_users * GENERATION_CONFIG['avg_reviews_per_user']:,}")
    logger.info("")

    try:
        # Połącz z bazą danych
        with DatabaseConnection(connection_string) as db:

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

            logger.info("✓ PHASE 1 zakończona")
            logger.info("")

            # ========================================
            # PHASE 2: Restaurants
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 2: Generowanie restauracji")
            logger.info("=" * 60)

            generate_restaurants(db, blueprints_dir="blueprints")

            logger.info("✓ PHASE 2 zakończona")
            logger.info("")

            # ========================================
            # PHASE 3: Dishes
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 3: Generowanie dań")
            logger.info("=" * 60)

            generate_dishes(db, blueprints_dir="blueprints")

            logger.info("✓ PHASE 3 zakończona")
            logger.info("")

            # ========================================
            # PHASE 4: Users
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 4: Generowanie użytkowników")
            logger.info("=" * 60)

            generate_users(db, num_users=num_users)

            logger.info("✓ PHASE 4 zakończona")
            logger.info("")

            # ========================================
            # PHASE 5: Reviews (NAJDŁUŻSZE!)
            # ========================================
            logger.info("=" * 60)
            logger.info("=> PHASE 5: Generowanie recenzji (to zajmie ~10-15 minut)")
            logger.info("=" * 60)

            generate_reviews(db)

            logger.info("✓ PHASE 5 zakończona")
            logger.info("")

            # ========================================
            # STATYSTYKI
            # ========================================
            print_statistics(db)

            # Oblicz czas trwania
            end_time = datetime.now()
            duration = end_time - start_time

            logger.info("=" * 60)
            logger.info("✓ MOCKDATAFACTORY - ZAKOŃCZONE POMYŚLNIE")
            logger.info("=" * 60)
            logger.info(f"Koniec: {end_time.strftime('%Y-%m-%d %H:%M:%S')}")
            logger.info(f"Czas trwania: {duration}")
            logger.info("=" * 60)

    except Exception as e:
        logger.error("=" * 60)
        logger.error("✗ BŁĄD KRYTYCZNY")
        logger.error("=" * 60)
        logger.error(f"Błąd: {e}", exc_info=True)
        logger.error("=" * 60)
        sys.exit(1)


if __name__ == "__main__":
    main()
