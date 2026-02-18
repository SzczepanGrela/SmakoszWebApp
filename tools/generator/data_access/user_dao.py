"""User Data Access Object."""

from utils.db_connection import DatabaseConnection

class UserDAO:
    """Handles SELECT queries for users."""

    @staticmethod
    def get_all_users_basic(db: DatabaseConnection) -> list[tuple[int, int]]:
        """Fetch basic user information for role assignment."""
        return db.fetch_all("SELECT user_id, secret_total_review_count FROM users")

    @staticmethod
    def get_all_users_for_social(db: DatabaseConnection) -> list[tuple[int, str, int, bool]]:
        """Fetch user data for social graph generation."""
        return db.fetch_all("SELECT user_id, username, home_city_id, secret_is_influencer FROM users WHERE role = 'user'")

    @staticmethod
    def get_all_users_for_reviews(db: DatabaseConnection) -> list[tuple]:
        """Fetch comprehensive user data for review generation."""
        return db.fetch_all("""
            SELECT user_id, home_city_id, secret_total_review_count, secret_travel_propensity,
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
        """Fetch users with characteristic vectors for preference materialization."""
        return db.fetch_all("""
            SELECT user_id, secret_characteristics_vector
            FROM users
            ORDER BY user_id
        """)
