import json
import logging
import time
import urllib.parse
from pathlib import Path

from tqdm import tqdm

from utils.blueprint_loader import BlueprintLoader
from utils.db_connection import DatabaseConnection
from utils.photo_pools import PhotoPools
from config import PHOTO_CONFIG

logger = logging.getLogger(__name__)

# Hero images index path
HERO_INDEX_PATH = Path(PHOTO_CONFIG.get("local_photo_dir", "E:/smakosz/images")) / "hero" / "hero_index.json"

def generate_ingredient_icon_url(ingredient_name: str) -> str:
    """
    Generate a high-quality placeholder icon URL for an ingredient using ui-avatars.com.

    Creates a square icon with the first 2 letters of the ingredient name,
    suitable for UI display in menus and ingredient lists.

    Args:
        ingredient_name: Name of the ingredient (e.g., "Pomidor", "Mozzarella")

    Returns:
        str: URL to ui-avatars.com icon (128x128, square format)
    """
    # URL-encode the ingredient name for safe URL usage
    encoded_name = urllib.parse.quote_plus(ingredient_name)

    icon_url = (
        f"https://ui-avatars.com/api/"
        f"?name={encoded_name}"
        f"&background=random"
        f"&color=fff"
        f"&size=128"
        f"&length=2"
        f"&font-size=0.5"
        f"&rounded=false"
    )

    return icon_url

def generate_cities(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating cities...")

    loader = BlueprintLoader(blueprints_dir)
    city_rules = loader.load_blueprint("cities.json")
    city_config = city_rules.get("CITY_CONFIG", {})

    if not city_config:
        logger.error("CITY_CONFIG missing in cities.json!")
        raise ValueError("cities.json must contain CITY_CONFIG key")

    # Polish postal code prefixes by major city
    POSTAL_CODE_PREFIXES = {
        "Warszawa": "00",
        "Kraków": "30",
        "Wrocław": "50",
        "Łódź": "90",
        "Poznań": "60",
        "Gdańsk": "80",
        "Szczecin": "70",
        "Bydgoszcz": "85",
        "Lublin": "20",
        "Białystok": "15",
        "Katowice": "40",
        "Gdynia": "81",
        "Toruń": "87",
        "Rzeszów": "35",
        "Kielce": "25",
        "Olsztyn": "10",
        "Opole": "45",
        "Gorzów Wlkp.": "66",
    }

    city_data = []
    for city_name in city_config:
        city_data.append({
            "city_name": city_name,
            "postal_code_prefix": POSTAL_CODE_PREFIXES.get(city_name, "00")
        })

    if not city_data:
        logger.error("No cities to generate!")
        raise ValueError("No cities found in CITY_CONFIG")

    db.insert_bulk("cities", city_data)

    duration = time.time() - start_time
    logger.info(f"Generated {len(city_data)} cities in {duration:.2f}s")

def generate_cuisine_types(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    """Generate cuisine types dictionary from restaurant themes."""
    start_time = time.time()
    logger.info("Generating cuisine types...")

    if cleanup:
        logger.info("Cleaning up old cuisine_types data...")
        db.execute_query("TRUNCATE TABLE cuisine_types RESTART IDENTITY CASCADE")
        db.commit()

    loader = BlueprintLoader(blueprints_dir)
    restaurant_rules = loader.load_blueprint("restaurant_types.json")
    themes = restaurant_rules.get("RESTAURANT_THEMES", {})

    # Map themes to display names
    CUISINE_DISPLAY_NAMES = {
        "Pizzeria": "Włoska",
        "Kebab": "Turecka",
        "Burgerownia": "Amerykańska",
        "Kuchnia Polska": "Polska",
        "Sushi Bar": "Japońska",
        "Wegańska Kawiarnia": "Wegańska",
        "Kuchnia Chińska": "Chińska",
        "Kuchnia Indyjska": "Indyjska",
        "Kuchnia Meksykańska": "Meksykańska",
        "Kuchnia Francuska": "Francuska",
        "Kuchnia Włoska": "Włoska",
        "Kuchnia Tajska": "Tajska",
        "Ramen Bar": "Japońska",
        "Kawiarnia": "Kawiarnia",
        "Food Truck": "Street Food",
        "Smażalnia Ryb": "Ryby",
        "BBQ & Grill": "BBQ",
        "Taqueria": "Meksykańska",
        "Creperie": "Naleśnikarnia",
        "Piekarnia": "Piekarnia",
    }

    cuisine_data = []
    for theme_name in themes.keys():
        display_name = CUISINE_DISPLAY_NAMES.get(theme_name, theme_name)
        cuisine_data.append({
            "name": theme_name.lower().replace(" ", "_"),
            "display_name": display_name,
            "icon": None
        })

    if cuisine_data:
        db.insert_bulk("cuisine_types", cuisine_data)

    duration = time.time() - start_time
    logger.info(f"Generated {len(cuisine_data)} cuisine types in {duration:.2f}s")

def generate_ingredients(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating ingredients...")

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dishes.json")
    
    # Initialize PhotoPools for real ingredient icons
    photo_pools = PhotoPools()

    all_ingredients = set()
    for _, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue
        variants = category_data.get("variants", {})
        for _, variant_data in variants.items():
            if isinstance(variant_data, dict):
                ingredients = variant_data.get("ingredients", [])
                all_ingredients.update(ingredients)

    if not all_ingredients:
        logger.warning("No ingredients found in dishes.json")

    allergens = {
        "orzechy",
        "krewetki",
        "mleko",
        "gluten",
        "jaja",
        "soja",
        "ryby",
        "seler",
        "gorczyca",
        "sezam",
        "łubin",
    }

    # Load global config
    loader = BlueprintLoader(blueprints_dir)
    global_config = loader.load_blueprint("global_config.json")
    dietary_keywords = global_config.get("DIETARY_KEYWORDS", {})

    meat_keywords = dietary_keywords.get("meat", [])
    dairy_keywords = dietary_keywords.get("dairy", [])
    egg_keywords = dietary_keywords.get("eggs", [])
    gluten_keywords = dietary_keywords.get("gluten", [])

    ingredient_data = []
    for ingredient in tqdm(sorted(all_ingredients), desc="Generating ingredients", unit=" ingredient", mininterval=1.0):
        ing_lower = ingredient.lower()
        
        is_allergen = any(allergen in ing_lower for allergen in allergens)

        # Default to True (Positive logic)
        is_vegetarian = True
        is_vegan = True
        is_gluten_free = True
        is_lactose_free = True

        # Check for Meat (Non-Veg, Non-Vegan)
        if any(kw in ing_lower for kw in meat_keywords):
            is_vegetarian = False
            is_vegan = False
            is_lactose_free = True # Meat itself is lactose free usually (unless prepared with butter, but here we assume raw/processed ingredient)

        # Check for Dairy (Non-Vegan, Non-Lactose-Free)
        if any(kw in ing_lower for kw in dairy_keywords):
            is_vegan = False
            is_lactose_free = False
            # Dairy is vegetarian

        # Check for Eggs (Non-Vegan)
        if any(kw in ing_lower for kw in egg_keywords):
            is_vegan = False
            # Eggs are vegetarian, lactose free, gluten free

        # Check for Gluten
        if any(kw in ing_lower for kw in gluten_keywords) or "gluten" in ing_lower:
            is_gluten_free = False

        # Corrections for specific items
        if "tofu" in ing_lower:
            is_vegetarian = True
            is_vegan = True
        
        if "miód" in ing_lower:
            is_vegan = False # Debatable, but often considered non-vegan

        # Generate icon URL: Try real photo first, then fallback to UI Avatars
        photo_data = photo_pools.get_ingredient_photo(ingredient)
        icon_url = photo_data.get("url")
        icon_blurhash = photo_data.get("blurhash")
        
        if not icon_url:
            icon_url = generate_ingredient_icon_url(ingredient)
            icon_blurhash = None

        ingredient_data.append({
            "ingredient_name": ingredient,
            "icon_url": icon_url,
            "icon_blurhash": icon_blurhash,
            "is_allergen": is_allergen,
            "is_vegetarian": is_vegetarian,
            "is_vegan": is_vegan,
            "is_gluten_free": is_gluten_free,
            "is_lactose_free": is_lactose_free
        })

    db.insert_bulk("ingredients", ingredient_data)

    duration = time.time() - start_time
    allergen_count = sum(1 for i in ingredient_data if i["is_allergen"])
    logger.info(f"Generated {len(ingredient_data)} ingredients ({allergen_count} allergens) in {duration:.2f}s")

def generate_tag_color(tag_category: str, tag_name: str) -> str:
    color_schemes = {
        "dietary": {
            "Wegetariańskie": "#2ecc71",
            "Wegańskie": "#27ae60",
            "Bezglutenowe": "#16a085",
            "Bez laktozy": "#1abc9c",
            "Keto": "#95a5a6",
            "Paleo": "#7f8c8d",
            "Niskokaloryczne": "#3498db",
            "__default__": "#2ecc71",
        },
        "spice": {
            "Łagodne": "#95a5a6",
            "Średnio ostre": "#f39c12",
            "Ostre": "#e67e22",
            "Bardzo ostre": "#e74c3c",
            "__default__": "#f39c12",
        },
        "cuisine": {
            "Włoska": "#e74c3c",
            "Azjatycka": "#f39c12",
            "Meksykańska": "#27ae60",
            "Amerykańska": "#3498db",
            "Francuska": "#9b59b6",
            "Polska": "#e74c3c",
            "Grecka": "#3498db",
            "Indyjska": "#f39c12",
            "Japońska": "#e74c3c",
            "Tajska": "#27ae60",
            "Wietnamska": "#f39c12",
            "Bliskowschodnia": "#e67e22",
            "Śródziemnomorska": "#3498db",
            "__default__": "#3498db",
        },
        "mood": {
            "Romantyczne": "#e91e63",
            "Rodzinne": "#2ecc71",
            "Biznesowe": "#34495e",
            "Casual": "#95a5a6",
            "Fine dining": "#8e44ad",
            "Fast casual": "#f39c12",
            "__default__": "#9b59b6",
        },
        "occasion": {
            "Śniadanie": "#f39c12",
            "Brunch": "#e67e22",
            "Lunch": "#3498db",
            "Obiad": "#e74c3c",
            "Kolacja": "#8e44ad",
            "Przekąska": "#95a5a6",
            "Deser": "#e91e63",
            "__default__": "#f39c12",
        },
        "feature": {
            "Sezonowe": "#27ae60",
            "Lokalne składniki": "#16a085",
            "Farm to table": "#2ecc71",
            "Organiczne": "#1abc9c",
            "Comfort food": "#e67e22",
            "Street food": "#f39c12",
            "Fusion": "#9b59b6",
            "__default__": "#1abc9c",
        },
    }

    if tag_category in color_schemes:
        scheme = color_schemes[tag_category]
        if tag_name in scheme:
            return scheme[tag_name]
        return scheme["__default__"]

    return "#95a5a6"

def generate_tags(db: DatabaseConnection, cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating tags...")

    tags = [
        {"tag_name": "Wegetariańskie", "category": "dietary"},
        {"tag_name": "Wegańskie", "category": "dietary"},
        {"tag_name": "Bezglutenowe", "category": "dietary"},
        {"tag_name": "Bez laktozy", "category": "dietary"},
        {"tag_name": "Keto", "category": "dietary"},
        {"tag_name": "Paleo", "category": "dietary"},
        {"tag_name": "Niskokaloryczne", "category": "dietary"},
        {"tag_name": "Łagodne", "category": "spice"},
        {"tag_name": "Średnio ostre", "category": "spice"},
        {"tag_name": "Ostre", "category": "spice"},
        {"tag_name": "Bardzo ostre", "category": "spice"},
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
        {"tag_name": "Romantyczne", "category": "mood"},
        {"tag_name": "Rodzinne", "category": "mood"},
        {"tag_name": "Biznesowe", "category": "mood"},
        {"tag_name": "Casual", "category": "mood"},
        {"tag_name": "Fine dining", "category": "mood"},
        {"tag_name": "Fast casual", "category": "mood"},
        {"tag_name": "Śniadanie", "category": "occasion"},
        {"tag_name": "Brunch", "category": "occasion"},
        {"tag_name": "Lunch", "category": "occasion"},
        {"tag_name": "Obiad", "category": "occasion"},
        {"tag_name": "Kolacja", "category": "occasion"},
        {"tag_name": "Przekąska", "category": "occasion"},
        {"tag_name": "Deser", "category": "occasion"},
        {"tag_name": "Sezonowe", "category": "feature"},
        {"tag_name": "Lokalne składniki", "category": "feature"},
        {"tag_name": "Farm to table", "category": "feature"},
        {"tag_name": "Organiczne", "category": "feature"},
        {"tag_name": "Comfort food", "category": "feature"},
        {"tag_name": "Street food", "category": "feature"},
        {"tag_name": "Fusion", "category": "feature"},
    ]

    for tag in tqdm(tags, desc="Generating tag colors", unit=" tag", mininterval=1.0):
        tag["display_color"] = generate_tag_color(tag["category"], tag["tag_name"])

    db.insert_bulk("tags", tags)

    duration = time.time() - start_time
    logger.info(f"Generated {len(tags)} tags in {duration:.2f}s")

def generate_hero_images(db: DatabaseConnection, cleanup: bool = True):
    """
    Generate hero images in media_assets table for homepage backgrounds.
    
    Reads from hero_index.json and inserts records with:
    - entity_type = 'hero'
    - entity_id = sequential (1, 2, 3...)
    - credit_text = "Photographer / Source" for Unsplash images (NULL for Pixabay)
    
    Backend can then query: SELECT * FROM media_assets WHERE entity_type = 'hero' ORDER BY random() LIMIT 1
    """
    start_time = time.time()
    logger.info("Generating hero images...")
    
    if cleanup:
        logger.info("Cleaning up old hero images from media_assets...")
        db.execute_query("DELETE FROM media_assets WHERE entity_type = 'hero'")
        db.commit()
    
    if not HERO_INDEX_PATH.exists():
        logger.warning(f"Hero index not found: {HERO_INDEX_PATH}")
        return
    
    with open(HERO_INDEX_PATH, encoding="utf-8") as f:
        hero_index = json.load(f)
    
    images = hero_index.get("images", [])
    if not images:
        logger.warning("No hero images found in index")
        return
    
    # R2 base URL for hero images
    r2_base = PHOTO_CONFIG.get("r2_public_base_url", "").rstrip("/")
    r2_mock_prefix = PHOTO_CONFIG.get("r2_mock_prefix", "smakosz/images/mock")
    
    hero_data = []
    for idx, img in enumerate(images, start=1):
        filename = img.get("filename")
        if not filename:
            continue
        
        # Build URL: {r2_base}/{r2_mock_prefix}/hero/{filename}
        url = f"{r2_base}/{r2_mock_prefix}/hero/{filename}"
        
        # Build credit_text for Unsplash images (Pixabay = CC0, no attribution needed)
        credit_text = None
        source = img.get("source", "").lower()
        if source == "unsplash":
            credit_user = img.get("credit_user", "Unknown")
            credit_text = f"{credit_user} / Unsplash"
        
        hero_data.append({
            "entity_type": "hero",
            "entity_id": idx,  # Sequential ID (1, 2, 3...)
            "url": url,
            "blurhash": img.get("blurhash"),
            "width": img.get("width", 1600),
            "height": img.get("height", 900),
            "is_primary": False,
            "status": "approved",
            "credit_text": credit_text,
        })
    
    if hero_data:
        db.insert_bulk("media_assets", hero_data)
    
    unsplash_count = sum(1 for h in hero_data if h["credit_text"])
    duration = time.time() - start_time
    logger.info(f"Generated {len(hero_data)} hero images ({unsplash_count} with attribution) in {duration:.2f}s")

if __name__ == "__main__":
    import os
    import sys

    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

    from config import get_connection_params

    logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

    try:
        connection_params = get_connection_params()

        with DatabaseConnection(connection_params) as db:
            generate_cities(db, blueprints_dir="blueprints")
            generate_ingredients(db, blueprints_dir="blueprints")
            generate_tags(db)

            logger.info("Phase 1 completed.")

    except Exception as e:
        logger.error(f"Error: {e}", exc_info=True)
        sys.exit(1)
