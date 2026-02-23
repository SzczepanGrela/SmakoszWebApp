"""
Unified Media Pipeline

Single script for fetching and processing images from multiple providers
(Pixabay + Unsplash) for all entity types.

R2 upload is handled separately by mirror_to_r2.py after manual review.

Usage:
    python tools/media_pipeline.py --all           # Fetch all types
    python tools/media_pipeline.py --ingredients   # Fetch ingredients only
    python tools/media_pipeline.py --validate      # Validate folder counts
"""

import argparse
import json
import logging
import os
import sys
import time
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv
from tqdm import tqdm

# Fix Windows console encoding for emoji/Polish characters
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

load_dotenv()

# Add parent to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.image_download import ImageDownloadService
from tools.image_providers import ImageResult, ProviderManager, RateLimitError
from tools.utils import slugify

# Configure logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

# Constants from config
OUTPUT_DIR = Path(str(PHOTO_CONFIG["output_dir"]))
WORKERS = int(PHOTO_CONFIG.get("workers", 5))
IMAGE_FORMAT = str(PHOTO_CONFIG.get("image_format", "WEBP"))

# Size configurations
SIZE_FULL = tuple(PHOTO_CONFIG["size_full"])
SIZE_THUMB = tuple(PHOTO_CONFIG["size_thumb"])
SIZE_TINY = tuple(PHOTO_CONFIG.get("size_tiny", (50, 50)))
SIZE_AVATAR = tuple(PHOTO_CONFIG["size_avatar"])
SIZE_INGREDIENT = tuple(PHOTO_CONFIG["size_ingredient"])
SIZE_HERO = tuple(PHOTO_CONFIG.get("size_hero", (1600, 900)))
SUFFIX_THUMB = str(PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
SUFFIX_TINY = str(PHOTO_CONFIG.get("suffix_tiny", "_tiny"))

# Hero image search queries (curated food/restaurant backgrounds)
HERO_QUERIES = [
    "gourmet food table",
    "restaurant ambiance",
    "fine dining table",
    "culinary presentation",
    "food photography aesthetic",
    "chef cooking kitchen",
    "fresh ingredients cooking",
    "elegant dinner setting",
]
HERO_TARGET_COUNT = 60  # Target number of hero images

@dataclass
class ValidationResult:
    """Result of folder validation."""

    folder: str
    entity_type: str
    count: int
    min_required: int
    max_allowed: int | None
    status: str  # 'ok', 'error', 'warning'
    message: str

class FolderValidator:
    """Validates photo counts per folder type."""

    RULES = {
        "ingredient": {"min": 1, "max": 1},
        "dish_variant": {"min": 5, "max": 15},
        "restaurant_theme": {"min": 5, "max": None},
        "avatar_pool": {"min": 500, "max": 2000},
        "hero_pool": {"min": 20, "max": 100},
    }

    def __init__(self, output_dir: Path):
        self.output_dir = output_dir

    def validate_all(self) -> list[ValidationResult]:
        """Validate all folder types."""
        results = []
        results.extend(self._validate_ingredients())
        results.extend(self._validate_dishes())
        results.extend(self._validate_restaurants())
        results.extend(self._validate_avatars())
        results.extend(self._validate_hero())
        return results

    def _count_images(self, folder: Path) -> int:
        """Count original image files in folder (excludes derived _thumb/_tiny/_hero)."""
        if not folder.exists():
            return 0
        skip_suffixes = {"_thumb", "_tiny", "_hero"}
        return len(
            [
                f
                for f in folder.iterdir()
                if f.is_file()
                and f.suffix.lower() in {".webp", ".jpg", ".png"}
                and not any(f.stem.endswith(s) for s in skip_suffixes)
            ]
        )

    def _validate_ingredients(self) -> list[ValidationResult]:
        """Validate ingredient folders (exactly 1 per ingredient)."""
        results = []
        ing_dir = self.output_dir / "ingredients"

        if not ing_dir.exists():
            return [
                ValidationResult(
                    folder=str(ing_dir),
                    entity_type="ingredients",
                    count=0,
                    min_required=1,
                    max_allowed=None,
                    status="error",
                    message="Ingredients directory does not exist",
                )
            ]

        # Each ingredient should have exactly 1 file
        for img_file in ing_dir.glob("*.webp"):
            results.append(
                ValidationResult(
                    folder=str(img_file),
                    entity_type="ingredient",
                    count=1,
                    min_required=1,
                    max_allowed=1,
                    status="ok",
                    message="",
                )
            )

        return results

    def _validate_dishes(self) -> list[ValidationResult]:
        """Validate dish variant folders (5-15 per variant)."""
        results = []
        dishes_dir = self.output_dir / "dishes"

        if not dishes_dir.exists():
            return [
                ValidationResult(
                    folder=str(dishes_dir),
                    entity_type="dishes",
                    count=0,
                    min_required=5,
                    max_allowed=15,
                    status="error",
                    message="Dishes directory does not exist",
                )
            ]

        rules = self.RULES["dish_variant"]

        for category in dishes_dir.iterdir():
            if not category.is_dir():
                continue
            for variant in category.iterdir():
                if not variant.is_dir():
                    continue

                count = self._count_images(variant)
                status = "ok"
                message = ""

                if count < rules["min"]:
                    status = "error"
                    message = f"Below minimum ({count} < {rules['min']})"
                elif rules["max"] and count > rules["max"]:
                    status = "warning"
                    message = f"Above maximum ({count} > {rules['max']})"

                results.append(
                    ValidationResult(
                        folder=str(variant.relative_to(self.output_dir)),
                        entity_type="dish_variant",
                        count=count,
                        min_required=rules["min"],
                        max_allowed=rules["max"],
                        status=status,
                        message=message,
                    )
                )

        return results

    def _validate_restaurants(self) -> list[ValidationResult]:
        """Validate restaurant theme folders (min 5 per theme)."""
        results = []
        rest_dir = self.output_dir / "restaurants"

        if not rest_dir.exists():
            return [
                ValidationResult(
                    folder=str(rest_dir),
                    entity_type="restaurants",
                    count=0,
                    min_required=5,
                    max_allowed=None,
                    status="error",
                    message="Restaurants directory does not exist",
                )
            ]

        rules = self.RULES["restaurant_theme"]

        for theme in rest_dir.iterdir():
            if not theme.is_dir():
                continue

            count = self._count_images(theme)
            status = "ok"
            message = ""

            if count < rules["min"]:
                status = "error"
                message = f"Below minimum ({count} < {rules['min']})"

            results.append(
                ValidationResult(
                    folder=str(theme.relative_to(self.output_dir)),
                    entity_type="restaurant_theme",
                    count=count,
                    min_required=rules["min"],
                    max_allowed=rules["max"],
                    status=status,
                    message=message,
                )
            )

        return results

    def _validate_avatars(self) -> list[ValidationResult]:
        """Validate avatar pool (500-2000 total)."""
        avatar_dir = self.output_dir / "avatars" / "pool"

        if not avatar_dir.exists():
            return [
                ValidationResult(
                    folder=str(avatar_dir),
                    entity_type="avatar_pool",
                    count=0,
                    min_required=500,
                    max_allowed=2000,
                    status="error",
                    message="Avatar pool directory does not exist",
                )
            ]

        rules = self.RULES["avatar_pool"]
        count = self._count_images(avatar_dir)

        status = "ok"
        message = ""

        if count < rules["min"]:
            status = "error"
            message = f"Below minimum ({count} < {rules['min']})"
        elif rules["max"] and count > rules["max"]:
            status = "warning"
            message = f"Above maximum ({count} > {rules['max']})"

        return [
            ValidationResult(
                folder=str(avatar_dir.relative_to(self.output_dir)),
                entity_type="avatar_pool",
                count=count,
                min_required=rules["min"],
                max_allowed=rules["max"],
                status=status,
                message=message,
            )
        ]

    def _validate_hero(self) -> list[ValidationResult]:
        """Validate hero images pool (20-100 total)."""
        hero_dir = self.output_dir / "hero"

        if not hero_dir.exists():
            return [
                ValidationResult(
                    folder=str(hero_dir),
                    entity_type="hero_pool",
                    count=0,
                    min_required=20,
                    max_allowed=100,
                    status="error",
                    message="Hero images directory does not exist",
                )
            ]

        rules = self.RULES["hero_pool"]
        count = self._count_images(hero_dir)

        status = "ok"
        message = ""

        if count < rules["min"]:
            status = "error"
            message = f"Below minimum ({count} < {rules['min']})"
        elif rules["max"] and count > rules["max"]:
            status = "warning"
            message = f"Above maximum ({count} > {rules['max']})"

        return [
            ValidationResult(
                folder=str(hero_dir.relative_to(self.output_dir)),
                entity_type="hero_pool",
                count=count,
                min_required=rules["min"],
                max_allowed=rules["max"],
                status=status,
                message=message,
            )
        ]

    def print_report(self, results: list[ValidationResult]) -> tuple[int, int]:
        """Print validation report and return (errors, warnings)."""
        errors = [r for r in results if r.status == "error"]
        warnings = [r for r in results if r.status == "warning"]

        print("\n" + "=" * 60)
        print("FOLDER VALIDATION REPORT")
        print("=" * 60)

        # Group by entity type
        by_type: dict[str, list[ValidationResult]] = {}
        for r in results:
            by_type.setdefault(r.entity_type, []).append(r)

        for entity_type, items in by_type.items():
            type_errors = [i for i in items if i.status == "error"]
            type_warnings = [i for i in items if i.status == "warning"]

            if not type_errors and not type_warnings:
                print(f"\nOK: {entity_type}: {len(items)} folders OK")
            else:
                print(f"\nWARNING: {entity_type}: {len(type_errors)} errors, {len(type_warnings)} warnings")
                for item in type_errors + type_warnings:
                    icon = "ERROR:" if item.status == "error" else "WARNING:"
                    print(f"   {icon} {item.folder}: {item.message}")

        print("\n" + "-" * 60)
        print(f"SUMMARY: {len(errors)} errors, {len(warnings)} warnings")
        print("=" * 60 + "\n")

        return len(errors), len(warnings)

class MediaPipeline:
    """Unified media fetching pipeline."""

    def __init__(self):
        import requests
        from requests.adapters import HTTPAdapter
        from urllib3.util.retry import Retry

        self.provider_manager = ProviderManager()
        self.validator = FolderValidator(OUTPUT_DIR)
        self.output_dir = OUTPUT_DIR

        # Build a session with retry logic (same policy as fetch_photos.py)
        session = requests.Session()
        retries = Retry(total=5, backoff_factor=1, status_forcelist=[429, 500, 502, 503, 504], allowed_methods=["GET"])
        session.mount("https://", HTTPAdapter(max_retries=retries))
        session.mount("http://", HTTPAdapter(max_retries=retries))
        self.download_service = ImageDownloadService(session, {}, OUTPUT_DIR)

        # Load blueprints
        blueprints_dir = Path(__file__).parent.parent / "blueprints"
        self.ingredient_mappings = self._load_ingredient_mappings(blueprints_dir)
        self.dish_variants = self._load_dish_variants(blueprints_dir)
        self.restaurant_themes = self._load_restaurant_themes(blueprints_dir)

    @staticmethod
    def _prepare_url(url: str) -> str:
        """Append Unsplash size limit params to prevent decompression bombs."""
        if "unsplash.com" in url:
            sep = "&" if "?" in url else "?"
            return f"{url}{sep}w=1600&q=85"
        return url

    def _load_ingredient_mappings(self, blueprints_dir: Path) -> dict[str, str]:
        """Load ingredient name -> English search term mappings."""
        mapping_file = blueprints_dir / "ingredients_pixabay.json"
        if mapping_file.exists():
            with open(mapping_file, encoding="utf-8") as f:
                mappings = json.load(f)
                logger.info(f"Loaded {len(mappings)} ingredient mappings")
                return mappings
        logger.warning(f"Ingredient mapping file not found: {mapping_file}")
        return {}

    def _load_dish_variants(self, blueprints_dir: Path) -> dict:
        """Load dish variants from blueprints."""
        variants_file = blueprints_dir / "dishes.json"
        if variants_file.exists():
            with open(variants_file, encoding="utf-8") as f:
                return json.load(f)
        return {}

    def _load_restaurant_themes(self, blueprints_dir: Path) -> dict:
        """Load restaurant themes from blueprints."""
        themes_file = blueprints_dir / "restaurant_types.json"
        if themes_file.exists():
            with open(themes_file, encoding="utf-8") as f:
                data = json.load(f)
                return data.get("RESTAURANT_THEMES", {})
        return {}

    def search_with_backoff(
        self, query: str, count: int, orientation: str = "horizontal", max_attempts: int = 15
    ) -> list[ImageResult]:
        """Search with proper rate limit handling - keeps retrying until success."""
        for attempt in range(max_attempts):
            try:
                return self.provider_manager.search_mixed(query, count, orientation)
            except RateLimitError as e:
                wait_time = min(e.retry_after, 3600)  # Use API's suggested wait, max 1h
                logger.warning(
                    f"Waiting: Rate limit hit for '{query}'. Waiting {wait_time // 60}min (attempt {attempt + 1}/{max_attempts})..."
                )
                time.sleep(wait_time)
            except Exception as e:
                logger.error(f"Search failed for '{query}': {e}")
                return []

        logger.error(f"ERROR: Max retries exceeded for '{query}'")
        return []

    def search_pixabay_only(
        self,
        query: str,
        count: int,
        orientation: str = "horizontal",
        category: str | None = None,
        max_attempts: int = 15,
    ) -> list[ImageResult]:
        """
        Search ONLY Pixabay (CC0 license, no attribution required).

        Use this for all non-hero images to avoid Unsplash attribution requirements.
        Hero images should use search_with_backoff() which includes Unsplash.
        """
        pixabay = self.provider_manager.get_provider("pixabay")
        if not pixabay:
            logger.error("Pixabay provider not available!")
            return []

        for attempt in range(max_attempts):
            try:
                return pixabay.search(query, count, orientation, category=category)
            except RateLimitError as e:
                wait_time = min(e.retry_after, 3600)
                logger.warning(
                    f"Waiting: Pixabay rate limit for '{query}'. Waiting {wait_time // 60}min (attempt {attempt + 1}/{max_attempts})..."
                )
                time.sleep(wait_time)
            except Exception as e:
                logger.error(f"Pixabay search failed for '{query}': {e}")
                return []

        logger.error(f"ERROR: Max retries exceeded for '{query}'")
        return []

    def run_ingredients(self, dry_run: bool = False):
        """
        Download ingredient photos from Pixabay only (CC0, no attribution).
        Creates: ingredients/{ingredient_name}/ingredient_name_001.webp, etc.
        """
        logger.info("--- FETCHING INGREDIENTS ---")

        PHOTOS_PER_PROVIDER = 3  # 3 from Pixabay + 3 from Unsplash = 6 total per ingredient

        ing_base_dir = self.output_dir / "ingredients"
        ing_base_dir.mkdir(parents=True, exist_ok=True)

        if not self.ingredient_mappings:
            logger.warning("No ingredient mappings found!")
            return

        # Count ingredients that need downloads
        to_process = []
        for ing_name, search_term in self.ingredient_mappings.items():
            safe_name = slugify(ing_name)
            ing_dir = ing_base_dir / safe_name

            # Check if we have enough photos already
            existing_count = len(list(ing_dir.glob(f"*.{IMAGE_FORMAT.lower()}"))) if ing_dir.exists() else 0
            if existing_count < PHOTOS_PER_PROVIDER * 2:  # Need at least 6 total
                to_process.append((ing_name, search_term, safe_name, ing_dir))

        logger.info(f"Need to download: {len(to_process)} ingredients")

        if dry_run:
            logger.info("[DRY RUN] Would download ingredients")
            return

        for ing_name, search_term, safe_name, ing_dir in tqdm(to_process, desc="Ingredients"):
            ing_dir.mkdir(parents=True, exist_ok=True)

            # Check existing photos
            existing = len(list(ing_dir.glob(f"*.{IMAGE_FORMAT.lower()}")))
            target = PHOTOS_PER_PROVIDER * 2  # 6 total (3 per provider mixed)
            needed = max(0, target - existing)

            if needed == 0:
                continue

            logger.info(f"Downloading: Downloading ingredient: {ing_name} ({needed} photos)")

            # Use Pixabay only (CC0 license, no attribution needed)
            # Request extra images since we'll filter for square-ish ones
            results = self.search_pixabay_only(search_term, needed * 3, orientation="all")

            for i, result in enumerate(results[:needed]):
                idx = existing + i + 1
                filename = f"{safe_name}_{idx:03d}.{IMAGE_FORMAT.lower()}"
                save_path = ing_dir / filename

                if save_path.exists():
                    continue

                self.download_service.process_image(self._prepare_url(result.url), save_path, SIZE_INGREDIENT)

    def run_dishes(self, dry_run: bool = False):
        """Download dish photos from Pixabay only (CC0, no attribution)."""
        logger.info("--- FETCHING DISHES ---")

        dishes_dir = self.output_dir / "dishes"

        min_photos = int(PHOTO_CONFIG["min_photos_per_variant"])
        int(PHOTO_CONFIG["max_photos_per_variant"])

        for category, data in self.dish_variants.items():
            category_slug = slugify(category)
            variants = data.get("variants", {})
            archetype_term = data.get("pixabay_term")

            for variant_name, variant_data in variants.items():
                variant_slug = slugify(variant_name)
                variant_dir = dishes_dir / category_slug / variant_slug
                variant_dir.mkdir(parents=True, exist_ok=True)

                existing = len(list(variant_dir.glob(f"*.{IMAGE_FORMAT.lower()}")))
                target = min_photos  # Fixed target instead of random
                needed = max(0, target - existing)

                if needed == 0:
                    continue

                query = variant_data.get("pixabay_term") or archetype_term or f"{variant_name} {category}"

                if dry_run:
                    logger.info(f"[DRY RUN] Would download {needed} for {category}/{variant_name}")
                    continue

                logger.info(f"Downloading: Downloading dish: {category}/{variant_name} ({needed} photos)")

                results = self.search_pixabay_only(query, needed)

                for i, result in enumerate(results):
                    idx = existing + i + 1
                    filename = f"{slugify(variant_name)}_{idx:03d}.{IMAGE_FORMAT.lower()}"
                    save_path = variant_dir / filename
                    self.download_service.process_image_multi_size(
                        self._prepare_url(result.url), save_path, include_tiny=False
                    )

    def run_restaurants(self, dry_run: bool = False):
        """Download restaurant photos from Pixabay only (CC0, no attribution)."""
        logger.info("--- FETCHING RESTAURANTS ---")

        rest_dir = self.output_dir / "restaurants"
        base_count = int(PHOTO_CONFIG["restaurant_base_count"])

        for theme, theme_data in self.restaurant_themes.items():
            theme_dir = rest_dir / theme
            theme_dir.mkdir(parents=True, exist_ok=True)

            weight = theme_data.get("distribution_chance", 0.05) * 100
            target = max(5, int(base_count * weight / 10.0))

            existing = len(list(theme_dir.glob(f"*.{IMAGE_FORMAT.lower()}")))
            needed = max(0, target - existing)

            if needed == 0:
                continue

            query = theme_data.get("pixabay_term", f"{theme} restaurant interior")

            if dry_run:
                logger.info(f"[DRY RUN] Would download {needed} for {theme}")
                continue

            logger.info(f"Downloading: Downloading restaurant: {theme} ({needed} photos)")

            results = self.search_pixabay_only(query, needed)

            for i, result in enumerate(results):
                idx = existing + i + 1
                filename = f"{slugify(theme)}_{idx:03d}.{IMAGE_FORMAT.lower()}"
                save_path = theme_dir / filename
                self.download_service.process_image_multi_size(
                    self._prepare_url(result.url), save_path, include_tiny=False
                )

    def run_avatars(self, dry_run: bool = False):
        """Download avatar pool from Pixabay only (CC0, no attribution)."""
        logger.info("--- FETCHING AVATARS ---")

        avatar_dir = self.output_dir / "avatars" / "pool"
        avatar_dir.mkdir(parents=True, exist_ok=True)

        existing = len(list(avatar_dir.glob(f"*.{IMAGE_FORMAT.lower()}")))
        target = 1000
        needed = max(0, target - existing)

        if needed == 0:
            logger.info("Avatar pool already complete")
            return

        queries = [
            "person portrait",
            "professional headshot",
            "casual portrait",
            "young adult portrait",
            "mature adult portrait",
            "smiling person",
        ]
        per_query = needed // len(queries) + 1

        if dry_run:
            logger.info(f"[DRY RUN] Would download {needed} avatars")
            return

        idx = existing + 1
        for query in queries:
            results = self.search_pixabay_only(query, per_query, orientation="vertical")

            for result in results:
                if idx > target:
                    break
                filename = f"avatar_{idx:04d}.{IMAGE_FORMAT.lower()}"
                save_path = avatar_dir / filename
                self.download_service.process_image_multi_size(
                    self._prepare_url(result.url), save_path, include_tiny=True, avatar_mode=True
                )
                idx += 1

    def run_hero(self, dry_run: bool = False):
        """
        Download hero images for homepage backgrounds.

        Creates: hero/hero_001.webp, hero_002.webp, etc.
        Also generates hero_index.json with credits for attribution.
        """
        logger.info("--- FETCHING HERO IMAGES ---")

        hero_dir = self.output_dir / "hero"
        hero_dir.mkdir(parents=True, exist_ok=True)

        existing = len(list(hero_dir.glob(f"*.{IMAGE_FORMAT.lower()}")))
        target = HERO_TARGET_COUNT
        needed = max(0, target - existing)

        if needed == 0:
            logger.info("Hero images pool already complete")
            return

        if dry_run:
            logger.info(f"[DRY RUN] Would download {needed} hero images")
            return

        # Load existing index if present
        index_path = hero_dir / "hero_index.json"
        if index_path.exists():
            with open(index_path, encoding="utf-8") as f:
                hero_index = json.load(f)
        else:
            hero_index = {"images": []}

        per_query = (needed // len(HERO_QUERIES)) + 1
        idx = existing + 1

        for query in HERO_QUERIES:
            if idx > target:
                break

            logger.info(f"Downloading: Searching hero images: '{query}'")
            results = self.search_with_backoff(query, per_query, orientation="horizontal")

            for result in results:
                if idx > target:
                    break

                filename = f"hero_{idx:03d}.{IMAGE_FORMAT.lower()}"
                save_path = hero_dir / filename

                success, _ = self.download_service.process_image(self._prepare_url(result.url), save_path, SIZE_HERO)
                if success:
                    # Add to index with credits
                    credit = result.credit or {}
                    hero_index["images"].append(
                        {
                            "filename": filename,
                            "credit_user": credit.get("name") or credit.get("username") or "Unknown",
                            "credit_url": credit.get("link") or "",
                            "source": result.provider,
                        }
                    )
                    idx += 1

        # Save updated index
        with open(index_path, "w", encoding="utf-8") as f:
            json.dump(hero_index, f, indent=2, ensure_ascii=False)

        logger.info(f"Hero images complete. Index saved to {index_path}")

    def clear_all(self):
        """Delete all existing photos (before fresh download)."""
        import shutil

        folders = [
            self.output_dir / "ingredients",
            self.output_dir / "dishes",
            self.output_dir / "restaurants",
            self.output_dir / "avatars",
            self.output_dir / "hero",
        ]

        for folder in folders:
            if folder.exists():
                logger.info(f"Deleting {folder}...")
                shutil.rmtree(folder)

        logger.info("All photo folders cleared.")

    def run_all(self, dry_run: bool = False):
        """Run all fetch operations."""
        self.run_ingredients(dry_run)
        self.run_dishes(dry_run)
        self.run_restaurants(dry_run)
        self.run_avatars(dry_run)
        self.run_hero(dry_run)

    def validate(self) -> tuple[int, int]:
        """Run validation and print report."""
        results = self.validator.validate_all()
        return self.validator.print_report(results)

def main():
    parser = argparse.ArgumentParser(description="Unified Media Pipeline")
    parser.add_argument("--all", action="store_true", help="Fetch all types")
    parser.add_argument("--ingredients", action="store_true", help="Fetch ingredients")
    parser.add_argument("--dishes", action="store_true", help="Fetch dishes")
    parser.add_argument("--restaurants", action="store_true", help="Fetch restaurants")
    parser.add_argument("--avatars", action="store_true", help="Fetch avatars")
    parser.add_argument("--hero", action="store_true", help="Fetch hero images for homepage")
    parser.add_argument("--validate", action="store_true", help="Validate folders only")
    parser.add_argument("--dry-run", action="store_true", help="Show what would be done")
    parser.add_argument("--force", action="store_true", help="Delete existing and redownload all")

    args = parser.parse_args()

    pipeline = MediaPipeline()

    if args.validate:
        errors, warnings = pipeline.validate()
        sys.exit(1 if errors > 0 else 0)

    if args.force:
        confirm = input("WARNING: This will DELETE all existing photos. Continue? (yes/no): ")
        if confirm.lower() != "yes":
            print("Cancelled.")
            sys.exit(0)
        pipeline.clear_all()

    if args.all:
        pipeline.run_all(args.dry_run)
    else:
        if args.ingredients:
            pipeline.run_ingredients(args.dry_run)
        if args.dishes:
            pipeline.run_dishes(args.dry_run)
        if args.restaurants:
            pipeline.run_restaurants(args.dry_run)
        if args.avatars:
            pipeline.run_avatars(args.dry_run)
        if args.hero:
            pipeline.run_hero(args.dry_run)

    # Always validate after fetching
    if not args.validate:
        print("\nRunning post-fetch validation...")
        pipeline.validate()

if __name__ == "__main__":
    main()
