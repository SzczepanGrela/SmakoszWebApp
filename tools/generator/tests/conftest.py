"""
Shared pytest fixtures for MockDataFactory tests.

Provides common test data, mock database connections, and sample objects.
"""

import json
import sys
from pathlib import Path
from unittest.mock import MagicMock

import pytest

# Add project root to path - do this FIRST before any other imports
PROJECT_ROOT = Path(__file__).parent.parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

def pytest_configure(config):
    """Pytest hook to ensure sys.path is set up before imports."""
    project_root = Path(__file__).parent.parent
    if str(project_root) not in sys.path:
        sys.path.insert(0, str(project_root))

from algorithms.preference_calculator import DIMENSIONS

@pytest.fixture
def project_root() -> Path:
    """Return project root path."""
    return PROJECT_ROOT

@pytest.fixture
def blueprints_dir(project_root) -> Path:
    """Return blueprints directory path."""
    return project_root / "blueprints"

@pytest.fixture
def dishes_json(blueprints_dir) -> dict:
    """Load full dishes.json blueprint."""
    with open(blueprints_dir / "dishes.json", encoding="utf-8") as f:
        return json.load(f)

@pytest.fixture
def menu_templates_json(blueprints_dir) -> dict:
    """Load menu_templates.json blueprint."""
    with open(blueprints_dir / "menu_templates.json", encoding="utf-8") as f:
        return json.load(f)

@pytest.fixture
def ingredients_json(blueprints_dir) -> list:
    """Load ingredients_list.json blueprint (returns list of ingredient names)."""
    with open(blueprints_dir / "ingredients_list.json", encoding="utf-8") as f:
        return json.load(f)

@pytest.fixture
def sample_archetype() -> dict:
    """Minimal archetype data for unit tests."""
    return {
        "base_price": {"mean": 30.0, "stdev": 3.5},
        "archetype_base": {
            "characteristics": {
                "physics_temperature": 0.9,
                "flavor_sweetness": 0.2,
            },
            "default_weights": {"physics_temperature": 0.1, "flavor_sweetness": 0.5, "_default": 1.0},
        },
        "variants": {
            "Test Variant": {
                "price_multiplier": {"mean": 1.0, "stdev": 0.1},
                "ingredients": ["flour", "water"],
                "characteristics": {"physics_richness": 0.7},
                "weights": None,
            }
        },
    }

@pytest.fixture
def sample_user() -> dict:
    """Sample user with preference vector."""
    return {
        "user_id": 1,
        "username": "test_user",
        "secret_characteristics_vector": {dim: {"value": 0.5, "tolerance": 0.2} for dim in DIMENSIONS},
    }

@pytest.fixture
def sample_dish() -> dict:
    """Sample dish with characteristics."""
    return {
        "dish_id": 1,
        "dish_name": "Test Pizza",
        "secret_archetype": "Pizza",
        "secret_variant_name": "Margherita",
        "secret_characteristics_vector": dict.fromkeys(DIMENSIONS, 0.5),
    }

@pytest.fixture
def sample_restaurant() -> dict:
    """Sample restaurant for testing."""
    return {
        "restaurant_id": 1,
        "name": "Test Restaurant",
        "avg_rating": 4.0,
        "price_range": 2,
        "secret_quality_modifiers": {},
    }

@pytest.fixture
def mock_db_connection():
    """Mock database connection that returns configurable results."""
    mock_conn = MagicMock()
    mock_cursor = MagicMock()
    mock_conn.cursor.return_value.__enter__ = MagicMock(return_value=mock_cursor)
    mock_conn.cursor.return_value.__exit__ = MagicMock(return_value=False)
    return mock_conn, mock_cursor

@pytest.fixture
def mock_pixabay_response():
    """Mock Pixabay API response."""
    return {
        "total": 1,
        "totalHits": 1,
        "hits": [
            {
                "id": 12345,
                "webformatURL": "https://pixabay.com/get/test_image.jpg",
                "largeImageURL": "https://pixabay.com/get/test_image_large.jpg",
                "previewURL": "https://pixabay.com/get/test_image_preview.jpg",
            }
        ],
    }

@pytest.fixture
def mock_s3_client():
    """Mock boto3 S3 client for R2 tests."""
    mock_client = MagicMock()
    mock_client.upload_file = MagicMock(return_value=None)
    mock_client.delete_object = MagicMock(return_value=None)
    mock_client.list_objects_v2 = MagicMock(return_value={"Contents": []})
    return mock_client
