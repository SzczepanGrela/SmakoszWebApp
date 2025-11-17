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
        {"tag_name": "Wegetariańskie", "tag_category": "dietary"},
        {"tag_name": "Wegańskie", "tag_category": "dietary"},
        {"tag_name": "Bezglutenowe", "tag_category": "dietary"},
        {"tag_name": "Bez laktozy", "tag_category": "dietary"},
        {"tag_name": "Keto", "tag_category": "dietary"},
        {"tag_name": "Paleo", "tag_category": "dietary"},
        {"tag_name": "Niskokaloryczne", "tag_category": "dietary"},

        # Spice level tags
        {"tag_name": "Łagodne", "tag_category": "spice"},
        {"tag_name": "Średnio ostre", "tag_category": "spice"},
        {"tag_name": "Ostre", "tag_category": "spice"},
        {"tag_name": "Bardzo ostre", "tag_category": "spice"},

        # Cuisine tags
        {"tag_name": "Włoska", "tag_category": "cuisine"},
        {"tag_name": "Azjatycka", "tag_category": "cuisine"},
        {"tag_name": "Meksykańska", "tag_category": "cuisine"},
        {"tag_name": "Amerykańska", "tag_category": "cuisine"},
        {"tag_name": "Francuska", "tag_category": "cuisine"},
        {"tag_name": "Polska", "tag_category": "cuisine"},
        {"tag_name": "Grecka", "tag_category": "cuisine"},
        {"tag_name": "Indyjska", "tag_category": "cuisine"},
        {"tag_name": "Japońska", "tag_category": "cuisine"},
        {"tag_name": "Tajska", "tag_category": "cuisine"},
        {"tag_name": "Wietnamska", "tag_category": "cuisine"},
        {"tag_name": "Bliskowschodnia", "tag_category": "cuisine"},
        {"tag_name": "Śródziemnomorska", "tag_category": "cuisine"},

        # Mood tags
        {"tag_name": "Romantyczne", "tag_category": "mood"},
        {"tag_name": "Rodzinne", "tag_category": "mood"},
        {"tag_name": "Biznesowe", "tag_category": "mood"},
        {"tag_name": "Casual", "tag_category": "mood"},
        {"tag_name": "Fine dining", "tag_category": "mood"},
        {"tag_name": "Fast casual", "tag_category": "mood"},

        # Occasion tags
        {"tag_name": "Śniadanie", "tag_category": "occasion"},
        {"tag_name": "Brunch", "tag_category": "occasion"},
        {"tag_name": "Lunch", "tag_category": "occasion"},
        {"tag_name": "Obiad", "tag_category": "occasion"},
        {"tag_name": "Kolacja", "tag_category": "occasion"},
        {"tag_name": "Przekąska", "tag_category": "occasion"},
        {"tag_name": "Deser", "tag_category": "occasion"},

        # Feature tags
        {"tag_name": "Sezonowe", "tag_category": "feature"},
        {"tag_name": "Lokalne składniki", "tag_category": "feature"},
        {"tag_name": "Farm to table", "tag_category": "feature"},
        {"tag_name": "Organiczne", "tag_category": "feature"},
        {"tag_name": "Comfort food", "tag_category": "feature"},
        {"tag_name": "Street food", "tag_category": "feature"},
        {"tag_name": "Fusion", "tag_category": "feature"},
    ]

    db.insert_bulk("Tags", tags)
    logger.info(f"✅ Wygenerowano {len(tags)} tagów")

def generate_ingredient_restrictions(db: DatabaseConnection):
    """
    Generuje powiązania składnik -> restrykcja dietetyczna

    Args:
        db: Połączenie z bazą danych
    """
    logger.info("🔗 Generowanie powiązań składnik-restrykcja...")

    # Pobierz wszystkie składniki
    ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM Ingredients")

    restrictions = []

    for ingredient_id, ingredient_name in ingredients:
        ingredient_lower = ingredient_name.lower()

        # Mapowanie składników na restrykcje (FIXED: English names matching schema)
        if any(meat in ingredient_lower for meat in ["mięso", "wołowina", "wieprzowina", "kurczak", "ryba", "krewetki", "łosoś", "szynka"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "vegetarian"  # FIXED: English name
            })
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "vegan"  # FIXED: English name
            })

        if any(dairy in ingredient_lower for dairy in ["mleko", "ser", "śmietana", "masło", "jogurt", "mozzarella", "parmezan"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "vegan"  # FIXED: English name
            })
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "lactose-free"  # FIXED: English name
            })

        if any(gluten in ingredient_lower for gluten in ["mąka", "chleb", "makaron", "pszenica", "bułka"]):
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "gluten-free"  # FIXED: English name
            })

        if ingredient_name.lower() == "jaja" or "jajko" in ingredient_lower:
            restrictions.append({
                "ingredient_id": ingredient_id,
                "restriction_type": "vegan"  # FIXED: English name
            })

    if restrictions:
        db.insert_bulk("Ingredient_Restrictions", restrictions)
        logger.info(f"✅ Wygenerowano {len(restrictions)} powiązań składnik-restrykcja")
    else:
        logger.warning("⚠️  Brak powiązań składnik-restrykcja do wygenerowania")
