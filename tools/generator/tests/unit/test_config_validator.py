"""
Unit tests for configuration validation with Pydantic.

Tests:
- Valid configuration passes
- Invalid types rejected
- Range validation (gt, le, ge constraints)
- Cross-field validation (proportionality)
- Unknown fields rejected
"""

import pytest
from pydantic import ValidationError

from orchestration.config_validator import GenerationConfigSchema, validate_config

def test_valid_config_passes():
    """Test that valid configuration is accepted."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
        "enable_dietary_restrictions": True,
        "enable_allergen_tracking": True,
    }

    result = validate_config(config)

    assert result.num_users == 50000
    assert result.num_restaurants == 2000
    assert isinstance(result, GenerationConfigSchema)

def test_invalid_type_rejected():
    """Test that invalid types are rejected."""
    config = {
        "num_users": "invalid",  # Should be int
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
    }

    with pytest.raises(ValidationError):
        validate_config(config)

def test_num_users_too_low():
    """Test that num_users < 100 is rejected."""
    config = {
        "num_users": 50,  # Too low
        "num_restaurants": 100,
        "num_dishes": 500,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
    }

    with pytest.raises(ValidationError, match="too low"):
        validate_config(config)

def test_dishes_not_proportional_to_restaurants():
    """Test that dishes must be 5-20x restaurants."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 2000,  # Should be 10000-40000 (5-20x restaurants)
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
    }

    with pytest.raises(ValidationError, match="should be 5-20x"):
        validate_config(config)

def test_percentages_exceed_100():
    """Test that premium + budget percentages can't exceed 100%."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 60,
        "budget_restaurant_pct": 50,  # Total: 110% - invalid!
    }

    with pytest.raises(ValidationError, match="exceeds 100%"):
        validate_config(config)

def test_unknown_field_rejected():
    """Test that unknown fields are rejected (extra='forbid')."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
        "unknown_field": "should fail",  # Extra field
    }

    # Pydantic v2 error message: "Extra inputs are not permitted"
    with pytest.raises(ValidationError, match="Extra inputs are not permitted"):
        validate_config(config)

def test_range_validation_cpu_percent():
    """Test that worker_cpu_usage_percent must be 0.1-1.0."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 0.5,
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 1.5,  # Too high (max 1.0)
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
    }

    with pytest.raises(ValidationError):
        validate_config(config)

def test_power_user_percentage_range():
    """Test that power_user_percentage must be 0.0-1.0."""
    config = {
        "num_users": 50000,
        "num_restaurants": 2000,
        "num_dishes": 20000,
        "avg_reviews_per_user": 40,
        "power_user_percentage": 1.5,  # Too high
        "zipf_alpha": 1.0,
        "default_mood_propensity": 0.3,
        "worker_cpu_usage_percent": 0.75,
        "max_db_connections_limit": 16,
        "premium_restaurant_pct": 20,
        "budget_restaurant_pct": 30,
    }

    with pytest.raises(ValidationError):
        validate_config(config)
