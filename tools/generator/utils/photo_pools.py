import json
import logging
import os
import random
from pathlib import Path
from typing import Any

from config import PHOTO_CONFIG

logger = logging.getLogger(__name__)
INDEX_FILE = Path(str(PHOTO_CONFIG["local_photo_index"]))

class PhotoPools:
    """
    Photo pool manager for dish and restaurant images.

    Manages photo assignment with usage tracking to avoid repetition
    within the same restaurant context.
    """

    def __init__(self) -> None:
        """Initialize photo pools with index and usage tracking."""
        self.index = self._load_index()
        self.usage_history: dict[int, dict[str, set[str]]] = {}
        # Cache the R2 domain to avoid repeated env lookups
        self.r2_domain = os.getenv("R2_PUBLIC_DOMAIN")
        if self.r2_domain:
            self.r2_domain = self.r2_domain.rstrip("/")
            logger.info(f"PhotoPools: Using R2 Public Domain: {self.r2_domain}")
        else:
            logger.info("PhotoPools: Using local paths (/seed/)")

    def _load_index(self) -> dict[str, Any]:
        """
        Load photo index from JSON file.

        Returns:
            dict: Photo index with structure:
                {
                    "dishes": {category: {variant: [photo_paths]}},
                    "restaurants": {theme: [photo_paths]}
                }
        """
        if not INDEX_FILE.exists():
            # Log as DEBUG to avoid spamming console in multiprocessing environments
            # when running without local assets (using placeholders).
            logger.debug(f"Photo index file not found: {INDEX_FILE}. Using placeholders.")
            return {"dishes": {}, "restaurants": {}}

        try:
            with open(INDEX_FILE, encoding="utf-8") as f:
                return json.load(f)
        except (FileNotFoundError, json.JSONDecodeError, OSError) as e:
            logger.error(f"Failed to load photo index from {INDEX_FILE}: {e}")
            return {"dishes": {}, "restaurants": {}}

    def _get_used(self, res_id: int, type_key: str) -> set[str]:
        """Get or create set of used photos for a restaurant."""
        if res_id not in self.usage_history:
            self.usage_history[res_id] = {"dishes": set(), "interior": set()}
        return self.usage_history[res_id][type_key]

    def _format_url(self, path: str) -> str:
        """Helper to format URL based on configuration (R2 vs Local).

        R2 Path Architecture (v3):
        - Seed data: seed/{dishes,restaurants,avatars,hero}/...
        - Ingredients: seed/ingredients/...
        """
        if self.r2_domain:
            return f"{self.r2_domain}/seed/{path}"
        return f"/seed/{path}"

    def _extract_photo_data(self, photo_entry: str | dict) -> tuple[str, str | None, int | None, int | None]:
        """
        Extract path, blurhash, width, and height from photo entry (backward compatible).

        Args:
            photo_entry: Either a string (old format) or dict with {path, blurhash, width, height} (new format)

        Returns:
            tuple[str, str | None, int | None, int | None]: (path, blurhash, width, height)
        """
        if isinstance(photo_entry, dict):
            return (
                photo_entry.get("path", ""),
                photo_entry.get("blurhash"),
                photo_entry.get("width"),
                photo_entry.get("height"),
            )
        elif isinstance(photo_entry, str):
            # Old format: just a path string
            return (photo_entry, None, None, None)
        else:
            logger.warning(f"Unknown photo entry format: {type(photo_entry)}")
            return ("", None, None, None)

    def get_dish_photo(self, category: str, variant: str, restaurant_id: int) -> dict[str, str | int | None]:
        """
        Get photo metadata for a dish, avoiding repetition within the same restaurant.

        Args:
            category: Dish category/archetype (e.g., "Pizza", "Burger")
            variant: Specific dish variant (e.g., "Margherita", "Cheeseburger")
            restaurant_id: Restaurant ID for usage tracking

        Returns:
            dict: {"url": str, "blurhash": str | None, "width": int | None, "height": int | None}
        """
        # Try direct lookup first, then slugified (to match snake_case folders)
        cat_data = self.index.get("dishes", {}).get(category)
        if cat_data is None:
            from tools.utils import slugify

            cat_data = self.index.get("dishes", {}).get(slugify(category), {})

        photos = cat_data.get(variant)
        if photos is None:
            from tools.utils import slugify

            photos = cat_data.get(slugify(variant), [])

        if not photos:
            photos = [p for sublist in cat_data.values() for p in sublist]

        if not photos:
            logger.error(f"No photos available for dish: {category}/{variant}")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        used = self._get_used(restaurant_id, "dishes")

        # Extract paths for usage tracking (works with both old and new format)
        photo_paths = []
        for p in photos:
            path, _, _, _ = self._extract_photo_data(p)
            if path:
                photo_paths.append((p, path))

        # Filter by unused paths
        unused = [p for p, path in photo_paths if path not in used]

        if unused:
            selected = random.choice(unused)
        else:
            selected = random.choice([p for p, _ in photo_paths]) if photo_paths else photos[0]

        # Track usage by path and extract all metadata
        selected_path, selected_hash, width, height = self._extract_photo_data(selected)
        used.add(selected_path)

        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_restaurant_photo(self, theme: str, restaurant_id: int) -> dict[str, str | int | None]:
        """
        Get photo metadata for a restaurant interior, avoiding repetition.

        Args:
            theme: Restaurant theme (e.g., "Pizzeria", "Sushi Bar")
            restaurant_id: Restaurant ID for usage tracking

        Returns:
            dict: {"url": str, "blurhash": str | None, "width": int | None, "height": int | None}
        """
        photos = self.index.get("restaurants", {}).get(theme)
        if photos is None:
            from tools.utils import slugify

            photos = self.index.get("restaurants", {}).get(slugify(theme), [])
        if not photos:
            logger.error(f"No photos available for restaurant theme: {theme}")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        used = self._get_used(restaurant_id, "interior")

        # Extract paths for usage tracking (works with both old and new format)
        photo_paths = []
        for p in photos:
            path, _, _, _ = self._extract_photo_data(p)
            if path:
                photo_paths.append((p, path))

        # Filter by unused paths
        unused = [p for p, path in photo_paths if path not in used]

        if unused:
            selected = random.choice(unused)
        else:
            selected = random.choice([p for p, _ in photo_paths]) if photo_paths else photos[0]

        # Track usage by path and extract all metadata
        selected_path, selected_hash, width, height = self._extract_photo_data(selected)
        used.add(selected_path)

        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_review_photo(self, archetype: str, variant: str) -> dict[str, str | int | None]:
        """
        Get photo metadata for a user review (dish photo).
        Does not track usage history as multiple users can upload similar photos.

        Args:
            archetype: Dish archetype
            variant: Dish variant

        Returns:
            dict: {"url": str, "blurhash": str | None, "width": int | None, "height": int | None}
        """
        # Try direct lookup first, then slugified
        cat_data = self.index.get("dishes", {}).get(archetype)
        if cat_data is None:
            from tools.utils import slugify

            cat_data = self.index.get("dishes", {}).get(slugify(archetype), {})

        photos = cat_data.get(variant)
        if photos is None:
            from tools.utils import slugify

            photos = cat_data.get(slugify(variant), [])

        if not photos:
            # Fallback to any photo from category
            photos = [p for sublist in cat_data.values() for p in sublist]

        if not photos:
            logger.error(f"No photos available for review: {archetype}/{variant}")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        selected = random.choice(photos)
        selected_path, selected_hash, width, height = self._extract_photo_data(selected)
        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_user_photo_generic(self) -> str:
        """
        Get a generic user photo URL (UI Avatars service).

        Returns:
            str: UI Avatars URL with random background color
        """
        return "https://ui-avatars.com/api/?name=User&background=random"

    def get_user_avatar(self) -> dict[str, str | int | None]:
        """
        Get custom user avatar metadata from the avatar pool.

        Randomly selects from the pre-downloaded avatar image pool (300x300 squares).
        Returns None URL if no avatars are available in the index.

        Returns:
            dict: {"url": str, "blurhash": str | None, "width": int | None, "height": int | None}
        """
        avatars: list = self.index.get("avatars", [])

        # Fallback: If no avatars downloaded, log warning and return None
        if not avatars:
            logger.warning("No avatars in index - photo_index may be missing avatar data")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        # Select random avatar from pool and format with R2/local domain
        selected = random.choice(avatars)
        selected_path, selected_hash, width, height = self._extract_photo_data(selected)
        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_ingredient_photo(self, ingredient_name: str) -> dict[str, str | int | None]:
        """
        Get photo metadata for an ingredient icon.

        Args:
            ingredient_name: Name of the ingredient (e.g., "Tomato")

        Returns:
            dict: {"url": str | None, "blurhash": str | None, "width": int | None, "height": int | None}
        """
        ing_index = self.index.get("ingredients", {})
        photo_entry = ing_index.get(ingredient_name)

        if photo_entry:
            path, blurhash_val, width, height = self._extract_photo_data(photo_entry)
            if path:
                return {"url": self._format_url(path), "blurhash": blurhash_val, "width": width, "height": height}

        return {"url": None, "blurhash": None, "width": None, "height": None}
