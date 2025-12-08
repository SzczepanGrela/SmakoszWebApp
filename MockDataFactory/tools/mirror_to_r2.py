"""
Mirror Photos to Cloudflare R2

This script creates a mirror of the local 'images/' directory in a Cloudflare R2 bucket
under a specific prefix to keep mock data separate from production uploads.

Path Structure:
- smakosz/images/mock/     - Generated dishes, restaurants, avatars (SYNCED)
- smakosz/images/ingredients/ - Shared ingredient icons (UPLOAD ONLY)
- smakosz/images/{entity}/    - Real user uploads (NEVER TOUCHED)

Features:
- "Mirror" logic with prefix-scoped deletion (safe for other prefixes)
- Deletes orphaned files ONLY within the mock prefix
- Multi-threaded upload/delete using boto3
- Safety prompt before deletion
"""

import logging
import mimetypes
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

# Load environment variables first
from dotenv import load_dotenv

load_dotenv()

import boto3
from botocore.exceptions import ClientError
from tqdm import tqdm

# Adjust path to find config module
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG

# Configure Logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

# Load R2 Config from Environment
R2_ENDPOINT_URL = os.getenv("R2_ENDPOINT_URL")
R2_ACCESS_KEY_ID = os.getenv("R2_ACCESS_KEY_ID")
R2_SECRET_ACCESS_KEY = os.getenv("R2_SECRET_ACCESS_KEY")
R2_BUCKET_NAME = os.getenv("R2_BUCKET_NAME")

# Local Config
LOCAL_IMAGES_DIR = Path(str(PHOTO_CONFIG["output_dir"]))
WORKERS = 10

# R2 Path Prefixes (v2 Architecture)
MOCK_PREFIX = "smakosz/images/mock/"           # For generated mock data (dishes, restaurants, avatars)
INGREDIENTS_PREFIX = "smakosz/images/ingredients/"  # For shared ingredient icons

class R2Mirror:
    def __init__(self):
        self._validate_config()
        self.s3_client = boto3.client(
            service_name="s3",
            endpoint_url=R2_ENDPOINT_URL,
            aws_access_key_id=R2_ACCESS_KEY_ID,
            aws_secret_access_key=R2_SECRET_ACCESS_KEY,
        )
        self.bucket_name = R2_BUCKET_NAME

    def _validate_config(self):
        if not all([R2_ENDPOINT_URL, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME]):
            logger.error("Missing R2 configuration in environment variables.")
            sys.exit(1)

        if not LOCAL_IMAGES_DIR.exists():
            logger.error(f"Local images directory does not exist: {LOCAL_IMAGES_DIR}")
            sys.exit(1)

    def get_remote_keys(self) -> set[str]:
        """Fetch list of all keys in the bucket."""
        logger.info("Fetching list of existing files in R2...")
        keys = set()
        paginator = self.s3_client.get_paginator("list_objects_v2")

        try:
            for page in paginator.paginate(Bucket=self.bucket_name):
                if "Contents" in page:
                    for obj in page["Contents"]:
                        keys.add(obj["Key"])
        except ClientError as e:
            logger.error(f"Failed to list objects: {e}")
            sys.exit(1)

        logger.info(f"Found {len(keys)} existing files in bucket.")
        return keys

    def delete_batch(self, keys: list[str]) -> int:
        """Delete a batch of keys from R2."""
        if not keys:
            return 0
        
        # S3 delete_objects takes max 1000 keys
        chunk_size = 1000
        deleted_count = 0
        
        for i in range(0, len(keys), chunk_size):
            chunk = keys[i : i + chunk_size]
            delete_request = {'Objects': [{'Key': k} for k in chunk]}
            try:
                self.s3_client.delete_objects(Bucket=self.bucket_name, Delete=delete_request)
                deleted_count += len(chunk)
            except ClientError as e:
                logger.error(f"Failed to delete batch: {e}")
        
        return deleted_count

    def upload_file(self, local_path: Path, r2_key: str) -> bool:
        """Upload a single file to R2."""
        mime_type, _ = mimetypes.guess_type(local_path)
        if not mime_type:
            mime_type = "application/octet-stream"

        try:
            self.s3_client.upload_file(
                str(local_path),
                self.bucket_name,
                r2_key,
                ExtraArgs={
                    "ContentType": mime_type,
                    "CacheControl": "public, max-age=31536000",
                },
            )
            return True
        except ClientError as e:
            logger.error(f"Failed to upload {r2_key}: {e}")
            return False

    def run(self):
        """Execute the Sync process with interactive mode selection."""
        print("\n--- R2 Sync Tool (v2 - Prefix Architecture) ---")
        print("1. Dosyłanie (Upload Only) - Nadpisuje pliki, NIE usuwa nic z R2.")
        print("2. Mirror Mock (Pełna Synchronizacja) - Nadpisuje pliki i USUWA z R2 (TYLKO smakosz/images/mock/).")
        print(f"\nPrefix for mock data: {MOCK_PREFIX}")
        print(f"Prefix for ingredients: {INGREDIENTS_PREFIX}")
        
        mode = input("\nWybierz tryb (1/2): ").strip()
        
        if mode not in ["1", "2"]:
            print("Niepoprawny wybór. Anulowano.")
            return

        logger.info(f"Scanning local directory: {LOCAL_IMAGES_DIR}")

        # 1. Scan local files and categorize by type
        mock_files: dict[str, Path] = {}      # dishes/, restaurants/, avatars/
        ingredient_files: dict[str, Path] = {}  # ingredients/
        
        for path in LOCAL_IMAGES_DIR.rglob("*"):
            if path.is_file() and path.suffix.lower() in {".webp", ".jpg", ".jpeg", ".png", ".svg"}:
                rel_path = str(path.relative_to(LOCAL_IMAGES_DIR)).replace("\\", "/")
                
                if rel_path.startswith("ingredients/"):
                    # Ingredients go to smakosz/images/ingredients/
                    r2_key = INGREDIENTS_PREFIX + rel_path.replace("ingredients/", "")
                    ingredient_files[r2_key] = path
                else:
                    # Everything else (dishes, restaurants, avatars) goes to smakosz/images/mock/
                    r2_key = MOCK_PREFIX + rel_path
                    mock_files[r2_key] = path

        all_local_files = {**mock_files, **ingredient_files}
        mock_keys = set(mock_files.keys())
        
        logger.info(f"Found {len(mock_files)} mock files (dishes/restaurants/avatars)")
        logger.info(f"Found {len(ingredient_files)} ingredient files")

        # 2. Get remote files (only within our prefixes for efficiency)
        remote_keys = self.get_remote_keys()
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
                
                res = input("Czy na pewno chcesz USUNĄĆ te pliki z R2? (tak/nie): ").lower().strip()
                if res in ["tak", "yes", "y"]:
                    logger.info("Deleting orphaned mock files...")
                    count = self.delete_batch(to_delete)
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
            futures = [executor.submit(self.upload_file, all_local_files[key], key) for key in to_upload]
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
