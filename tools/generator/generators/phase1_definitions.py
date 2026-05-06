import json
import logging
import os
import time
import urllib.parse
import uuid
from pathlib import Path

from tqdm import tqdm
from uuid6 import uuid7

from config import PHOTO_CONFIG
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_db import BlueprintDB
from utils.blueprint_loader import BlueprintLoader
from utils.logging_config import LoggingConfig
from utils.photo_pools import PhotoPools
from utils.text_generator import slugify

logger = logging.getLogger(__name__)

HERO_INDEX_PATH = Path(PHOTO_CONFIG.get("local_photo_dir", "E:/smakosz/images")) / "hero" / "hero_index.json"  # type: ignore[arg-type]

def generate_ingredient_icon_url(ingredient_name: str) -> str:
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

class CitiesPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_cities",
            display_name="Cities Generation",
            dependencies=[],
            required_tables=["cities"],
            cleanup_tables=["cities"],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Generating cities...")
        logger.debug(f"Loading cities blueprint from {self.blueprints_dir}")

        try:
            loader = BlueprintLoader(self.blueprints_dir)
            city_rules = loader.load_blueprint("cities.json")
            city_config = city_rules.get("CITY_CONFIG", {})

            if not city_config:
                raise ValueError("cities.json must contain CITY_CONFIG key")

            fallback_city = [{"city_id": 1, "city_name": "Inne", "region": None}]
            other_cities = [{"city_name": city_name, "region": None} for city_name in city_config]

            if not other_cities:
                raise ValueError("No cities found in CITY_CONFIG")

            context.db.insert_bulk("cities", fallback_city)
            context.db.execute_query("SELECT setval(pg_get_serial_sequence('cities', 'city_id'), (SELECT MAX(city_id) FROM cities));")
            context.db.commit()
            context.db.insert_bulk("cities", other_cities)
            city_data = fallback_city + other_cities

            duration = time.time() - start_time
            logger.info(f"[OK] Generated {len(city_data)} cities in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"cities": len(city_data)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Cities generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )

class CuisineTypesPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_cuisines",
            display_name="Cuisine Types Generation",
            dependencies=[],
            required_tables=["cuisine_types"],
            cleanup_tables=["cuisine_types"],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Generating cuisine types...")

        try:
            bdb = BlueprintDB()
            themes = bdb.get_themes()
            bdb.close()

            fallback_cuisine = [
                {
                    "cuisine_type_id": 1,
                    "name": "inna",
                    "display_name": "Inna kuchnia",
                    "icon": None,
                }
            ]
            cuisine_dedup: dict[str, dict] = {}
            for theme in themes:
                cuisine_display = theme.get("cuisine") or theme["name"]
                if cuisine_display not in cuisine_dedup:
                    cuisine_dedup[cuisine_display] = {
                        "name": slugify(cuisine_display),
                        "display_name": cuisine_display,
                        "icon": theme.get("icon"),
                    }
            other_cuisines = list(cuisine_dedup.values())

            context.db.insert_bulk("cuisine_types", fallback_cuisine)
            context.db.execute_query("SELECT setval(pg_get_serial_sequence('cuisine_types', 'cuisine_type_id'), (SELECT MAX(cuisine_type_id) FROM cuisine_types));")
            context.db.commit()
            if other_cuisines:
                context.db.insert_bulk("cuisine_types", other_cuisines)
            cuisine_data = fallback_cuisine + other_cuisines

            duration = time.time() - start_time
            logger.info(f"[OK] Generated {len(cuisine_data)} cuisine types in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"cuisine_types": len(cuisine_data)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Cuisine types generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )

class IngredientsPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_ingredients",
            display_name="Ingredients Generation",
            dependencies=[],
            required_tables=["ingredients"],
            cleanup_tables=["ingredients"],
            estimated_duration=10,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Generating ingredients...")

        try:
            bdb = BlueprintDB()
            db_ingredients = bdb.get_all_ingredients()
            bdb.close()

            photo_pools = PhotoPools()

            logger.debug(f"Found {len(db_ingredients)} unique ingredients")

            allergens = {
                "orzechy", "krewetki", "mleko", "gluten", "jaja",
                "soja", "ryby", "seler", "gorczyca", "sezam", "łupin",
            }

            ingredient_data = []
            for ing in tqdm(
                db_ingredients,
                desc="Generating ingredients",
                unit=" ingredient",
                mininterval=1.0,
                disable=LoggingConfig.is_quiet(),
            ):
                name = ing["name"]
                ing_lower = name.lower()

                is_allergen = any(allergen in ing_lower for allergen in allergens)

                is_meat = bool(ing["is_meat"])
                is_dairy = bool(ing["is_dairy"])
                is_egg = bool(ing["is_egg"])
                is_gluten = bool(ing["is_gluten"])

                is_vegetarian = not is_meat
                is_vegan = not (is_meat or is_dairy or is_egg)
                is_gluten_free = not is_gluten
                is_lactose_free = not is_dairy

                photo_data = photo_pools.get_ingredient_photo(name)
                icon_url = photo_data.get("url")
                icon_blurhash = photo_data.get("blurhash")

                if not icon_url:
                    icon_url = generate_ingredient_icon_url(name)
                    icon_blurhash = None

                ingredient_data.append(
                    {
                        "ingredient_name": name.replace("_", " "),
                        "icon_url": icon_url,
                        "icon_blurhash": icon_blurhash,
                        "is_allergen": is_allergen,
                        "is_vegetarian": is_vegetarian,
                        "is_vegan": is_vegan,
                        "is_gluten_free": is_gluten_free,
                        "is_lactose_free": is_lactose_free,
                    }
                )

            context.db.insert_bulk("ingredients", ingredient_data)

            duration = time.time() - start_time
            allergen_count = sum(1 for i in ingredient_data if i["is_allergen"])
            logger.info(
                f"[OK] Generated {len(ingredient_data)} ingredients ({allergen_count} allergens) in {duration:.2f}s"
            )

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"ingredients": len(ingredient_data), "allergens": allergen_count},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Ingredients generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )

class RestaurantThemesPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_themes",
            display_name="Restaurant Themes Generation",
            dependencies=["phase1_cuisines"],
            required_tables=["restaurant_themes"],
            cleanup_tables=["restaurant_themes"],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Generating restaurant themes...")

        try:
            bdb = BlueprintDB()
            themes = bdb.get_themes()
            bdb.close()

            cuisine_rows = context.db.fetch_all("SELECT cuisine_type_id, display_name FROM cuisine_types")
            cuisine_display_to_id = {row[1]: row[0] for row in cuisine_rows}

            fallback_theme = [
                {
                    "theme_id": 1,
                    "public_id": str(uuid7()),
                    "name": "inne",
                    "display_name": "Inne",
                    "icon": None,
                    "cuisine_type_id": 1,
                    "weight": 0.0,
                    "prompt": None,
                }
            ]

            theme_data = []
            for theme in themes:
                cuisine_display = theme.get("cuisine") or theme["name"]
                cuisine_id = cuisine_display_to_id.get(cuisine_display, 1)
                theme_data.append(
                    {
                        "public_id": str(uuid7()),
                        "name": slugify(theme["name"]),
                        "display_name": theme["name"],
                        "icon": theme.get("icon"),
                        "cuisine_type_id": cuisine_id,
                        "weight": float(theme.get("distribution_chance") or theme.get("weight") or 0.0),
                        "prompt": theme.get("prompt"),
                    }
                )

            context.db.insert_bulk("restaurant_themes", fallback_theme)
            context.db.execute_query("SELECT setval(pg_get_serial_sequence('restaurant_themes', 'theme_id'), (SELECT MAX(theme_id) FROM restaurant_themes));")
            context.db.commit()
            if theme_data:
                context.db.insert_bulk("restaurant_themes", theme_data)
            all_themes = fallback_theme + theme_data

            duration = time.time() - start_time
            logger.info(f"[OK] Generated {len(all_themes)} restaurant themes in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"restaurant_themes": len(all_themes)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Restaurant themes generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )

class TagsPhase(BasePhase):

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_tags",
            display_name="Tags Generation",
            dependencies=[],
            required_tables=["tags"],
            cleanup_tables=["tags"],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Generating tags...")

        try:
            tags = [
                {"tag_name": "Wegetariańskie", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Wegańskie", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Bezglutenowe", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Bez laktozy", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Keto", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Paleo", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Niskokaloryczne", "category": "dietary", "target_entity": "both"},
                {"tag_name": "Łagodne", "category": "spice", "target_entity": "dish"},
                {"tag_name": "Średnio ostre", "category": "spice", "target_entity": "dish"},
                {"tag_name": "Ostre", "category": "spice", "target_entity": "dish"},
                {"tag_name": "Bardzo ostre", "category": "spice", "target_entity": "dish"},
                {"tag_name": "Włoska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Azjatycka", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Meksykańska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Amerykańska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Francuska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Polska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Grecka", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Indyjska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Japońska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Tajska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Wietnamska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Bliskowschodnia", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Śródziemnomorska", "category": "cuisine", "target_entity": "both"},
                {"tag_name": "Romantyczne", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Rodzinne", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Biznesowe", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Casual", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Fine dining", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Fast casual", "category": "mood", "target_entity": "restaurant"},
                {"tag_name": "Brunch", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Lunch", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Obiad", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Kolacja", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Przekąska", "category": "occasion", "target_entity": "dish"},
                {"tag_name": "Pizza", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Burger", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Kebab", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Makaron", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Sushi", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Zupa", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Sałatka", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Deser", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Napój", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Śniadanie", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Przystawka", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Kanapka", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Pierogi", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Stek", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Ryba", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Kuchnia domowa", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Fast food", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Inne", "category": "dish_category", "target_entity": "dish"},
                {"tag_name": "Sezonowe", "category": "feature", "target_entity": "both"},
                {"tag_name": "Lokalne składniki", "category": "feature", "target_entity": "both"},
                {"tag_name": "Farm to table", "category": "feature", "target_entity": "both"},
                {"tag_name": "Organiczne", "category": "feature", "target_entity": "both"},
                {"tag_name": "Comfort food", "category": "feature", "target_entity": "both"},
                {"tag_name": "Street food", "category": "feature", "target_entity": "both"},
                {"tag_name": "Fusion", "category": "feature", "target_entity": "both"},
            ]

            for tag in tqdm(
                tags, desc="Generating tag colors", unit=" tag", mininterval=1.0, disable=LoggingConfig.is_quiet()
            ):
                tag["display_color"] = generate_tag_color(tag["category"], tag["tag_name"])

            context.db.insert_bulk("tags", tags)

            duration = time.time() - start_time
            logger.info(f"[OK] Generated {len(tags)} tags in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"tags": len(tags)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Tags generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )

class HeroImagesPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_hero",
            display_name="Hero Images Registration",
            dependencies=[],
            required_tables=["media_assets"],
            cleanup_tables=[],
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Registering hero images...")

        try:
            context.db.execute_query("DELETE FROM media_assets WHERE entity_type = 'hero'")
            context.db.commit()

            if not HERO_INDEX_PATH.exists():
                logger.warning(f"Hero index not found: {HERO_INDEX_PATH}")
                return PhaseResult(
                    phase_id=self.metadata.phase_id,
                    status=PhaseStatus.COMPLETED,
                    duration_seconds=time.time() - start_time,
                    entities_generated={"hero_images": 0},
                )

            with open(HERO_INDEX_PATH, encoding="utf-8") as f:
                hero_index = json.load(f)

            images = hero_index.get("images", [])
            r2_base = os.getenv("R2_PUBLIC_DOMAIN", "").rstrip("/")
            r2_mock_prefix = PHOTO_CONFIG.get("r2_mock_prefix", "seed")

            hero_data = []
            for idx, img in enumerate(images, start=1):
                filename = img.get("filename")
                if not filename:
                    continue
                url = f"{r2_base}/{r2_mock_prefix}/hero/{filename}"
                credit_text = None
                if img.get("source", "").lower() == "unsplash":
                    credit_text = f"{img.get('credit_user', 'Unknown')} / Unsplash"
                hero_data.append(
                    {
                        "public_id": str(uuid7()),
                        "entity_type": "hero",
                        "entity_id": idx,
                        "url": url,
                        "blurhash": img.get("blurhash"),
                        "width": img.get("width", 1600),
                        "height": img.get("height", 900),
                        "is_primary": False,
                        "status": "approved",
                        "credit_text": credit_text,
                    }
                )

            if hero_data:
                context.db.insert_bulk("media_assets", hero_data)

            duration = time.time() - start_time
            logger.info(f"Registered {len(hero_data)} hero images in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"hero_images": len(hero_data)},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Hero images registration failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
