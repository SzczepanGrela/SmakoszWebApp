from models.domain import UserForReview, UserForSocial
from utils.db_connection import DatabaseConnection
from utils.helpers import safe_json_loads

class UserDAO:

    @staticmethod
    def get_all_users_basic(db: DatabaseConnection) -> list[tuple[int, int]]:
        return db.fetch_all("SELECT user_id, secret_total_review_count FROM users")

    @staticmethod
    def get_all_users_for_social(db: DatabaseConnection) -> list[UserForSocial]:
        rows = db.fetch_all(
            "SELECT user_id, username, secret_home_city_id, secret_is_influencer FROM users WHERE role = 'user'"
        )
        return [
            UserForSocial(
                user_id=int(row[0]),
                username=row[1],
                secret_home_city_id=row[2],
                secret_is_influencer=row[3] is True,
            )
            for row in rows
        ]

    @staticmethod
    def get_all_users_for_reviews(db: DatabaseConnection) -> list[UserForReview]:
        rows = db.fetch_all("""
            SELECT user_id, username, secret_home_city_id, secret_total_review_count, secret_travel_propensity,
                   secret_enjoyed_archetypes, secret_ingredient_preferences,
                   secret_cleanliness_preference, secret_preferred_ambiance,
                   secret_mood_propensity, secret_cross_impact_factor,
                   secret_chance_dine_random, secret_chance_pick_random_dish,
                   created_at, secret_characteristics_vector,
                   secret_rating_baseline
            FROM users
            WHERE role = 'user'
        """)
        result = []
        for u in rows:
            join_date = u[13]
            if join_date and hasattr(join_date, "replace"):
                join_date = join_date.replace(tzinfo=None)

            pref_vector = safe_json_loads(u[14], {})

            result.append(
                UserForReview(
                    user_id=u[0],
                    username=u[1],
                    city_id=u[2],
                    secret_total_review_count=u[3],
                    travel_propensity=u[4],
                    secret_enjoyed_archetypes=safe_json_loads(u[5], {}),
                    secret_ingredient_preferences=safe_json_loads(u[6], {}),
                    secret_cleanliness_preference=safe_json_loads(u[7], {}),
                    secret_preferred_ambiance=u[8],
                    secret_mood_propensity=u[9],
                    secret_cross_impact_factor=u[10],
                    secret_chance_dine_random=u[11] if u[11] is not None else 0.1,
                    secret_chance_pick_random_dish=u[12] if u[12] is not None else 0.05,
                    join_date=join_date,
                    secret_characteristics_vector=pref_vector,
                    secret_rating_baseline=u[15] if len(u) > 15 else 6.0,
                    secret_spice_preference=pref_vector.get("flavor_spiciness", 0.5),
                    secret_richness_preference=pref_vector.get("physics_richness", 0.5),
                    secret_texture_preference=pref_vector.get("texture_crispy", 0.5),
                )
            )
        return result

    @staticmethod
    def get_users_with_vectors(db: DatabaseConnection) -> list[tuple[int, str]]:
        return db.fetch_all("""
            SELECT user_id, secret_characteristics_vector
            FROM users
            ORDER BY user_id
        """)
