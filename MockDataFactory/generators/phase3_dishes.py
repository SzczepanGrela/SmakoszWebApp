"""
Phase 3 - Generowanie dań (~20,000)
"""

import logging
import random
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.blueprint_loader import BlueprintLoader
from utils.statistical import sample_beta, zipf_distribution
from utils.photo_pools import PhotoPools

logger = logging.getLogger(__name__)


def generate_dishes(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje ~20,000 dań z secret attributes

    FIXED: Now uses proper dish_id from database after INSERT
    """
    logger.info("🍕 Generowanie dań...")

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dish_variants.json")

    # Pobierz restauracje
    restaurants = db.fetch_all("""
        SELECT restaurant_id, menu_blueprint, secret_price_multiplier
        FROM Restaurants
    """)

    # Pobierz wszystkie składniki
    all_ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM Ingredients")
    ingredient_map = {name: id for id, name in all_ingredients}

    photo_pools = PhotoPools()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0

    for restaurant_id, menu_blueprint, price_multiplier in restaurants:
        # Wybierz dania dla tego typu menu
        menu_dishes = _select_dishes_for_menu(menu_blueprint, dish_variants)

        if not menu_dishes:
            continue

        # Zipf distribution dla popularności dań
        popularity_scores = zipf_distribution(len(menu_dishes), alpha=1.5)

        # FIXED: Insert pojedynczo aby mieć prawdziwe dish_id
        for i, variant in enumerate(menu_dishes):
            dish_name = variant.get("name", "Danie")
            archetype = variant.get("archetype", "Unknown")
            base_price = variant.get("price", 35.0)

            # Secret attributes
            secret_base_price = base_price
            public_price = round(base_price * price_multiplier, 2)
            secret_quality = sample_beta(5, 2, 0.3, 0.95)
            secret_spiciness = random.uniform(0, 10) if "spicy" in variant.get("tags", []) else random.uniform(0, 3)
            secret_richness = random.uniform(0.0, 1.0)
            secret_texture_score = sample_beta(4, 2, 0.0, 1.0)

            # Insert dania i pobierz prawdziwe ID
            dish_data = {
                "restaurant_id": restaurant_id,
                "dish_name": dish_name,
                "archetype": archetype,  # NEW column in schema
                "public_price": public_price,
                "secret_base_price": round(secret_base_price, 2),
                "secret_quality": round(secret_quality, 3),
                "secret_spiciness": round(secret_spiciness, 2),
                "secret_richness": round(secret_richness, 3),  # NEW column
                "secret_texture_score": round(secret_texture_score, 3),  # NEW column
                "popularity_factor": round(popularity_scores[i], 4)  # NEW column
            }

            # FIXED: Insert pojedynczo i pobierz prawdziwe ID
            dish_id = db.insert_single("Dishes", dish_data)
            total_dishes += 1

            # Przypisz składniki (teraz z prawdziwym dish_id)
            ingredients = variant.get("ingredients", [])
            ingredient_links = []
            for ingredient_name in ingredients:
                if ingredient_name in ingredient_map:
                    ingredient_links.append({
                        "dish_id": dish_id,  # FIXED: prawdziwe ID
                        "ingredient_id": ingredient_map[ingredient_name]
                    })

            if ingredient_links:
                db.insert_bulk("Dish_Ingredients_Link", ingredient_links)
                total_ingredients_links += len(ingredient_links)

            # Dodaj zdjęcie (FIXED: entity_type + entity_id)
            photo_url = photo_pools.get_dish_photo(archetype)
            db.insert_single("Photos", {
                "entity_type": "dish",  # FIXED: proper column
                "entity_id": dish_id,  # FIXED: was dish_id direct
                "photo_url": photo_url,
                "is_primary": True
            })
            total_photos += 1

        if (total_dishes % 1000) == 0:
            logger.info(f"  Wygenerowano {total_dishes} dań...")

    logger.info(f"✅ Wygenerowano {total_dishes} dań")
    logger.info(f"✅ Przypisano {total_ingredients_links} składników do dań")
    logger.info(f"✅ Dodano {total_photos} zdjęć dań")


def _select_dishes_for_menu(menu_blueprint: str, dish_variants: dict) -> list:
    """Wybiera dania odpowiednie dla danego typu menu"""
    variants = dish_variants.get("variants", [])

    # Filtruj według menu blueprint
    menu_mappings = {
        "pizza_menu": ["Pizza"],
        "burger_menu": ["Burger"],
        "sushi_menu": ["Sushi"],
        "asian_menu": ["Ramen", "Noodles", "Dim Sum", "Pho"],
        "steak_menu": ["Steak", "BBQ"],
        "vegan_menu": ["Vegan", "Salad"],
        "mexican_menu": ["Tacos", "Quesadilla", "Nachos"],
        "italian_menu": ["Pizza", "Pasta", "Risotto"],
        "french_menu": ["Steak", "Soup"],
        "seafood_menu": ["Seafood", "Sushi"],
        "general_menu": ["Pizza", "Burger", "Pasta", "Salad"]
    }

    archetypes = menu_mappings.get(menu_blueprint, ["Pizza", "Burger", "Pasta"])

    # Filtruj warianty
    matching_dishes = [v for v in variants if v.get("archetype") in archetypes]

    # Wybierz 10-20 dań
    num_dishes = random.randint(10, 20)

    if len(matching_dishes) > num_dishes:
        return random.sample(matching_dishes, num_dishes)
    else:
        return matching_dishes
