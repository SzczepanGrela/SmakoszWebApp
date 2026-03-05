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
        result = []
        for u in rows:
            join_date = u[12]
            if join_date and hasattr(join_date, "replace"):
                join_date = join_date.replace(tzinfo=None)

            pref_vector = safe_json_loads(u[13], {})

            result.append(
                UserForReview(
                    user_id=u[0],
                    city_id=u[1],
                    secret_total_review_count=u[2],
                    travel_propensity=u[3],
                    secret_enjoyed_archetypes=safe_json_loads(u[4], {}),
                    secret_ingredient_preferences=safe_json_loads(u[5], {}),
                    secret_cleanliness_preference=safe_json_loads(u[6], {}),
                    secret_preferred_ambiance=u[7],
                    secret_mood_propensity=u[8],
                    secret_cross_impact_factor=u[9],
                    secret_chance_dine_random=u[10] if u[10] is not None else 0.1,
                    secret_chance_pick_random_dish=u[11] if u[11] is not None else 0.05,
                    join_date=join_date,
                    secret_characteristics_vector=pref_vector,
                    secret_rating_baseline=u[14] if len(u) > 14 else 6.0,
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
