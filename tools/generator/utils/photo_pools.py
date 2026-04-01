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

    def __init__(self) -> None:
        self.index = self._load_index()
        self.usage_history: dict[int, dict[str, set[str]]] = {}
        self.r2_domain = os.getenv("R2_PUBLIC_DOMAIN")
        if self.r2_domain:
            self.r2_domain = self.r2_domain.rstrip("/")
            logger.info(f"PhotoPools: Using R2 Public Domain: {self.r2_domain}")
        else:
            logger.info("PhotoPools: Using local paths (/seed/)")

    def _load_index(self) -> dict[str, Any]:
        if not INDEX_FILE.exists():
            logger.debug(f"Photo index file not found: {INDEX_FILE}. Using placeholders.")
            return {"dishes": {}, "restaurants": {}}

        try:
            with open(INDEX_FILE, encoding="utf-8") as f:
                return json.load(f)
        except (FileNotFoundError, json.JSONDecodeError, OSError) as e:
            logger.error(f"Failed to load photo index from {INDEX_FILE}: {e}")
            return {"dishes": {}, "restaurants": {}}

    def _get_used(self, res_id: int, type_key: str) -> set[str]:
        if res_id not in self.usage_history:
            self.usage_history[res_id] = {"dishes": set(), "interior": set()}
        return self.usage_history[res_id][type_key]

    def _format_url(self, path: str) -> str:
        if self.r2_domain:
            return f"{self.r2_domain}/seed/{path}"
        return f"/seed/{path}"

    def _extract_photo_data(self, photo_entry: str | dict) -> tuple[str, str | None, int | None, int | None]:
        if isinstance(photo_entry, dict):
            return (
                photo_entry.get("path", ""),
                photo_entry.get("blurhash"),
                photo_entry.get("width"),
                photo_entry.get("height"),
            )
        elif isinstance(photo_entry, str):
            return (photo_entry, None, None, None)
        else:
            logger.warning(f"Unknown photo entry format: {type(photo_entry)}")
            return ("", None, None, None)

    def _select_photo(
        self, section: str, category: str, variant: str | None = None, deduplicate_for: int | None = None
    ) -> dict[str, str | int | None]:
        if section == "restaurants":
            photos = self.index.get("restaurants", {}).get(category)
            if photos is None:
                from tools.utils import slugify
                photos = self.index.get("restaurants", {}).get(slugify(category), [])
        else:
            cat_data = self.index.get("dishes", {}).get(category)
            if cat_data is None:
                from tools.utils import slugify
                cat_data = self.index.get("dishes", {}).get(slugify(category), {})

            if variant is not None:
                photos = cat_data.get(variant)
                if photos is None:
                    from tools.utils import slugify
                    photos = cat_data.get(slugify(variant), [])
            else:
                photos = []

            if not photos:
                photos = [p for sublist in cat_data.values() for p in sublist]

        if not photos:
            logger.error(f"No photos available for {section}: {category}/{variant}")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        if deduplicate_for is not None:
            usage_key = "interior" if section == "restaurants" else "dishes"
            used = self._get_used(deduplicate_for, usage_key)

            photo_paths = []
            for p in photos:
                path, _, _, _ = self._extract_photo_data(p)
                if path:
                    photo_paths.append((p, path))

            unused = [p for p, path in photo_paths if path not in used]

            if unused:
                selected = random.choice(unused)
            else:
                selected = random.choice([p for p, _ in photo_paths]) if photo_paths else photos[0]

            selected_path, selected_hash, width, height = self._extract_photo_data(selected)
            used.add(selected_path)
        else:
            selected = random.choice(photos)
            selected_path, selected_hash, width, height = self._extract_photo_data(selected)

        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_dish_photo(self, category: str, variant: str, restaurant_id: int) -> dict[str, str | int | None]:
        return self._select_photo("dishes", category, variant, deduplicate_for=restaurant_id)

    def get_restaurant_photo(self, theme: str, restaurant_id: int) -> dict[str, str | int | None]:
        return self._select_photo("restaurants", theme, deduplicate_for=restaurant_id)

    def get_review_photo(self, archetype: str, variant: str) -> dict[str, str | int | None]:
        return self._select_photo("dishes", archetype, variant)

    def get_user_photo_generic(self) -> str:
        return "https://ui-avatars.com/api/?name=User&background=random"

    def get_user_avatar(self) -> dict[str, str | int | None]:
        avatars: list = self.index.get("avatars", [])

        if not avatars:
            logger.warning("No avatars in index - photo_index may be missing avatar data")
            return {"url": None, "blurhash": None, "width": None, "height": None}

        selected = random.choice(avatars)
        selected_path, selected_hash, width, height = self._extract_photo_data(selected)
        return {"url": self._format_url(selected_path), "blurhash": selected_hash, "width": width, "height": height}

    def get_ingredient_photo(self, ingredient_name: str) -> dict[str, str | int | None]:
        ing_index = self.index.get("ingredients", {})
        photo_entry = ing_index.get(ingredient_name)

        if photo_entry:
            path, blurhash_val, width, height = self._extract_photo_data(photo_entry)
            if path:
                return {"url": self._format_url(path), "blurhash": blurhash_val, "width": width, "height": height}

        return {"url": None, "blurhash": None, "width": None, "height": None}
