from __future__ import annotations

import logging
from pathlib import Path
from typing import TYPE_CHECKING

import numpy as np
from PIL import Image

if TYPE_CHECKING:
    pass

logger = logging.getLogger(__name__)

try:
    import blurhash

    _BLURHASH_AVAILABLE = True
except ImportError:
    _BLURHASH_AVAILABLE = False
    logger.debug("blurhash library not available. Install with: pip install blurhash-python")

def resize_and_crop(img: Image.Image, target: tuple[int, int]) -> Image.Image:
    target_w, target_h = target
    orig_w, orig_h = img.size

    scale = max(target_w / orig_w, target_h / orig_h)
    new_w = int(orig_w * scale)
    new_h = int(orig_h * scale)

    img = img.resize((new_w, new_h), Image.Resampling.LANCZOS)

    left = (new_w - target_w) // 2
    top = (new_h - target_h) // 2
    return img.crop((left, top, left + target_w, top + target_h))

def generate_blurhash(image_path: Path) -> tuple[str | None, int | None, int | None]:
    if not _BLURHASH_AVAILABLE:
        return None, None, None

    try:
        with Image.open(image_path) as img:
            if img.mode != "RGB":
                img = img.convert("RGB")
            width, height = img.size

            thumb = img.copy()
            thumb.thumbnail((100, 100))

            thumb_array = np.array(thumb)
            hash_value = blurhash.encode(thumb_array, components_x=4, components_y=3)
            return hash_value, width, height

    except Exception as e:
        logger.error(f"Blurhash error for {image_path}: {e}")
        return None, None, None
