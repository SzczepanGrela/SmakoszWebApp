"""
Image Download Service

Handles HTTP download, multi-size image generation, batch downloads,
and URL deduplication logic.

Separated from PixabayDownloader so that:
- Image processing can be tested independently (no API key needed).
- Other tools (media_pipeline, refetch_photos) can reuse the same
  download machinery without coupling to Pixabay-specific search logic.

Responsibility: *Given a URL and a target path, download and process it.*
Not responsible for: search queries, index management, directory cleanup.
"""

from __future__ import annotations

import logging
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from pathlib import Path
from typing import Any

import requests
from PIL import Image

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from config import PHOTO_CONFIG
from utils.image_processor import resize_and_crop

logger = logging.getLogger(__name__)

# Optional blurhash/numpy - graceful degradation when not installed.
try:
    import blurhash  # type: ignore
    import numpy as np  # type: ignore

    BLURHASH_AVAILABLE = True
except ImportError:
    BLURHASH_AVAILABLE = False
    logger.debug("blurhash library not available. BlurHash generation skipped.")

# Size and format constants derived from project config.
TARGET_SIZE: tuple[int, int] = (int(PHOTO_CONFIG["target_width"]), int(PHOTO_CONFIG["target_height"]))
SIZE_FULL: tuple[int, int] = tuple(PHOTO_CONFIG.get("size_full", (1280, 960)))  # type: ignore[assignment]
SIZE_AVATAR: tuple[int, int] = tuple(PHOTO_CONFIG.get("size_avatar", (300, 300)))  # type: ignore[assignment]
SIZE_THUMB: tuple[int, int] = tuple(PHOTO_CONFIG.get("size_thumb", (200, 150)))  # type: ignore[assignment]
SIZE_TINY: tuple[int, int] = tuple(PHOTO_CONFIG.get("size_tiny", (50, 50)))  # type: ignore[assignment]
SUFFIX_THUMB: str = str(PHOTO_CONFIG.get("suffix_thumb", "_thumb"))
SUFFIX_TINY: str = str(PHOTO_CONFIG.get("suffix_tiny", "_tiny"))
IMAGE_FORMAT: str = str(PHOTO_CONFIG.get("image_format", "WEBP"))
IMAGE_QUALITY: int = int(PHOTO_CONFIG.get("image_quality", 80))  # type: ignore[arg-type]
WORKERS: int = int(PHOTO_CONFIG.get("workers", 10))  # type: ignore[arg-type]

class ImageDownloadService:
    """
    Downloads and processes images into one or more output sizes.

    Args:
        session:    Pre-configured requests.Session (with retry logic).
        seen_urls:  Mutable dict shared with caller - maps URL -> relative path.
                    Used for deduplication across runs.
        output_dir: Root directory for all saved images. Relative paths in
                    returned dicts are computed relative to this directory.
    """

    def __init__(
        self,
        session: requests.Session,
        seen_urls: dict[str, str],
        output_dir: Path,
    ) -> None:
        self.session = session
        self.seen_urls = seen_urls
        self.output_dir = output_dir

    # ------------------------------------------------------------------
    # Single-image processing
    # ------------------------------------------------------------------

    def process_image(
        self,
        url: str,
        save_path: Path,
        target_size: tuple[int, int] | None = None,
    ) -> tuple[bool, dict[str, Any] | None]:
        """
        Download, resize/crop, generate BlurHash, and save one image.

        Returns:
            (success, metadata) where metadata contains blurhash/width/height.
        """
        if save_path.exists():
            try:
                existing_img = Image.open(save_path)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")

                metadata: dict[str, Any] = {
                    "blurhash": None,
                    "width": existing_img.width,
                    "height": existing_img.height,
                }

                if BLURHASH_AVAILABLE:
                    try:
                        metadata["blurhash"] = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass

                return (True, metadata)
            except Exception:
                return (True, None)

        size = target_size if target_size else TARGET_SIZE

        try:
            resp = self.session.get(url, timeout=15)
            if resp.status_code != 200:
                return (False, None)

            img: Image.Image = Image.open(BytesIO(resp.content))
            if img.mode != "RGB":
                img = img.convert("RGB")

            if img.width < size[0] or img.height < size[1]:
                logger.debug(f"Skipping low-res image: {img.width}x{img.height} (min: {size[0]}x{size[1]})")
                return (False, None)

            img_ratio = img.width / img.height
            target_ratio = size[0] / size[1]

            if img_ratio > target_ratio:
                new_height = size[1]
                new_width = int(new_height * img_ratio)
            else:
                new_width = size[0]
                new_height = int(new_width / img_ratio)

            img = img.resize((new_width, new_height), Image.Resampling.LANCZOS)

            left = (new_width - size[0]) / 2
            top = (new_height - size[1]) / 2
            img = img.crop((left, top, left + size[0], top + size[1]))

            final_width = img.width
            final_height = img.height

            hash_str: str | None = None
            if BLURHASH_AVAILABLE:
                try:
                    hash_str = blurhash.encode(np.array(img), components_x=4, components_y=3)
                except Exception as e:
                    logger.debug(f"BlurHash generation failed for {url}: {e}")

            save_path.parent.mkdir(parents=True, exist_ok=True)
            img.save(save_path, IMAGE_FORMAT, quality=IMAGE_QUALITY)

            return (True, {"blurhash": hash_str, "width": final_width, "height": final_height})

        except Exception as e:
            logger.debug(f"Error processing {url}: {e}")
            return (False, None)

    def process_image_multi_size(
        self,
        url: str,
        save_path_full: Path,
        include_tiny: bool = False,
        avatar_mode: bool = False,
    ) -> tuple[bool, dict[str, Any] | None]:
        """
        Download one image and save it at full, thumb, and optionally tiny size.

        Returns:
            (success, metadata) where metadata includes path_thumb/path_tiny.
        """
        full_size = SIZE_AVATAR if avatar_mode else SIZE_FULL
        stem = save_path_full.stem
        suffix = save_path_full.suffix
        parent = save_path_full.parent

        save_path_thumb = parent / f"{stem}{SUFFIX_THUMB}{suffix}"
        save_path_tiny = parent / f"{stem}{SUFFIX_TINY}{suffix}" if include_tiny else None

        all_exist = save_path_full.exists() and save_path_thumb.exists()
        if include_tiny and save_path_tiny:
            all_exist = all_exist and save_path_tiny.exists()

        if all_exist:
            try:
                existing_img = Image.open(save_path_full)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")

                metadata: dict[str, Any] = {
                    "blurhash": None,
                    "width": existing_img.width,
                    "height": existing_img.height,
                    "path_thumb": str(
                        save_path_thumb.relative_to(self.output_dir.parent)
                        if save_path_thumb.is_relative_to(self.output_dir.parent)
                        else save_path_thumb
                    ),
                }

                if include_tiny and save_path_tiny:
                    metadata["path_tiny"] = str(
                        save_path_tiny.relative_to(self.output_dir.parent)
                        if save_path_tiny.is_relative_to(self.output_dir.parent)
                        else save_path_tiny
                    )

                if BLURHASH_AVAILABLE:
                    try:
                        metadata["blurhash"] = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass

                return (True, metadata)
            except Exception:
                return (True, None)

        # Full file exists but some derived sizes are missing - regenerate from disk.
        if save_path_full.exists():
            try:
                existing_img = Image.open(save_path_full)
                if existing_img.mode != "RGB":
                    existing_img = existing_img.convert("RGB")

                if not save_path_thumb.exists():
                    img_thumb = resize_and_crop(existing_img.copy(), SIZE_THUMB)
                    img_thumb.save(save_path_thumb, IMAGE_FORMAT, quality=IMAGE_QUALITY)

                if include_tiny and save_path_tiny and not save_path_tiny.exists():
                    img_tiny = resize_and_crop(existing_img.copy(), SIZE_TINY)
                    img_tiny.save(save_path_tiny, IMAGE_FORMAT, quality=IMAGE_QUALITY)

                hash_str: str | None = None
                if BLURHASH_AVAILABLE:
                    try:
                        hash_str = blurhash.encode(np.array(existing_img), components_x=4, components_y=3)
                    except Exception:
                        pass

                metadata = {
                    "blurhash": hash_str,
                    "width": existing_img.width,
                    "height": existing_img.height,
                    "path_thumb": str(save_path_thumb.relative_to(self.output_dir)),
                }
                if include_tiny and save_path_tiny:
                    metadata["path_tiny"] = str(save_path_tiny.relative_to(self.output_dir))

                return (True, metadata)
            except Exception as e:
                logger.debug(f"Error generating derived sizes from existing {save_path_full}: {e}")
                return (True, None)

        # Full file does NOT exist - download fresh.
        try:
            resp = self.session.get(url, timeout=15)
            if resp.status_code != 200:
                return (False, None)

            img: Image.Image = Image.open(BytesIO(resp.content))
            if img.mode != "RGB":
                img = img.convert("RGB")

            if img.width < full_size[0] or img.height < full_size[1]:
                logger.debug(f"Skipping low-res image: {img.width}x{img.height} (min: {full_size[0]}x{full_size[1]})")
                return (False, None)

            parent.mkdir(parents=True, exist_ok=True)

            img_full = resize_and_crop(img.copy(), full_size)
            img_full.save(save_path_full, IMAGE_FORMAT, quality=IMAGE_QUALITY)

            img_thumb = resize_and_crop(img.copy(), SIZE_THUMB)
            img_thumb.save(save_path_thumb, IMAGE_FORMAT, quality=IMAGE_QUALITY)

            if include_tiny and save_path_tiny:
                img_tiny = resize_and_crop(img.copy(), SIZE_TINY)
                img_tiny.save(save_path_tiny, IMAGE_FORMAT, quality=IMAGE_QUALITY)

            hash_str = None
            if BLURHASH_AVAILABLE:
                try:
                    hash_str = blurhash.encode(np.array(img_full), components_x=4, components_y=3)
                except Exception as e:
                    logger.debug(f"BlurHash generation failed for {url}: {e}")

            metadata = {
                "blurhash": hash_str,
                "width": img_full.width,
                "height": img_full.height,
                "path_thumb": str(save_path_thumb.relative_to(self.output_dir)),
            }

            if include_tiny and save_path_tiny:
                metadata["path_tiny"] = str(save_path_tiny.relative_to(self.output_dir))

            return (True, metadata)

        except Exception as e:
            logger.debug(f"Error processing multi-size {url}: {e}")
            return (False, None)

    # ------------------------------------------------------------------
    # Batch orchestration
    # ------------------------------------------------------------------

    def download_batch(self, tasks: list[tuple]) -> list[dict[str, Any]]:
        """
        Execute a batch of download tasks using ThreadPoolExecutor.

        Each task is either:
        - ``(url, save_path, rel_path)``          - uses default TARGET_SIZE
        - ``(url, save_path, rel_path, size)``    - uses custom size tuple
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

                if url in self.seen_urls:
                    old_rel_path = self.seen_urls[url]
                    if (self.output_dir / old_rel_path).exists():
                        _, metadata = self.process_image(url, self.output_dir / old_rel_path, target_size)
                        result = {"path": old_rel_path}
                        if metadata:
                            result.update(metadata)
                        saved_files.append(result)
                        continue

                futures.append(executor.submit(self.process_image, url, save_path, target_size))
                task_indices.append(len(futures) - 1)

            for i, future_idx in enumerate(task_indices):
                f = futures[future_idx]
                task = tasks[i]
                url = task[0]
                rel_path = task[2]

                if url in self.seen_urls and (self.output_dir / self.seen_urls[url]).exists():
                    continue

                success, metadata = f.result()
                if success:
                    self.seen_urls[url] = rel_path
                    result = {"path": rel_path}
                    if metadata:
                        result.update(metadata)
                    saved_files.append(result)

        return saved_files

    def download_batch_multi_size(
        self,
        tasks: list[tuple[str, Path, str]],
        include_tiny: bool = False,
        avatar_mode: bool = False,
    ) -> list[dict[str, Any]]:
        """
        Execute batch download with multi-size image generation.

        Each task is ``(url, save_path_full, rel_path_full)``.
        """
        saved_files: list[dict[str, Any]] = []
        futures = []
        task_map: list[tuple[int, str, str]] = []

        with ThreadPoolExecutor(max_workers=WORKERS) as executor:
            for url, save_path, rel_path in tasks:
                if url in self.seen_urls:
                    old_rel_path = self.seen_urls[url]
                    old_full_path = self.output_dir / old_rel_path
                    if old_full_path.exists():
                        _, metadata = self.process_image_multi_size(url, old_full_path, include_tiny, avatar_mode)
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
                    avatar_mode,
                )
                task_map.append((len(futures), url, rel_path))
                futures.append(future)

            for future_idx, url, rel_path in task_map:
                if url in self.seen_urls and (self.output_dir / self.seen_urls[url]).exists():
                    continue

                success, metadata = futures[future_idx].result()
                if success:
                    self.seen_urls[url] = rel_path
                    result = {"path": rel_path}
                    if metadata:
                        result.update(metadata)
                    saved_files.append(result)

        return saved_files
