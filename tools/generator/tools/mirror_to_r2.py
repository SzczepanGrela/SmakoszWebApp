"""
Mirror Photos to Cloudflare R2

This script creates a mirror of the local 'images/' directory in a Cloudflare R2 bucket
under a specific prefix to keep mock data separate from production uploads.

Path Structure:
- seed/                    - Generated dishes, restaurants, avatars, hero (SYNCED)
- seed/ingredients/        - Shared ingredient icons (UPLOAD ONLY)
- uploads/{entity}/        - Real user uploads (NEVER TOUCHED)

Features:
- "Mirror" logic with prefix-scoped deletion (safe for other prefixes)
- Deletes orphaned files ONLY within the mock prefix
- Multi-threaded upload/delete using boto3
- Safety prompt before deletion
- CloudStorageProvider abstraction for testability and provider swapping
"""

import logging
import mimetypes
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

# Load environment variables first
from dotenv import load_dotenv

load_dotenv(Path(__file__).resolve().parent.parent.parent.parent / ".env")

from tqdm import tqdm

# Adjust path to find config module
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.storage.cloud_storage import CloudStorageProvider, R2Provider

# Configure Logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

# Local Config
LOCAL_IMAGES_DIR = Path(str(PHOTO_CONFIG["output_dir"]))
WORKERS = 10

# R2 Path Prefixes (v3 Architecture)
MOCK_PREFIX = "seed/"  # For generated mock data (dishes, restaurants, avatars, hero)
INGREDIENTS_PREFIX = "seed/ingredients/"  # For shared ingredient icons

class R2Mirror:
    def __init__(self, provider: CloudStorageProvider | None = None):
        if provider is None:
            try:
                provider = R2Provider.from_env()
            except ValueError as e:
                logger.error(str(e))
                sys.exit(1)
        self.provider = provider

        if not LOCAL_IMAGES_DIR.exists():
            logger.error(f"Local images directory does not exist: {LOCAL_IMAGES_DIR}")
            sys.exit(1)

    def run(self):
        """Execute the Sync process with interactive mode selection."""
        print("\n--- R2 Sync Tool (v2 - Prefix Architecture) ---")
        print("1. Upload Only - Overwrites files, does NOT delete anything from R2.")
        print("2. Mirror Mock (Full Sync) - Overwrites files and DELETES from R2 (ONLY seed/).")
        print(f"\nPrefix for mock data: {MOCK_PREFIX}")
        print(f"Prefix for ingredients: {INGREDIENTS_PREFIX}")

        mode = input("\nSelect mode (1/2): ").strip()

        if mode not in ["1", "2"]:
            print("Invalid selection. Cancelled.")
            return

        logger.info(f"Scanning local directory: {LOCAL_IMAGES_DIR}")

        # 1. Scan local files and categorize by type
        mock_files: dict[str, Path] = {}  # dishes/, restaurants/, avatars/
        ingredient_files: dict[str, Path] = {}  # ingredients/

        for path in LOCAL_IMAGES_DIR.rglob("*"):
            if path.is_file() and path.suffix.lower() in {".webp", ".jpg", ".jpeg", ".png", ".svg"}:
                rel_path = str(path.relative_to(LOCAL_IMAGES_DIR)).replace("\\", "/")

                if rel_path.startswith("ingredients/"):
                    # Ingredients go to seed/ingredients/
                    r2_key = INGREDIENTS_PREFIX + rel_path.replace("ingredients/", "")
                    ingredient_files[r2_key] = path
                else:
                    # Everything else (dishes, restaurants, avatars, hero) goes to seed/
                    r2_key = MOCK_PREFIX + rel_path
                    mock_files[r2_key] = path

        all_local_files = {**mock_files, **ingredient_files}
        mock_keys = set(mock_files.keys())

        logger.info(f"Found {len(mock_files)} mock files (dishes/restaurants/avatars)")
        logger.info(f"Found {len(ingredient_files)} ingredient files")

        # 2. Get remote files (only within our prefixes for efficiency)
        remote_keys = self.provider.list_keys()
        remote_mock_keys = {k for k in remote_keys if k.startswith(MOCK_PREFIX)}

        logger.info(f"Found {len(remote_mock_keys)} existing mock files in R2")
        logger.info(f"Found {len(remote_keys) - len(remote_mock_keys)} other files in R2 (PROTECTED)")

        # 3. Calculate Operations
        to_upload = list(all_local_files.keys())  # Upload all local files

        if mode == "2":  # Mirror Mode - ONLY delete from MOCK_PREFIX
            to_delete = list(remote_mock_keys - mock_keys)

            if to_delete:
                logger.warning("=" * 60)
                logger.warning(f"WARNING: Found {len(to_delete)} orphaned files in MOCK prefix.")
                logger.warning(f"These files will be DELETED from: {MOCK_PREFIX}")
                logger.warning("Sample deletion: " + to_delete[0])
                logger.warning("=" * 60)
                logger.info("NOTE: Files outside mock prefix are PROTECTED and will NOT be deleted.")

                res = input("Are you sure you want to DELETE these files from R2? (yes/no): ").lower().strip()
                if res in ["yes", "y"]:
                    logger.info("Deleting orphaned mock files...")
                    count = self.provider.delete_batch(to_delete)
                    logger.info(f"Deleted {count} files from mock prefix.")
                else:
                    logger.info("Deletion skipped by user.")
            else:
                logger.info("No orphaned files in mock prefix (clean).")
        else:
            logger.info("Mode 1 selected: Skipping deletion check.")

        # 4. Handle Uploads
        logger.info(f"Files to upload/update: {len(to_upload)}")

        if not to_upload:
            logger.info("Nothing to upload.")
            return

        logger.info(f"Starting upload with {WORKERS} workers...")

        success_count = 0
        with ThreadPoolExecutor(max_workers=WORKERS) as executor:
            futures = [executor.submit(self.provider.upload_file, all_local_files[key], key) for key in to_upload]
            for f in tqdm(futures, total=len(to_upload), unit="img", desc="Uploading"):
                if f.result():
                    success_count += 1

        logger.info("=" * 60)
        logger.info(f"Operation complete. Uploaded {success_count}/{len(to_upload)} files.")
        logger.info(f"  - Mock files: {len(mock_files)} (to {MOCK_PREFIX})")
        logger.info(f"  - Ingredient files: {len(ingredient_files)} (to {INGREDIENTS_PREFIX})")
        logger.info("=" * 60)

if __name__ == "__main__":
    mimetypes.add_type("image/webp", ".webp")
    mirror = R2Mirror()
    mirror.run()
