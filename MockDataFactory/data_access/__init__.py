"""
Data Access Layer (DAO Pattern)

This package contains Data Access Objects (DAOs) that encapsulate all database
queries and provide a clean separation between business logic and data persistence.

Design Principles:
- Each DAO handles queries for a specific domain entity (User, Restaurant, Review)
- DAOs accept DatabaseConnection objects and return Python data structures
- SELECT queries are separated from INSERT/UPDATE operations
- Read operations are centralized for consistency and maintainability
"""

from .city_dao import CityDAO
from .restaurant_dao import RestaurantDAO
from .review_dao import ReviewDAO
from .user_dao import UserDAO

__all__ = [
    "UserDAO",
    "RestaurantDAO",
    "ReviewDAO",
    "CityDAO",
]
