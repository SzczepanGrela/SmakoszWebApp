"""
Phase 1 Core - Generowanie podstawowych danych (miasta, składniki, tagi)
"""

import logging
from typing import Dict, Any, List
import sys
import os

# Add parent directory to path for imports
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.blueprint_loader import BlueprintLoader

logger = logging.getLogger(__name__)


def generate_cities(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje miasta z blueprintu 01_city_rules.json

    Args:
        db: Połączenie z bazą danych
        blueprints_dir: Ścieżka do folderu z blueprintami
    """
    logger.info("📍 Generowanie miast...")

    loader = BlueprintLoader(blueprints_dir)
    city_rules = loader.load_blueprint("01_city_rules.json")

    cities = city_rules.get("cities", [])

    city_data = []
    for city in cities:
        city_data.append({
            "city_name": city["city_name"]
        })

    db.insert_bulk("Cities", city_data)
    logger.info(f"✅ Wygenerowano {len(city_data)} miast")


def generate_ingredients(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje składniki ekstraktowane z dish_variants.json

    Args:
        db: Połączenie z bazą danych
        blueprints_dir: Ścieżka do folderu z blueprintami
    """
    logger.info("🥗 Generowanie składników...")

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dish_variants.json")

    # Ekstraktuj unikalne składniki
    all_ingredients = set()
    for variant in dish_variants.get("variants", []):
        ingredients = variant.get("ingredients", [])
        all_ingredients.update(ingredients)

    # Oznacz alergeny
    allergens = {
        "orzechy", "krewetki", "mleko", "gluten", "jaja", "soja",
        "ryby", "seler", "gorczyca", "sezam", "łubin"
    }

    ingredient_data = []
    for ingredient in sorted(all_ingredients):
        is_allergen = any(allergen in ingredient.lower() for allergen in allergens)

        ingredient_data.append({
            "ingredient_name": ingredient,
            "is_allergen": is_allergen
        })

    db.insert_bulk("Ingredients", ingredient_data)
    logger.info(f"✅ Wygenerowano {len(ingredient_data)} składników ({sum(1 for i in ingredient_data if i['is_allergen'])} alergenów)")


def generate_tags(db: DatabaseConnection):
    """
    Generuje tagi (dietary, spice, cuisine, mood, occasion)

    Args:
        db: Połączenie z bazą danych
    """
    logger.info("🏷️  Generowanie tagów...")

    tags = [
        # Dietary tags
        {"tag_name": "Wegetariańskie", "category": "dietary"},
        {"tag_name": "Wegańskie", "category": "dietary"},
        {"tag_name": "Bezglutenowe", "category": "dietary"},
        {"tag_name": "Bez laktozy", "category": "dietary"},
        {"tag_name": "Keto", "category": "dietary"},
        {"tag_name": "Paleo", "category": "dietary"},
        {"tag_name": "Niskokaloryczne", "category": "dietary"},

        # Spice level tags
        {"tag_name": "Łagodne", "category": "spice"},
        {"tag_name": "Średnio ostre", "category": "spice"},
        {"tag_name": "Ostre", "category": "spice"},
        {"tag_name": "Bardzo ostre", "category": "spice"},

        # Cuisine tags
        {"tag_name": "Włoska", "category": "cuisine"},
        {"tag_name": "Azjatycka", "category": "cuisine"},
        {"tag_name": "Meksykańska", "category": "cuisine"},
        {"tag_name": "Amerykańska", "category": "cuisine"},
        {"tag_name": "Francuska", "category": "cuisine"},
        {"tag_name": "Polska", "category": "cuisine"},
        {"tag_name": "Grecka", "category": "cuisine"},
        {"tag_name": "Indyjska", "category": "cuisine"},
        {"tag_name": "Japońska", "category": "cuisine"},
        {"tag_name": "Tajska", "category": "cuisine"},
        {"tag_name": "Wietnamska", "category": "cuisine"},
        {"tag_name": "Bliskowschodnia", "category": "cuisine"},
        {"tag_name": "Śródziemnomorska", "category": "cuisine"},

        # Mood tags
        {"tag_name": "Romantyczne", "category": "mood"},
        {"tag_name": "Rodzinne", "category": "mood"},
        {"tag_name": "Biznesowe", "category": "mood"},
        {"tag_name": "Casual", "category": "mood"},
        {"tag_name": "Fine dining", "category": "mood"},
        {"tag_name": "Fast casual", "category": "mood"},

        # Occasion tags
        {"tag_name": "Śniadanie", "category": "occasion"},
        {"tag_name": "Brunch", "category": "occasion"},
        {"tag_name": "Lunch", "category": "occasion"},
        {"tag_name": "Obiad", "category": "occasion"},
        {"tag_name": "Kolacja", "category": "occasion"},
        {"tag_name": "Przekąska", "category": "occasion"},
        {"tag_name": "Deser", "category": "occasion"},

        # Feature tags
        {"tag_name": "Sezonowe", "category": "feature"},
        {"tag_name": "Lokalne składniki", "category": "feature"},
        {"tag_name": "Farm to table", "category": "feature"},
        {"tag_name": "Organiczne", "category": "feature"},
        {"tag_name": "Comfort food", "category": "feature"},
        {"tag_name": "Street food", "category": "feature"},
        {"tag_name": "Fusion", "category": "feature"},
    ]

    db.insert_bulk("Tags", tags)
    logger.info(f"✅ Wygenerowano {len(tags)} tagów")


def generate_ingredient_restrictions(db: DatabaseConnection):
    """
    Generuje powiązania składnik → restrykcja dietetyczna

    Args:
        db: Połączenie z bazą danych
    """
    logger.info("🔗 Generowanie powiązań składnik-restrykcja...")

    # Pobierz wszystkie składniki
    ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM Ingredients")

    restrictions = []

    for ingredient_id, ingredient_name in ingredients:
        ingredient_lower = ingredient_name.lower()

        # Mapowanie składników na restrykcje
        if any(meat in ingredient_lower for meat in ["mięso", "wołowina", "wieprzowina", "kurczak", "ryba", "krewetki", "łosoś", "szynka"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Wegetariańskie"
            })
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Wegańskie"
            })

        if any(dairy in ingredient_lower for dairy in ["mleko", "ser", "śmietana", "masło", "jogurt", "mozzarella", "parmezan"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Wegańskie"
            })
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Bez laktozy"
            })

        if any(gluten in ingredient_lower for gluten in ["mąka", "chleb", "makaron", "pszenica", "bułka"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Bezglutenowe"
            })

        if ingredient_name == "jaja" or "jajko" in ingredient_lower:
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "Wegańskie"
            })

    if restrictions:
        db.insert_bulk("Ingredient_Restrictions", restrictions)
        logger.info(f"✅ Wygenerowano {len(restrictions)} powiązań składnik-restrykcja")
    else:
        logger.warning("⚠️  Brak powiązań składnik-restrykcja do wygenerowania")
