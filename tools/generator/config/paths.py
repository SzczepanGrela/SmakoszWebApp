import os

LOCALE = "pl_PL"

BLUEPRINTS_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "blueprints")

PIXABAY_API_KEY = os.getenv("PIXABAY_API_KEY", "")
PIXABAY_ENABLED = bool(PIXABAY_API_KEY)

PHOTO_CONFIG = {
    "pixabay_api_key": PIXABAY_API_KEY,
    "pixabay_enabled": PIXABAY_ENABLED,
    "output_dir": os.getenv("IMAGE_OUTPUT_DIR", "E:/smakosz/images"),
    "cache_file": "data/photo_cache.json",
    "size_hero": (1600, 900),
    "size_full": (1280, 960),
    "size_thumb": (200, 150),
    "size_avatar": (300, 300),
    "size_ingredient": (200, 200),
    "size_tiny": (50, 50),
    "suffix_hero": "_hero",
    "suffix_thumb": "_thumb",
    "suffix_tiny": "_tiny",
    "target_width": 1280,
    "target_height": 960,
    "image_quality": 80,
    "image_format": "WEBP",
    "workers": 5,
    "images_per_query": 200,
    "min_photos_per_variant": 5,
    "max_photos_per_variant": 15,
    "restaurant_base_count": 20,
    "local_photo_index": os.getenv("LOCAL_PHOTO_INDEX", "data/photo_index.json"),
    "max_retries": 3,
    "timeout_seconds": 10,
    "rate_limit_per_hour": 4900,
    "fallback_enabled": True,
    "fallback_base_url": "https://picsum.photos",
}
