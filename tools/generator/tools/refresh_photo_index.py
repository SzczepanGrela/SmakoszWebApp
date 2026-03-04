import argparse
import json
import logging
import os
import sys
from pathlib import Path

from tqdm import tqdm

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from config import PHOTO_CONFIG
from tools.utils import slugify
from utils.image_processor import generate_blurhash
from utils.logging_config import LoggingConfig
from utils.photo_index_manager import PhotoIndexManager

logger = logging.getLogger(__name__)

IMAGE_EXTENSIONS = {".webp", ".jpg", ".jpeg", ".png"}
DERIVED_SUFFIXES = {"_thumb", "_tiny", "_hero"}
WORKERS = 8

def is_original(path: Path) -> bool:
    return not any(path.stem.endswith(s) for s in DERIVED_SUFFIXES)

def rename_files_in_folder(folder: Path, prefix: str) -> dict[str, str]:
    renames = {}
    originals = sorted([f for f in folder.iterdir() if f.suffix.lower() in IMAGE_EXTENSIONS and is_original(f)])

    temp_map: list[tuple[Path, str]] = []
    for idx, old_file in enumerate(originals, start=1):
        final_name = f"{prefix}_{idx:03d}.webp"
        temp_name = f"__temp_{idx:03d}.webp"
        temp_path = folder / temp_name

        for suffix in DERIVED_SUFFIXES:
            derived = folder / f"{old_file.stem}{suffix}{old_file.suffix}"
            if derived.exists():
                derived_temp = folder / f"__temp_{idx:03d}{suffix}{old_file.suffix}"
                derived.rename(derived_temp)

        if old_file.name != final_name:
            renames[str(old_file)] = str(folder / final_name)
        old_file.rename(temp_path)
        temp_map.append((temp_path, final_name))

    for temp_path, final_name in temp_map:
        final_path = folder / final_name
        temp_path.rename(final_path)

        idx_str = temp_path.stem.replace("__temp_", "")
        base_name = final_path.stem
        for suffix in DERIVED_SUFFIXES:
            derived_temp = folder / f"__temp_{idx_str}{suffix}{temp_path.suffix}"
            if derived_temp.exists():
                derived_final = folder / f"{base_name}{suffix}{final_path.suffix}"
                derived_temp.rename(derived_final)

    return renames

def process_photo_entry(entry: dict, output_root: Path, generate_hash: bool) -> tuple[dict, bool]:
    if not generate_hash:
        return entry, False

    if entry.get("blurhash") and entry.get("width") and entry.get("height"):
        return entry, False

    full_path = output_root / entry["path"]
    if not full_path.exists():
        return entry, False

    hash_val, width, height = generate_blurhash(full_path)
    if hash_val:
        entry["blurhash"] = hash_val
        entry["width"] = width
        entry["height"] = height
        return entry, True

    return entry, False

def refresh_index(generate_hash: bool = True, rename_files: bool = False):
    index_path = Path(str(PHOTO_CONFIG["local_photo_index"]))
    output_root = Path(str(PHOTO_CONFIG["output_dir"]))
    mgr = PhotoIndexManager(index_path)

    renamed_count = 0
    if rename_files:
        logger.info("Renaming files to standard format...")

        dishes_dir = output_root / "dishes"
        if dishes_dir.exists():
            for category_dir in dishes_dir.iterdir():
                if not category_dir.is_dir():
                    continue
                for variant_dir in category_dir.iterdir():
                    if not variant_dir.is_dir():
                        continue
                    prefix = slugify(variant_dir.name)
                    renames = rename_files_in_folder(variant_dir, prefix)
                    if renames:
                        logger.info(f"[DISHES] {variant_dir.name}: {len(renames)} files")
                        renamed_count += len(renames)

        rest_dir = output_root / "restaurants"
        if rest_dir.exists():
            for theme_dir in rest_dir.iterdir():
                if not theme_dir.is_dir():
                    continue
                prefix = slugify(theme_dir.name)
                renames = rename_files_in_folder(theme_dir, prefix)
                if renames:
                    logger.info(f"[RESTAURANTS] {theme_dir.name}: {len(renames)} files")
                    renamed_count += len(renames)

        avatars_dir = output_root / "avatars" / "pool"
        if avatars_dir.exists():
            renames = rename_files_in_folder(avatars_dir, "avatar")
            if renames:
                logger.info(f"[AVATARS] pool: {len(renames)} files")
                renamed_count += len(renames)

        ing_dir = output_root / "ingredients"
        if ing_dir.exists():
            for ing_folder in ing_dir.iterdir():
                if ing_folder.is_dir():
                    prefix = slugify(ing_folder.name)
                    renames = rename_files_in_folder(ing_folder, prefix)
                    if renames:
                        logger.info(f"[INGREDIENTS] {ing_folder.name}: {len(renames)} files")
                        renamed_count += len(renames)

        hero_dir = output_root / "hero"
        if hero_dir.exists():
            renames = rename_files_in_folder(hero_dir, "hero")
            if renames:
                logger.info(f"[HERO] hero: {len(renames)} files")
                renamed_count += len(renames)

        logger.info(f"Total renamed: {renamed_count} files")

    if not index_path.exists():
        logger.info(f"Creating new index: {index_path}")
    index_data = mgr.load()

    logger.info("Synchronizing index with files on disk...")

    removed_count = 0
    added_count = 0
    blurhash_count = 0

    dishes_dir = output_root / "dishes"
    if "dishes" not in index_data:
        index_data["dishes"] = {}

    if dishes_dir.exists():
        indexed_dish_paths = set()
        for _category, variants in index_data["dishes"].items():
            for _variant, photos in variants.items():
                for photo in photos:
                    indexed_dish_paths.add(photo["path"])

        for category, variants in list(index_data["dishes"].items()):
            for variant, photos in list(variants.items()):
                valid_photos = []
                for photo in photos:
                    full_path = output_root / photo["path"]
                    if full_path.exists():
                        valid_photos.append(photo)
                    else:
                        logger.info(f"Removed: {photo['path']}")
                        removed_count += 1
                variants[variant] = valid_photos

        for category_dir in dishes_dir.iterdir():
            if not category_dir.is_dir():
                continue
            category = category_dir.name

            if category not in index_data["dishes"]:
                index_data["dishes"][category] = {}

            for variant_dir in category_dir.iterdir():
                if not variant_dir.is_dir():
                    continue
                variant = variant_dir.name

                if variant not in index_data["dishes"][category]:
                    index_data["dishes"][category][variant] = []

                for img_file in variant_dir.iterdir():
                    if img_file.suffix.lower() in IMAGE_EXTENSIONS and is_original(img_file):
                        rel_path = str(img_file.relative_to(output_root)).replace("\\", "/")
                        if rel_path not in indexed_dish_paths:
                            new_entry = {"path": rel_path, "blurhash": None, "width": None, "height": None}
                            if generate_hash:
                                hash_val, w, h = generate_blurhash(img_file)
                                new_entry.update({"blurhash": hash_val, "width": w, "height": h})
                            index_data["dishes"][category][variant].append(new_entry)
                            logger.info(f"Added: {rel_path}")
                            added_count += 1

    restaurants_dir = output_root / "restaurants"
    if "restaurants" not in index_data:
        index_data["restaurants"] = {}

    if restaurants_dir.exists():
        indexed_rest_paths = set()
        for _theme, photos in index_data["restaurants"].items():
            for photo in photos:
                indexed_rest_paths.add(photo["path"])

        for theme, photos in list(index_data["restaurants"].items()):
            valid_photos = []
            for photo in photos:
                full_path = output_root / photo["path"]
                if full_path.exists():
                    valid_photos.append(photo)
                else:
                    logger.info(f"Removed: {photo['path']}")
                    removed_count += 1
            index_data["restaurants"][theme] = valid_photos

        for theme_dir in restaurants_dir.iterdir():
            if not theme_dir.is_dir():
                continue
            theme = theme_dir.name

            if theme not in index_data["restaurants"]:
                index_data["restaurants"][theme] = []

            for img_file in theme_dir.iterdir():
                if img_file.suffix.lower() in IMAGE_EXTENSIONS and is_original(img_file):
                    rel_path = str(img_file.relative_to(output_root)).replace("\\", "/")
                    if rel_path not in indexed_rest_paths:
                        new_entry = {"path": rel_path, "blurhash": None, "width": None, "height": None}
                        if generate_hash:
                            hash_val, w, h = generate_blurhash(img_file)
                            new_entry.update({"blurhash": hash_val, "width": w, "height": h})
                        index_data["restaurants"][theme].append(new_entry)
                        logger.info(f"Added: {rel_path}")
                        added_count += 1

    avatars_dir = output_root / "avatars" / "pool"
    if "avatars" not in index_data:
        index_data["avatars"] = []

    if avatars_dir.exists():
        indexed_avatar_paths = {photo["path"] for photo in index_data["avatars"]}

        valid_avatars = []
        for photo in index_data["avatars"]:
            full_path = output_root / photo["path"]
            if full_path.exists():
                valid_avatars.append(photo)
            else:
                logger.info(f"Removed: {photo['path']}")
                removed_count += 1
        index_data["avatars"] = valid_avatars

        for img_file in avatars_dir.iterdir():
            if img_file.suffix.lower() in IMAGE_EXTENSIONS and is_original(img_file):
                rel_path = str(img_file.relative_to(output_root)).replace("\\", "/")
                if rel_path not in indexed_avatar_paths:
                    new_entry = {"path": rel_path, "blurhash": None, "width": None, "height": None}
                    if generate_hash:
                        hash_val, w, h = generate_blurhash(img_file)
                        new_entry.update({"blurhash": hash_val, "width": w, "height": h})
                    index_data["avatars"].append(new_entry)
                    logger.info(f"Added: {rel_path}")
                    added_count += 1

    ingredients_dir = output_root / "ingredients"
    if "ingredients" not in index_data:
        index_data["ingredients"] = {}

    if ingredients_dir.exists():
        indexed_ing_paths = {v["path"] for v in index_data["ingredients"].values()}

        keys_to_remove = []
        for ing_name, photo in index_data["ingredients"].items():
            full_path = output_root / photo["path"]
            if not full_path.exists():
                logger.info(f"Removed: {photo['path']}")
                keys_to_remove.append(ing_name)
                removed_count += 1

        for k in keys_to_remove:
            del index_data["ingredients"][k]

        for img_file in ingredients_dir.iterdir():
            if img_file.suffix.lower() in IMAGE_EXTENSIONS:
                rel_path = str(img_file.relative_to(output_root)).replace("\\", "/")
                if rel_path not in indexed_ing_paths:
                    ing_name = img_file.stem
                    if ing_name not in index_data["ingredients"]:
                        new_entry = {"path": rel_path, "blurhash": None, "width": None, "height": None}
                        if generate_hash:
                            hash_val, w, h = generate_blurhash(img_file)
                            new_entry.update({"blurhash": hash_val, "width": w, "height": h})
                        index_data["ingredients"][ing_name] = new_entry
                        logger.info(f"Added: {rel_path} (as '{ing_name}')")
                        added_count += 1

    hero_dir = output_root / "hero"
    hero_index_path = hero_dir / "hero_index.json"

    if hero_dir.exists():
        if hero_index_path.exists():
            with open(hero_index_path, encoding="utf-8") as f:
                hero_index = json.load(f)
        else:
            hero_index = {"images": []}

        indexed_hero_filenames = {img.get("filename") for img in hero_index.get("images", []) if img.get("filename")}

        valid_images = []
        for img_entry in hero_index.get("images", []):
            filename = img_entry.get("filename")
            if filename:
                full_path = hero_dir / filename
                if full_path.exists():
                    valid_images.append(img_entry)
                else:
                    logger.info(f"Removed hero: {filename}")
                    removed_count += 1
        hero_index["images"] = valid_images

        for img_file in hero_dir.iterdir():
            if img_file.suffix.lower() in IMAGE_EXTENSIONS:
                filename = img_file.name
                if filename not in indexed_hero_filenames:
                    new_entry = {
                        "filename": filename,
                        "credit_user": "Unknown",
                        "credit_url": "",
                        "source": "unknown",
                    }
                    if generate_hash:
                        hash_val, w, h = generate_blurhash(img_file)
                        if hash_val:
                            new_entry["blurhash"] = hash_val
                            new_entry["width"] = w
                            new_entry["height"] = h
                    hero_index["images"].append(new_entry)
                    logger.info(f"Added hero: {filename}")
                    added_count += 1

        if generate_hash:
            for img_entry in hero_index.get("images", []):
                if not img_entry.get("blurhash"):
                    filename = img_entry.get("filename")
                    if filename:
                        full_path = hero_dir / filename
                        if full_path.exists():
                            hash_val, w, h = generate_blurhash(full_path)
                            if hash_val:
                                img_entry["blurhash"] = hash_val
                                img_entry["width"] = w
                                img_entry["height"] = h
                                blurhash_count += 1

        with open(hero_index_path, "w", encoding="utf-8") as f:
            json.dump(hero_index, f, indent=2, ensure_ascii=False)

    if generate_hash:
        logger.info("Generating missing blurhash...")
        entries_to_update = []

        for _category, variants in index_data.get("dishes", {}).items():
            for _variant, photos in variants.items():
                for photo in photos:
                    if not photo.get("blurhash"):
                        entries_to_update.append((photo, output_root))

        for _theme, photos in index_data.get("restaurants", {}).items():
            for photo in photos:
                if not photo.get("blurhash"):
                    entries_to_update.append((photo, output_root))

        for photo in index_data.get("avatars", []):
            if not photo.get("blurhash"):
                entries_to_update.append((photo, output_root))

        for _ing_name, photo in index_data.get("ingredients", {}).items():
            if not photo.get("blurhash"):
                entries_to_update.append((photo, output_root))

        if entries_to_update:
            logger.info(f"Found {len(entries_to_update)} entries without blurhash")

            for entry, root in tqdm(
                entries_to_update, desc="Generowanie blurhash", mininterval=1.0, disable=LoggingConfig.is_quiet()
            ):
                full_path = root / entry["path"]
                if full_path.exists():
                    hash_val, w, h = generate_blurhash(full_path)
                    if hash_val:
                        entry["blurhash"] = hash_val
                        entry["width"] = w
                        entry["height"] = h
                        blurhash_count += 1

    mgr.save(index_data)

    logger.info("-" * 50)
    logger.info("Synchronization completed:")
    logger.info(f"  - Removed:     {removed_count} dead entries")
    logger.info(f"  - Added:       {added_count} new files")
    if generate_hash:
        logger.info(f"  - Blurhash:    {blurhash_count} generated")
    if rename_files:
        logger.info(f"  - Renamed:     {renamed_count} files")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Synchronizacja photo_index.json")
    parser.add_argument("--no-blurhash", action="store_true", help="Pomiń generowanie blurhash")
    parser.add_argument("--rename", action="store_true", help="Przemianuj pliki do formatu folder_001.webp")
    parser.add_argument("--quiet", "-q", action="store_true", help="Only show warnings and errors")
    parser.add_argument("--debug", "-d", action="store_true", help="Show DEBUG logs (most verbose)")
    args = parser.parse_args()

    log_level = "DEBUG" if args.debug else "INFO"
    LoggingConfig.setup(level=log_level, quiet=args.quiet)

    refresh_index(generate_hash=not args.no_blurhash, rename_files=args.rename)
