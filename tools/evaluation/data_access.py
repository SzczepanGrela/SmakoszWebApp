import logging

from utils.db_connection import DatabaseConnection
from utils.helpers import safe_json_loads

logger = logging.getLogger(__name__)

class EvaluationDAO:
    @staticmethod
    def get_test_users(db: DatabaseConnection, min_reviews: int = 3) -> list[dict]:
        rows = db.fetch_all(
            """
            SELECT u.user_id,
                   u.username,
                   u.secret_characteristics_vector,
                   u.secret_rating_baseline,
                   u.secret_preferred_ambiance,
                   u.secret_cleanliness_preference,
                   u.secret_ingredient_preferences
            FROM users u
            WHERE u.secret_characteristics_vector IS NOT NULL
              AND u.secret_characteristics_vector != '{}'
              AND (
                  SELECT COUNT(*) FROM reviews r WHERE r.user_id = u.user_id
              ) >= %s
            """,
            (min_reviews,),
        )
        logger.info("Loaded %d test users (min_reviews=%d)", len(rows), min_reviews)

        users = []
        for r in rows:
            users.append(
                {
                    "user_id": r[0],
                    "username": r[1],
                    "secret_characteristics_vector": safe_json_loads(r[2]),
                    "secret_rating_baseline": float(r[3]) if r[3] is not None else 6.0,
                    "secret_preferred_ambiance": r[4] or "Casual",
                    "secret_cleanliness_preference": safe_json_loads(r[5]),
                    "secret_ingredient_preferences": safe_json_loads(r[6]),
                }
            )
        return users

    @staticmethod
    def get_all_dishes_enriched(db: DatabaseConnection) -> list[dict]:
        rows = db.fetch_all(
            """
            SELECT d.dish_id, d.dish_name, a.archetype_name AS secret_archetype,
                   d.price, d.secret_base_price, d.secret_quality,
                   d.secret_popularity_factor, d.secret_characteristics_vector,
                   d.secret_penalty_vector, v.variant_name AS secret_variant_name,
                   d.restaurant_id
            FROM dishes d
            LEFT JOIN dish_variants v ON d.secret_variant_id = v.variant_id
            LEFT JOIN dish_archetypes a ON v.archetype_id = a.archetype_id
            WHERE d.secret_characteristics_vector IS NOT NULL
              AND d.secret_characteristics_vector::text != '{}'
            """
        )

        dish_ids = [r[0] for r in rows]
        if not dish_ids:
            return []

        placeholders = ",".join(["%s"] * len(dish_ids))
        all_ingredients = db.fetch_all(
            f"""
            SELECT dil.dish_id, i.ingredient_name
            FROM dish_ingredients dil
            JOIN ingredients i ON dil.ingredient_id = i.ingredient_id
            WHERE dil.dish_id IN ({placeholders})
            """,
            tuple(dish_ids),
        )

        ingredients_by_dish: dict[int, list[str]] = {}
        if all_ingredients:
            for d_id, i_name in all_ingredients:
                if d_id not in ingredients_by_dish:
                    ingredients_by_dish[d_id] = []
                ingredients_by_dish[d_id].append(i_name)

        dishes = []
        for r in rows:
            d_id = r[0]
            dishes.append(
                {
                    "dish_id": d_id,
                    "dish_name": r[1],
                    "secret_archetype": r[2],
                    "price": r[3],
                    "secret_base_price": r[4],
                    "secret_quality": r[5],
                    "secret_popularity_factor": r[6],
                    "secret_characteristics_vector": safe_json_loads(r[7]),
                    "secret_penalty_vector": safe_json_loads(r[8]),
                    "secret_variant_name": r[9],
                    "restaurant_id": r[10],
                    "ingredients": ingredients_by_dish.get(d_id, []),
                }
            )

        logger.info("Loaded %d enriched dishes", len(dishes))
        return dishes

    @staticmethod
    def get_all_restaurants_enriched(db: DatabaseConnection) -> list[dict]:
        rows = db.fetch_all(
            """
            SELECT r.restaurant_id, r.restaurant_name, r.price_level,
                   r.secret_service_quality, r.secret_cleanliness_score,
                   r.secret_ambiance_quality, r.secret_ambiance_type
            FROM restaurants r
            WHERE r.secret_service_quality IS NOT NULL
            """
        )

        restaurants = []
        for r in rows:
            restaurants.append(
                {
                    "restaurant_id": r[0],
                    "restaurant_name": r[1],
                    "price_level": r[2],
                    "secret_service_quality": r[3],
                    "secret_cleanliness_score": r[4],
                    "secret_ambiance_quality": r[5],
                    "secret_ambiance_type": r[6],
                }
            )

        logger.info("Loaded %d restaurants", len(restaurants))
        return restaurants

    @staticmethod
    def get_user_reviewed_dishes(db: DatabaseConnection, user_id: int) -> set[int]:
        rows = db.fetch_all(
            "SELECT DISTINCT dish_id FROM reviews WHERE user_id = %s",
            (user_id,),
        )
        return {r[0] for r in rows}
