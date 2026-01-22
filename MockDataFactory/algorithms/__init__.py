"""
Algorithms Package - Algorytmy dla MockDataFactory
"""

__version__ = "1.0.0"

from .rating_engine import calculate_review_ratings
from .restaurant_selector import select_restaurants_for_user
from .dish_selector import select_dish_from_menu

__all__ = [
    'calculate_review_ratings',
    'select_restaurants_for_user',
    'select_dish_from_menu'
]
