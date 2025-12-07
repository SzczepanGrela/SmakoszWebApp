"""
Phase 1 Core - Generowanie podstawowych danych (miasta, składniki, tagi)
"""

import logging
from typing import Dict, Any, List
import random

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
    logger.info(" Generowanie miast...")

    loader = BlueprintLoader(blueprints_dir)
    city_rules = loader.load_blueprint("01_city_rules.json")

    # FIXED: Parse CITY_CONFIG structure instead of expecting "cities" list
    city_config = city_rules.get("CITY_CONFIG", {})

    if not city_config:
        logger.error(" Brak CITY_CONFIG w 01_city_rules.json!")
        raise ValueError("01_city_rules.json must contain CITY_CONFIG key")

    city_data = []
    for city_name in city_config.keys():
        city_data.append({
            "city_name": city_name
        })

    if not city_data:
        logger.error(" Brak miast do wygenerowania!")
        raise ValueError("No cities found in CITY_CONFIG")

    db.insert_bulk("cities", city_data)
    logger.info(f" Wygenerowano {len(city_data)} miast")

def generate_ingredients(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje składniki ekstraktowane z dish_variants.json

    Args:
        db: Połączenie z bazą danych
        blueprints_dir: Ścieżka do folderu z blueprintami
    """
    logger.info(" Generowanie składników...")

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dish_variants.json")

    # FIXED: Extract ingredients from nested structure
    # Structure: {"Pizza": {"variants": {"Margherita": {"ingredients": [...]}}}}
    all_ingredients = set()
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue
        variants = category_data.get("variants", {})
        for variant_name, variant_data in variants.items():
            if isinstance(variant_data, dict):
                ingredients = variant_data.get("ingredients", [])
                all_ingredients.update(ingredients)

    if not all_ingredients:
        logger.warning("⚠️ Nie znaleziono składników w dish_variants.json")

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

    db.insert_bulk("ingredients", ingredient_data)
    logger.info(f" Wygenerowano {len(ingredient_data)} składników ({sum(1 for i in ingredient_data if i['is_allergen'])} alergenów)")

def generate_tag_color(tag_category: str, tag_name: str) -> str:
    """
    Generuje kolor tagu według kategorii i nazwy
    Zwraca hex color (#RRGGBB)
    """
    color_schemes = {
        'dietary': {
            'Wegetariańskie': '#2ecc71',
            'Wegańskie': '#27ae60',
            'Bezglutenowe': '#16a085',
            'Bez laktozy': '#1abc9c',
            'Keto': '#95a5a6',
            'Paleo': '#7f8c8d',
            'Niskokaloryczne': '#3498db',
            '__default__': '#2ecc71'
        },
        'spice': {
            'Łagodne': '#95a5a6',
            'Średnio ostre': '#f39c12',
            'Ostre': '#e67e22',
            'Bardzo ostre': '#e74c3c',
            '__default__': '#f39c12'
        },
        'cuisine': {
            'Włoska': '#e74c3c',
            'Azjatycka': '#f39c12',
            'Meksykańska': '#27ae60',
            'Amerykańska': '#3498db',
            'Francuska': '#9b59b6',
            'Polska': '#e74c3c',
            'Grecka': '#3498db',
            'Indyjska': '#f39c12',
            'Japońska': '#e74c3c',
            'Tajska': '#27ae60',
            'Wietnamska': '#f39c12',
            'Bliskowschodnia': '#e67e22',
            'Śródziemnomorska': '#3498db',
            '__default__': '#3498db'
        },
        'mood': {
            'Romantyczne': '#e91e63',
            'Rodzinne': '#2ecc71',
            'Biznesowe': '#34495e',
            'Casual': '#95a5a6',
            'Fine dining': '#8e44ad',
            'Fast casual': '#f39c12',
            '__default__': '#9b59b6'
        },
        'occasion': {
            'Śniadanie': '#f39c12',
            'Brunch': '#e67e22',
            'Lunch': '#3498db',
            'Obiad': '#e74c3c',
            'Kolacja': '#8e44ad',
            'Przekąska': '#95a5a6',
            'Deser': '#e91e63',
            '__default__': '#f39c12'
        },
        'feature': {
            'Sezonowe': '#27ae60',
            'Lokalne składniki': '#16a085',
            'Farm to table': '#2ecc71',
            'Organiczne': '#1abc9c',
            'Comfort food': '#e67e22',
            'Street food': '#f39c12',
            'Fusion': '#9b59b6',
            '__default__': '#1abc9c'
        }
    }

    if tag_category in color_schemes:
        scheme = color_schemes[tag_category]
        if tag_name in scheme:
            return scheme[tag_name]
        return scheme['__default__']

    return '#95a5a6'  # Fallback gray

def generate_tags(db: DatabaseConnection):
    """
    Generuje tagi (dietary, spice, cuisine, mood, occasion)

    Args:
        db: Połączenie z bazą danych
    """
    logger.info(" Generowanie tagów...")

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

    # Add display_color to each tag
    for tag in tags:
        tag["display_color"] = generate_tag_color(tag["tag_category"], tag["tag_name"])

    db.insert_bulk("tags", tags)
    logger.info(f" Wygenerowano {len(tags)} tagów")

def generate_ingredient_restrictions(db: DatabaseConnection):
    """
    Generuje powiązania składnik -> restrykcja dietetyczna

    Args:
        db: Połączenie z bazą danych
    """
    logger.info(" Generowanie powiązań składnik-restrykcja...")

    # Pobierz wszystkie składniki
    ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM ingredients")

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
        db.insert_bulk("ingredient_restrictions", restrictions)
        logger.info(f" Wygenerowano {len(restrictions)} powiązań składnik-restrykcja")
    else:
        logger.warning("⚠️  Brak powiązań składnik-restrykcja do wygenerowania")
