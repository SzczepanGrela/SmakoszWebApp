import json
import logging
import random
import time
import uuid

from tqdm import tqdm

from algorithms.preference_calculator import DIMENSIONS, add_dish_variance, apply_restaurant_bias
from config import GENERATION_CONFIG
from data_access import RestaurantDAO
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_db import BlueprintDB
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
    archetype_name: str, characteristics: dict, weights: dict | None, restaurant_modifiers: dict
) -> tuple:
    if not characteristics:
        characteristics = {dim: round(random.uniform(0.1, 0.9), 2) for dim in DIMENSIONS}

    characteristics = apply_restaurant_bias(characteristics, archetype_name, restaurant_modifiers)
    characteristics = add_dish_variance(characteristics, variance=0.25)

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

    bdb = BlueprintDB()

    all_bdb_variants = bdb.get_all_variants_with_details()
    variant_ingredients_cache = {}
    for v in all_bdb_variants:
        variant_ingredients_cache[v["id"]] = bdb.get_variant_ingredients(v["id"])

    restaurants_objs = RestaurantDAO.get_all_restaurants_for_dishes(db)

    restaurants_list = [
        {
            "restaurant_id": r.restaurant_id,
            "cuisine_type": r.cuisine_type,
            "secret_price_multiplier": r.secret_price_multiplier,
            "secret_archetype_modifiers": r.secret_archetype_modifiers,
            "status": r.status,
            "created_at": r.created_at,
        }
        for r in restaurants_objs
    ]

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

    ingredient_flags_cache = {ing["name"]: ing for ing in bdb.get_all_ingredients()}

    all_tags = db.fetch_all("SELECT tag_id, tag_name, category FROM tags")
    tag_map = {tag_name: tag_id for tag_id, tag_name, _ in all_tags}
    tag_by_category: dict[str, list[tuple[int, str]]] = {}
    for tag_id, tag_name, tag_category in all_tags:
        if tag_category not in tag_by_category:
            tag_by_category[tag_category] = []
        tag_by_category[tag_category].append((tag_id, tag_name))

    logger.info("Populating dish archetypes dictionary...")

    db.execute_query("TRUNCATE TABLE dish_archetypes RESTART IDENTITY CASCADE")

    archetype_names = bdb.get_archetype_names()
    archetype_insert_data = [{"archetype_name": a} for a in archetype_names]
    if archetype_insert_data:
        db.insert_bulk("dish_archetypes", archetype_insert_data)

    archetype_rows = db.fetch_all("SELECT archetype_id, archetype_name FROM dish_archetypes")
    archetype_map = {name: id for id, name in archetype_rows}
    logger.info(f"Loaded {len(archetype_map)} archetypes.")

    logger.info("Populating dish variants dictionary...")
    variant_insert_data = []
    for v in all_bdb_variants:
        if v["archetype_name"] in archetype_map:
            variant_insert_data.append(
                {"variant_name": v["name"], "archetype_id": archetype_map[v["archetype_name"]]}
            )

    variant_insert_data.sort(key=lambda x: (x["archetype_id"], x["variant_name"]))
    if variant_insert_data:
        db.insert_bulk("dish_variants", variant_insert_data)

    variant_rows = db.fetch_all("""
        SELECT dv.variant_id, dv.variant_name, da.archetype_name
        FROM dish_variants dv
        JOIN dish_archetypes da ON dv.archetype_id = da.archetype_id
    """)
    variant_map = {(row[1], row[2]): row[0] for row in variant_rows}
    logger.info(f"Loaded {len(variant_map)} variants.")

    bdb_variant_lookup = {}
    for v in all_bdb_variants:
        bdb_variant_lookup[(v["name"], v["archetype_name"])] = v

    photo_pools = PhotoPools()

    used_slugs: set[str] = set()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0
    total_dish_tags = 0
    dish_tags_buffer = []
    dish_sections_buffer = []
    ingredients_buffer = []
    photos_buffer = []

    DISH_BATCH_SIZE = 500
    dish_buffer = []
    dish_meta_buffer: list[dict] = []

    def _flush_dish_buffer():
        nonlocal total_dishes, total_ingredients_links, total_photos, total_dish_tags

        if not dish_buffer:
            return

        dish_ids = db.insert_bulk_returning("dishes", dish_buffer, "dish_id")
        total_dishes += len(dish_ids)

        for idx, dish_id in enumerate(dish_ids):
            meta = dish_meta_buffer[idx]

            for sec_id in meta["section_ids"]:
                dish_sections_buffer.append(
                    {"dish_id": dish_id, "section_id": sec_id, "created_at": meta["created_at"]}
                )

            for ingredient_name in meta["ingredients"]:
                if ingredient_name in ingredient_map:
                    ingredients_buffer.append({"dish_id": dish_id, "ingredient_id": ingredient_map[ingredient_name]})

            photos_buffer.append(
                {
                    "public_id": str(uuid.uuid4()),
                    "entity_type": "dish",
                    "entity_id": dish_id,
                    "url": meta["photo"]["url"],
                    "blurhash": meta["photo"]["blurhash"],
                    "width": meta["photo"]["width"],
                    "height": meta["photo"]["height"],
                    "is_primary": True,
                    "status": "approved",
                }
            )

            for tag_id in meta["tag_ids"]:
                dish_tags_buffer.append({"dish_id": dish_id, "tag_id": tag_id})

        if len(ingredients_buffer) >= 5000:
            db.insert_bulk("dish_ingredients", ingredients_buffer)
            total_ingredients_links += len(ingredients_buffer)
            ingredients_buffer.clear()

        if len(photos_buffer) >= 5000:
            db.insert_bulk("media_assets", photos_buffer)
            total_photos += len(photos_buffer)
            photos_buffer.clear()

        if len(dish_tags_buffer) >= 5000:
            db.insert_bulk("dish_tags", dish_tags_buffer)
            total_dish_tags += len(dish_tags_buffer)
            dish_tags_buffer.clear()

        if len(dish_sections_buffer) >= 5000:
            db.insert_bulk("dish_section_assignments", dish_sections_buffer)
            dish_sections_buffer.clear()

        dish_buffer.clear()
        dish_meta_buffer.clear()

    for restaurant in tqdm(restaurants_list, desc="Generating dishes", unit=" restaurant", mininterval=1.0):
        restaurant_id = restaurant["restaurant_id"]
        cuisine_type = restaurant["cuisine_type"]
        price_multiplier = restaurant["secret_price_multiplier"]
        restaurant_status = restaurant.get("status", "active")
        restaurant_quality_skill = restaurant.get("secret_overall_food_quality", 0.5)

        menu_dishes = _select_dishes_for_menu(cuisine_type, all_bdb_variants, variant_ingredients_cache, bdb, scaling_factor)

        if not menu_dishes:
            continue

        popularity_scores = zipf_distribution(len(menu_dishes), alpha=1.5)

        restaurant_modifiers = restaurant["secret_archetype_modifiers"] or {}

        available_sections = restaurant_sections_map.get(restaurant_id, [])

        for i, variant in enumerate(menu_dishes):
            dish_name = variant.get("variant_name", "Danie")
            archetype = variant.get("archetype", "Unknown")
            base_price = variant.get("price", 35.0)

            variant_id = variant_map.get((dish_name, archetype))
            if not variant_id:
                logger.warning(f"Variant ID not found for {dish_name} ({archetype})")
                continue

            bdb_variant = bdb_variant_lookup.get((dish_name, archetype), {})

            characteristics_vec, weights_vec = generate_dish_vector(
                archetype_name=archetype,
                characteristics=dict(bdb_variant.get("characteristics", {})),
                weights=bdb_variant.get("weights"),
                restaurant_modifiers=restaurant_modifiers,
            )

            secret_base_price = base_price

            final_price = base_price * price_multiplier * random.gauss(1.0, 0.1)
            price = round(max(10.0, final_price))

            base_potential = sample_beta(5, 2, 0.3, 0.95)
            secret_quality = (base_potential * 0.3) + (restaurant_quality_skill * 0.7)
            secret_quality = max(0.1, min(1.0, secret_quality))

            secret_spiciness_val = characteristics_vec.get("flavor_spiciness", 0.0) * 10.0

            dish_ingredients = variant.get("ingredients", [])

            description = generate_dish_description(
                dish_name=dish_name,
                archetype=archetype,
                ingredients=dish_ingredients,
                quality=secret_quality,
                spiciness=secret_spiciness_val,
            )

            primary_photo_metadata = photo_pools.get_dish_photo(archetype, dish_name, restaurant_id)

            secret_richness_val = characteristics_vec.get("physics_richness", 0.5)

            calories = generate_dish_calories(archetype=archetype, price=0, richness=secret_richness_val)

            dish_tag_ids = _get_tags_for_dish(
                cuisine_tag=bdb_variant.get("cuisine_tag"),
                spiciness=secret_spiciness_val,
                ingredients=dish_ingredients,
                ingredient_flags_cache=ingredient_flags_cache,
                tag_map=tag_map,
                tag_by_category=tag_by_category,
            )

            is_spicy = secret_spiciness_val > 6.0
            is_vegan = "Wegańskie" in tag_map and tag_map["Wegańskie"] in dish_tag_ids

            is_vegetarian = is_vegan
            if not is_vegetarian and "Wegetariańskie" in tag_map and tag_map["Wegetariańskie"] in dish_tag_ids:
                is_vegetarian = True

            is_gluten_free = "Bezglutenowe" in tag_map and tag_map["Bezglutenowe"] in dish_tag_ids
            is_lactose_free = is_vegan or ("Bez laktozy" in tag_map and tag_map["Bez laktozy"] in dish_tag_ids)

            is_available = restaurant_status == "active"

            dish_buffer.append(
                {
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
                    "ingredients_json": json.dumps([ing.replace("_", " ") for ing in dish_ingredients]),
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
                    "moderation_status": "none",
                }
            )

            assigned_sections: list[int] = []
            if available_sections:
                routed_section_names = bdb.get_sections_for_dish(cuisine_type, archetype)

                for sec in available_sections:
                    if sec["section_name"] in routed_section_names:
                        assigned_sections.append(sec["id"])
                        break

                if not assigned_sections:
                    assigned_sections.append(random.choice(available_sections)["id"])

            dish_meta_buffer.append(
                {
                    "ingredients": dish_ingredients,
                    "photo": primary_photo_metadata,
                    "tag_ids": dish_tag_ids,
                    "section_ids": list(set(assigned_sections)),
                    "created_at": restaurant.get("created_at"),
                }
            )

        if len(dish_buffer) >= DISH_BATCH_SIZE:
            _flush_dish_buffer()

    _flush_dish_buffer()

    if ingredients_buffer:
        db.insert_bulk("dish_ingredients", ingredients_buffer)
        total_ingredients_links += len(ingredients_buffer)

    if photos_buffer:
        db.insert_bulk("media_assets", photos_buffer)
        total_photos += len(photos_buffer)

    if dish_tags_buffer:
        db.insert_bulk("dish_tags", dish_tags_buffer)
        total_dish_tags += len(dish_tags_buffer)

    if dish_sections_buffer:
        db.insert_bulk("dish_section_assignments", dish_sections_buffer)

    bdb.close()

    duration = time.time() - start_time
    logger.info(f"Generated {total_dishes} dishes in {duration:.2f}s")
    logger.info(f"  - Linked {total_ingredients_links} ingredients")
    logger.info(f"  - Added {total_photos} photos")
    logger.info(f"  - Assigned {total_dish_tags} tags")

def _get_tags_for_dish(
    cuisine_tag: str | None, spiciness: float, ingredients: list,
    ingredient_flags_cache: dict, tag_map: dict, tag_by_category: dict
) -> list:
    tag_ids = set()

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

    has_meat = False
    has_dairy = False
    has_egg = False
    has_gluten = False

    for ing_name in ingredients:
        flags = ingredient_flags_cache.get(ing_name, {})
        if flags.get("is_meat"):
            has_meat = True
        if flags.get("is_dairy"):
            has_dairy = True
        if flags.get("is_egg"):
            has_egg = True
        if flags.get("is_gluten"):
            has_gluten = True

    if not has_meat and not has_dairy and not has_egg:
        if "Wegańskie" in tag_map:
            tag_ids.add(tag_map["Wegańskie"])
    elif not has_meat and "Wegetariańskie" in tag_map:
        tag_ids.add(tag_map["Wegetariańskie"])

    if not has_gluten and "Bezglutenowe" in tag_map:
        tag_ids.add(tag_map["Bezglutenowe"])

    optional_categories = ["occasion", "feature", "mood"]
    for category in random.sample(optional_categories, k=random.randint(1, 2)):
        if tag_by_category.get(category):
            random_tag = random.choice(tag_by_category[category])
            tag_ids.add(random_tag[0])

    return list(tag_ids)

def _select_dishes_for_menu(
    cuisine_type: str, all_bdb_variants: list, variant_ingredients_cache: dict,
    bdb: BlueprintDB, scaling_factor: float = 1.0
) -> list:
    all_variants = []
    for v in all_bdb_variants:
        final_price = round(v["base_price_mean"] * v["price_multiplier_mean"], 2)
        spiciness = v["characteristics"].get("flavor_spiciness", 0.0) * 10.0

        all_variants.append(
            {
                "variant_name": v["name"],
                "archetype": v["archetype_name"],
                "price": final_price,
                "ingredients": variant_ingredients_cache.get(v["id"], []),
                "spiciness": spiciness,
            }
        )

    theme_archetypes = bdb.get_theme_archetypes(cuisine_type)
    params = bdb.get_dish_count_params(cuisine_type)

    target_mean = float(params["mean"]) * scaling_factor
    target_sigma = float(params["sigma"]) * scaling_factor

    target_count = max(4, int(random.gauss(target_mean, target_sigma)))

    matching_dishes = [v for v in all_variants if v["archetype"] in theme_archetypes]

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
            dependencies=["phase1_ingredients", "phase1_tags", "phase2_restaurants"],
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
            logger.error(f"[FAIL] Dishes generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
