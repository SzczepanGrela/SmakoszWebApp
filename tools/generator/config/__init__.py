"""
Configuration Package

This package organizes configuration settings into logical modules:
- database: Database connection settings
- generation: Data generation parameters and metrics
- paths: File paths, locale, and photo service configuration

All exports are re-exported here for backward compatibility with existing imports.
"""

# Database configuration
from .database import DATABASE_CONFIG, get_connection_params

# Generation configuration
from .generation import (
    DERIVED_PREFERENCES_CONFIG,
    EXPECTED_METRICS,
    GENERATION_CONFIG,
    REVIEW_LOCALITY,
)

# Paths and locale configuration
from .paths import (
    BLUEPRINTS_DIR,
    LOCALE,
    PHOTO_CONFIG,
    PIXABAY_API_KEY,
    PIXABAY_ENABLED,
)

__all__ = [
    # Database
    "DATABASE_CONFIG",
    "get_connection_params",
    # Generation
    "GENERATION_CONFIG",
    "REVIEW_LOCALITY",
    "DERIVED_PREFERENCES_CONFIG",
    "EXPECTED_METRICS",
    # Paths
    "BLUEPRINTS_DIR",
    "LOCALE",
    "PHOTO_CONFIG",
    "PIXABAY_API_KEY",
    "PIXABAY_ENABLED",
    # Utility
    "print_config",
]

def print_config():
    """Print current configuration summary."""
    import os

    print("=" * 60)
    print("[CONFIG] MOCKDATAFACTORY CONFIGURATION")
    print("=" * 60)
    print(f"  DB Host: {DATABASE_CONFIG['host']}:{DATABASE_CONFIG['port']}")
    print(f"  Database: {DATABASE_CONFIG['database']}")

    print("\n[PARAMS] GENERATION PARAMETERS:")
    print(f"  Users: {GENERATION_CONFIG['num_users']:,}")
    print(f"  Restaurants: {GENERATION_CONFIG['num_restaurants']:,}")
    print(f"  Dishes: {GENERATION_CONFIG['num_dishes']:,}")
    print(f"  Custom Avatars: {GENERATION_CONFIG['custom_avatar_percentage'] * 100:.0f}% of users")  # type: ignore[operator]

    print("\n[PHOTOS]:")
    r2_domain = os.getenv("R2_PUBLIC_DOMAIN")
    if r2_domain:
        print("  Service: Cloudflare R2")
        print(f"  Domain: {r2_domain.rstrip('/')}")
    else:
        print("  Service: Local paths (/images/mock/)")

    print("\n[PERFORMANCE]:")
    print(f"  Worker CPU Usage: {float(GENERATION_CONFIG['worker_cpu_usage_percent']) * 100}%")  # type: ignore

    print("=" * 60)

if __name__ == "__main__":
    print_config()
