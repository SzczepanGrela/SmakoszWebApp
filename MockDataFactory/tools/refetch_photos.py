"""
Unified Refetch Photos Tool

Re-downloads photos for a specific dish, restaurant, or ingredient.
Deletes existing photos in the target folder and downloads fresh ones from Pixabay.
Automatically generates _thumb variants (except ingredients).

Uses PixabayProvider directly - no Unsplash init/overhead.

Usage:
  python tools/refetch_photos.py dish   --category "Burger" --name "BBQ Burger"
  python tools/refetch_photos.py dish   --category "Burger"                        # whole category
  python tools/refetch_photos.py restaurant --name "Kebab"
  python tools/refetch_photos.py restaurant --name "Kebab" --term "turkish doner restaurant"
  python tools/refetch_photos.py restaurant --all
  python tools/refetch_photos.py ingredient --name "Mozzarella"
"""
import argparse
import json
import logging
import os
import shutil
import sys
from pathlib import Path

import requests
from PIL import Image

# Project root
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.image_providers import PixabayProvider
from tools.utils import slugify
from utils.image_processor import resize_and_crop
from utils.logging_config import LoggingConfig

# ── Config ────────────────────────────────────────────────────────────────────
OUTPUT_DIR = Path(str(PHOTO_CONFIG["output_dir"]))
SIZE_FULL = tuple(PHOTO_CONFIG.get("size_full", (1280, 960)))
SIZE_THUMB = tuple(PHOTO_CONFIG.get("size_thumb", (200, 150)))
SIZE_INGREDIENT = tuple(PHOTO_CONFIG.get("size_ingredient", (200, 200)))
SUFFIX_THUMB = str(PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
IMAGE_FORMAT = str(PHOTO_CONFIG.get("image_format", "WEBP"))
IMAGE_QUALITY = int(PHOTO_CONFIG.get("image_quality", 80))

logger = logging.getLogger("refetch")

# Single Pixabay provider - no Unsplash
_pixabay = PixabayProvider()

# ── Helpers ───────────────────────────────────────────────────────────────────

def _generate_thumb(full_path: Path) -> None:
    """Generate a _thumb variant from a full-size image."""
    thumb_path = full_path.parent / f"{full_path.stem}{SUFFIX_THUMB}{full_path.suffix}"
    try:
        img = Image.open(full_path)
        if img.mode != "RGB":
            img = img.convert("RGB")
        img_thumb = resize_and_crop(img, SIZE_THUMB)
        img_thumb.save(thumb_path, IMAGE_FORMAT, quality=IMAGE_QUALITY)
    except Exception as e:
        logger.warning(f"  WARNING: Thumb failed for {full_path.name}: {e}")

def _load_blueprint(name: str) -> dict:
    root = Path(__file__).parent.parent / "blueprints"
    with open(root / name, encoding="utf-8") as f:
        return json.load(f)

def _download_single(url: str, save_path: Path, target_size: tuple[int, int]) -> bool:
    """Download a single image, resize/crop to target_size, save as WEBP."""
    try:
        resp = requests.get(url, timeout=15)
        if resp.status_code != 200:
            return False

        # Save raw bytes to temp, open, process
        from io import BytesIO
        img = Image.open(BytesIO(resp.content))
        if img.mode != "RGB":
            img = img.convert("RGB")

        # Skip images that are too small
        if img.width < target_size[0] // 2 or img.height < target_size[1] // 2:
            return False

        img = resize_and_crop(img, target_size)
        img.save(save_path, IMAGE_FORMAT, quality=IMAGE_QUALITY)
        return True
    except Exception as e:
        logger.debug(f"  Download failed for {url}: {e}")
        return False

# ── Core download logic ──────────────────────────────────────────────────────
def _download_and_rename(
    target_dir: Path,
    search_term: str,
    slug: str,
    count: int,
    target_size: tuple[int, int] = SIZE_FULL,
    generate_thumbs: bool = True,
    pixabay_category: str | None = None,
    orientation: str = "horizontal",
) -> list[Path]:
    """
    Search Pixabay, download photos, rename to slug_NNN.webp, generate thumbs.

    Args:
        target_dir: Folder to save photos into (will be wiped first)
        search_term: Pixabay search query
        slug: Base name for files (e.g. 'kebab' -> kebab_001.webp)
        count: Desired number of photos
        target_size: (width, height) to resize to
        generate_thumbs: Whether to create _thumb variants
        pixabay_category: Pixabay category filter (e.g. 'places', 'food', 'buildings')
        orientation: 'horizontal', 'vertical', or 'all'

    Returns:
        List of saved full-size file paths.
    """
    # 1. Clean existing
    if target_dir.exists():
        logger.info(f"  Cleaning:  Cleaning: {target_dir}")
        shutil.rmtree(target_dir)
    target_dir.mkdir(parents=True, exist_ok=True)

    # 2. Search Pixabay (with optional category filter)
    logger.info(f"  Searching: Searching: '{search_term}'" + (f" [category={pixabay_category}]" if pixabay_category else ""))
    results = _pixabay.search(search_term, count * 3, orientation=orientation, category=pixabay_category)

    if not results:
        logger.error(f"  ERROR: No photos found for '{search_term}'!")
        return []

    logger.info(f"  Candidates: {len(results)} candidates, downloading top {count}...")

    # 3. Download sequentially, stop when we have enough
    final_files: list[Path] = []
    for result in results:
        if len(final_files) >= count:
            break
        idx = len(final_files) + 1
        filename = f"{slug}_{idx:03d}.webp"
        save_path = target_dir / filename

        if _download_single(result.url, save_path, target_size):
            final_files.append(save_path)
            logger.info(f"Downloaded: {filename}")
        else:
            logger.debug(f"  Skipped: {result.url}")

    if len(final_files) < count:
        logger.warning(f"  WARNING: Only {len(final_files)} valid images (requested {count}).")

    # 4. Thumbs
    if generate_thumbs and final_files:
        for fp in final_files:
            _generate_thumb(fp)
        logger.info(f"  Generated:  Generated {len(final_files)} thumbnails")

    logger.info(f"  Result: Result: {len(final_files)} photos" + (" + thumbs" if generate_thumbs else ""))
    return final_files

# ── Entity-specific commands ──────────────────────────────────────────────────
def refetch_dish(category: str, dish_name: str, term: str | None, count: int):
    """Refetch photos for a single dish variant."""
    if not term:
        bp = _load_blueprint("dishes.json")
        variant_data = bp.get(category, {}).get("variants", {}).get(dish_name, {})
        term = variant_data.get("pixabay_term")
    if not term:
        logger.error(f"ERROR: No pixabay_term for {category}/{dish_name} and --term not given.")
        return

    cat_slug = slugify(category)
    dish_slug = slugify(dish_name)
    target_dir = OUTPUT_DIR / "dishes" / cat_slug / dish_slug

    logger.info(f"--- Refetching DISH: {category} / {dish_name} ---")
    _download_and_rename(
        target_dir, term, dish_slug, count,
    )

def refetch_dish_category(category: str, count: int):
    """Refetch photos for all variants in a dish category."""
    bp = _load_blueprint("dishes.json")
    cat_data = bp.get(category)
    if not cat_data:
        logger.error(f"ERROR: Category '{category}' not found in dishes.json")
        return

    variants = cat_data.get("variants", {})
    logger.info(f"Folder: Category: {category} ({len(variants)} variants)")

    for dish_name, dish_data in variants.items():
        term = dish_data.get("pixabay_term")
        if not term:
            logger.warning(f"Skipping {dish_name}: No pixabay_term")
            continue
        try:
            refetch_dish(category, dish_name, term, count)
        except Exception as e:
            logger.error(f"Failed {dish_name}: {e}")

def refetch_restaurant(theme_name: str, term: str | None, count: int):
    """Refetch photos for a single restaurant theme."""
    if not term:
        bp = _load_blueprint("restaurant_types.json")
        theme_data = bp.get("RESTAURANT_THEMES", {}).get(theme_name, {})
        term = theme_data.get("pixabay_term")
    if not term:
        logger.error(f"ERROR: No pixabay_term for restaurant '{theme_name}' and --term not given.")
        return

    target_dir = OUTPUT_DIR / "restaurants" / theme_name
    slug = slugify(theme_name)

    logger.info(f"--- Refetching RESTAURANT: {theme_name} ---")
    _download_and_rename(
        target_dir, term, slug, count,
    )

def refetch_all_restaurants(count: int):
    """Refetch photos for all restaurant themes."""
    bp = _load_blueprint("restaurant_types.json")
    themes = bp.get("RESTAURANT_THEMES", {})
    logger.info(f"Folder: All restaurants: {len(themes)} themes")

    for theme_name, theme_data in themes.items():
        term = theme_data.get("pixabay_term")
        if not term:
            logger.warning(f"Skipping {theme_name}: No pixabay_term")
            continue
        try:
            refetch_restaurant(theme_name, term, count)
        except Exception as e:
            logger.error(f"Failed {theme_name}: {e}")

def refetch_ingredient(ing_name: str, term: str | None, count: int):
    """Refetch photos for a single ingredient."""
    if not term:
        bp = _load_blueprint("ingredient_pixabay_map.json")
        term = bp.get(ing_name)
    if not term:
        logger.error(f"ERROR: No mapping for ingredient '{ing_name}' and --term not given.")
        return

    safe_name = slugify(ing_name)
    target_dir = OUTPUT_DIR / "ingredients" / safe_name

    logger.info(f"--- Refetching INGREDIENT: {ing_name} ---")
    _download_and_rename(
        target_dir, term, safe_name, count,
        target_size=SIZE_INGREDIENT,
        generate_thumbs=False,  # ingredients don't use thumbs
        orientation="all",
    )

# ── CLI ───────────────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(
        description="Unified photo refetch tool for dishes, restaurants, and ingredients.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s dish   --category "Burger" --name "BBQ Burger"
  %(prog)s dish   --category "Burger"                        # entire category
  %(prog)s restaurant --name "Kebab"
  %(prog)s restaurant --name "Kebab" --term "turkish doner restaurant"
  %(prog)s restaurant --all
  %(prog)s ingredient --name "Mozzarella"

After refetching, run:  python tools/refresh_photo_index.py
""",
    )
    sub = parser.add_subparsers(dest="entity", required=True)

    # ── dish ──────────────────────────────────────────────────
    p_dish = sub.add_parser("dish", help="Refetch dish photos")
    p_dish.add_argument("--category", required=True, help="Dish category (e.g. 'Burger')")
    p_dish.add_argument("--name", help="Specific variant (omit = whole category)")
    p_dish.add_argument("--term", help="Override Pixabay search term")
    p_dish.add_argument("--count", type=int, default=5, help="Photos per item (default: 5)")

    # ── restaurant ────────────────────────────────────────────
    p_rest = sub.add_parser("restaurant", help="Refetch restaurant photos")
    p_rest.add_argument("--name", help="Theme name (e.g. 'Kebab')")
    p_rest.add_argument("--term", help="Override Pixabay search term")
    p_rest.add_argument("--all", action="store_true", help="Refetch ALL themes")
    p_rest.add_argument("--count", type=int, default=5, help="Photos per theme (default: 5)")

    # ── ingredient ────────────────────────────────────────────
    p_ing = sub.add_parser("ingredient", help="Refetch ingredient photos")
    p_ing.add_argument("--name", required=True, help="Ingredient name")
    p_ing.add_argument("--term", help="Override Pixabay search term")
    p_ing.add_argument("--count", type=int, default=6, help="Photos (default: 6)")

    # ── Global options ─────────────────────────────────────────
    parser.add_argument("--quiet", "-q", action="store_true", help="Only show warnings and errors")
    parser.add_argument("--debug", "-d", action="store_true", help="Show DEBUG logs (most verbose)")

    args = parser.parse_args()

    # Setup logging
    log_level = "DEBUG" if args.debug else "INFO"
    LoggingConfig.setup(level=log_level, quiet=args.quiet)

    # ── Dispatch ──────────────────────────────────────────────
    if args.entity == "dish":
        if args.name:
            refetch_dish(args.category, args.name, args.term, args.count)
        else:
            refetch_dish_category(args.category, args.count)

    elif args.entity == "restaurant":
        if args.all:
            refetch_all_restaurants(args.count)
        elif args.name:
            refetch_restaurant(args.name, args.term, args.count)
        else:
            parser.error("restaurant requires --name or --all")

    elif args.entity == "ingredient":
        refetch_ingredient(args.name, args.term, args.count)

    logger.info("\nReminder: Don't forget to run: python tools/refresh_photo_index.py")

if __name__ == "__main__":
    main()
