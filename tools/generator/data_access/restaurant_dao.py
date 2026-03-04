from utils.db_connection import DatabaseConnection

class RestaurantDAO:

    @staticmethod
    def get_all_restaurants_for_dishes(db: DatabaseConnection) -> list[tuple[int, str, float, str, str, str]]:
        return db.fetch_all("""
            SELECT restaurant_id, secret_menu_blueprint, secret_price_multiplier, secret_archetype_modifiers, status, created_at
            FROM restaurants
        """)

    @staticmethod
    def get_all_restaurants_for_reviews(db: DatabaseConnection) -> list[tuple]:
        return db.fetch_all("""
            SELECT restaurant_id, city_id, cuisine_type, created_at,
                   secret_price_multiplier, secret_overall_food_quality,
                   secret_service_quality, secret_cleanliness_score,
                   secret_ambiance_type, secret_ambiance_quality
            FROM restaurants
        """)

    @staticmethod
    def get_all_restaurant_ids(db: DatabaseConnection) -> list[tuple[int]]:
        return db.fetch_all("SELECT restaurant_id FROM restaurants")

    @staticmethod
    def get_restaurants_with_cuisine(db: DatabaseConnection) -> list[tuple[int, str]]:
        return db.fetch_all("SELECT restaurant_id, cuisine_type FROM restaurants")

    @staticmethod
    def get_restaurants_with_images(db: DatabaseConnection) -> list[tuple[int, str, str]]:
        return db.fetch_all("SELECT restaurant_id, cuisine_type, image_url FROM restaurants")
