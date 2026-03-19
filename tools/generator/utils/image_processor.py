"""
Image processing utilities - single source of truth for all tools.

Extracted from: tools/fetch_photos.py, tools/media_pipeline.py,
                tools/generate_thumbs.py, tools/refetch_photos.py,
                tools/refresh_photo_index.py
"""

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
    import blurhash  # type: ignore
    _BLURHASH_AVAILABLE = True
except ImportError:
    _BLURHASH_AVAILABLE = False
    logger.debug("blurhash library not available. Install with: pip install blurhash-python")

def resize_and_crop(img: Image.Image, target: tuple[int, int]) -> Image.Image:
    """
    Resize an image maintaining aspect ratio, then center-crop to exact target dimensions.

    Uses a scale-cover approach: the image is scaled up until it covers the entire
    target rectangle, then cropped to center.

    Args:
        img: PIL Image to resize.
        target: (width, height) tuple for the desired output size.

    Returns:
        New PIL Image at exactly (width, height).
    """
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
    """
    Generate a BlurHash placeholder and read image dimensions from a file.

    Args:
        image_path: Path to an image file on disk.

    Returns:
        (hash_str, width, height) - any value may be None on failure or if
        the blurhash library is not installed.
    """
    if not _BLURHASH_AVAILABLE:
        return None, None, None

    try:
        with Image.open(image_path) as img:
            if img.mode != "RGB":
                img = img.convert("RGB")
            width, height = img.size

            # Resize to thumbnail for faster computation (does not change aspect ratio)
            thumb = img.copy()
            thumb.thumbnail((100, 100))

            thumb_array = np.array(thumb)
            hash_value = blurhash.encode(thumb_array, components_x=4, components_y=3)
            return hash_value, width, height

    except Exception as e:
        logger.error(f"Blurhash error for {image_path}: {e}")
        return None, None, None
