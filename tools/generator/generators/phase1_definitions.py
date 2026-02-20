import json
import logging
import time
import urllib.parse
import uuid
from pathlib import Path

from tqdm import tqdm

from config import PHOTO_CONFIG
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_loader import BlueprintLoader
from utils.logging_config import LoggingConfig
from utils.photo_pools import PhotoPools

logger = logging.getLogger(__name__)

# Hero images index path
HERO_INDEX_PATH = Path(PHOTO_CONFIG.get("local_photo_dir", "E:/smakosz/images")) / "hero" / "hero_index.json"  # type: ignore[arg-type]

def generate_ingredient_icon_url(ingredient_name: str) -> str:
    """Generate placeholder icon URL using ui-avatars.com (128x128 square)."""
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
    """
    Phase 1a: Cities Generation

    Populates cities table with Polish cities and postal code prefixes.

    Dependencies: None (parallel with other Phase 1 components)
    Required Tables: cities
    Estimated Duration: ~2 seconds
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """Initialize CitiesPhase."""
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase1_cities",
            display_name="Cities Generation",
            dependencies=[],  # No dependencies - parallel with others
            required_tables=["cities"],
            cleanup_tables=["cities"],
            estimated_duration=2
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """Execute cities generation."""
        start_time = time.time()
        logger.info("Generating cities...")
        logger.debug(f"Loading cities blueprint from {self.blueprints_dir}")

        try:
            loader = BlueprintLoader(self.blueprints_dir)
            city_rules = loader.load_blueprint("cities.json")
            city_config = city_rules.get("CITY_CONFIG", {})

            if not city_config:
                raise ValueError("cities.json must contain CITY_CONFIG key")

            # Polish postal code prefixes by major city
            POSTAL_CODE_PREFIXES = {
                "Warszawa": "00", "Kraków": "30", "Wrocław": "50", "Łódź": "90",
                "Poznań": "60", "Gdańsk": "80", "Szczecin": "70", "Bydgoszcz": "85",
                "Lublin": "20", "Białystok": "15", "Katowice": "40", "Gdynia": "81",
                "Toruń": "87", "Rzeszów": "35", "Kielce": "25", "Olsztyn": "10",
                "Opole": "45", "Gorzów Wlkp.": "66",
            }

            city_data = []
            for city_name in city_config:
                city_data.append({
                    "city_name": city_name,
                    "postal_code_prefix": POSTAL_CODE_PREFIXES.get(city_name, "00")
                })

            if not city_data:
                raise ValueError("No cities found in CITY_CONFIG")

            context.db.insert_bulk("cities", city_data)

            duration = time.time() - start_time
            logger.info(f"✓ Generated {len(city_data)} cities in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"cities": len(city_data)}
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Cities generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e
            )

class CuisineTypesPhase(BasePhase):
    """
    Phase 1b: Cuisine Types Generation

    Populates cuisine_types table from restaurant themes.

    Dependencies: None (parallel with other Phase 1 components)
    Required Tables: cuisine_types
    Estimated Duration: ~2 seconds
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """Initialize CuisineTypesPhase."""
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase1_cuisines",
            display_name="Cuisine Types Generation",
            dependencies=[],  # No dependencies - parallel with others
            required_tables=["cuisine_types"],
            cleanup_tables=["cuisine_types"],
            estimated_duration=2
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """Execute cuisine types generation."""
        start_time = time.time()
        logger.info("Generating cuisine types...")

        try:
            loader = BlueprintLoader(self.blueprints_dir)
            restaurant_rules = loader.load_blueprint("restaurant_types.json")
            themes = restaurant_rules.get("RESTAURANT_THEMES", {})

            # Map themes to display names
            CUISINE_DISPLAY_NAMES = {
                "Pizzeria": "Włoska", "Kebab": "Turecka", "Burgerownia": "Amerykańska",
                "Kuchnia Polska": "Polska", "Sushi Bar": "Japońska",
                "Wegańska Kawiarnia": "Wegańska", "Kuchnia Chińska": "Chińska",
                "Kuchnia Indyjska": "Indyjska", "Kuchnia Meksykańska": "Meksykańska",
                "Kuchnia Francuska": "Francuska", "Kuchnia Włoska": "Włoska",
                "Kuchnia Tajska": "Tajska", "Ramen Bar": "Japońska",
                "Kawiarnia": "Kawiarnia", "Food Truck": "Street Food",
                "Smażalnia Ryb": "Ryby", "BBQ & Grill": "BBQ",
                "Taqueria": "Meksykańska", "Creperie": "Naleśnikarnia",
                "Piekarnia": "Piekarnia",
            }

            cuisine_data = []
            for theme_name in themes:
                display_name = CUISINE_DISPLAY_NAMES.get(theme_name, theme_name)
                cuisine_data.append({
                    "name": theme_name.lower().replace(" ", "_"),
                    "display_name": display_name,
                    "icon": None
                })

            if cuisine_data:
                context.db.insert_bulk("cuisine_types", cuisine_data)

            duration = time.time() - start_time
            logger.info(f"✓ Generated {len(cuisine_data)} cuisine types in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"cuisine_types": len(cuisine_data)}
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Cuisine types generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e
            )

class IngredientsPhase(BasePhase):
    """
    Phase 1c: Ingredients Generation

    Populates ingredients table with dietary flags and icons.

    Dependencies: None (parallel with other Phase 1 components)
    Required Tables: ingredients
    Estimated Duration: ~10 seconds (photo lookup)
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """Initialize IngredientsPhase."""
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase1_ingredients",
            display_name="Ingredients Generation",
            dependencies=[],  # No dependencies - parallel with others
            required_tables=["ingredients"],
            cleanup_tables=["ingredients"],
            estimated_duration=10
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """Execute ingredients generation."""
        start_time = time.time()
        logger.info("Generating ingredients...")

        try:
            loader = BlueprintLoader(self.blueprints_dir)
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

            logger.debug(f"Found {len(all_ingredients)} unique ingredients")

            if not all_ingredients:
                logger.warning("No ingredients found in dishes.json")

            allergens = {
                "orzechy", "krewetki", "mleko", "gluten", "jaja", "soja",
                "ryby", "seler", "gorczyca", "sezam", "łupin",
            }

            # Load dietary keywords
            global_config = loader.load_blueprint("global_config.json")
            dietary_keywords = global_config.get("DIETARY_KEYWORDS", {})
            meat_keywords = dietary_keywords.get("meat", [])
            dairy_keywords = dietary_keywords.get("dairy", [])
            egg_keywords = dietary_keywords.get("eggs", [])
            gluten_keywords = dietary_keywords.get("gluten", [])

            ingredient_data = []
            for ingredient in tqdm(
                sorted(all_ingredients),
                desc="Generating ingredients",
                unit=" ingredient",
                mininterval=1.0,
                disable=LoggingConfig.is_quiet()
            ):
                ing_lower = ingredient.lower()

                is_allergen = any(allergen in ing_lower for allergen in allergens)

                # Default to True (Positive logic)
                is_vegetarian = True
                is_vegan = True
                is_gluten_free = True
                is_lactose_free = True

                # Check for Meat
                if any(kw in ing_lower for kw in meat_keywords):
                    is_vegetarian = False
                    is_vegan = False

                # Check for Dairy
                if any(kw in ing_lower for kw in dairy_keywords):
                    is_vegan = False
                    is_lactose_free = False

                # Check for Eggs
                if any(kw in ing_lower for kw in egg_keywords):
                    is_vegan = False

                # Check for Gluten
                if any(kw in ing_lower for kw in gluten_keywords) or "gluten" in ing_lower:
                    is_gluten_free = False

                # Corrections for specific items
                if "tofu" in ing_lower:
                    is_vegetarian = True
                    is_vegan = True

                if "miód" in ing_lower:
                    is_vegan = False

                # Generate icon URL
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

            context.db.insert_bulk("ingredients", ingredient_data)

            duration = time.time() - start_time
            allergen_count = sum(1 for i in ingredient_data if i["is_allergen"])
            logger.info(
                f"✓ Generated {len(ingredient_data)} ingredients "
                f"({allergen_count} allergens) in {duration:.2f}s"
            )

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={
                    "ingredients": len(ingredient_data),
                    "allergens": allergen_count
                }
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Ingredients generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e
            )

class TagsPhase(BasePhase):
    """
    Phase 1d: Tags Generation

    Populates tags table with categorized tags and colors.

    Dependencies: None (parallel with other Phase 1 components)
    Required Tables: tags
    Estimated Duration: ~2 seconds
    """

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase1_tags",
            display_name="Tags Generation",
            dependencies=[],  # No dependencies - parallel with others
            required_tables=["tags"],
            cleanup_tables=["tags"],
            estimated_duration=2
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """Execute tags generation."""
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
                {"tag_name": "Śniadanie", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Brunch", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Lunch", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Obiad", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Kolacja", "category": "occasion", "target_entity": "both"},
                {"tag_name": "Przekąska", "category": "occasion", "target_entity": "dish"},
                {"tag_name": "Deser", "category": "occasion", "target_entity": "dish"},
                {"tag_name": "Sezonowe", "category": "feature", "target_entity": "both"},
                {"tag_name": "Lokalne składniki", "category": "feature", "target_entity": "both"},
                {"tag_name": "Farm to table", "category": "feature", "target_entity": "both"},
                {"tag_name": "Organiczne", "category": "feature", "target_entity": "both"},
                {"tag_name": "Comfort food", "category": "feature", "target_entity": "both"},
                {"tag_name": "Street food", "category": "feature", "target_entity": "both"},
                {"tag_name": "Fusion", "category": "feature", "target_entity": "both"},
            ]

            for tag in tqdm(
                tags,
                desc="Generating tag colors",
                unit=" tag",
                mininterval=1.0,
                disable=LoggingConfig.is_quiet()
            ):
                tag["display_color"] = generate_tag_color(tag["category"], tag["tag_name"])

            context.db.insert_bulk("tags", tags)

            duration = time.time() - start_time
            logger.info(f"✓ Generated {len(tags)} tags in {duration:.2f}s")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"tags": len(tags)}
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"✗ Tags generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e
            )

class HeroImagesPhase(BasePhase):
    """
    Phase 1e: Hero Images Registration

    Reads hero_index.json and registers hero background images in the
    media_assets table (entity_type = 'hero').

    These are used by the frontend as homepage background images,
    served from R2/CDN.

    Dependencies: None (parallel with other Phase 1 components)
    Required Tables: media_assets (entity_type = 'hero' rows only)
    Estimated Duration: ~2 seconds
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """Initialize HeroImagesPhase."""
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        """Return phase metadata with dependencies."""
        return PhaseMetadata(
            phase_id="phase1_hero",
            display_name="Hero Images Registration",
            dependencies=[],  # No dependencies - parallel with other Phase 1 components
            required_tables=["media_assets"],
            cleanup_tables=[],  # Targeted DELETE (not TRUNCATE) - handled inside execute()
            estimated_duration=2,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """Execute hero images registration."""
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
            r2_base = PHOTO_CONFIG.get("r2_public_base_url", "").rstrip("/")  # type: ignore[attr-defined]
            r2_mock_prefix = PHOTO_CONFIG.get("r2_mock_prefix", "smakosz/images/mock")

            hero_data = []
            for idx, img in enumerate(images, start=1):
                filename = img.get("filename")
                if not filename:
                    continue
                url = f"{r2_base}/{r2_mock_prefix}/hero/{filename}"
                credit_text = None
                if img.get("source", "").lower() == "unsplash":
                    credit_text = f"{img.get('credit_user', 'Unknown')} / Unsplash"
                hero_data.append({
                    "public_id": str(uuid.uuid4()),
                    "entity_type": "hero",
                    "entity_id": idx,
                    "url": url,
                    "blurhash": img.get("blurhash"),
                    "width": img.get("width", 1600),
                    "height": img.get("height", 900),
                    "is_primary": False,
                    "status": "approved",
                    "credit_text": credit_text,
                })

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

