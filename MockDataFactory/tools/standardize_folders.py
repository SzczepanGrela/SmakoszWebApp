import logging
import os
import shutil
import sys
from pathlib import Path

# Add parent to path for imports
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.utils import slugify

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

def standardize_folder_level(parent_dir: Path, recursive: bool = False):
    """
    Standardize folder names in parent_dir to snake_case using slugify.
    Handles collisions by merging.
    """
    if not parent_dir.exists():
        logger.warning(f"Directory not found: {parent_dir}")
        return

    # List all subdirectories
    subdirs = [d for d in parent_dir.iterdir() if d.is_dir()]

    for folder in subdirs:
        original_name = folder.name
        slugified_name = slugify(original_name)

        # Check if recursion is needed (e.g. for dishes -> category -> variant)
        if recursive:
             standardize_folder_level(folder, recursive=False)

        if original_name == slugified_name:
            continue

        target_path = parent_dir / slugified_name

        if target_path.exists():
            logger.info(f"Merging: Merging '{original_name}' -> '{slugified_name}' (Target exists)")
            # Merge contents
            for item in folder.iterdir():
                dest = target_path / item.name
                if dest.exists():
                    logger.warning(f"  WARNING: Skipping duplicate file: {item.name}")
                    continue
                shutil.move(str(item), str(dest))

            # Remove old folder if empty
            try:
                folder.rmdir()
                logger.info(f"  OK: Removed empty source: {original_name}")
            except OSError:
                logger.warning(f"  ERROR: Could not remove source (not empty?): {original_name}")
        else:
            logger.info(f"Renaming: Renaming '{original_name}' -> '{slugified_name}'")
            folder.rename(target_path)

def main():
    output_dir = Path(os.getenv("IMAGE_OUTPUT_DIR") or PHOTO_CONFIG["output_dir"])
    logger.info(f"Standardizing folders in: {output_dir}")

    # 1. Restaurants (Depth 1)
    logger.info("--- Processing RESTAURANTS ---")
    standardize_folder_level(output_dir / "restaurants", recursive=False)

    # 2. Ingredients (Depth 1)
    logger.info("--- Processing INGREDIENTS ---")
    standardize_folder_level(output_dir / "ingredients", recursive=False)

    # 3. Dishes (Depth 2: Category -> Variant)
    logger.info("--- Processing DISHES ---")
    # First standardize Categories
    standardize_folder_level(output_dir / "dishes", recursive=True)

    # 4. Avatars
    logger.info("--- Processing AVATARS ---")
    standardize_folder_level(output_dir / "avatars", recursive=True)

    # 5. Hero
    logger.info("--- Processing HERO ---")
    standardize_folder_level(output_dir / "hero", recursive=False)

    logger.info("Done.")

if __name__ == "__main__":
    main()
