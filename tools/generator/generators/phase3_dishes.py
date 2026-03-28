import json
import logging
import random
import time
import uuid

from tqdm import tqdm

from algorithms.preference_calculator import DIMENSIONS, add_dish_variance, apply_restaurant_bias, merge_vectors
from config import GENERATION_CONFIG
from data_access import RestaurantDAO
from generators.constants import MENU_BLUEPRINTS
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_loader import BlueprintLoader
from utils.db_connection import DatabaseConnection
from utils.dish_helpers import generate_dish_calories, generate_dish_description
from utils.distributions import sample_beta, zipf_distribution
from utils.photo_pools import PhotoPools
from utils.text_generator import slugify

logger = logging.getLogger(__name__)

def _unique_slug(dish_name: str, used_slugs: set[str]) -> str:
    base = slugify(dish_name)
    slug = base
    counter = 2
    while slug in used_slugs:
        slug = f"{base}-{counter}"
        counter += 1
    used_slugs.add(slug)
    return slug

def generate_dish_vector(
    archetype_name: str, archetype_data: dict, variant_name: str, variant_data: dict, restaurant_modifiers: dict
) -> tuple:
    base_data = archetype_data.get("archetype_base", {})
    base_chars = base_data.get("characteristics", {})
    base_weights = base_data.get("default_weights", None)

    variant_chars = variant_data.get("characteristics", {})
    variant_weights = variant_data.get("weights")

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
            db.execute_query("TRUNCATE TABLE dish_variants RESTART IDENTITY CASCADE")

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
                "status": row[4],
                "created_at": row[5],
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

    all_sections = db.fetch_all("SELECT section_id, restaurant_id, section_name FROM menu_sections")
    restaurant_sections_map: dict = {}
    for sec_id, rest_id, sec_name in all_sections:
        if rest_id not in restaurant_sections_map:
            restaurant_sections_map[rest_id] = []
        restaurant_sections_map[rest_id].append({"id": sec_id, "section_name": sec_name})

    global_config = loader.load_blueprint("global_config.json")
    dish_section_mapping = global_config.get("DISH_SECTION_MAPPING", {})

    all_tags = db.fetch_all("SELECT tag_id, tag_name, category FROM tags")
    tag_map = {tag_name: tag_id for tag_id, tag_name, _ in all_tags}
    tag_by_category: dict[str, list[tuple[int, str]]] = {}
    for tag_id, tag_name, tag_category in all_tags:
        if tag_category not in tag_by_category:
            tag_by_category[tag_category] = []
        tag_by_category[tag_category].append((tag_id, tag_name))

    logger.info("Populating dish archetypes dictionary...")

    db.execute_query("TRUNCATE TABLE dish_archetypes RESTART IDENTITY CASCADE")

    unique_archetypes = set()
    for category_name, category_data in dish_variants.items():
        if isinstance(category_data, dict):
            unique_archetypes.add(category_name)

    archetype_insert_data = [{"archetype_name": a} for a in sorted(unique_archetypes)]
    if archetype_insert_data:
        db.insert_bulk("dish_archetypes", archetype_insert_data)

    archetype_rows = db.fetch_all("SELECT archetype_id, archetype_name FROM dish_archetypes")
    archetype_map = {name: id for id, name in archetype_rows}
    logger.info(f"Loaded {len(archetype_map)} archetypes.")

    logger.info("Populating dish variants dictionary...")
    unique_variants = set()
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue
        variants = category_data.get("variants", {})
        for variant_name in variants:
            unique_variants.add((variant_name, category_name))

    variant_insert_data = [
        {"variant_name": v, "archetype_id": archetype_map[a]} for v, a in unique_variants if a in archetype_map
    ]

    if variant_insert_data:
        variant_insert_data.sort(key=lambda x: (x["archetype_id"], x["variant_name"]))
        db.insert_bulk("dish_variants", variant_insert_data)

    variant_rows = db.fetch_all("""
        SELECT dv.variant_id, dv.variant_name, da.archetype_name
        FROM dish_variants dv
        JOIN dish_archetypes da ON dv.archetype_id = da.archetype_id
    """)
    variant_map = {(row[1], row[2]): row[0] for row in variant_rows}
    logger.info(f"Loaded {len(variant_map)} variants.")

    photo_pools = PhotoPools()

    used_slugs: set[str] = set()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0
    total_dish_tags = 0
    dish_tags_buffer = []
    dish_sections_buffer = []

    for restaurant in tqdm(restaurants_list, desc="Generating dishes", unit=" restaurant", mininterval=1.0):
        restaurant_id = restaurant["restaurant_id"]
        menu_blueprint = restaurant["secret_menu_blueprint"]
        price_multiplier = restaurant["secret_price_multiplier"]
        restaurant_status = restaurant.get("status", "active")
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
                restaurant_modifiers=restaurant_modifiers,
            )

            secret_base_price = base_price

            final_price = base_price * price_multiplier * random.gauss(1.0, 0.1)
            price = round(max(10.0, final_price))

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

            calories = generate_dish_calories(archetype=archetype, price=0, richness=secret_richness_val)

            dish_tag_ids = _get_tags_for_dish(
                archetype=archetype,
                spiciness=secret_spiciness_val,
                ingredients=ingredients,
                tag_map=tag_map,
                tag_by_category=tag_by_category,
            )

            is_spicy = secret_spiciness_val > 6.0
            is_vegan = False
            if "Wegańskie" in tag_map and tag_map["Wegańskie"] in dish_tag_ids:
                is_vegan = True

            is_vegetarian = is_vegan
            if not is_vegetarian and "Wegetariańskie" in tag_map and tag_map["Wegetariańskie"] in dish_tag_ids:
                is_vegetarian = True

            is_gluten_free = "Bezglutenowe" in tag_map and tag_map["Bezglutenowe"] in dish_tag_ids
            is_lactose_free = is_vegan or ("Bez laktozy" in tag_map and tag_map["Bez laktozy"] in dish_tag_ids)

            is_available = True
            if restaurant_status != "active":
                is_available = False

            dish_data = {
                "public_id": str(uuid.uuid4()),
                "restaurant_id": restaurant_id,
                "secret_variant_id": variant_id,
                "dish_name": dish_name,
                "slug": _unique_slug(dish_name, used_slugs),
                "price": price,
                "description": description,
                "is_vegetarian": is_vegetarian,
                "is_vegan": is_vegan,
                "is_gluten_free": is_gluten_free,
                "is_lactose_free": is_lactose_free,
                "is_spicy": is_spicy,
                "ingredients_json": json.dumps([i.replace("_", " ") for i in ingredients]),
                "is_available": is_available,
                "secret_base_price": round(secret_base_price, 2),
                "secret_quality": round(secret_quality, 3),
                "secret_characteristics_vector": json.dumps(characteristics_vec),
                "secret_penalty_vector": json.dumps(weights_vec) if weights_vec else None,
                "secret_popularity_factor": round(popularity_scores[i], 4),
                "image_url": primary_photo_metadata["url"],
                "image_blurhash": primary_photo_metadata.get("blurhash"),
                "calories": calories,
                "created_at": restaurant.get("created_at"),
            }

            dish_id = db.insert_single("dishes", dish_data)
            total_dishes += 1

            assigned_sections = []
            if available_sections:
                preferred_keywords = dish_section_mapping.get(archetype, [])
                if not preferred_keywords:
                    preferred_keywords = ["Dania Główne"]

                for sec in available_sections:
                    sec_name_lower = sec["section_name"].lower()
                    for keyword in preferred_keywords:
                        if keyword.lower() in sec_name_lower:
                            assigned_sections.append(sec["id"])
                            break
                    if assigned_sections:
                        break

                if not assigned_sections:
                    for sec in available_sections:
                        if archetype.lower() in sec["section_name"].lower():
                            assigned_sections.append(sec["id"])
                            break

                if not assigned_sections:
                    assigned_sections.append(random.choice(available_sections)["id"])

            for sec_id in set(assigned_sections):
                dish_sections_buffer.append(
                    {"dish_id": dish_id, "section_id": sec_id, "created_at": restaurant.get("created_at")}
                )

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

    if spiciness >= 4:
        if spiciness <= 6:
            spice_tag = "Średnio ostre"
        elif spiciness <= 8:
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
        "steak",
        "befsztyk",
        "polędwica",
        "antrykot",
        "rostbef",
        "kaczka",
        "duck",
        "indyk",
        "turkey",
        "jagnięcina",
        "lamb",
        "cielęcina",
        "veal",
        "dziczyzna",
        "królik",
        "rabbit",
        "wątroba",
        "liver",
        "smalec",
        "lard",
        "słonina",
        "pepperoni",
        "salami",
        "mortadela",
        "parówka",
        "flaki",
        "żeberka",
        "ribs",
        "skrzydełka",
        "wings",
        "nuggets",
        "kotlet",
        "schab",
        "karkówka",
        "łopatka",
        "bekon",
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

    gluten_keywords = [
        "mąka", "flour", "chleb", "bread", "makaron", "pasta", "pszenica", "wheat",
        "ciasto", "bułka", "tortilla", "pita", "naleśnik", "pierogi", "kluski",
        "focaccia", "ravioli", "wonton", "noodle",
    ]
    has_gluten = any(any(kw in ing for kw in gluten_keywords) for ing in ingredients_lower)
    if not has_gluten and "Bezglutenowe" in tag_map:
        tag_ids.add(tag_map["Bezglutenowe"])

    optional_categories = ["occasion", "feature", "mood"]
    for category in random.sample(optional_categories, k=random.randint(1, 2)):
        if tag_by_category.get(category):
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

            characteristics = variant_data.get("characteristics", {})
            flavor_spiciness = characteristics.get("flavor_spiciness", 0.0)
            spiciness = flavor_spiciness * 10.0

            all_variants.append(
                {
                    "variant_name": variant_name,
                    "archetype": category_name,
                    "price": final_price,
                    "ingredients": variant_data.get("ingredients", []),
                    "tags": ["spicy"] if spiciness > 6 else [],
                    "spiciness": spiciness,
                }
            )

    config = MENU_BLUEPRINTS.get(menu_blueprint, MENU_BLUEPRINTS["General"])
    archetypes = list(config["archetypes"])

    base_mean = float(config["mean"])
    base_sigma = float(config["sigma"])

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

class DishesPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase3_dishes",
            display_name="Dishes Generation",
            dependencies=["phase1_ingredients", "phase2_restaurants"],
            required_tables=["dishes", "dish_variants", "dish_ingredients"],
            cleanup_tables=["dishes", "dish_variants", "dish_ingredients", "dish_tags"],
            estimated_duration=60,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Phase 3: Generating dishes...")

        try:
            generate_dishes(context.db, blueprints_dir=self.blueprints_dir, cleanup=False)

            dishes_count = context.db.fetch_val("SELECT COUNT(*) FROM dishes")
            variants_count = context.db.fetch_val("SELECT COUNT(*) FROM dish_variants")
            ingredients_count = context.db.fetch_val("SELECT COUNT(*) FROM dish_ingredients")

            duration = time.time() - start_time
            logger.info(
                f"[OK] Generated {dishes_count} dishes with "
                f"{variants_count} variants and "
                f"{ingredients_count} ingredient mappings in {duration:.2f}s"
            )

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={
                    "dishes": dishes_count,
                    "dish_variants": variants_count,
                    "dish_ingredients": ingredients_count,
                },
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Dishes generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
