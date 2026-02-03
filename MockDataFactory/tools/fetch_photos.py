"""
Photo Fetching Script V2 (Class-Based)

Downloads dish and restaurant photos from Pixabay API with intelligent deduplication,
persistent state tracking, and robust error handling.
Implements safe cleanup mechanism with user confirmation and validation.
"""

import json
import logging
import os
import random
import sys
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from pathlib import Path
from typing import Any, cast

from tqdm import tqdm  # type: ignore

# Load environment variables first
from dotenv import load_dotenv

load_dotenv()

import numpy as np
import requests  # type: ignore
from PIL import Image
from requests.adapters import HTTPAdapter  # type: ignore
from urllib3.util.retry import Retry

# BlurHash support for modern PWA placeholders
try:
    import blurhash  # type: ignore
    BLURHASH_AVAILABLE = True
except ImportError:
    BLURHASH_AVAILABLE = False
    logger.warning("blurhash library not found. Install with: pip install blurhash-python")
    logger.warning("BlurHash generation will be skipped.")

# Adjust path to find config module
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.utils import slugify

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

# Configuration
OUTPUT_DIR = Path(cast(str, PHOTO_CONFIG["output_dir"]))
INDEX_FILE = Path(cast(str, PHOTO_CONFIG["local_photo_index"]))
SEEN_URLS_FILE = Path("data/downloaded_urls.json")  # Persistent state file
BLUEPRINTS_DIR = Path(__file__).resolve().parent.parent / "blueprints"

TARGET_SIZE = (cast(int, PHOTO_CONFIG["target_width"]), cast(int, PHOTO_CONFIG["target_height"]))
IMAGE_QUALITY = cast(int, PHOTO_CONFIG["image_quality"])
IMAGE_FORMAT = cast(str, PHOTO_CONFIG["image_format"])
WORKERS = cast(int, PHOTO_CONFIG["workers"])
IMAGES_PER_QUERY = int(PHOTO_CONFIG.get("images_per_query", 200))  # type: ignore

# Multi-size image generation constants
SIZE_HERO = cast(tuple[int, int], PHOTO_CONFIG.get("size_hero", (1600, 900)))
SIZE_FULL = cast(tuple[int, int], PHOTO_CONFIG.get("size_full", (1280, 960)))
SIZE_THUMB = cast(tuple[int, int], PHOTO_CONFIG.get("size_thumb", (200, 150)))
SIZE_TINY = cast(tuple[int, int], PHOTO_CONFIG.get("size_tiny", (50, 50)))
SIZE_AVATAR = cast(tuple[int, int], PHOTO_CONFIG.get("size_avatar", (300, 300)))
SIZE_INGREDIENT = cast(tuple[int, int], PHOTO_CONFIG.get("size_ingredient", (200, 200)))
SUFFIX_HERO = cast(str, PHOTO_CONFIG.get("suffix_hero", "_hero"))
SUFFIX_THUMB = cast(str, PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
SUFFIX_TINY = cast(str, PHOTO_CONFIG.get("suffix_tiny", "_tiny"))

# Import provider manager for multi-source fetching
from tools.image_providers import ProviderManager, ImageResult

class PixabayDownloader:
    """
    Handles downloading, processing, and indexing of photos from multiple sources.
    Supports Pixabay (primary) and Unsplash (secondary) for visual diversity.
    Maintains persistent state to avoid re-downloading images across runs.
    """

    def __init__(self):
        self.api_key = self._get_api_key()
        self.session = self._create_session()
        self.seen_urls: dict[str, str] = self._load_seen_urls()
        self.index: dict[str, Any] = {"dishes": {}, "restaurants": {}, "avatars": []}
        self.restaurant_themes: dict[str, float] = {}
        self.restaurant_pixabay_terms: dict[str, str] = {}
        self._load_restaurant_themes()  # Populates both themes and pixabay_terms
        self.ingredient_mappings: dict[str, str] = self._load_ingredient_mappings()
        
        # Multi-provider support
        self.provider_manager = ProviderManager()

        # Ensure directories exist (but cleanup might remove them first)
        SEEN_URLS_FILE.parent.mkdir(parents=True, exist_ok=True)

    def _get_api_key(self) -> str:
        key = PHOTO_CONFIG.get("pixabay_api_key")
        if not key:
            logger.error("Missing pixabay_api_key in PHOTO_CONFIG (check .env)!")
            sys.exit(1)
        return str(key)

    def _create_session(self) -> requests.Session:
        """Create a requests session with retry logic for resilience."""
        session = requests.Session()
        retries = Retry(
            total=5,
            backoff_factor=1,
            status_forcelist=[429, 500, 502, 503, 504],
            allowed_methods=["GET"],
        )
        adapter = HTTPAdapter(max_retries=retries)
        session.mount("https://", adapter)
        session.mount("http://", adapter)
        return session

    def _load_seen_urls(self) -> dict[str, str]:
        """Load the persistent map of URL -> relative_path."""
        if SEEN_URLS_FILE.exists():
            try:
                with open(SEEN_URLS_FILE, encoding="utf-8") as f:
                    return json.load(f)
            except json.JSONDecodeError:
                logger.warning(f"Corrupted seen URLs file: {SEEN_URLS_FILE}. Starting fresh.")
        return {}

    def _save_seen_urls(self):
        """Save the map of URL -> relative_path to disk."""
        with open(SEEN_URLS_FILE, "w", encoding="utf-8") as f:
            json.dump(self.seen_urls, f, indent=2)

    def _load_restaurant_themes(self) -> None:
        """Load restaurant themes, weights, and pixabay search terms from blueprints."""
        blueprint_path = BLUEPRINTS_DIR / "restaurant_types.json"
        try:
            with open(blueprint_path, encoding="utf-8") as f:
                data = json.load(f)
                theme_config = data.get("RESTAURANT_THEMES", {})
                for theme, config in theme_config.items():
                    # Use distribution_chance as a weight proxy
                    weight = config.get("distribution_chance", 0.05) * 100
                    self.restaurant_themes[theme] = max(1.0, weight)

                    # Extract pixabay_term if available
                    pixabay_term = config.get("pixabay_term")
                    if pixabay_term:
                        self.restaurant_pixabay_terms[theme] = pixabay_term

                logger.info(f"Loaded {len(self.restaurant_themes)} restaurant themes")
                logger.info(f"Loaded {len(self.restaurant_pixabay_terms)} restaurant pixabay search terms")
        except Exception as e:
            logger.warning(f"Could not load restaurant themes from blueprint: {e}. Using defaults.")
            self.restaurant_themes = {
                "Italian": 3.0,
                "Asian": 3.0,
                "American": 2.5,
                "Modern": 4.0,
                "Cozy": 3.0,
                "Bar": 2.0,
                "Cafe": 2.0,
                "Mexican": 2.5,
                "French": 2.5,
            }
            self.restaurant_pixabay_terms = {}

    def _load_ingredient_mappings(self) -> dict[str, str]:
        """Load ingredient English search term mappings from blueprints."""
        mapping_path = BLUEPRINTS_DIR / "ingredients_pixabay.json"
        mappings = {}
        try:
            with open(mapping_path, encoding="utf-8") as f:
                mappings = json.load(f)
                logger.info(f"Loaded {len(mappings)} ingredient search term mappings")
        except FileNotFoundError:
            logger.warning(f"Ingredient mapping file not found: {mapping_path}")
            logger.warning("Will use fallback search terms for ingredients.")
        except Exception as e:
            logger.warning(f"Could not load ingredient mappings: {e}. Using fallback search terms.")
        return mappings

    def _load_dish_variants(self) -> dict[str, Any]:
        """Load dish variants from blueprints/dishes.json."""
        bp_path = BLUEPRINTS_DIR / "dishes.json"
        try:
            with open(bp_path, encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            logger.error(f"Cannot read dish blueprint file: {bp_path} - {e}")
            sys.exit(1)

    def search_pixabay(self, query: str, min_count: int) -> list[str]:
        """Search Pixabay API for unique photo URLs."""
        urls: list[str] = []
        page = 1

        while len(urls) < min_count:
            params = {
                "key": self.api_key,
                "q": query,
                "image_type": "photo",
                "orientation": "horizontal",
                "min_width": 800,
                "safesearch": "true",
                "per_page": IMAGES_PER_QUERY,
                "page": page,
            }

            try:
                response = self.session.get("https://pixabay.com/api/", params=params, timeout=10)
                if response.status_code != 200:
                    logger.warning(f"Pixabay API status {response.status_code} for '{query}'")
                    break

                data = response.json()
                hits = data.get("hits", [])
                if not hits:
                    break

                for hit in hits:
                    url = hit.get("largeImageURL") or hit.get("webformatURL")
                    if url and url not in urls:
                        urls.append(url)

                if len(hits) < IMAGES_PER_QUERY:
                    break
                page += 1

                # Safety break
                if page > 5:
                    break

            except requests.RequestException as e:
                logger.error(f"Network error searching Pixabay for '{query}': {e}")
                break

        return urls[:min_count]

    def search_mixed(
        self,
        query: str,
        count: int,
        orientation: str = "horizontal",
        pixabay_ratio: float = 0.6
    ) -> list[ImageResult]:
        """
        Search multiple providers and mix results for visual diversity.
        
        Args:
            query: Search term
            count: Total results needed
            orientation: 'horizontal' or 'vertical'
            pixabay_ratio: Fraction from Pixabay (default 60%, rest from Unsplash)
            
        Returns:
            Mixed list of ImageResult from multiple providers
        """
        return self.provider_manager.search_mixed(query, count, orientation, pixabay_ratio)

    def search_mixed_urls(
        self,
        query: str,
        count: int,
        orientation: str = "horizontal",
        pixabay_ratio: float = 0.6
    ) -> list[str]:
        """
        Search multiple providers and return URLs only (for compatibility).
        
        Args:
            query: Search term
            count: Total results needed
            orientation: 'horizontal' or 'vertical'
            pixabay_ratio: Fraction from Pixabay (default 60%, rest from Unsplash)
            
        Returns:
            List of image URLs from multiple providers
        """
        results = self.search_mixed(query, count, orientation, pixabay_ratio)
        return [r.url for r in results if r.url]

    def process_image(self, url: str, save_path: Path, target_size: tuple[int, int] | None = None) -> tuple[bool, dict[str, Any] | None]:
        """
        Download, process (resize/crop), generate BlurHash, and save image.

        Args:
            url: Image URL to download
            save_path: Local path to save processed image
            target_size: Optional (width, height) tuple. Defaults to TARGET_SIZE from config.

        Returns:
            tuple[bool, dict | None]: (success, metadata_dict)
            metadata_dict contains: {"blurhash": str | None, "width": int, "height": int}
        """
        if save_path.exists():
            # If file exists, try to generate metadata from existing file
            try:
                existing_img = Image.open(save_path)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")

                metadata = {
                    "blurhash": None,
                    "width": existing_img.width,
                    "height": existing_img.height
                }

                if BLURHASH_AVAILABLE:
                    try:
                        metadata["blurhash"] = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass

                return (True, metadata)
            except Exception:
                return (True, None)

        # Use provided target_size or fall back to config default
        size = target_size if target_size else TARGET_SIZE

        try:
            resp = self.session.get(url, timeout=15)
            if resp.status_code != 200:
                return (False, None)

            img: Image.Image = Image.open(BytesIO(resp.content))
            if img.mode != "RGB":
                img = img.convert("RGB")

            # Minimum resolution filter - reject images smaller than target
            # This ensures we never upscale, only downscale (better quality)
            if img.width < size[0] or img.height < size[1]:
                logger.debug(f"Skipping low-res image: {img.width}x{img.height} (min: {size[0]}x{size[1]})")
                return (False, None)

            # Aspect Ratio Calculation
            img_ratio = img.width / img.height
            target_ratio = size[0] / size[1]

            if img_ratio > target_ratio:
                new_height = size[1]
                new_width = int(new_height * img_ratio)
            else:
                new_width = size[0]
                new_height = int(new_width / img_ratio)

            img = img.resize((new_width, new_height), Image.Resampling.LANCZOS)

            # Center Crop
            left = (new_width - size[0]) / 2
            top = (new_height - size[1]) / 2
            img = img.crop((left, top, left + size[0], top + size[1]))

            # Capture final dimensions after cropping
            final_width = img.width
            final_height = img.height

            # Generate BlurHash (4x3 components = good balance)
            hash_str: str | None = None
            if BLURHASH_AVAILABLE:
                try:
                    hash_str = blurhash.encode(np.array(img), components_x=4, components_y=3)
                except Exception as e:
                    logger.debug(f"BlurHash generation failed for {url}: {e}")

            save_path.parent.mkdir(parents=True, exist_ok=True)
            img.save(save_path, IMAGE_FORMAT, quality=IMAGE_QUALITY)

            # Return metadata dict with blurhash and dimensions
            metadata = {
                "blurhash": hash_str,
                "width": final_width,
                "height": final_height
            }
            return (True, metadata)

        except Exception as e:
            logger.debug(f"Error processing {url}: {e}")
            return (False, None)

    def process_image_multi_size(
        self,
        url: str,
        save_path_full: Path,
        include_tiny: bool = False,
        avatar_mode: bool = False
    ) -> tuple[bool, dict[str, Any] | None]:
        """
        Download image and generate multiple sizes (full, thumb, optionally tiny).
        
        Uses naming convention: file.webp -> file_thumb.webp, file_tiny.webp
        
        Args:
            url: Image URL to download
            save_path_full: Path for full-size image (thumb/tiny derived from this)
            include_tiny: Whether to also generate tiny size (for avatars)
            avatar_mode: If True, uses SIZE_AVATAR for full size (300×300 square)
            
        Returns:
            tuple[bool, dict | None]: (success, metadata_dict)
            metadata_dict contains: {
                "blurhash": str | None,
                "width": int, "height": int,
                "path_thumb": str,
                "path_tiny": str | None
            }
        """
        # Determine sizes based on mode
        full_size = SIZE_AVATAR if avatar_mode else SIZE_FULL
        # Derive paths for other sizes
        stem = save_path_full.stem
        suffix = save_path_full.suffix
        parent = save_path_full.parent
        
        save_path_thumb = parent / f"{stem}{SUFFIX_THUMB}{suffix}"
        save_path_tiny = parent / f"{stem}{SUFFIX_TINY}{suffix}" if include_tiny else None
        
        # Check if all required files already exist
        all_exist = save_path_full.exists() and save_path_thumb.exists()
        if include_tiny and save_path_tiny:
            all_exist = all_exist and save_path_tiny.exists()
            
        if all_exist:
            # Generate metadata from existing full file
            try:
                existing_img = Image.open(save_path_full)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")
                    
                metadata: dict[str, Any] = {
                    "blurhash": None,
                    "width": existing_img.width,
                    "height": existing_img.height,
                    "path_thumb": str(save_path_thumb.relative_to(OUTPUT_DIR.parent) if save_path_thumb.is_relative_to(OUTPUT_DIR.parent) else save_path_thumb),
                }
                
                if include_tiny and save_path_tiny:
                    metadata["path_tiny"] = str(save_path_tiny.relative_to(OUTPUT_DIR.parent) if save_path_tiny.is_relative_to(OUTPUT_DIR.parent) else save_path_tiny)
                
                if BLURHASH_AVAILABLE:
                    try:
                        metadata["blurhash"] = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass
                        
                return (True, metadata)
            except Exception:
                return (True, None)
        
        # --- Full file exists but some derived sizes are missing ---
        # Generate missing variants from the existing full-size image (no re-download)
        if save_path_full.exists():
            try:
                existing_img = Image.open(save_path_full)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")

                # Generate missing THUMB
                if not save_path_thumb.exists():
                    img_thumb = self._resize_and_crop(existing_img.copy(), SIZE_THUMB)
                    img_thumb.save(save_path_thumb, IMAGE_FORMAT, quality=IMAGE_QUALITY)

                # Generate missing TINY (if requested)
                if include_tiny and save_path_tiny and not save_path_tiny.exists():
                    img_tiny = self._resize_and_crop(existing_img.copy(), SIZE_TINY)
                    img_tiny.save(save_path_tiny, IMAGE_FORMAT, quality=IMAGE_QUALITY)

                # Generate BlurHash
                hash_str: str | None = None
                if BLURHASH_AVAILABLE:
                    try:
                        hash_str = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass

                metadata: dict[str, Any] = {
                    "blurhash": hash_str,
                    "width": existing_img.width,
                    "height": existing_img.height,
                    "path_thumb": str(save_path_thumb.relative_to(OUTPUT_DIR)),
                }
                if include_tiny and save_path_tiny:
                    metadata["path_tiny"] = str(save_path_tiny.relative_to(OUTPUT_DIR))

                return (True, metadata)
            except Exception as e:
                logger.debug(f"Error generating derived sizes from existing {save_path_full}: {e}")
                return (True, None)

        # --- Full file does NOT exist - download fresh ---
        try:
            resp = self.session.get(url, timeout=15)
            if resp.status_code != 200:
                return (False, None)
                
            img: Image.Image = Image.open(BytesIO(resp.content))
            if img.mode != "RGB":
                img = img.convert("RGB")
                
            # Check minimum resolution (must be at least full size)
            if img.width < full_size[0] or img.height < full_size[1]:
                logger.debug(f"Skipping low-res image: {img.width}x{img.height} (min: {full_size[0]}x{full_size[1]})")
                return (False, None)
            
            # Ensure parent directory exists
            parent.mkdir(parents=True, exist_ok=True)
            
            # Generate FULL size
            img_full = self._resize_and_crop(img.copy(), full_size)
            img_full.save(save_path_full, IMAGE_FORMAT, quality=IMAGE_QUALITY)
            
            # Generate THUMB size
            img_thumb = self._resize_and_crop(img.copy(), SIZE_THUMB)
            img_thumb.save(save_path_thumb, IMAGE_FORMAT, quality=IMAGE_QUALITY)
            
            # Generate TINY size (if requested)
            if include_tiny and save_path_tiny:
                img_tiny = self._resize_and_crop(img.copy(), SIZE_TINY)
                img_tiny.save(save_path_tiny, IMAGE_FORMAT, quality=IMAGE_QUALITY)
            
            # Generate BlurHash from full image
            hash_str: str | None = None
            if BLURHASH_AVAILABLE:
                try:
                    hash_str = blurhash.encode(np.array(img_full), components_x=4, components_y=3)
                except Exception as e:
                    logger.debug(f"BlurHash generation failed for {url}: {e}")
            
            # Build metadata
            rel_thumb = str(save_path_thumb.relative_to(OUTPUT_DIR))
            
            metadata = {
                "blurhash": hash_str,
                "width": img_full.width,
                "height": img_full.height,
                "path_thumb": rel_thumb,
            }
            
            if include_tiny and save_path_tiny:
                metadata["path_tiny"] = str(save_path_tiny.relative_to(OUTPUT_DIR))
                
            return (True, metadata)
            
        except Exception as e:
            logger.debug(f"Error processing multi-size {url}: {e}")
            return (False, None)
    
    def _resize_and_crop(self, img: Image.Image, target_size: tuple[int, int]) -> Image.Image:
        """Resize image maintaining aspect ratio and center crop to target size."""
        img_ratio = img.width / img.height
        target_ratio = target_size[0] / target_size[1]
        
        if img_ratio > target_ratio:
            new_height = target_size[1]
            new_width = int(new_height * img_ratio)
        else:
            new_width = target_size[0]
            new_height = int(new_width / img_ratio)
            
        img = img.resize((new_width, new_height), Image.Resampling.LANCZOS)
        
        # Center crop
        left = (new_width - target_size[0]) / 2
        top = (new_height - target_size[1]) / 2
        img = img.crop((left, top, left + target_size[0], top + target_size[1]))
        
        return img

    def download_batch(self, tasks: list[tuple]) -> list[dict[str, Any]]:
        """
        Execute a batch of download tasks using ThreadPoolExecutor.

        Args:
            tasks: List of tuples, either:
                - (url, save_path, rel_path) - uses default TARGET_SIZE
                - (url, save_path, rel_path, target_size) - uses custom size

        Returns:
            List of dicts with format: {"path": "...", "blurhash": "...", "width": int, "height": int}
        """
        saved_files: list[dict[str, Any]] = []
        futures = []
        task_indices = []

        with ThreadPoolExecutor(max_workers=WORKERS) as executor:
            for task in tasks:
                url = task[0]
                save_path = task[1]
                rel_path = task[2]
                target_size = task[3] if len(task) == 4 else None

                # Deduplication logic
                if url in self.seen_urls:
                    old_rel_path = self.seen_urls[url]
                    if (OUTPUT_DIR / old_rel_path).exists():
                        # Try to generate metadata for existing file
                        _, metadata = self.process_image(url, OUTPUT_DIR / old_rel_path, target_size)
                        result = {"path": old_rel_path}
                        if metadata:
                            result.update(metadata)  # Add blurhash, width, height
                        saved_files.append(result)
                        continue

                futures.append(executor.submit(self.process_image, url, save_path, target_size))
                task_indices.append(len(futures) - 1)

            for i, future_idx in enumerate(task_indices):
                f = futures[future_idx]
                task = tasks[i]
                url = task[0]
                save_path = task[1]
                rel_path = task[2]

                if url in self.seen_urls and (OUTPUT_DIR / self.seen_urls[url]).exists():
                    continue

                success, metadata = f.result()
                if success:
                    self.seen_urls[url] = rel_path
                    result = {"path": rel_path}
                    if metadata:
                        result.update(metadata)  # Add blurhash, width, height
                    saved_files.append(result)

        return saved_files

    def download_batch_multi_size(
        self,
        tasks: list[tuple[str, Path, str]],
        include_tiny: bool = False,
        avatar_mode: bool = False
    ) -> list[dict[str, Any]]:
        """
        Execute batch download with multi-size image generation.
        
        Args:
            tasks: List of (url, save_path_full, rel_path_full) tuples
            include_tiny: Whether to generate tiny size (for avatars)
            avatar_mode: If True, uses SIZE_AVATAR for full size (300×300 square)
            
        Returns:
            List of dicts with format: {
                "path": "...",           # Full size path (stored in DB)
                "path_thumb": "...",     # Thumbnail path (derived)
                "path_tiny": "...",      # Tiny path (optional, derived)
                "blurhash": "...",
                "width": int,
                "height": int
            }
        """
        saved_files: list[dict[str, Any]] = []
        futures = []
        task_map: list[tuple[int, str, str]] = []  # (future_idx, url, rel_path)
        
        with ThreadPoolExecutor(max_workers=WORKERS) as executor:
            for url, save_path, rel_path in tasks:
                # Deduplication logic
                if url in self.seen_urls:
                    old_rel_path = self.seen_urls[url]
                    old_full_path = OUTPUT_DIR / old_rel_path
                    if old_full_path.exists():
                        # Try to generate metadata for existing file
                        _, metadata = self.process_image_multi_size(
                            url, old_full_path, include_tiny, avatar_mode
                        )
                        result = {"path": old_rel_path}
                        if metadata:
                            result.update(metadata)
                        saved_files.append(result)
                        continue
                
                future = executor.submit(
                    self.process_image_multi_size,
                    url,
                    save_path,
                    include_tiny,
                    avatar_mode
                )
                task_map.append((len(futures), url, rel_path))
                futures.append(future)
            
            # Collect results
            for future_idx, url, rel_path in task_map:
                if url in self.seen_urls and (OUTPUT_DIR / self.seen_urls[url]).exists():
                    continue
                    
                success, metadata = futures[future_idx].result()
                if success:
                    self.seen_urls[url] = rel_path
                    result = {"path": rel_path}
                    if metadata:
                        result.update(metadata)
                    saved_files.append(result)
        
        return saved_files

    def _scan_directory(self) -> dict:
        """
        Scan output directory for expected and unexpected files.
        """
        if not OUTPUT_DIR.exists():
            return {"expected": [], "unexpected": [], "empty_dirs": [], "has_files": False}

        expected_ext = {".webp", ".jpg", ".jpeg", ".png"}
        expected_files = []
        unexpected_files = []
        empty_dirs = []

        for path in OUTPUT_DIR.rglob("*"):
            if path.is_file():
                # Treat photo_index.json as expected (script's own output file)
                if path.name == "photo_index.json":
                    expected_files.append(path)
                    continue

                # Check extension
                if path.suffix.lower() not in expected_ext:
                    unexpected_files.append(path)
                    continue

                # Check structure:
                # - dishes/CAT/VAR/file (4 parts)
                # - restaurants/THEME/file (3 parts)
                # - avatars/pool/file (3 parts)
                rel = path.relative_to(OUTPUT_DIR)
                parts = rel.parts

                if (
                    len(parts) == 4 and parts[0] == "dishes"
                    or len(parts) == 3 and parts[0] == "restaurants"
                    or len(parts) == 2 and parts[0] == "avatars"
                    or len(parts) == 2 and parts[0] == "ingredients"
                ):
                    expected_files.append(path)
                else:
                    unexpected_files.append(path)

            elif path.is_dir() and not any(path.iterdir()):
                empty_dirs.append(path)

        return {
            "expected": expected_files,
            "unexpected": unexpected_files,
            "empty_dirs": empty_dirs,
            "has_files": bool(expected_files or unexpected_files),
        }

    def cleanup(self) -> bool:
        """
        Perform cleanup of the output directory (FRESH START mode).
        Deletes all existing photos and index to ensure clean state.
        Returns True if cleanup succeeded, False if cancelled.
        """
        if not OUTPUT_DIR.exists():
            logger.info("Output directory does not exist. Will be created.")
            return True

        scan = self._scan_directory()
        if not scan["has_files"] and not scan["empty_dirs"]:
            logger.info("Output directory is empty. Ready for fresh start.")
            return True

        # FRESH START MODE: Always delete existing data
        logger.info("=" * 60)
        logger.info("FRESH START MODE")
        logger.info(f"Target: {OUTPUT_DIR}")
        logger.info(f"Photos found: {len(scan['expected'])}")
        logger.info(f"Other files: {len(scan['unexpected'])}")
        logger.info("")
        logger.info("This script will DELETE all existing photos and create fresh data.")
        logger.info("=" * 60)

        # Warn about unexpected files (e.g., .doc, .txt, .zip in photo directories)
        if scan["unexpected"]:
            logger.warning("\n!!! WARNING: UNEXPECTED FILES DETECTED !!!")
            logger.warning("The following file types were found in the photo directory:")

            # Group by extension
            unexpected_by_ext = {}
            for f in scan["unexpected"]:
                ext = f.suffix.lower() or "(no extension)"
                if ext not in unexpected_by_ext:
                    unexpected_by_ext[ext] = []
                unexpected_by_ext[ext].append(f)

            for ext, files in unexpected_by_ext.items():
                logger.warning(f"  {ext}: {len(files)} file(s)")
                for f in files[:3]:  # Show first 3 of each type
                    logger.warning(f"    - {f.relative_to(OUTPUT_DIR)}")
                if len(files) > 3:
                    logger.warning(f"    ...and {len(files) - 3} more")

            logger.warning("\nThese files will also be DELETED if you proceed.")
            logger.warning("=" * 60)

        # Show sample of expected files to be deleted
        if scan["expected"]:
            logger.info("\nSample photos to be deleted:")
            for f in scan["expected"][:5]:
                logger.info(f"  - {f.relative_to(OUTPUT_DIR)}")
            if len(scan["expected"]) > 5:
                logger.info(f"  ...and {len(scan['expected']) - 5} more photos")

        # Safety prompt
        logger.info("")
        res = input("Delete all existing photos and start fresh? (yes/no): ").lower().strip()
        if res not in ["yes", "y"]:
            logger.info("Cleanup cancelled by user.")
            return False

        # Perform cleanup
        logger.info("\nCleaning up...")

        # Delete all files in OUTPUT_DIR
        deleted_count = 0
        for f in scan["expected"] + scan["unexpected"]:
            try:
                f.unlink()
                deleted_count += 1
            except OSError as e:
                logger.error(f"Failed to delete {f}: {e}")

        # Delete empty dirs (bottom-up)
        for root, dirs, _ in os.walk(OUTPUT_DIR, topdown=False):
            for d in dirs:
                try:
                    os.rmdir(os.path.join(root, d))
                except OSError:
                    pass

        # Delete index file if exists (fresh index will be created)
        if INDEX_FILE.exists():
            try:
                INDEX_FILE.unlink()
                logger.info(f"Deleted old index: {INDEX_FILE}")
            except OSError as e:
                logger.warning(f"Could not delete index file: {e}")

        # Reset state (clear seen URLs for fresh download)
        self.seen_urls = {}
        self._save_seen_urls()

        logger.info(f"Cleanup complete. Deleted {deleted_count} files.")
        logger.info("Ready for fresh photo download.")
        return True

    def _extract_all_ingredients(self) -> set[str]:
        """Extract all unique ingredient names from dish blueprints."""
        bp_path = BLUEPRINTS_DIR / "dishes.json"
        ingredients = set()
        try:
            with open(bp_path, encoding="utf-8") as f:
                data = json.load(f)
                for cat_data in data.values():
                    if not isinstance(cat_data, dict):
                        continue
                    variants = cat_data.get("variants", {})
                    for var_data in variants.values():
                        if isinstance(var_data, dict):
                            ing_list = var_data.get("ingredients", [])
                            ingredients.update(ing_list)
        except Exception as e:
            logger.error(f"Failed to load ingredients from blueprint: {e}")
        return ingredients

    def download_ingredients(self):
        """Download photos for ingredients (icons)."""
        logger.info("--- DOWNLOADING INGREDIENTS ---")
        ingredients = self._extract_all_ingredients()
        if not ingredients:
            logger.warning("No ingredients found to download.")
            return

        ing_dir = OUTPUT_DIR / "ingredients"
        # Icon size: small square
        ICON_SIZE = (200, 200)

        # Initialize index section
        if "ingredients" not in self.index:
            self.index["ingredients"] = {}

        tasks = []
        ing_name_map = {}  # Map task index to ingredient name

        for ing_name in tqdm(ingredients, desc="Preparing ingredient tasks"):
            # Sanitize filename: allow alphanumeric and underscore, dash.
            # This matches our actual folder structure (e.g. "ciasto_makaronowe")
            safe_name = "".join(c for c in ing_name if c.isalnum() or c in ('_', '-')).lower()
            
            # Structure: ingredients/name/name_001.webp
            ing_sub_dir = ing_dir / safe_name
            filename = f"{safe_name}_001.{IMAGE_FORMAT.lower()}"
            save_path = ing_sub_dir / filename
            rel_path = f"ingredients/{safe_name}/{filename}"

            # Check if file exists at target path
            if save_path.exists():
                _, blurhash_val = self.process_image("", save_path, ICON_SIZE)
                self.index["ingredients"][ing_name] = {"path": rel_path, "blurhash": blurhash_val}
                continue
            
            # Check if any file exists in the folder (robustness)
            found_existing = False
            if ing_sub_dir.exists():
                for f in ing_sub_dir.iterdir():
                    if f.is_file() and f.suffix.lower() == f".{IMAGE_FORMAT.lower()}":
                        rel_existing = f"ingredients/{safe_name}/{f.name}"
                        _, blurhash_val = self.process_image("", f, ICON_SIZE)
                        self.index["ingredients"][ing_name] = {"path": rel_existing, "blurhash": blurhash_val}
                        found_existing = True
                        break
            if found_existing:
                continue

            # Look up English search term from mapping
            if ing_name in self.ingredient_mappings:
                query = self.ingredient_mappings[ing_name]
            else:
                # Fallback to old logic if not found
                query = f"{ing_name} food"
                logger.warning(f"No mapping found for ingredient '{ing_name}', using fallback: '{query}'")

            # We only need 1 good photo per ingredient (try mixed)
            # Use search_mixed_urls to leverage Unsplash if Pixabay fails
            urls = self.search_mixed_urls(query, 1, orientation="horizontal", pixabay_ratio=0.5)

            if urls:
                ing_name_map[len(tasks)] = ing_name
                # Ensure directory will exist
                tasks.append((urls[0], save_path, rel_path, ICON_SIZE))

        if tasks:
            logger.info(f"Downloading {len(tasks)} new ingredient icons...")
            saved = self.download_batch(tasks)

            # Map saved results back to ingredient names
            for idx, photo_obj in enumerate(saved):
                if idx in ing_name_map:
                    ing_name = ing_name_map[idx]
                    self.index["ingredients"][ing_name] = photo_obj

            logger.info(f"Downloaded {len(saved)} ingredient icons.")
        else:
            logger.info("All ingredient icons already exist.")

    def download_dishes(self):
        """Download dish photos."""
        logger.info("--- DOWNLOADING DISHES ---")
        variants_data = self._load_dish_variants()
        dishes_dir = OUTPUT_DIR / "dishes"

        for category, data in variants_data.items():
            self.index["dishes"][category] = {}
            variants = data.get("variants", {})
            logger.info(f"Category: {category} ({len(variants)} variants)")
            
            # Get archetype-level fallback term
            archetype_term = data.get("pixabay_term")

            for variant_name, variant_data in variants.items():
                target_count = random.randint(
                    int(PHOTO_CONFIG["min_photos_per_variant"]), int(PHOTO_CONFIG["max_photos_per_variant"])  # type: ignore
                )

                query = variant_data.get("pixabay_term")
                if not query:
                    if archetype_term:
                        query = archetype_term
                        # logger.debug(f"Using archetype fallback '{query}' for '{variant_name}'")
                    else:
                        query = f"{variant_name} {category}"
                
                query = query.replace(",", "").strip()

                logger.info(f"  > '{query}' ({target_count})")
                urls = self.search_mixed_urls(query, target_count)

                if len(urls) < 2:  # Fallback to Pixabay-only
                    urls += self.search_pixabay(category, target_count - len(urls))

                tasks = []
                for i, url in enumerate(urls[:target_count]):
                    filename = f"{variant_name.lower().replace(' ', '_')}_{i + 1:03d}.{IMAGE_FORMAT.lower()}"
                    save_path = dishes_dir / category / variant_name / filename
                    rel_path = f"dishes/{category}/{variant_name}/{filename}"
                    tasks.append((url, save_path, rel_path))

                saved = self.download_batch_multi_size(tasks)  # Multi-size: full + thumb
                self.index["dishes"][category][variant_name] = saved

    def download_restaurants(self):
        """Download restaurant interior photos."""
        logger.info("--- DOWNLOADING RESTAURANTS ---")
        rest_dir = OUTPUT_DIR / "restaurants"
        base_count = int(PHOTO_CONFIG["restaurant_base_count"])  # type: ignore

        for theme, weight in self.restaurant_themes.items():
            target = int(base_count * weight / 10.0)  # Normalize weight slightly
            target = max(5, target)  # Minimum 5 photos per theme

            # Use pixabay_term if available, otherwise fallback to old logic
            if theme in self.restaurant_pixabay_terms:
                query = self.restaurant_pixabay_terms[theme]
                logger.info(f"Theme: {theme} ({target}) - Query: '{query}'")
            else:
                query = f"{theme} restaurant interior"
                logger.info(f"Theme: {theme} ({target}) - Query: '{query}' [fallback]")

            urls = self.search_mixed_urls(query, target)

            tasks = []
            for i, url in enumerate(urls):
                fname = f"{theme.lower().replace(' ', '_')}_{i + 1:03d}.{IMAGE_FORMAT.lower()}"
                save_path = rest_dir / theme / fname
                rel_path = f"restaurants/{theme}/{fname}"
                tasks.append((url, save_path, rel_path))

            saved = self.download_batch_multi_size(tasks)  # Multi-size: full + thumb
            self.index["restaurants"][theme] = saved

    def _load_avatar_queries(self) -> list[str]:
        """Load avatar search terms from global config."""
        config_path = BLUEPRINTS_DIR / "global_config.json"
        try:
            with open(config_path, encoding="utf-8") as f:
                data = json.load(f)
                return data.get("AVATAR_SEARCH_TERMS", [])
        except Exception as e:
            logger.warning(f"Could not load avatar queries from config: {e}. Using defaults.")
            return ["avatar icon", "user profile"]

    def download_avatars(self):
        """Download diverse user profile pictures (portraits, art, etc)."""
        logger.info("--- DOWNLOADING USER AVATARS ---")
        avatars_dir = OUTPUT_DIR / "avatars"
        
        # Local cleanup: Ensure fresh start for avatars
        if avatars_dir.exists():
            logger.info(f"Cleaning existing avatars folder: {avatars_dir}")
            try:
                import shutil
                shutil.rmtree(avatars_dir)
            except Exception as e:
                logger.warning(f"Failed to clean avatars folder: {e}")
        
        avatars_dir.mkdir(parents=True, exist_ok=True)

        # Load search terms from config
        avatar_queries = self._load_avatar_queries()
        if not avatar_queries:
            logger.warning("No avatar queries found in config!")
            return

        target_total = 1000  # Total avatar pool size
        per_query_target = max(5, target_total // len(avatar_queries))  # Ensure at least 5 per query

        all_tasks: list[tuple[str, Path, str]] = []

        for query in avatar_queries:
            logger.info(f"Query: '{query}' (target: {per_query_target})")
            # Use mixed search (Pixabay + Unsplash) for better variety (portraits!)
            urls = self.search_mixed_urls(query, per_query_target, orientation="vertical", pixabay_ratio=0.4)

            for i, url in enumerate(urls):
                # Unique filename based on query and index
                safe_query = slugify(query)[:30]
                filename = f"avatar_{safe_query}_{i + 1:03d}.{IMAGE_FORMAT.lower()}"
                save_path = avatars_dir / filename
                rel_path = f"avatars/{filename}"
                all_tasks.append((url, save_path, rel_path))

        logger.info(f"Downloading {len(all_tasks)} avatar images...")
        # Avatars get full (300x300) + tiny (50x50) with avatar_mode=True
        saved = self.download_batch_multi_size(all_tasks, include_tiny=True, avatar_mode=True)
        self.index["avatars"] = saved
        logger.info(f"Downloaded {len(saved)} avatars")

    def save_index(self):
        """Save the final photo index."""
        with open(INDEX_FILE, "w", encoding="utf-8") as f:
            json.dump(self.index, f, indent=2)
        logger.info(f"Index saved to {INDEX_FILE}")
        self._save_seen_urls()

    def run(self, args=None):
        """Main execution flow."""
        # Default behavior (if no args provided): Full Fresh Start
        skip_cleanup = args.no_cleanup if args else False
        only_ingredients = args.only_ingredients if args else False
        only_dishes = args.only_dishes if args else False
        only_restaurants = args.only_restaurants if args else False
        only_avatars = args.only_avatars if args else False

        # If any specific filter is set, assume we don't want to run others
        specific_mode = any([only_ingredients, only_dishes, only_restaurants, only_avatars])

        if not skip_cleanup and not specific_mode:
            # Ensure cleanup is done first if needed
            if not self.cleanup():
                logger.info("Exiting because cleanup was cancelled.")
                return
        else:
            logger.info("Skipping cleanup (--no-cleanup or specific mode selected)")

        # Re-create directory if it was removed
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

        # Execute selected modules (or all if no filter)
        if not specific_mode or only_dishes:
            self.download_dishes()
        
        if not specific_mode or only_restaurants:
            self.download_restaurants()
            
        if not specific_mode or only_avatars:
            self.download_avatars()
            
        if not specific_mode or only_ingredients:
            self.download_ingredients()  # NEW STEP
            
        self.save_index()

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Photo Fetcher")
    parser.add_argument("--no-cleanup", action="store_true", help="Skip the cleanup/delete phase")
    parser.add_argument("--only-ingredients", action="store_true", help="Download only ingredients")
    parser.add_argument("--only-dishes", action="store_true", help="Download only dishes")
    parser.add_argument("--only-restaurants", action="store_true", help="Download only restaurants")
    parser.add_argument("--only-avatars", action="store_true", help="Download only avatars")
    
    args = parser.parse_args()
    
    downloader = PixabayDownloader()
    downloader.run(args)
