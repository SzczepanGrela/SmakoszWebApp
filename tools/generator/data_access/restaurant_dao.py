"""Restaurant Data Access Object."""

from utils.db_connection import DatabaseConnection

class RestaurantDAO:
    """Handles SELECT queries for restaurants."""

    @staticmethod
    def get_all_restaurants_for_dishes(db: DatabaseConnection) -> list[tuple[int, str, float, str, str]]:
        """Fetch restaurant data needed for dish generation."""
        return db.fetch_all("""
            SELECT restaurant_id, secret_menu_blueprint, secret_price_multiplier, secret_archetype_modifiers, status, created_at
            FROM restaurants
        """)

    @staticmethod
    def get_all_restaurants_for_reviews(db: DatabaseConnection) -> list[tuple]:
        """Fetch comprehensive restaurant data needed for review generation."""
        return db.fetch_all("""
            SELECT restaurant_id, city_id, cuisine_type, created_at,
                   secret_price_multiplier, secret_overall_food_quality,
                   secret_service_quality, secret_cleanliness_score,
                   secret_ambiance_type, secret_ambiance_quality
            FROM restaurants
        """)

    @staticmethod
    def get_all_restaurant_ids(db: DatabaseConnection) -> list[tuple[int]]:
        """Fetch all restaurant IDs (minimal query for performance)."""
        return db.fetch_all("SELECT restaurant_id FROM restaurants")

    @staticmethod
    def get_restaurants_with_cuisine(db: DatabaseConnection) -> list[tuple[int, str]]:
        """Fetch restaurants with cuisine type information."""
        return db.fetch_all("SELECT restaurant_id, cuisine_type FROM restaurants")

    @staticmethod
    def get_restaurants_with_images(db: DatabaseConnection) -> list[tuple[int, str, str]]:
        """Fetch restaurants with cuisine and image information."""
        return db.fetch_all("SELECT restaurant_id, cuisine_type, image_url FROM restaurants")
