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
        FROM restaurants
    """)

    # Pobierz wszystkie składniki
    all_ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM ingredients")
    ingredient_map = {name: id for id, name in all_ingredients}

    # Pobierz wszystkie tagi do przypisania dish_tags
    all_tags = db.fetch_all("SELECT tag_id, tag_name, tag_category FROM tags")
    tag_map = {name: tag_id for tag_id, name, _ in all_tags}
    tag_by_category = {}
    for tag_id, tag_name, tag_category in all_tags:
        if tag_category not in tag_by_category:
            tag_by_category[tag_category] = []
        tag_by_category[tag_category].append((tag_id, tag_name))

    photo_pools = PhotoPools()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0
    total_dish_tags = 0
    dish_tags_buffer = []  # Buffer for bulk insert

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
            dish_id = db.insert_single("dishes", dish_data)
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
                db.insert_bulk("dish_ingredients_link", ingredient_links)
                total_ingredients_links += len(ingredient_links)

            # Dodaj zdjęcie (FIXED: entity_type + entity_id)
            photo_url = photo_pools.get_dish_photo(archetype)
            db.insert_single("photos", {
                "entity_type": "dish",  # FIXED: proper column
                "entity_id": dish_id,  # FIXED: was dish_id direct
                "photo_url": photo_url,
                "is_primary": True
            })
            total_photos += 1

            # Przypisz tagi do dania (dish_tags)
            dish_tag_ids = _get_tags_for_dish(
                archetype=archetype,
                spiciness=secret_spiciness,
                ingredients=ingredients,
                tag_map=tag_map,
                tag_by_category=tag_by_category
            )
            for tag_id in dish_tag_ids:
                dish_tags_buffer.append({
                    "dish_id": dish_id,
                    "tag_id": tag_id
                })

            # Bulk insert dish_tags co 5000 rekordów
            if len(dish_tags_buffer) >= 5000:
                db.insert_bulk("dish_tags", dish_tags_buffer)
                total_dish_tags += len(dish_tags_buffer)
                dish_tags_buffer = []

        if (total_dishes % 1000) == 0:
            logger.info(f"  Wygenerowano {total_dishes} dań...")

    # Insert remaining dish_tags
    if dish_tags_buffer:
        db.insert_bulk("dish_tags", dish_tags_buffer)
        total_dish_tags += len(dish_tags_buffer)

    logger.info(f"✅ Wygenerowano {total_dishes} dań")
    logger.info(f"✅ Przypisano {total_ingredients_links} składników do dań")
    logger.info(f"✅ Dodano {total_photos} zdjęć dań")
    logger.info(f"✅ Przypisano {total_dish_tags} tagów do dań")

def _get_tags_for_dish(archetype: str, spiciness: float, ingredients: list,
                       tag_map: dict, tag_by_category: dict) -> list:
    """
    Określa tagi dla dania na podstawie jego właściwości.

    Args:
        archetype: Typ dania (Pizza, Burger, Sushi, etc.)
        spiciness: Poziom ostrości (0-10)
        ingredients: Lista składników
        tag_map: Mapowanie nazwa_tagu -> tag_id
        tag_by_category: Mapowanie kategoria -> lista (tag_id, tag_name)

    Returns:
        Lista tag_id do przypisania
    """
    tag_ids = set()

    # 1. Mapowanie archetype -> cuisine tag
    archetype_cuisine_map = {
        "Pizza": "Włoska",
        "Pasta": "Włoska",
        "Risotto": "Włoska",
        "Gnocchi": "Włoska",
        "Burger": "Amerykańska",
        "Steak": "Amerykańska",
        "BBQ": "Amerykańska",
        "Sushi": "Japońska",
        "Ramen": "Japońska",
        "Pho": "Wietnamska",
        "Noodles": "Azjatycka",
        "Dim Sum": "Azjatycka",
        "Curry": "Indyjska",
        "Tacos": "Meksykańska",
        "Quesadilla": "Meksykańska",
        "Nachos": "Meksykańska",
        "Kebab": "Bliskowschodnia",
        "Salad": "Śródziemnomorska",
        "Seafood": "Śródziemnomorska",
        "Oysters": "Francuska",
        "Fondue": "Francuska",
        "Soup": "Polska",
        "Vegan": "Śródziemnomorska",
    }

    cuisine_tag = archetype_cuisine_map.get(archetype)
    if cuisine_tag and cuisine_tag in tag_map:
        tag_ids.add(tag_map[cuisine_tag])

    # 2. Mapowanie spiciness -> spice tag
    if spiciness <= 1:
        spice_tag = "Łagodne"
    elif spiciness <= 3:
        spice_tag = "Średnio ostre"
    elif spiciness <= 6:
        spice_tag = "Ostre"
    else:
        spice_tag = "Bardzo ostre"

    if spice_tag in tag_map:
        tag_ids.add(tag_map[spice_tag])

    # 3. Tagi dietetyczne na podstawie składników
    ingredients_lower = [i.lower() for i in ingredients]

    # Wegańskie - jeśli nie ma mięsa, nabiału, jaj
    meat_keywords = ['mięso', 'kurczak', 'wołowina', 'wieprzowina', 'boczek', 'szynka',
                     'kiełbasa', 'beef', 'chicken', 'pork', 'bacon', 'ham', 'sausage',
                     'ryba', 'fish', 'łosoś', 'salmon', 'tuńczyk', 'tuna', 'krewetki', 'shrimp']
    dairy_keywords = ['ser', 'cheese', 'mleko', 'milk', 'śmietana', 'cream', 'masło', 'butter']
    egg_keywords = ['jajko', 'egg', 'jaja']

    has_meat = any(any(kw in ing for kw in meat_keywords) for ing in ingredients_lower)
    has_dairy = any(any(kw in ing for kw in dairy_keywords) for ing in ingredients_lower)
    has_eggs = any(any(kw in ing for kw in egg_keywords) for ing in ingredients_lower)

    if not has_meat and not has_dairy and not has_eggs:
        if "Wegańskie" in tag_map:
            tag_ids.add(tag_map["Wegańskie"])
    elif not has_meat:
        if "Wegetariańskie" in tag_map:
            tag_ids.add(tag_map["Wegetariańskie"])

    # Bezglutenowe
    gluten_keywords = ['mąka', 'flour', 'chleb', 'bread', 'makaron', 'pasta', 'pszenica', 'wheat']
    has_gluten = any(any(kw in ing for kw in gluten_keywords) for ing in ingredients_lower)
    if not has_gluten and "Bezglutenowe" in tag_map:
        tag_ids.add(tag_map["Bezglutenowe"])

    # 4. Losowo dodaj 1-2 dodatkowe tagi (occasion/feature/mood)
    optional_categories = ['occasion', 'feature', 'mood']
    for category in random.sample(optional_categories, k=random.randint(1, 2)):
        if category in tag_by_category and tag_by_category[category]:
            random_tag = random.choice(tag_by_category[category])
            tag_ids.add(random_tag[0])  # tag_id

    return list(tag_ids)

def _select_dishes_for_menu(menu_blueprint: str, dish_variants: dict) -> list:
    """
    Wybiera dania odpowiednie dla danego typu menu

    FIXED: Handles nested JSON structure:
    {"Pizza": {"base_price": {...}, "variants": {"Margherita": {"ingredients": [...]}}}}
    """
    # FIXED: Build flat variant list from nested structure
    all_variants = []
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue

        # Get base price for this category
        base_price_info = category_data.get("base_price", {"mean": 35.0, "stdev": 5.0})
        base_price = base_price_info.get("mean", 35.0)

        variants = category_data.get("variants", {})
        for variant_name, variant_data in variants.items():
            if not isinstance(variant_data, dict):
                continue

            # Calculate final price
            price_mult_info = variant_data.get("price_multiplier", {"mean": 1.0})
            price_multiplier = price_mult_info.get("mean", 1.0)
            final_price = round(base_price * price_multiplier, 2)

            # Get spiciness
            spiciness_info = variant_data.get("spiciness", {"mean": 0})
            spiciness = spiciness_info.get("mean", 0)

            all_variants.append({
                "name": variant_name,
                "archetype": category_name,  # Category name becomes archetype
                "price": final_price,
                "ingredients": variant_data.get("ingredients", []),
                "tags": ["spicy"] if spiciness > 2 else [],
                "spiciness": spiciness
            })

    # Menu mappings (archetype -> menu types)
    menu_mappings = {
        "pizza_menu": ["Pizza"],
        "burger_menu": ["Burger"],
        "sushi_menu": ["Sushi"],
        "asian_menu": ["Ramen", "Noodles", "Dim Sum", "Pho", "Curry"],
        "steak_menu": ["Steak", "BBQ"],
        "vegan_menu": ["Vegan", "Salad"],
        "mexican_menu": ["Tacos", "Quesadilla", "Nachos", "Kebab"],
        "italian_menu": ["Pizza", "Pasta", "Risotto", "Gnocchi"],
        "french_menu": ["Steak", "Soup", "Fondue"],
        "seafood_menu": ["Seafood", "Sushi", "Oysters"],
        "general_menu": ["Pizza", "Burger", "Pasta", "Salad", "Kebab"]
    }

    archetypes = menu_mappings.get(menu_blueprint, ["Pizza", "Burger", "Pasta"])

    # Filter variants by archetype
    matching_dishes = [v for v in all_variants if v.get("archetype") in archetypes]

    # If no matches, use all variants as fallback
    if not matching_dishes:
        matching_dishes = all_variants

    # Select 10-20 dishes
    num_dishes = random.randint(10, 20)

    if len(matching_dishes) > num_dishes:
        return random.sample(matching_dishes, num_dishes)
    else:
        return matching_dishes
