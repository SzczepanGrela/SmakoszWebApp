import json
import logging
import random
import time
import uuid
from pathlib import Path

from tqdm import tqdm

from algorithms.preference_calculator import DIMENSIONS, add_dish_variance, apply_restaurant_bias, merge_vectors
from config import GENERATION_CONFIG, get_connection_params
from data_access import RestaurantDAO
from utils.blueprint_loader import BlueprintLoader
from utils.db_connection import DatabaseConnection
from utils.dish_helpers import generate_dish_calories, generate_dish_description
from utils.distributions import sample_beta, zipf_distribution
from utils.photo_pools import PhotoPools

logger = logging.getLogger(__name__)

def generate_dish_vector(archetype_name: str, archetype_data: dict, variant_name: str, variant_data: dict, restaurant_modifiers: dict) -> tuple:
    base_data = archetype_data.get("archetype_base", {})
    base_chars = base_data.get("characteristics", {})
    base_weights = base_data.get("default_weights", None)

    variant_chars = variant_data.get("characteristics", {})
    variant_weights = variant_data.get("weights", None)

    characteristics = merge_vectors(base_chars, variant_chars)

    if not characteristics:
        characteristics = {dim: round(random.uniform(0.1, 0.9), 2) for dim in DIMENSIONS}
        if "ostry" in variant_name.lower() or "pikant" in variant_name.lower():
            characteristics["flavor_spiciness"] = round(random.uniform(0.7, 0.9), 2)
        if "słod" in variant_name.lower() or "deser" in variant_name.lower():
            characteristics["flavor_sweetness"] = round(random.uniform(0.7, 0.9), 2)

    if archetype_name != "Inne" and not base_chars.get("_neutral", False):
        characteristics = apply_restaurant_bias(characteristics, archetype_name, restaurant_modifiers)

    characteristics = add_dish_variance(characteristics, variance=0.25)

    # Mod 13: Fallback to base weights if variant weights are missing
    weights = variant_weights if variant_weights is not None else base_weights

    return (characteristics, weights)

def generate_dishes(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating dishes...")

    if cleanup:
        logger.info("Cleaning up old Phase 3 data...")
        try:
            db.execute_query("TRUNCATE TABLE dishes RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE dish_ingredients RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE dish_tags RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE dish_variants RESTART IDENTITY CASCADE") # Also clean dictionary

            db.commit()
            logger.info("Cleanup complete.")

        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            raise e

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dishes.json")

    restaurants = RestaurantDAO.get_all_restaurants_for_dishes(db)

    restaurants_list = []
    for row in restaurants:
        restaurants_list.append(
            {
                "restaurant_id": row[0],
                "secret_menu_blueprint": row[1],
                "secret_price_multiplier": row[2],
                "secret_archetype_modifiers": row[3],
                "status": row[4],  # Unpack status
            }
        )

    target_total_dishes = GENERATION_CONFIG["num_dishes"]
    num_restaurants = len(restaurants_list)

    if num_restaurants > 0:
        base_avg_menu_size = 25.0
        scaling_factor = float(target_total_dishes) / (num_restaurants * base_avg_menu_size)  # type: ignore

        logger.info(f"Menu scaling factor: {scaling_factor:.2f}")
    else:
        scaling_factor = 1.0
        logger.warning("No restaurants found, scaling factor = 1.0")

    all_ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM ingredients")
    ingredient_map = {ing_name: id for id, ing_name in all_ingredients}

    # Fetch Menu Sections (Mod 10)
    all_sections = db.fetch_all("SELECT section_id, restaurant_id, section_name FROM menu_sections")
    restaurant_sections_map = {}
    for sec_id, rest_id, sec_name in all_sections:
        if rest_id not in restaurant_sections_map:
            restaurant_sections_map[rest_id] = []
        restaurant_sections_map[rest_id].append({"id": sec_id, "section_name": sec_name})

    # Load Global Config for Section Mapping (Problem 4)
    global_config = loader.load_blueprint("global_config.json")
    dish_section_mapping = global_config.get("DISH_SECTION_MAPPING", {})

    all_tags = db.fetch_all("SELECT tag_id, tag_name, category FROM tags")
    tag_map = {tag_name: tag_id for tag_id, tag_name, _ in all_tags}
    tag_by_category: dict[str, list[tuple[int, str]]] = {}
    for tag_id, tag_name, tag_category in all_tags:
        if tag_category not in tag_by_category:
            tag_by_category[tag_category] = []
        tag_by_category[tag_category].append((tag_id, tag_name))

    # Populate Dish Archetypes Dictionary FIRST
    logger.info("Populating dish archetypes dictionary...")
    
    # Clean up archetypes table too
    db.execute_query("TRUNCATE TABLE dish_archetypes RESTART IDENTITY CASCADE")
    
    unique_archetypes = set()
    for category_name, category_data in dish_variants.items():
        if isinstance(category_data, dict):
            unique_archetypes.add(category_name)
    
    archetype_insert_data = [{"archetype_name": a} for a in sorted(unique_archetypes)]
    if archetype_insert_data:
        db.insert_bulk("dish_archetypes", archetype_insert_data)
    
    # Fetch archetype_id map
    archetype_rows = db.fetch_all("SELECT archetype_id, archetype_name FROM dish_archetypes")
    archetype_map = {name: id for id, name in archetype_rows}
    logger.info(f"Loaded {len(archetype_map)} archetypes.")
    
    # Populate Dish Variants with archetype_id FK
    logger.info("Populating dish variants dictionary...")
    unique_variants = set()
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue
        variants = category_data.get("variants", {})
        for variant_name in variants.keys():
            unique_variants.add((variant_name, category_name))
    
    # Use archetype_id instead of archetype_name
    variant_insert_data = [
        {"variant_name": v, "archetype_id": archetype_map[a]} 
        for v, a in unique_variants 
        if a in archetype_map
    ]

    if variant_insert_data:
        variant_insert_data.sort(key=lambda x: (x['archetype_id'], x['variant_name']))
        db.insert_bulk("dish_variants", variant_insert_data)

    # Fetch variant map with archetype_name for compatibility
    variant_rows = db.fetch_all("""
        SELECT dv.variant_id, dv.variant_name, da.archetype_name 
        FROM dish_variants dv 
        JOIN dish_archetypes da ON dv.archetype_id = da.archetype_id
    """)
    variant_map = {(row[1], row[2]): row[0] for row in variant_rows}
    logger.info(f"Loaded {len(variant_map)} variants.")

    photo_pools = PhotoPools()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0
    total_dish_tags = 0
    dish_tags_buffer = []
    dish_sections_buffer = [] # Buffer for dish_section_assignments

    # Disable Trigger Logic removed as we use statement trigger now

    for restaurant in tqdm(restaurants_list, desc="Generating dishes", unit=" restaurant", mininterval=1.0):
        restaurant_id = restaurant["restaurant_id"]
        menu_blueprint = restaurant["secret_menu_blueprint"]
        price_multiplier = restaurant["secret_price_multiplier"]
        restaurant_status = restaurant.get("status", "active") # Get status
        
        # Mod 3: Get restaurant quality for dish generation
        restaurant_quality_skill = restaurant.get("secret_overall_food_quality", 0.5)

        menu_dishes = _select_dishes_for_menu(menu_blueprint, dish_variants, scaling_factor)

        if not menu_dishes:
            continue

        popularity_scores = zipf_distribution(len(menu_dishes), alpha=1.5)

        restaurant_modifiers_raw = restaurant["secret_archetype_modifiers"]
        if isinstance(restaurant_modifiers_raw, str):
            try:
                restaurant_modifiers = json.loads(restaurant_modifiers_raw)
            except json.JSONDecodeError:
                restaurant_modifiers = {}
        else:
            restaurant_modifiers = restaurant_modifiers_raw or {}
            
        available_sections = restaurant_sections_map.get(restaurant_id, [])

        for i, variant in enumerate(menu_dishes):
            dish_name = variant.get("variant_name", "Danie")
            archetype = variant.get("archetype", "Unknown")
            base_price = variant.get("price", 35.0)
            
            # Get Variant ID
            variant_id = variant_map.get((dish_name, archetype))
            if not variant_id:
                logger.warning(f"Variant ID not found for {dish_name} ({archetype})")
                continue

            archetype_full_data = dish_variants.get(archetype, {})
            variant_full_data = archetype_full_data.get("variants", {}).get(dish_name, {})

            characteristics_vec, weights_vec = generate_dish_vector(
                archetype_name=archetype,
                archetype_data=archetype_full_data,
                variant_name=dish_name,
                variant_data=variant_full_data,
                restaurant_modifiers=restaurant_modifiers
            )

            secret_base_price = base_price

            final_price = base_price * price_multiplier * random.gauss(1.0, 0.1)
            price = round(max(10.0, final_price))

            # Mod 3: Secret quality calculation
            base_potential = sample_beta(5, 2, 0.3, 0.95)
            secret_quality = (base_potential * 0.3) + (restaurant_quality_skill * 0.7)
            secret_quality = max(0.1, min(1.0, secret_quality))

            secret_spiciness_val = characteristics_vec.get("flavor_spiciness", 0.0) * 10.0

            ingredients = variant.get("ingredients", [])

            description = generate_dish_description(
                dish_name=dish_name,
                archetype=archetype,
                ingredients=ingredients,
                quality=secret_quality,
                spiciness=secret_spiciness_val,
            )

            primary_photo_metadata = photo_pools.get_dish_photo(archetype, dish_name, restaurant_id)

            secret_richness_val = characteristics_vec.get("physics_richness", 0.5)

            # Mod 12: Calories (no price arg)
            calories = generate_dish_calories(archetype=archetype, price=0, richness=secret_richness_val)

            dish_tag_ids = _get_tags_for_dish(
                archetype=archetype,
                spiciness=secret_spiciness_val,
                ingredients=ingredients,
                tag_map=tag_map,
                tag_by_category=tag_by_category,
            )

            is_spicy = secret_spiciness_val > 2.0
            is_vegan = False
            if "Wegańskie" in tag_map and tag_map["Wegańskie"] in dish_tag_ids:
                is_vegan = True
            
            # Check availability based on restaurant status
            is_available = True
            if restaurant_status != 'active':
                is_available = False

            dish_data = {
                "public_id": str(uuid.uuid4()),
                "restaurant_id": restaurant_id,
                "variant_id": variant_id, # NEW
                "dish_name": dish_name,
                # secret_archetype REMOVED
                # secret_variant_name REMOVED
                "price": price,
                "description": description,
                "is_vegan": is_vegan,
                "is_spicy": is_spicy,
                "ingredients_json": json.dumps(ingredients),
                "is_available": is_available, # Use dynamic value
                "secret_base_price": round(secret_base_price, 2),
                "secret_quality": round(secret_quality, 3),
                "secret_characteristics_vector": json.dumps(characteristics_vec),
                "secret_penalty_vector": json.dumps(weights_vec) if weights_vec else None,
                "secret_popularity_factor": round(popularity_scores[i], 4),
                "image_url": primary_photo_metadata["url"],
                "image_blurhash": primary_photo_metadata.get("blurhash"),
                "calories": calories,
                "created_at": restaurant.get("created_at"),  # Use restaurant's created_at
            }

            dish_id = db.insert_single("dishes", dish_data)
            total_dishes += 1
            
            # Mod 10 & Problem 4: Robust Section Assignment
            assigned_sections = []
            if available_sections:
                # 1. Look for preferred sections from config
                preferred_keywords = dish_section_mapping.get(archetype, [])
                if not preferred_keywords:
                    preferred_keywords = ["Dania Główne"]

                for sec in available_sections:
                    sec_name_lower = sec["section_name"].lower()
                    for keyword in preferred_keywords:
                        if keyword.lower() in sec_name_lower:
                            assigned_sections.append(sec["id"])
                            break 
                    if assigned_sections: break

                # 2. Fuzzy match
                if not assigned_sections:
                        for sec in available_sections:
                            if archetype.lower() in sec["section_name"].lower():
                                assigned_sections.append(sec["id"])
                                break
                
                # 3. Fallback
                if not assigned_sections:
                    assigned_sections.append(random.choice(available_sections)["id"])
            
            for sec_id in set(assigned_sections):
                dish_sections_buffer.append({"dish_id": dish_id, "section_id": sec_id})

            ingredient_links = []
            for ingredient_name in ingredients:
                if ingredient_name in ingredient_map:
                    ingredient_links.append({"dish_id": dish_id, "ingredient_id": ingredient_map[ingredient_name]})

            if ingredient_links:
                db.insert_bulk("dish_ingredients", ingredient_links)
                total_ingredients_links += len(ingredient_links)

            db.insert_single(
                "media_assets",
                {
                    "public_id": str(uuid.uuid4()),
                    "entity_type": "dish",
                    "entity_id": dish_id,
                    "url": primary_photo_metadata["url"],
                    "blurhash": primary_photo_metadata["blurhash"],
                    "width": primary_photo_metadata["width"],
                    "height": primary_photo_metadata["height"],
                    "is_primary": True,
                    "status": "approved",
                },
            )
            total_photos += 1

            for tag_id in dish_tag_ids:
                dish_tags_buffer.append({"dish_id": dish_id, "tag_id": tag_id})

            if len(dish_tags_buffer) >= 5000:
                db.insert_bulk("dish_tags", dish_tags_buffer)
                total_dish_tags += len(dish_tags_buffer)
                dish_tags_buffer = []
            
            if len(dish_sections_buffer) >= 5000:
                db.insert_bulk("dish_section_assignments", dish_sections_buffer)
                dish_sections_buffer = []

    if dish_tags_buffer:
        db.insert_bulk("dish_tags", dish_tags_buffer)
        total_dish_tags += len(dish_tags_buffer)
        
    if dish_sections_buffer:
        db.insert_bulk("dish_section_assignments", dish_sections_buffer)

    duration = time.time() - start_time
    logger.info(f"Generated {total_dishes} dishes in {duration:.2f}s")
    logger.info(f"  - Linked {total_ingredients_links} ingredients")
    logger.info(f"  - Added {total_photos} photos")
    logger.info(f"  - Assigned {total_dish_tags} tags")

def _get_tags_for_dish(
    archetype: str, spiciness: float, ingredients: list, tag_map: dict, tag_by_category: dict
) -> list:
    tag_ids = set()

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

    ingredients_lower = [i.lower() for i in ingredients]

    meat_keywords = [
        "mięso",
        "kurczak",
        "wołowina",
        "wieprzowina",
        "boczek",
        "szynka",
        "kiełbasa",
        "beef",
        "chicken",
        "pork",
        "bacon",
        "ham",
        "sausage",
        "ryba",
        "fish",
        "łosoś",
        "salmon",
        "tuńczyk",
        "tuna",
        "krewetki",
        "shrimp",
    ]
    dairy_keywords = ["ser", "cheese", "mleko", "milk", "śmietana", "cream", "masło", "butter"]
    egg_keywords = ["jajko", "egg", "jaja"]

    if (
        not any(any(kw in ing for kw in meat_keywords) for ing in ingredients_lower)
        and not any(any(kw in ing for kw in dairy_keywords) for ing in ingredients_lower)
        and not any(any(kw in ing for kw in egg_keywords) for ing in ingredients_lower)
    ):
        if "Wegańskie" in tag_map:
            tag_ids.add(tag_map["Wegańskie"])
    elif not any(any(kw in ing for kw in meat_keywords) for ing in ingredients_lower) and "Wegetariańskie" in tag_map:
        tag_ids.add(tag_map["Wegetariańskie"])

    gluten_keywords = ["mąka", "flour", "chleb", "bread", "makaron", "pasta", "pszenica", "wheat"]
    has_gluten = any(any(kw in ing for kw in gluten_keywords) for ing in ingredients_lower)
    if not has_gluten and "Bezglutenowe" in tag_map:
        tag_ids.add(tag_map["Bezglutenowe"])

    optional_categories = ["occasion", "feature", "mood"]
    for category in random.sample(optional_categories, k=random.randint(1, 2)):
        if category in tag_by_category and tag_by_category[category]:
            random_tag = random.choice(tag_by_category[category])
            tag_ids.add(random_tag[0])

    return list(tag_ids)

def _select_dishes_for_menu(menu_blueprint: str, dish_variants: dict, scaling_factor: float = 1.0) -> list:
    all_variants = []
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue

        base_price_info = category_data.get("base_price", {"mean": 35.0, "stdev": 5.0})
        base_price = base_price_info.get("mean", 35.0)

        variants = category_data.get("variants", {})
        for variant_name, variant_data in variants.items():
            if not isinstance(variant_data, dict):
                continue

            price_mult_info = variant_data.get("price_multiplier", {"mean": 1.0})
            price_multiplier = price_mult_info.get("mean", 1.0)
            final_price = round(base_price * price_multiplier, 2)

            # Derive spiciness from characteristics vector (0-1 scale -> 0-10)
            characteristics = variant_data.get("characteristics", {})
            flavor_spiciness = characteristics.get("flavor_spiciness", 0.0)
            spiciness = flavor_spiciness * 10.0  # Convert to 0-10 scale

            all_variants.append(
                {
                    "variant_name": variant_name,
                    "archetype": category_name,
                    "price": final_price,
                    "ingredients": variant_data.get("ingredients", []),
                    "tags": ["spicy"] if spiciness > 2 else [],
                    "spiciness": spiciness,
                }
            )

    menu_configs = {
        "Pizzeria": {"archetypes": ["Pizza", "Pasta", "Salad", "Deser"], "mean": 25, "sigma": 5},
        "Burger Bar": {"archetypes": ["Burger", "Steak", "Salad"], "mean": 15, "sigma": 3},
        "Sushi Bar": {"archetypes": ["Sushi", "Soup", "Salad"], "mean": 40, "sigma": 8},
        "Asian Fusion": {
            "archetypes": ["Ramen", "Noodles", "Dim Sum", "Pho", "Curry", "Sushi", "Kanapka", "Danie Azjatyckie"],
            "mean": 35,
            "sigma": 7,
        },
        "Steakhouse": {"archetypes": ["Steak", "BBQ", "Burger", "Salad"], "mean": 20, "sigma": 4},
        "Vegan Cafe": {"archetypes": ["Vegan", "Salad", "Soup", "Smoothie Bowl"], "mean": 22, "sigma": 5},
        "Mexican Restaurant": {"archetypes": ["Tacos", "Quesadilla", "Nachos", "Burrito"], "mean": 28, "sigma": 6},
        "Italian Restaurant": {"archetypes": ["Pizza", "Pasta", "Risotto", "Gnocchi", "Deser"], "mean": 30, "sigma": 6},
        "French Bistro": {"archetypes": ["Steak", "Soup", "Fondue", "Deser"], "mean": 20, "sigma": 4},
        "Seafood Restaurant": {"archetypes": ["Seafood", "Sushi", "Oysters", "Fish"], "mean": 25, "sigma": 5},
        "General": {"archetypes": ["Pizza", "Burger", "Pasta", "Salad", "Kebab", "Zupa"], "mean": 20, "sigma": 5},
        
        # New Profiles
        "Kebab Place": {"archetypes": ["Kebab", "Salad", "Frytki", "Napój Bezalkoholowy"], "mean": 12, "sigma": 3},
        "Polish Restaurant": {"archetypes": ["Danie Polskie", "Zupa", "Pierogi", "Deser", "Piwo"], "mean": 25, "sigma": 5},
        "Indian Restaurant": {"archetypes": ["Curry", "Naan", "Ryż", "Zupa"], "mean": 30, "sigma": 6},
        "Greek Taverna": {"archetypes": ["Danie Greckie", "Sałatka", "Owoce Morza", "Wino"], "mean": 28, "sigma": 5},
        "BBQ Smokehouse": {"archetypes": ["Dania BBQ", "Stek", "Burger", "Frytki", "Piwo"], "mean": 25, "sigma": 5},
        "Korean Restaurant": {"archetypes": ["Danie Koreańskie", "Zupa", "Ryż", "Danie Azjatyckie"], "mean": 26, "sigma": 6},
        "Tapas Bar": {"archetypes": ["Tapas", "Wino", "Owoce Morza", "Przystawka"], "mean": 18, "sigma": 4},
        "American Diner": {"archetypes": ["Burger", "Milkshake", "Naleśniki", "Frytki", "Kawa"], "mean": 20, "sigma": 5},
        "German Pub": {"archetypes": ["Danie Niemieckie", "Kiełbasa", "Piwo", "Precel"], "mean": 22, "sigma": 4},
        "Middle Eastern": {"archetypes": ["Danie Bliskowschodnie", "Kebab", "Hummus", "Falafel"], "mean": 24, "sigma": 5},
        "Ice Cream Shop": {"archetypes": ["Lody", "Sorbet", "Deser", "Milkshake", "Kawa", "Gorąca Czekolada"], "mean": 15, "sigma": 4},
        "Sandwich Shop": {"archetypes": ["Kanapka", "Panini", "Sałatka", "Kawa", "Napój Bezalkoholowy"], "mean": 12, "sigma": 3},
        "Cafe": {"archetypes": ["Kawa", "Herbata", "Deser", "Ciasto", "Kanapka"], "mean": 15, "sigma": 4},
        "Fine Dining": {"archetypes": ["Stek", "Owoce Morza", "Wino", "Deser", "Danie Francuskie"], "mean": 45, "sigma": 10},
    }

    config = menu_configs.get(menu_blueprint, menu_configs["General"])
    archetypes = list(config["archetypes"])  # type: ignore

    base_mean = float(config["mean"])  # type: ignore
    base_sigma = float(config["sigma"])  # type: ignore

    target_mean = base_mean * scaling_factor
    target_sigma = base_sigma * scaling_factor

    target_count = int(random.gauss(target_mean, target_sigma))

    target_count = max(4, target_count)

    matching_dishes = [v for v in all_variants if v.get("archetype") in archetypes]

    if not matching_dishes:
        matching_dishes = all_variants

    if len(matching_dishes) > target_count:
        return random.sample(matching_dishes, target_count)
    else:
        return matching_dishes

if __name__ == "__main__":
    import os
    import sys

    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

    from config import get_connection_params

    logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

    try:
        connection_params = get_connection_params()

        with DatabaseConnection(connection_params) as db:
            generate_dishes(db, blueprints_dir="blueprints")
            logger.info("Phase 3 completed.")

    except Exception as e:
        logger.error(f"Error: {e}", exc_info=True)
        sys.exit(1)