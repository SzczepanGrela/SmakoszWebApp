"""
Generate thumbnail images from existing full-size photos.

READ-ONLY on originals - only CREATES new *_thumb.webp files.
Never modifies or deletes existing files.

Usage:
    python tools/generate_thumbs.py              # dry-run (default)
    python tools/generate_thumbs.py --apply      # actually create thumbs
    python tools/generate_thumbs.py --apply --update-index  # + update photo_index.json
"""

import argparse
import json
import logging
import os
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from config import PHOTO_CONFIG

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

OUTPUT_DIR = Path(str(PHOTO_CONFIG["output_dir"]))
INDEX_FILE = Path(str(PHOTO_CONFIG["local_photo_index"]))
SIZE_THUMB = tuple(PHOTO_CONFIG.get("size_thumb", (200, 150)))
SUFFIX_THUMB = str(PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
IMAGE_FORMAT = str(PHOTO_CONFIG.get("image_format", "WEBP"))
IMAGE_QUALITY = int(PHOTO_CONFIG.get("image_quality", 80))

# Suffixes to skip (these are already derived files)
SKIP_SUFFIXES = {SUFFIX_THUMB, "_tiny", "_hero"}

def is_original(path: Path) -> bool:
    """Check if file is an original (not a thumb/tiny/hero variant)."""
    stem = path.stem
    return not any(stem.endswith(s) for s in SKIP_SUFFIXES)

def thumb_path_for(path: Path) -> Path:
    """Derive thumb path: photo_001.webp -> photo_001_thumb.webp"""
    return path.parent / f"{path.stem}{SUFFIX_THUMB}{path.suffix}"

def resize_and_crop(img: Image.Image, target: tuple) -> Image.Image:
    """Resize maintaining aspect ratio, then center-crop to target."""
    img_ratio = img.width / img.height
    target_ratio = target[0] / target[1]

    if img_ratio > target_ratio:
        new_h = target[1]
        new_w = int(new_h * img_ratio)
    else:
        new_w = target[0]
        new_h = int(new_w / img_ratio)

    img = img.resize((new_w, new_h), Image.Resampling.LANCZOS)
    left = (new_w - target[0]) / 2
    top = (new_h - target[1]) / 2
    return img.crop((left, top, left + target[0], top + target[1]))

def scan_missing_thumbs(categories: list[str]) -> list[tuple[Path, Path]]:
    """Find originals that are missing a corresponding thumb file.
    
    Returns list of (original_path, thumb_path) tuples.
    """
    pairs: list[tuple[Path, Path]] = []

    for category in categories:
        cat_dir = OUTPUT_DIR / category
        if not cat_dir.exists():
            logger.warning(f"Directory not found: {cat_dir}")
            continue

        for img_file in cat_dir.rglob("*.webp"):
            if not img_file.is_file():
                continue
            if not is_original(img_file):
                continue

            thumb = thumb_path_for(img_file)
            if not thumb.exists():
                pairs.append((img_file, thumb))

    return pairs

def generate_thumbs(pairs: list[tuple[Path, Path]], apply: bool) -> int:
    """Generate thumbnail files. Returns count of created files."""
    created = 0
    errors = 0

    for original, thumb in pairs:
        if not apply:
            continue

        try:
            img = Image.open(original)
            if img.mode != "RGB":
                img = img.convert("RGB")

            img_thumb = resize_and_crop(img, SIZE_THUMB)
            img_thumb.save(thumb, IMAGE_FORMAT, quality=IMAGE_QUALITY)
            created += 1
        except Exception as e:
            logger.error(f"Failed: {original.name} -> {e}")
            errors += 1

    if errors:
        logger.warning(f"{errors} errors during generation")
    return created

def update_index(categories: list[str]) -> None:
    """Add path_thumb entries to photo_index.json for dishes/restaurants."""
    if not INDEX_FILE.exists():
        logger.warning(f"Index file not found: {INDEX_FILE}")
        return

    with open(INDEX_FILE, encoding="utf-8") as f:
        index = json.load(f)

    updated = 0
    for category in categories:
        section = index.get(category, {})

        if category == "avatars":
            # Avatars are a flat list
            for entry in section:
                path = entry.get("path", "")
                if "path_thumb" not in entry or not entry["path_thumb"]:
                    stem = Path(path).stem
                    suffix = Path(path).suffix
                    parent = str(Path(path).parent)
                    thumb_rel = f"{parent}/{stem}{SUFFIX_THUMB}{suffix}"
                    if (OUTPUT_DIR / thumb_rel).exists():
                        entry["path_thumb"] = thumb_rel
                        updated += 1
        else:
            # Dishes/restaurants are nested dicts
            for group_key, items in section.items():
                if isinstance(items, dict):
                    # dishes: category -> variant -> [photos]
                    for variant_key, photos in items.items():
                        if isinstance(photos, list):
                            for entry in photos:
                                _update_entry(entry, updated)
                                updated += 1
                elif isinstance(items, list):
                    # restaurants: theme -> [photos]
                    for entry in items:
                        path = entry.get("path", "")
                        if "path_thumb" not in entry or not entry["path_thumb"]:
                            stem = Path(path).stem
                            suffix = Path(path).suffix
                            parent = str(Path(path).parent)
                            thumb_rel = f"{parent}/{stem}{SUFFIX_THUMB}{suffix}"
                            if (OUTPUT_DIR / thumb_rel).exists():
                                entry["path_thumb"] = thumb_rel
                                updated += 1

    # Write back
    with open(INDEX_FILE, "w", encoding="utf-8") as f:
        json.dump(index, f, indent=2, ensure_ascii=False)

    logger.info(f"Updated {updated} entries in {INDEX_FILE}")

def _update_entry(entry: dict, count: int) -> None:
    """Update a single index entry with thumb path if missing."""
    path = entry.get("path", "")
    if "path_thumb" not in entry or not entry["path_thumb"]:
        stem = Path(path).stem
        suffix = Path(path).suffix
        parent = str(Path(path).parent)
        thumb_rel = f"{parent}/{stem}{SUFFIX_THUMB}{suffix}"
        if (OUTPUT_DIR / thumb_rel).exists():
            entry["path_thumb"] = thumb_rel

def main():
    parser = argparse.ArgumentParser(description="Generate thumbnails from existing photos")
    parser.add_argument("--apply", action="store_true", help="Actually create files (default is dry-run)")
    parser.add_argument("--update-index", action="store_true", help="Update photo_index.json with thumb paths")
    parser.add_argument("--categories", nargs="+", default=["dishes", "restaurants"],
                        help="Categories to process (default: dishes restaurants)")
    args = parser.parse_args()

    logger.info(f"Output dir: {OUTPUT_DIR}")
    logger.info(f"Thumb size: {SIZE_THUMB}")
    logger.info(f"Categories: {args.categories}")
    logger.info(f"Mode: {'APPLY' if args.apply else 'DRY-RUN'}")
    logger.info("")

    pairs = scan_missing_thumbs(args.categories)
    logger.info(f"Found {len(pairs)} originals missing thumbnails")

    if not pairs:
        logger.info("Nothing to do!")
        return

    # Show sample
    for orig, thumb in pairs[:5]:
        logger.info(f"  {orig.relative_to(OUTPUT_DIR)} -> {thumb.name}")
    if len(pairs) > 5:
        logger.info(f"  ...and {len(pairs) - 5} more")

    if not args.apply:
        logger.info("")
        logger.info("This is a DRY-RUN. Use --apply to create thumbnails.")
        return

    created = generate_thumbs(pairs, apply=True)
    logger.info(f"Created {created} thumbnails")

    if args.update_index:
        update_index(args.categories)

if __name__ == "__main__":
    main()
