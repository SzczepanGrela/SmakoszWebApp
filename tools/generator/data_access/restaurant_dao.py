from models.domain import RestaurantForDishes, RestaurantForReview
from utils.db_connection import DatabaseConnection
from utils.helpers import safe_json_loads

class RestaurantDAO:

    @staticmethod
    def get_all_restaurants_for_dishes(db: DatabaseConnection) -> list[RestaurantForDishes]:
        rows = db.fetch_all("""
            SELECT r.restaurant_id, COALESCE(ct.name, ''), r.secret_price_multiplier,
                   r.secret_archetype_modifiers, r.status, r.created_at
            FROM restaurants r
            LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
        """)
        return [
            RestaurantForDishes(
                restaurant_id=row[0],
                cuisine_type=row[1],
                secret_price_multiplier=row[2],
                secret_archetype_modifiers=safe_json_loads(row[3], {}),
                status=row[4],
                created_at=row[5],
            )
            for row in rows
        ]

    @staticmethod
    def get_all_restaurants_for_reviews(db: DatabaseConnection) -> list[RestaurantForReview]:
        rows = db.fetch_all("""
            SELECT r.restaurant_id, r.city_id, COALESCE(ct.name, ''), r.created_at,
                   r.secret_price_multiplier, r.secret_overall_food_quality,
                   r.secret_service_quality, r.secret_cleanliness_score,
                   r.secret_ambiance_type, r.secret_ambiance_quality
            FROM restaurants r
            LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
        """)
        result = []
        for row in rows:
            created_at = row[3]
            if created_at and hasattr(created_at, "replace"):
                created_at = created_at.replace(tzinfo=None)

            result.append(
                RestaurantForReview(
                    restaurant_id=row[0],
                    city_id=row[1],
                    cuisine_type=row[2],
                    created_at=created_at,
                    secret_price_multiplier=row[4],
                    secret_overall_food_quality=row[5],
                    secret_service_quality=row[6],
                    secret_cleanliness_score=row[7],
                    secret_ambiance_type=row[8],
                    secret_ambiance_quality=row[9],
                )
            )
        return result

    @staticmethod
    def get_all_restaurant_ids(db: DatabaseConnection) -> list[tuple[int]]:
        return db.fetch_all("SELECT restaurant_id FROM restaurants")

    @staticmethod
    def get_restaurants_with_cuisine(db: DatabaseConnection) -> list[tuple[int, str]]:
        return db.fetch_all("""
            SELECT r.restaurant_id, COALESCE(ct.name, '')
            FROM restaurants r
            LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
        """)

    @staticmethod
    def get_restaurants_with_images(db: DatabaseConnection) -> list[tuple[int, str, str]]:
        return db.fetch_all("""
            SELECT r.restaurant_id, COALESCE(ct.name, ''), r.image_url
            FROM restaurants r
            LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
        """)
