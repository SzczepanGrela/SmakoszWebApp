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
]
