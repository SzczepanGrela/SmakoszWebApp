"""
Generators Package - Generatory danych dla MockDataFactory
"""

__version__ = "1.0.0"

from .phase1_core import (
    generate_cities,
    generate_ingredients,
    generate_tags,
    generate_ingredient_restrictions
)
from .phase2_restaurants import generate_restaurants
from .phase3_dishes import generate_dishes
from .phase4_users import generate_users
from .phase5_reviews import generate_reviews

__all__ = [
    'generate_cities',
    'generate_ingredients',
    'generate_tags',
    'generate_ingredient_restrictions',
    'generate_restaurants',
    'generate_dishes',
    'generate_users',
    'generate_reviews'
]
