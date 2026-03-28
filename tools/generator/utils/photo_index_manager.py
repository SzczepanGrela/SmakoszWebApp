from __future__ import annotations

import json
import logging
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

_EMPTY_INDEX: dict[str, Any] = {
    "dishes": {},
    "restaurants": {},
    "avatars": [],
    "ingredients": {},
}

class PhotoIndexManager:

    def __init__(self, index_path: Path) -> None:
        self.index_path = index_path

    def load(self) -> dict[str, Any]:
        if not self.index_path.exists():
            logger.debug(f"Index not found - returning empty index: {self.index_path}")
            return self._empty()

        try:
            with open(self.index_path, encoding="utf-8") as f:
                data: dict[str, Any] = json.load(f)
        except json.JSONDecodeError as exc:
            logger.warning(f"Corrupted index file {self.index_path}: {exc}. Starting fresh.")
            return self._empty()

        for key, default in _EMPTY_INDEX.items():
            data.setdefault(key, type(default)())

        return data

    def save(self, data: dict[str, Any]) -> None:
        self.index_path.parent.mkdir(parents=True, exist_ok=True)
        with open(self.index_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        logger.debug(f"Index saved: {self.index_path}")

    @staticmethod
    def make_entry(path: str) -> dict[str, Any]:
        return {"path": path, "blurhash": None, "width": None, "height": None}

    @staticmethod
    def _empty() -> dict[str, Any]:
        return {k: type(v)() for k, v in _EMPTY_INDEX.items()}
