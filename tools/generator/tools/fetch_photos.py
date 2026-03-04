import json
import logging
import os
import random
import sys
from pathlib import Path
from typing import Any, cast

from dotenv import load_dotenv
from tqdm import tqdm  # type: ignore

load_dotenv(Path(__file__).resolve().parent.parent.parent.parent / ".env")

import requests  # type: ignore
from requests.adapters import HTTPAdapter  # type: ignore
from urllib3.util.retry import Retry

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from tools.image_download import ImageDownloadService
from tools.utils import slugify
from utils.logging_config import LoggingConfig
from utils.photo_index_manager import PhotoIndexManager

logger = logging.getLogger(__name__)

OUTPUT_DIR = Path(cast(str, PHOTO_CONFIG["output_dir"]))
INDEX_FILE = Path(cast(str, PHOTO_CONFIG["local_photo_index"]))
SEEN_URLS_FILE = Path("data/downloaded_urls.json")
BLUEPRINTS_DIR = Path(__file__).resolve().parent.parent / "blueprints"

TARGET_SIZE = (cast(int, PHOTO_CONFIG["target_width"]), cast(int, PHOTO_CONFIG["target_height"]))
IMAGE_QUALITY = cast(int, PHOTO_CONFIG["image_quality"])
IMAGE_FORMAT = cast(str, PHOTO_CONFIG["image_format"])
WORKERS = cast(int, PHOTO_CONFIG["workers"])
IMAGES_PER_QUERY = int(PHOTO_CONFIG.get("images_per_query", 200))  # type: ignore

SIZE_HERO = cast(tuple[int, int], PHOTO_CONFIG.get("size_hero", (1600, 900)))
SIZE_FULL = cast(tuple[int, int], PHOTO_CONFIG.get("size_full", (1280, 960)))
SIZE_THUMB = cast(tuple[int, int], PHOTO_CONFIG.get("size_thumb", (200, 150)))
SIZE_TINY = cast(tuple[int, int], PHOTO_CONFIG.get("size_tiny", (50, 50)))
SIZE_AVATAR = cast(tuple[int, int], PHOTO_CONFIG.get("size_avatar", (300, 300)))
SIZE_INGREDIENT = cast(tuple[int, int], PHOTO_CONFIG.get("size_ingredient", (200, 200)))
SUFFIX_HERO = cast(str, PHOTO_CONFIG.get("suffix_hero", "_hero"))
SUFFIX_THUMB = cast(str, PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
SUFFIX_TINY = cast(str, PHOTO_CONFIG.get("suffix_tiny", "_tiny"))

from tools.image_providers import ImageResult, ProviderManager

class PixabayDownloader:

    def __init__(self):
        self.api_key = self._get_api_key()
        self.session = self._create_session()
        self.seen_urls: dict[str, str] = self._load_seen_urls()
        self.download_service = ImageDownloadService(self.session, self.seen_urls, OUTPUT_DIR)
        self._index_mgr = PhotoIndexManager(INDEX_FILE)
        self.index: dict[str, Any] = self._index_mgr._empty()
        self.restaurant_themes: dict[str, float] = {}
        self.restaurant_pixabay_terms: dict[str, str] = {}
        self._load_restaurant_themes()
        self.ingredient_mappings: dict[str, str] = self._load_ingredient_mappings()

        self.provider_manager = ProviderManager()

        SEEN_URLS_FILE.parent.mkdir(parents=True, exist_ok=True)

    def _get_api_key(self) -> str:
        key = PHOTO_CONFIG.get("pixabay_api_key")
        if not key:
            logger.error("Missing pixabay_api_key in PHOTO_CONFIG (check .env)!")
            sys.exit(1)
        return str(key)

    def _create_session(self) -> requests.Session:
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
        if SEEN_URLS_FILE.exists():
            try:
                with open(SEEN_URLS_FILE, encoding="utf-8") as f:
                    return json.load(f)
            except json.JSONDecodeError:
                logger.warning(f"Corrupted seen URLs file: {SEEN_URLS_FILE}. Starting fresh.")
        return {}

    def _save_seen_urls(self):
        with open(SEEN_URLS_FILE, "w", encoding="utf-8") as f:
            json.dump(self.seen_urls, f, indent=2)

    def _load_restaurant_themes(self) -> None:
        blueprint_path = BLUEPRINTS_DIR / "restaurant_types.json"
        try:
            with open(blueprint_path, encoding="utf-8") as f:
                data = json.load(f)
                theme_config = data.get("RESTAURANT_THEMES", {})
                for theme, config in theme_config.items():
                    weight = config.get("distribution_chance", 0.05) * 100
                    self.restaurant_themes[theme] = max(1.0, weight)

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
        bp_path = BLUEPRINTS_DIR / "dishes.json"
        try:
            with open(bp_path, encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            logger.error(f"Cannot read dish blueprint file: {bp_path} - {e}")
            sys.exit(1)

    def search_pixabay(self, query: str, min_count: int) -> list[str]:
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

                if page > 5:
                    break

            except requests.RequestException as e:
                logger.error(f"Network error searching Pixabay for '{query}': {e}")
                break

        return urls[:min_count]

    def search_mixed(
        self, query: str, count: int, orientation: str = "horizontal", pixabay_ratio: float = 0.6
    ) -> list[ImageResult]:
        return self.provider_manager.search_mixed(query, count, orientation, pixabay_ratio)

    def search_mixed_urls(
        self, query: str, count: int, orientation: str = "horizontal", pixabay_ratio: float = 0.6
    ) -> list[str]:
        results = self.search_mixed(query, count, orientation, pixabay_ratio)
        return [r.url for r in results if r.url]

    def process_image(
        self, url: str, save_path: Path, target_size: tuple[int, int] | None = None
    ) -> tuple[bool, dict[str, Any] | None]:
        return self.download_service.process_image(url, save_path, target_size)

    def process_image_multi_size(
        self,
        url: str,
        save_path_full: Path,
        include_tiny: bool = False,
        avatar_mode: bool = False,
    ) -> tuple[bool, dict[str, Any] | None]:
        return self.download_service.process_image_multi_size(url, save_path_full, include_tiny, avatar_mode)

    def download_batch(self, tasks: list[tuple]) -> list[dict[str, Any]]:
        return self.download_service.download_batch(tasks)

    def download_batch_multi_size(
        self,
        tasks: list[tuple[str, Path, str]],
        include_tiny: bool = False,
        avatar_mode: bool = False,
    ) -> list[dict[str, Any]]:
        return self.download_service.download_batch_multi_size(tasks, include_tiny, avatar_mode)

    def _scan_directory(self) -> dict:
        if not OUTPUT_DIR.exists():
            return {"expected": [], "unexpected": [], "empty_dirs": [], "has_files": False}

        expected_ext = {".webp", ".jpg", ".jpeg", ".png"}
        expected_files = []
        unexpected_files = []
        empty_dirs = []

        for path in OUTPUT_DIR.rglob("*"):
            if path.is_file():
                if path.name == "photo_index.json":
                    expected_files.append(path)
                    continue

                if path.suffix.lower() not in expected_ext:
                    unexpected_files.append(path)
                    continue

                rel = path.relative_to(OUTPUT_DIR)
                parts = rel.parts

                if (
                    len(parts) == 4
                    and parts[0] == "dishes"
                    or len(parts) == 3
                    and parts[0] == "restaurants"
                    or len(parts) == 2
                    and parts[0] == "avatars"
                    or len(parts) == 2
                    and parts[0] == "ingredients"
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
        if not OUTPUT_DIR.exists():
            logger.info("Output directory does not exist. Will be created.")
            return True

        scan = self._scan_directory()
        if not scan["has_files"] and not scan["empty_dirs"]:
            logger.info("Output directory is empty. Ready for fresh start.")
            return True

        logger.info("=" * 60)
        logger.info("FRESH START MODE")
        logger.info(f"Target: {OUTPUT_DIR}")
        logger.info(f"Photos found: {len(scan['expected'])}")
        logger.info(f"Other files: {len(scan['unexpected'])}")
        logger.info("")
        logger.info("This script will DELETE all existing photos and create fresh data.")
        logger.info("=" * 60)

        if scan["unexpected"]:
            logger.warning("\n!!! WARNING: UNEXPECTED FILES DETECTED !!!")
            logger.warning("The following file types were found in the photo directory:")

            unexpected_by_ext = {}
            for f in scan["unexpected"]:
                ext = f.suffix.lower() or "(no extension)"
                if ext not in unexpected_by_ext:
                    unexpected_by_ext[ext] = []
                unexpected_by_ext[ext].append(f)

            for ext, files in unexpected_by_ext.items():
                logger.warning(f"  {ext}: {len(files)} file(s)")
                for f in files[:3]:
                    logger.warning(f"    - {f.relative_to(OUTPUT_DIR)}")
                if len(files) > 3:
                    logger.warning(f"    ...and {len(files) - 3} more")

            logger.warning("\nThese files will also be DELETED if you proceed.")
            logger.warning("=" * 60)

        if scan["expected"]:
            logger.info("\nSample photos to be deleted:")
            for f in scan["expected"][:5]:
                logger.info(f"  - {f.relative_to(OUTPUT_DIR)}")
            if len(scan["expected"]) > 5:
                logger.info(f"  ...and {len(scan['expected']) - 5} more photos")

        logger.info("")
        res = input("Delete all existing photos and start fresh? (yes/no): ").lower().strip()
        if res not in ["yes", "y"]:
            logger.info("Cleanup cancelled by user.")
            return False

        logger.info("\nCleaning up...")

        deleted_count = 0
        for f in scan["expected"] + scan["unexpected"]:
            try:
                f.unlink()
                deleted_count += 1
            except OSError as e:
                logger.error(f"Failed to delete {f}: {e}")

        for root, dirs, _ in os.walk(OUTPUT_DIR, topdown=False):
            for d in dirs:
                try:
                    os.rmdir(os.path.join(root, d))
                except OSError:
                    pass

        if INDEX_FILE.exists():
            try:
                INDEX_FILE.unlink()
                logger.info(f"Deleted old index: {INDEX_FILE}")
            except OSError as e:
                logger.warning(f"Could not delete index file: {e}")

        self.seen_urls = {}
        self._save_seen_urls()

        logger.info(f"Cleanup complete. Deleted {deleted_count} files.")
        logger.info("Ready for fresh photo download.")
        return True

    def _extract_all_ingredients(self) -> set[str]:
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
        logger.info("--- DOWNLOADING INGREDIENTS ---")
        ingredients = self._extract_all_ingredients()
        if not ingredients:
            logger.warning("No ingredients found to download.")
            return

        ing_dir = OUTPUT_DIR / "ingredients"
        ICON_SIZE = (200, 200)

        if "ingredients" not in self.index:
            self.index["ingredients"] = {}

        tasks = []
        ing_name_map = {}

        for ing_name in tqdm(
            ingredients, desc="Preparing ingredient tasks", mininterval=1.0, disable=LoggingConfig.is_quiet()
        ):
            safe_name = "".join(c for c in ing_name if c.isalnum() or c in ("_", "-")).lower()

            ing_sub_dir = ing_dir / safe_name
            filename = f"{safe_name}_001.{IMAGE_FORMAT.lower()}"
            save_path = ing_sub_dir / filename
            rel_path = f"ingredients/{safe_name}/{filename}"

            if save_path.exists():
                _, blurhash_val = self.process_image("", save_path, ICON_SIZE)
                self.index["ingredients"][ing_name] = {"path": rel_path, "blurhash": blurhash_val}
                continue

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

            if ing_name in self.ingredient_mappings:
                query = self.ingredient_mappings[ing_name]
            else:
                query = f"{ing_name} food"
                logger.warning(f"No mapping found for ingredient '{ing_name}', using fallback: '{query}'")

            urls = self.search_mixed_urls(query, 1, orientation="horizontal", pixabay_ratio=0.5)

            if urls:
                ing_name_map[len(tasks)] = ing_name
                tasks.append((urls[0], save_path, rel_path, ICON_SIZE))

        if tasks:
            logger.info(f"Downloading {len(tasks)} new ingredient icons...")
            saved = self.download_batch(tasks)

            for idx, photo_obj in enumerate(saved):
                if idx in ing_name_map:
                    ing_name = ing_name_map[idx]
                    self.index["ingredients"][ing_name] = photo_obj

            logger.info(f"Downloaded {len(saved)} ingredient icons.")
        else:
            logger.info("All ingredient icons already exist.")

    def download_dishes(self):
        logger.info("--- DOWNLOADING DISHES ---")
        variants_data = self._load_dish_variants()
        dishes_dir = OUTPUT_DIR / "dishes"

        for category, data in variants_data.items():
            self.index["dishes"][category] = {}
            variants = data.get("variants", {})
            logger.info(f"Category: {category} ({len(variants)} variants)")

            archetype_term = data.get("pixabay_term")

            for variant_name, variant_data in variants.items():
                target_count = random.randint(
                    int(PHOTO_CONFIG["min_photos_per_variant"]),
                    int(PHOTO_CONFIG["max_photos_per_variant"]),  # type: ignore
                )

                query = variant_data.get("pixabay_term")
                if not query:
                    if archetype_term:
                        query = archetype_term
                    else:
                        query = f"{variant_name} {category}"

                query = query.replace(",", "").strip()

                logger.info(f"  > '{query}' ({target_count})")
                urls = self.search_mixed_urls(query, target_count)

                if len(urls) < 2:
                    urls += self.search_pixabay(category, target_count - len(urls))

                tasks = []
                for i, url in enumerate(urls[:target_count]):
                    filename = f"{variant_name.lower().replace(' ', '_')}_{i + 1:03d}.{IMAGE_FORMAT.lower()}"
                    save_path = dishes_dir / category / variant_name / filename
                    rel_path = f"dishes/{category}/{variant_name}/{filename}"
                    tasks.append((url, save_path, rel_path))

                saved = self.download_batch_multi_size(tasks)
                self.index["dishes"][category][variant_name] = saved

    def download_restaurants(self):
        logger.info("--- DOWNLOADING RESTAURANTS ---")
        rest_dir = OUTPUT_DIR / "restaurants"
        base_count = int(PHOTO_CONFIG["restaurant_base_count"])  # type: ignore

        for theme, weight in self.restaurant_themes.items():
            target = int(base_count * weight / 10.0)
            target = max(5, target)

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

            saved = self.download_batch_multi_size(tasks)
            self.index["restaurants"][theme] = saved

    def _load_avatar_queries(self) -> list[str]:
        config_path = BLUEPRINTS_DIR / "global_config.json"
        try:
            with open(config_path, encoding="utf-8") as f:
                data = json.load(f)
                return data.get("AVATAR_SEARCH_TERMS", [])
        except Exception as e:
            logger.warning(f"Could not load avatar queries from config: {e}. Using defaults.")
            return ["avatar icon", "user profile"]

    def download_avatars(self):
        logger.info("--- DOWNLOADING USER AVATARS ---")
        avatars_dir = OUTPUT_DIR / "avatars"

        if avatars_dir.exists():
            logger.info(f"Cleaning existing avatars folder: {avatars_dir}")
            try:
                import shutil

                shutil.rmtree(avatars_dir)
            except Exception as e:
                logger.warning(f"Failed to clean avatars folder: {e}")

        avatars_dir.mkdir(parents=True, exist_ok=True)

        avatar_queries = self._load_avatar_queries()
        if not avatar_queries:
            logger.warning("No avatar queries found in config!")
            return

        target_total = 1000
        per_query_target = max(5, target_total // len(avatar_queries))

        all_tasks: list[tuple[str, Path, str]] = []

        for query in avatar_queries:
            logger.info(f"Query: '{query}' (target: {per_query_target})")
            # Use mixed search (Pixabay + Unsplash) for better variety (portraits!)
            urls = self.search_mixed_urls(query, per_query_target, orientation="vertical", pixabay_ratio=0.4)

            for i, url in enumerate(urls):
                safe_query = slugify(query)[:30]
                filename = f"avatar_{safe_query}_{i + 1:03d}.{IMAGE_FORMAT.lower()}"
                save_path = avatars_dir / filename
                rel_path = f"avatars/{filename}"
                all_tasks.append((url, save_path, rel_path))

        logger.info(f"Downloading {len(all_tasks)} avatar images...")
        saved = self.download_batch_multi_size(all_tasks, include_tiny=True, avatar_mode=True)
        self.index["avatars"] = saved
        logger.info(f"Downloaded {len(saved)} avatars")

    def save_index(self):
        self._index_mgr.save(self.index)
        logger.info(f"Index saved to {INDEX_FILE}")
        self._save_seen_urls()

    def run(self, args=None):
        skip_cleanup = args.no_cleanup if args else False
        only_ingredients = args.only_ingredients if args else False
        only_dishes = args.only_dishes if args else False
        only_restaurants = args.only_restaurants if args else False
        only_avatars = args.only_avatars if args else False

        specific_mode = any([only_ingredients, only_dishes, only_restaurants, only_avatars])

        if not skip_cleanup and not specific_mode:
            if not self.cleanup():
                logger.info("Exiting because cleanup was cancelled.")
                return
        else:
            logger.info("Skipping cleanup (--no-cleanup or specific mode selected)")

        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

        if not specific_mode or only_dishes:
            self.download_dishes()

        if not specific_mode or only_restaurants:
            self.download_restaurants()

        if not specific_mode or only_avatars:
            self.download_avatars()

        if not specific_mode or only_ingredients:
            self.download_ingredients()

        self.save_index()

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Photo Fetcher")
    parser.add_argument("--no-cleanup", action="store_true", help="Skip the cleanup/delete phase")
    parser.add_argument("--only-ingredients", action="store_true", help="Download only ingredients")
    parser.add_argument("--only-dishes", action="store_true", help="Download only dishes")
    parser.add_argument("--only-restaurants", action="store_true", help="Download only restaurants")
    parser.add_argument("--only-avatars", action="store_true", help="Download only avatars")
    parser.add_argument("--quiet", "-q", action="store_true", help="Only show warnings and errors")
    parser.add_argument("--debug", "-d", action="store_true", help="Show DEBUG logs (most verbose)")

    args = parser.parse_args()

    log_level = "DEBUG" if args.debug else "INFO"
    LoggingConfig.setup(level=log_level, quiet=args.quiet)

    downloader = PixabayDownloader()
    downloader.run(args)
