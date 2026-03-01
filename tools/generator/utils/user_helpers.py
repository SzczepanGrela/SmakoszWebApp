"""
User Helper Functions

This module contains utility functions for generating user-specific attributes
including preference vectors, personal information, and profile data.
"""

import random

from scipy.stats import beta as beta_dist

from utils.faker_instance import fake

def generate_user_characteristics_vector() -> dict:
    """
    Generates a 14-dimensional user preference vector with tolerances.

    Creates a comprehensive preference profile across flavor, texture, physics,
    and context dimensions. Each dimension has:
    - value: User's preferred level (0.0-1.0) from Beta distribution
    - tolerance: Comfort zone width (0.1-0.7) - lower means pickier user

    Returns:
        dict: 14-dimensional vector with nested {value, tolerance} structure
    """
    vector = {}

    # Flavor dimensions (6)
    vector["flavor_sweetness"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_bitterness"] = {
        "value": round(float(beta_dist.rvs(1.5, 3.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_spiciness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_umami"] = {
        "value": round(float(beta_dist.rvs(3.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_sourness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_saltiness"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    # Texture dimensions (3)
    vector["texture_crispy"] = {
        "value": round(float(beta_dist.rvs(3.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["texture_creamy"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["texture_chewy"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    # Physics dimensions (3)
    vector["physics_richness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["physics_temperature"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["physics_freshness"] = {
        "value": round(float(beta_dist.rvs(3.5, 1.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    # Context dimensions (2)
    vector["context_price_sensitivity"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["context_portion_preference"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    return vector

def generate_full_name() -> str:
    """
    Generate a Polish full name using Faker.

    Returns:
        str: Full name (e.g., "Jan Kowalski")
    """
    return fake.name()

def generate_phone() -> str:
    """
    Generate a Polish phone number in E.164 format.

    Returns:
        str: Phone number (e.g., "+48 555 123 456")
    """
    return f"+48 {random.randint(500, 999)} {random.randint(100, 999)} {random.randint(100, 999)}"
