"""
Phase 2 - Generowanie restauracji (~1200)
"""

import logging
import random
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.blueprint_loader import BlueprintLoader
from utils.statistical import sample_beta, sample_normal
from utils.date_generator import DateGenerator
from utils.photo_pools import PhotoPools

logger = logging.getLogger(__name__)


def generate_restaurants(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje ~1200 restauracji z secret attributes

    Secret Attributes:
    - secret_price_multiplier (0.8-1.3)
    - secret_overall_food_quality (0.4-0.95, beta distribution)
    - secret_service_quality (0.3-0.95)
    - secret_cleanliness_score (3.0-9.5)
    - secret_ambiance_type ("Romantyczny", "Rodzinny", "Biznesowy")
    - secret_ambiance_quality (0.4-0.95)
    """
    logger.info("🏪 Generowanie restauracji...")

    loader = BlueprintLoader(blueprints_dir)
    restaurant_rules = loader.load_blueprint("02_restaurant_rules.json")

    # Pobierz miasta
    cities = db.fetch_all("SELECT city_id, city_name FROM Cities")

    date_gen = DateGenerator()
    photo_pools = PhotoPools()

    restaurant_data = []
    restaurant_id_counter = 1

    for city_id, city_name in cities:
        # Liczba restauracji per miasto (z restaurant_rules)
        num_restaurants = _get_restaurant_count_for_city(city_name, restaurant_rules)

        for _ in range(num_restaurants):
            # Wybierz typ restauracji
            theme = _select_restaurant_theme(restaurant_rules)

            # Generuj nazwę
            name = _generate_restaurant_name(theme, city_name)

            # Data otwarcia
            created_date = date_gen.generate_restaurant_created_date()

            # Secret attributes
            secret_price_multiplier = random.uniform(0.8, 1.3)
            secret_overall_food_quality = sample_beta(5, 2, 0.4, 0.95)
            secret_service_quality = sample_beta(4, 3, 0.3, 0.95)
            secret_cleanliness_score = sample_normal(7.5, 1.5, 3.0, 9.5)

            ambiance_types = ["Romantyczny", "Rodzinny", "Biznesowy", "Casual", "Energiczny", "Spokojny"]
            secret_ambiance_type = random.choice(ambiance_types)
            secret_ambiance_quality = sample_beta(4, 3, 0.4, 0.95)

            # Menu blueprint
            menu_blueprint = _get_menu_blueprint(theme)

            restaurant_data.append({
                "city_id": city_id,
                "restaurant_name": name,
                "public_cuisine_theme": theme,  # FIXED: proper column name
                "theme": theme,  # Keep for backward compatibility
                "created_at": DateGenerator.to_sql_datetime(created_date),  # FIXED: was created_date
                "secret_price_multiplier": round(secret_price_multiplier, 3),
                "secret_overall_food_quality": round(secret_overall_food_quality, 3),
                "secret_service_quality": round(secret_service_quality, 3),
                "secret_cleanliness_score": round(secret_cleanliness_score, 2),
                "secret_ambiance_type": secret_ambiance_type,
                "secret_ambiance_quality": round(secret_ambiance_quality, 3),
                "menu_blueprint": menu_blueprint
            })

            restaurant_id_counter += 1

    db.insert_bulk("Restaurants", restaurant_data)
    logger.info(f"✅ Wygenerowano {len(restaurant_data)} restauracji")

    # Dodaj tagi i zdjęcia
    _assign_restaurant_tags(db)
    _assign_restaurant_photos(db, photo_pools)


def _get_restaurant_count_for_city(city_name: str, rules: dict) -> int:
    """Zwraca liczbę restauracji dla miasta z rules"""
    city_counts = {
        "Warszawa": 200,
        "Kraków": 150,
        "Wrocław": 120,
        "Poznań": 100,
        "Gdańsk": 90,
        "Szczecin": 70,
        "Lublin": 60,
        "Katowice": 80,
        "Bydgoszcz": 50,
        "Białystok": 40,
        "Olsztyn": 35,
        "Rzeszów": 40,
        "Toruń": 35,
        "Kielce": 30,
        "Gliwice": 40,
        "Zabrze": 35,
        "Radom": 25,
        "Łódź": 100
    }

    return city_counts.get(city_name, 30)


def _select_restaurant_theme(rules: dict) -> str:
    """Wybiera typ restauracji"""
    themes = ["Pizzeria", "Burger Bar", "Sushi Bar", "Asian Fusion", "Steakhouse",
              "Vegan Cafe", "Mexican", "Italian", "French Bistro", "Seafood"]
    return random.choice(themes)


def _generate_restaurant_name(theme: str, city: str) -> str:
    """Generuje polską nazwę restauracji"""
    prefixes = ["Restauracja", "Bistro", "Gospoda", "Smaki", "Bar"]
    suffixes = ["Pod Aniołem", "Starówka", "Centrum", "Parkowa", "Królewska"]

    if theme == "Pizzeria":
        return f"Pizzeria {random.choice(['Bella', 'Roma', 'Napoli', 'Milano'])}"
    elif theme == "Sushi Bar":
        return f"Sushi {random.choice(['Tokyo', 'Osaka', 'Sakura', 'Zen'])}"
    elif theme == "Burger Bar":
        return f"{random.choice(['The', 'Big', 'Best'])} Burger {random.choice(['House', 'Bar', 'Kitchen'])}"
    else:
        return f"{random.choice(prefixes)} {random.choice(suffixes)}"


def _get_menu_blueprint(theme: str) -> str:
    """Zwraca blueprint menu dla typu restauracji"""
    blueprints = {
        "Pizzeria": "pizza_menu",
        "Burger Bar": "burger_menu",
        "Sushi Bar": "sushi_menu",
        "Asian Fusion": "asian_menu",
        "Steakhouse": "steak_menu",
        "Vegan Cafe": "vegan_menu",
        "Mexican": "mexican_menu",
        "Italian": "italian_menu",
        "French Bistro": "french_menu",
        "Seafood": "seafood_menu"
    }
    return blueprints.get(theme, "general_menu")


def _assign_restaurant_tags(db: DatabaseConnection):
    """Przypisuje tagi do restauracji"""
    logger.info("🏷️  Przypisywanie tagów do restauracji...")

    restaurants = db.fetch_all("SELECT restaurant_id, theme FROM Restaurants")
    tags = db.fetch_all("SELECT tag_id, tag_name, tag_category FROM Tags")  # FIXED: category → tag_category

    tag_assignments = []

    for restaurant_id, theme in restaurants:
        # Przypisz 2-4 losowe tagi
        num_tags = random.randint(2, 4)
        selected_tags = random.sample(tags, min(num_tags, len(tags)))

        for tag_id, _, _ in selected_tags:
            tag_assignments.append({
                "restaurant_id": restaurant_id,
                "tag_id": tag_id
            })

    if tag_assignments:
        db.insert_bulk("Restaurant_Tags", tag_assignments)
        logger.info(f"✅ Przypisano {len(tag_assignments)} tagów do restauracji")


def _assign_restaurant_photos(db: DatabaseConnection, photo_pools: PhotoPools):
    """Dodaje zdjęcia restauracji"""
    logger.info("📸 Dodawanie zdjęć restauracji...")

    restaurants = db.fetch_all("SELECT restaurant_id, theme FROM Restaurants")

    photo_data = []

    for restaurant_id, theme in restaurants:
        # 2-3 zdjęcia na restaurację
        num_photos = random.randint(2, 3)

        for i in range(num_photos):
            url = photo_pools.get_restaurant_photo(theme)

            photo_data.append({
                "entity_type": "restaurant",  # FIXED: proper column
                "entity_id": restaurant_id,  # FIXED: was restaurant_id
                "photo_url": url,
                "is_primary": (i == 0)  # First photo is primary
                # created_at is DEFAULT in schema, no need to specify
            })

    db.insert_bulk("Photos", photo_data)
    logger.info(f"✅ Dodano {len(photo_data)} zdjęć restauracji")
