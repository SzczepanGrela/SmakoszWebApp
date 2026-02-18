"""
Photo Index Manager

Centralises all read/write access to photo_index.json so that fetch_photos,
refresh_photo_index, and generate_thumbs always use the same file path,
the same empty-index template, and the same JSON serialisation settings
(indent=2, ensure_ascii=False).

Usage:
    from utils.photo_index_manager import PhotoIndexManager

    mgr = PhotoIndexManager(index_path)

    # Load (returns empty index if file is missing or corrupted):
    data = mgr.load()

    # Modify data …

    # Save back:
    mgr.save(data)

    # Build a fresh photo entry dict:
    entry = PhotoIndexManager.make_entry("dishes/Burger/Classic/burger_001.webp")
"""

from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

# Canonical empty structure expected by all consumers.
_EMPTY_INDEX: dict[str, Any] = {
    "dishes": {},
    "restaurants": {},
    "avatars": [],
    "ingredients": {},
}

class PhotoIndexManager:
    """Read/write wrapper for photo_index.json."""

    def __init__(self, index_path: Path) -> None:
        self.index_path = index_path

    # ------------------------------------------------------------------
    # Core I/O
    # ------------------------------------------------------------------

    def load(self) -> dict[str, Any]:
        """
        Load photo index from disk.

        Returns an empty index (copy of the canonical template) when the
        file does not exist or contains invalid JSON.
        """
        if not self.index_path.exists():
            logger.debug(f"Index not found - returning empty index: {self.index_path}")
            return self._empty()

        try:
            with open(self.index_path, encoding="utf-8") as f:
                data: dict[str, Any] = json.load(f)
        except json.JSONDecodeError as exc:
            logger.warning(f"Corrupted index file {self.index_path}: {exc}. Starting fresh.")
            return self._empty()

        # Ensure all top-level keys are present (backwards compatibility).
        for key, default in _EMPTY_INDEX.items():
            data.setdefault(key, type(default)())

        return data

    def save(self, data: dict[str, Any]) -> None:
        """
        Persist *data* to disk.

        Creates parent directories if necessary.
        Uses indent=2 and ensure_ascii=False consistently across all callers.
        """
        self.index_path.parent.mkdir(parents=True, exist_ok=True)
        with open(self.index_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        logger.debug(f"Index saved: {self.index_path}")

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def make_entry(path: str) -> dict[str, Any]:
        """
        Create a standard photo entry dict with null blurhash/dimensions.

        Args:
            path: Relative path from the images output directory.

        Returns:
            ``{"path": path, "blurhash": None, "width": None, "height": None}``
        """
        return {"path": path, "blurhash": None, "width": None, "height": None}

    @staticmethod
    def _empty() -> dict[str, Any]:
        """Return a fresh copy of the canonical empty index."""
        return {k: type(v)() for k, v in _EMPTY_INDEX.items()}
