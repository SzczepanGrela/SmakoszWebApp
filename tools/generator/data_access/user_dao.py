from utils.db_connection import DatabaseConnection

class UserDAO:

    @staticmethod
    def get_all_users_basic(db: DatabaseConnection) -> list[tuple[int, int]]:
        return db.fetch_all("SELECT user_id, secret_total_review_count FROM users")

    @staticmethod
    def get_all_users_for_social(db: DatabaseConnection) -> list[tuple[int, str, int, bool]]:
        return db.fetch_all(
            "SELECT user_id, username, secret_home_city_id, secret_is_influencer FROM users WHERE role = 'user'"
        )

    @staticmethod
    def get_all_users_for_reviews(db: DatabaseConnection) -> list[tuple]:
        return db.fetch_all("""
            SELECT user_id, secret_home_city_id, secret_total_review_count, secret_travel_propensity,
                   secret_enjoyed_archetypes, secret_ingredient_preferences,
                   secret_cleanliness_preference, secret_preferred_ambiance,
                   secret_mood_propensity, secret_cross_impact_factor,
                   secret_chance_dine_random, secret_chance_pick_random_dish,
                   created_at, secret_characteristics_vector,
                   secret_rating_baseline
            FROM users
            WHERE role = 'user'
        """)

    @staticmethod
    def get_users_with_vectors(db: DatabaseConnection) -> list[tuple[int, str]]:
        return db.fetch_all("""
            SELECT user_id, secret_characteristics_vector
            FROM users
            ORDER BY user_id
        """)
