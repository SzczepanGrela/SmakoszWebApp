"""
Refresh Photo Index

Synchronizes photo_index.json with actual files on disk:
- Removes entries for deleted files
- Adds new files found on disk with blurhash generation
- Generates blurhash for existing entries missing it
- Optionally renames files to standard format: foldername_001.webp

Usage:
    python tools/refresh_photo_index.py
    python tools/refresh_photo_index.py --no-blurhash  # Skip blurhash generation
    python tools/refresh_photo_index.py --rename       # Rename files to standard format
"""

import argparse
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import blurhash
import numpy as np
from PIL import Image
from tqdm import tqdm

# Fix Windows console encoding for Polish characters
if sys.platform == "win32":
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# Add parent directory to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from config import PHOTO_CONFIG
from tools.utils import slugify

IMAGE_EXTENSIONS = {".webp", ".jpg", ".jpeg", ".png"}
DERIVED_SUFFIXES = {"_thumb", "_tiny", "_hero"}
WORKERS = 8

def is_original(path: Path) -> bool:
    """Check if file is an original (not a derived _thumb/_tiny/_hero variant)."""
    return not any(path.stem.endswith(s) for s in DERIVED_SUFFIXES)

def rename_files_in_folder(folder: Path, prefix: str) -> dict[str, str]:
    """
    Rename original image files in folder to prefix_001.webp format.
    Also renames associated derived files (_thumb, _tiny) to match.
    Returns mapping of old_path -> new_path (relative).
    """
    renames = {}
    # Only rename originals - derived files follow their original
    originals = sorted([f for f in folder.iterdir()
                        if f.suffix.lower() in IMAGE_EXTENSIONS and is_original(f)])
    
    # Phase 1: rename originals to temp names to avoid collisions
    temp_map: list[tuple[Path, str]] = []  # (temp_path, final_name)
    for idx, old_file in enumerate(originals, start=1):
        final_name = f"{prefix}_{idx:03d}.webp"
        temp_name = f"__temp_{idx:03d}.webp"
        temp_path = folder / temp_name
        
        # Also find and rename associated derived files
        for suffix in DERIVED_SUFFIXES:
            derived = folder / f"{old_file.stem}{suffix}{old_file.suffix}"
            if derived.exists():
                derived_temp = folder / f"__temp_{idx:03d}{suffix}{old_file.suffix}"
                derived.rename(derived_temp)
        
        if old_file.name != final_name:
            renames[str(old_file)] = str(folder / final_name)
        old_file.rename(temp_path)
        temp_map.append((temp_path, final_name))
    
    # Phase 2: rename temp -> final
    for temp_path, final_name in temp_map:
        final_path = folder / final_name
        temp_path.rename(final_path)
        
        # Rename derived temp files too
        idx_str = temp_path.stem.replace("__temp_", "")
        base_name = final_path.stem  # e.g. prefix_001
        for suffix in DERIVED_SUFFIXES:
            derived_temp = folder / f"__temp_{idx_str}{suffix}{temp_path.suffix}"
            if derived_temp.exists():
                derived_final = folder / f"{base_name}{suffix}{final_path.suffix}"
                derived_temp.rename(derived_final)
    
    return renames

def generate_blurhash(image_path: Path) -> tuple[str | None, int | None, int | None]:
    """Generate blurhash and get dimensions for an image file."""
    try:
        with Image.open(image_path) as img:
            if img.mode != "RGB":
                img = img.convert("RGB")
            width, height = img.size
            
            # Resize for faster blurhash computation
            thumb = img.copy()
            thumb.thumbnail((100, 100))

            # Convert PIL Image to numpy array for blurhash
            thumb_array = np.array(thumb)
            hash_value = blurhash.encode(thumb_array, components_x=4, components_y=3)
            return hash_value, width, height
    except Exception as e:
        print(f"  [BŁĄD blurhash]: {image_path}: {e}")
        return None, None, None

def process_photo_entry(entry: dict, output_root: Path, generate_hash: bool) -> tuple[dict, bool]:
    """
    Process a photo entry, optionally generating blurhash.
    Returns (updated_entry, was_updated).
    """
    if not generate_hash:
        return entry, False
    
    # Skip if already has blurhash
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

    # 0. Rename files first (before index sync)
    renamed_count = 0
    if rename_files:
        print("Przemianowywanie plików do standardowego formatu...")
        
        # Dishes: dish/Category/Variant/ -> variant_001.webp
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
                        print(f"  [DISHES] {variant_dir.name}: {len(renames)} plików")
                        renamed_count += len(renames)
        
        # Restaurants: restaurants/Theme/ -> theme_001.webp
        rest_dir = output_root / "restaurants"
        if rest_dir.exists():
            for theme_dir in rest_dir.iterdir():
                if not theme_dir.is_dir():
                    continue
                prefix = slugify(theme_dir.name)
                renames = rename_files_in_folder(theme_dir, prefix)
                if renames:
                    print(f"  [RESTAURANTS] {theme_dir.name}: {len(renames)} plików")
                    renamed_count += len(renames)
        
        # Avatars: avatars/pool/ -> avatar_001.webp
        avatars_dir = output_root / "avatars" / "pool"
        if avatars_dir.exists():
            renames = rename_files_in_folder(avatars_dir, "avatar")
            if renames:
                print(f"  [AVATARS] pool: {len(renames)} plików")
                renamed_count += len(renames)
        
        # Ingredients: ingredients/name/ -> name_001.webp (or single file name.webp)
        ing_dir = output_root / "ingredients"
        if ing_dir.exists():
            for ing_folder in ing_dir.iterdir():
                if ing_folder.is_dir():
                    prefix = slugify(ing_folder.name)
                    renames = rename_files_in_folder(ing_folder, prefix)
                    if renames:
                        print(f"  [INGREDIENTS] {ing_folder.name}: {len(renames)} plików")
                        renamed_count += len(renames)

        # Hero: templates/hero/ -> hero_001.webp
        hero_dir = output_root / "templates" / "hero"
        if hero_dir.exists():
            renames = rename_files_in_folder(hero_dir, "hero")
            if renames:
                print(f"  [HERO] templates/hero: {len(renames)} plików")
                renamed_count += len(renames)

        print(f"  Przemianowano łącznie: {renamed_count} plików\n")

    if not index_path.exists():
        print(f"Tworzenie nowego indeksu: {index_path}")
        index_data = {"dishes": {}, "restaurants": {}, "avatars": [], "ingredients": {}}
    else:
        with open(index_path, "r", encoding="utf-8") as f:
            index_data = json.load(f)

    print("Synchronizacja indeksu z plikami na dysku...")
    
    removed_count = 0
    added_count = 0
    blurhash_count = 0

    # 1. Dishes
    dishes_dir = output_root / "dishes"
    if "dishes" not in index_data:
        index_data["dishes"] = {}
    
    if dishes_dir.exists():
        indexed_dish_paths = set()
        for category, variants in index_data["dishes"].items():
            for variant, photos in variants.items():
                for photo in photos:
                    indexed_dish_paths.add(photo["path"])
        
        # Remove deleted files
        for category, variants in list(index_data["dishes"].items()):
            for variant, photos in list(variants.items()):
                valid_photos = []
                for photo in photos:
                    full_path = output_root / photo["path"]
                    if full_path.exists():
                        valid_photos.append(photo)
                    else:
                        print(f"  [USUNIĘTO]: {photo['path']}")
                        removed_count += 1
                variants[variant] = valid_photos
        
        # Add new files
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
                            print(f"  [DODANO]: {rel_path}")
                            added_count += 1

    # 2. Restaurants
    restaurants_dir = output_root / "restaurants"
    if "restaurants" not in index_data:
        index_data["restaurants"] = {}
    
    if restaurants_dir.exists():
        indexed_rest_paths = set()
        for theme, photos in index_data["restaurants"].items():
            for photo in photos:
                indexed_rest_paths.add(photo["path"])
        
        # Remove deleted
        for theme, photos in list(index_data["restaurants"].items()):
            valid_photos = []
            for photo in photos:
                full_path = output_root / photo["path"]
                if full_path.exists():
                    valid_photos.append(photo)
                else:
                    print(f"  [USUNIĘTO]: {photo['path']}")
                    removed_count += 1
            index_data["restaurants"][theme] = valid_photos
        
        # Add new files
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
                        print(f"  [DODANO]: {rel_path}")
                        added_count += 1

    # 3. Avatars
    avatars_dir = output_root / "avatars" / "pool"
    if "avatars" not in index_data:
        index_data["avatars"] = []
    
    if avatars_dir.exists():
        indexed_avatar_paths = {photo["path"] for photo in index_data["avatars"]}
        
        # Remove deleted
        valid_avatars = []
        for photo in index_data["avatars"]:
            full_path = output_root / photo["path"]
            if full_path.exists():
                valid_avatars.append(photo)
            else:
                print(f"  [USUNIĘTO]: {photo['path']}")
                removed_count += 1
        index_data["avatars"] = valid_avatars
        
        # Add new files
        for img_file in avatars_dir.iterdir():
            if img_file.suffix.lower() in IMAGE_EXTENSIONS and is_original(img_file):
                rel_path = str(img_file.relative_to(output_root)).replace("\\", "/")
                if rel_path not in indexed_avatar_paths:
                    new_entry = {"path": rel_path, "blurhash": None, "width": None, "height": None}
                    if generate_hash:
                        hash_val, w, h = generate_blurhash(img_file)
                        new_entry.update({"blurhash": hash_val, "width": w, "height": h})
                    index_data["avatars"].append(new_entry)
                    print(f"  [DODANO]: {rel_path}")
                    added_count += 1

    # 4. Ingredients
    ingredients_dir = output_root / "ingredients"
    if "ingredients" not in index_data:
        index_data["ingredients"] = {}
    
    if ingredients_dir.exists():
        indexed_ing_paths = {v["path"] for v in index_data["ingredients"].values()}
        
        # Remove deleted
        keys_to_remove = []
        for ing_name, photo in index_data["ingredients"].items():
            full_path = output_root / photo["path"]
            if not full_path.exists():
                print(f"  [USUNIĘTO]: {photo['path']}")
                keys_to_remove.append(ing_name)
                removed_count += 1
        
        for k in keys_to_remove:
            del index_data["ingredients"][k]

        # Add new files
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
                        print(f"  [DODANO]: {rel_path} (jako '{ing_name}')")
                        added_count += 1

    # 5. Hero images (separate hero_index.json)
    hero_dir = output_root / "templates" / "hero"
    hero_index_path = hero_dir / "hero_index.json"

    if hero_dir.exists():
        # Load or create hero index
        if hero_index_path.exists():
            with open(hero_index_path, "r", encoding="utf-8") as f:
                hero_index = json.load(f)
        else:
            hero_index = {"images": []}

        indexed_hero_filenames = {img.get("filename") for img in hero_index.get("images", []) if img.get("filename")}

        # Remove deleted files
        valid_images = []
        for img_entry in hero_index.get("images", []):
            filename = img_entry.get("filename")
            if filename:
                full_path = hero_dir / filename
                if full_path.exists():
                    valid_images.append(img_entry)
                else:
                    print(f"  [USUNIĘTO HERO]: {filename}")
                    removed_count += 1
        hero_index["images"] = valid_images

        # Add new files
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
                    # Generate blurhash if requested
                    if generate_hash:
                        hash_val, w, h = generate_blurhash(img_file)
                        if hash_val:
                            new_entry["blurhash"] = hash_val
                            new_entry["width"] = w
                            new_entry["height"] = h
                    hero_index["images"].append(new_entry)
                    print(f"  [DODANO HERO]: {filename}")
                    added_count += 1

        # Generate blurhash for hero entries missing it
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

        # Save hero index
        with open(hero_index_path, "w", encoding="utf-8") as f:
            json.dump(hero_index, f, indent=2, ensure_ascii=False)

    # 6. Generate blurhash for existing entries missing it
    if generate_hash:
        print("\nGenerowanie brakujących blurhash...")
        entries_to_update = []
        
        # Collect entries missing blurhash
        for category, variants in index_data.get("dishes", {}).items():
            for variant, photos in variants.items():
                for photo in photos:
                    if not photo.get("blurhash"):
                        entries_to_update.append((photo, output_root))
        
        for theme, photos in index_data.get("restaurants", {}).items():
            for photo in photos:
                if not photo.get("blurhash"):
                    entries_to_update.append((photo, output_root))
        
        for photo in index_data.get("avatars", []):
            if not photo.get("blurhash"):
                entries_to_update.append((photo, output_root))
        
        for ing_name, photo in index_data.get("ingredients", {}).items():
            if not photo.get("blurhash"):
                entries_to_update.append((photo, output_root))
        
        if entries_to_update:
            print(f"  Znaleziono {len(entries_to_update)} wpisów bez blurhash")
            
            for entry, root in tqdm(entries_to_update, desc="Generowanie blurhash"):
                full_path = root / entry["path"]
                if full_path.exists():
                    hash_val, w, h = generate_blurhash(full_path)
                    if hash_val:
                        entry["blurhash"] = hash_val
                        entry["width"] = w
                        entry["height"] = h
                        blurhash_count += 1

    # Save
    index_path.parent.mkdir(parents=True, exist_ok=True)
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump(index_data, f, indent=2, ensure_ascii=False)

    print("-" * 50)
    print(f"Synchronizacja zakończona:")
    print(f"  - Usunięto:     {removed_count} martwych wpisów")
    print(f"  - Dodano:       {added_count} nowych plików")
    if generate_hash:
        print(f"  - Blurhash:     {blurhash_count} wygenerowanych")
    if rename_files:
        print(f"  - Przemianowano: {renamed_count} plików")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Synchronizacja photo_index.json")
    parser.add_argument("--no-blurhash", action="store_true", help="Pomiń generowanie blurhash")
    parser.add_argument("--rename", action="store_true", help="Przemianuj pliki do formatu folder_001.webp")
    args = parser.parse_args()
    
    refresh_index(generate_hash=not args.no_blurhash, rename_files=args.rename)

