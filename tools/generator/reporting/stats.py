import json
import logging
import math
from datetime import datetime
from pathlib import Path

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class DatasetStatistics:

    def __init__(self, db: DatabaseConnection):
        self.db = db
        self.stats: dict = {}

    def collect_all(self) -> dict:
        logger.info("Collecting dataset statistics...")

        self.stats = {
            "generated_at": datetime.now().isoformat(),
            "row_counts": self._row_counts(),
            "ratings": self._rating_stats(),
            "ncf_matrix": self._ncf_matrix_stats(),
            "cold_start": self._cold_start_analysis(),
            "user_activity": self._user_activity_stats(),
            "dish_popularity": self._dish_popularity_stats(),
            "restaurant_distribution": self._restaurant_distribution(),
            "social_graph": self._social_graph_stats(),
            "moderation": self._moderation_stats(),
            "ncf_dataset": self._ncf_dataset_profile(),
            "generator_validation": self._generator_validation(),
            "photo_pipeline": self._photo_pipeline_stats(),
            "temporal": self._temporal_stats(),
            "integrity": self._integrity_checks(),
        }

        logger.info("Statistics collection complete.")
        return self.stats

    def _row_counts(self) -> dict[str, int]:
        tables = [
            "users",
            "restaurants",
            "dishes",
            "reviews",
            "cities",
            "cuisine_types",
            "tags",
            "ingredients",
            "menu_sections",
            "dish_archetypes",
            "dish_variants",
            "dish_ingredients",
            "dish_tags",
            "dish_section_assignments",
            "restaurant_tags",
            "restaurant_opening_hours",
            "user_follows",
            "review_likes",
            "notifications",
            "favorite_restaurants",
            "saved_dishes",
            "search_histories",
            "media_assets",
            "data_correction_requests",
            "reports",
            "report_reason_definitions",
            "report_reason_assignments",
            "restaurant_edit_requests",
            "ingredient_suggestions",
            "user_sessions",
            "system.moderation_results",
            "system.banned_identifiers",
            "system.tickets",
        ]
        counts = {}
        for table in tables:
            try:
                counts[table] = self.db.fetch_val(f"SELECT COUNT(*) FROM {table}") or 0
            except Exception:
                counts[table] = -1
        return counts

    def _rating_stats(self) -> dict:
        result = {}
        for col in ("dish_rating", "service_rating", "cleanliness_rating", "ambiance_rating"):
            row = self.db.fetch_one(f"""
                SELECT
                    AVG({col})::float,
                    STDDEV({col})::float,
                    MIN({col}),
                    MAX({col}),
                    PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY {col})::float,
                    PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY {col})::float,
                    PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY {col})::float
                FROM reviews
            """)
            if row:
                result[col] = {
                    "mean": _r(row[0]),
                    "std": _r(row[1]),
                    "min": row[2],
                    "max": row[3],
                    "p25": _r(row[4]),
                    "median": _r(row[5]),
                    "p75": _r(row[6]),
                }

            dist_rows = self.db.fetch_all(f"""
                SELECT {col}, COUNT(*) FROM reviews
                GROUP BY {col} ORDER BY {col}
            """)
            if dist_rows and col in result:
                result[col]["distribution"] = {str(r): c for r, c in dist_rows}

        return result

    def _ncf_matrix_stats(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                COUNT(DISTINCT user_id),
                COUNT(DISTINCT dish_id),
                COUNT(*)
            FROM reviews
        """)
        if not row:
            return {}

        users, items, interactions = row
        matrix_size = users * items
        sparsity = 1.0 - (interactions / matrix_size) if matrix_size > 0 else 0
        return {
            "users": users,
            "items": items,
            "interactions": interactions,
            "matrix_size": matrix_size,
            "sparsity": _r(sparsity, 6),
            "density": _r(1.0 - sparsity, 6),
            "avg_per_user": _r(interactions / users) if users else 0,
            "avg_per_item": _r(interactions / items) if items else 0,
        }

    def _cold_start_analysis(self) -> dict:
        user_row = self.db.fetch_one("""
            SELECT
                COUNT(*) FILTER (WHERE cnt = 1),
                COUNT(*) FILTER (WHERE cnt BETWEEN 2 AND 5),
                COUNT(*) FILTER (WHERE cnt BETWEEN 6 AND 10),
                COUNT(*) FILTER (WHERE cnt > 10)
            FROM (SELECT user_id, COUNT(*) cnt FROM reviews GROUP BY user_id) t
        """)
        item_row = self.db.fetch_one("""
            SELECT
                COUNT(*) FILTER (WHERE cnt < 3),
                COUNT(*) FILTER (WHERE cnt BETWEEN 3 AND 10),
                COUNT(*) FILTER (WHERE cnt > 10)
            FROM (SELECT dish_id, COUNT(*) cnt FROM reviews GROUP BY dish_id) t
        """)
        return {
            "users": {
                "1_review": user_row[0] if user_row else 0,
                "2_to_5": user_row[1] if user_row else 0,
                "6_to_10": user_row[2] if user_row else 0,
                "over_10": user_row[3] if user_row else 0,
            },
            "items": {
                "under_3": item_row[0] if item_row else 0,
                "3_to_10": item_row[1] if item_row else 0,
                "over_10": item_row[2] if item_row else 0,
            },
        }

    def _user_activity_stats(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(cnt)::float, STDDEV(cnt)::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY cnt)::float,
                MIN(cnt), MAX(cnt)
            FROM (SELECT user_id, COUNT(*) cnt FROM reviews GROUP BY user_id) t
        """)
        if not row:
            return {}
        return {
            "mean": _r(row[0]),
            "std": _r(row[1]),
            "median": _r(row[2]),
            "min": row[3],
            "max": row[4],
        }

    def _dish_popularity_stats(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(cnt)::float, STDDEV(cnt)::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY cnt)::float,
                MIN(cnt), MAX(cnt)
            FROM (SELECT dish_id, COUNT(*) cnt FROM reviews GROUP BY dish_id) t
        """)
        if not row:
            return {}
        return {
            "mean": _r(row[0]),
            "std": _r(row[1]),
            "median": _r(row[2]),
            "min": row[3],
            "max": row[4],
        }

    def _restaurant_distribution(self) -> dict:
        by_city = self.db.fetch_all("""
            SELECT c.city_name, COUNT(r.restaurant_id)
            FROM restaurants r JOIN cities c ON r.city_id = c.city_id
            GROUP BY c.city_name ORDER BY COUNT(r.restaurant_id) DESC
        """)
        by_price = self.db.fetch_all("""
            SELECT price_level, COUNT(*) FROM restaurants
            GROUP BY price_level ORDER BY price_level
        """)
        avg_row = self.db.fetch_one("""
            SELECT AVG(avg_r)::float, STDDEV(avg_r)::float
            FROM (
                SELECT restaurant_id, AVG(dish_rating)::float avg_r
                FROM reviews GROUP BY restaurant_id
            ) t
        """)
        return {
            "by_city": dict(by_city),
            "by_price_level": {str(pl): cnt for pl, cnt in by_price},
            "avg_restaurant_rating": {
                "mean": _r(avg_row[0]) if avg_row else None,
                "std": _r(avg_row[1]) if avg_row else None,
            },
        }

    def _social_graph_stats(self) -> dict:
        result = {}

        follows_row = self.db.fetch_one("""
            SELECT COUNT(*), AVG(cnt)::float, STDDEV(cnt)::float, MAX(cnt)
            FROM (SELECT follower_id, COUNT(*) cnt FROM user_follows GROUP BY follower_id) t
        """)
        if follows_row:
            total = self.db.fetch_val("SELECT COUNT(*) FROM user_follows") or 0
            result["follows"] = {
                "total": total,
                "avg_per_user": _r(follows_row[1]),
                "std": _r(follows_row[2]),
                "max": follows_row[3],
            }

        likes_row = self.db.fetch_one("""
            SELECT AVG(cnt)::float, STDDEV(cnt)::float, MAX(cnt)
            FROM (SELECT review_id, COUNT(*) cnt FROM review_likes GROUP BY review_id) t
        """)
        if likes_row:
            total = self.db.fetch_val("SELECT COUNT(*) FROM review_likes") or 0
            result["review_likes"] = {
                "total": total,
                "avg_per_review": _r(likes_row[0]),
                "std": _r(likes_row[1]),
                "max": likes_row[2],
            }

        fav_row = self.db.fetch_one("""
            SELECT AVG(cnt)::float, STDDEV(cnt)::float
            FROM (SELECT user_id, COUNT(*) cnt FROM favorite_restaurants GROUP BY user_id) t
        """)
        if fav_row:
            total = self.db.fetch_val("SELECT COUNT(*) FROM favorite_restaurants") or 0
            result["favorites"] = {
                "total": total,
                "avg_per_user": _r(fav_row[0]),
                "std": _r(fav_row[1]),
            }

        saved_row = self.db.fetch_one("""
            SELECT AVG(cnt)::float, STDDEV(cnt)::float
            FROM (SELECT user_id, COUNT(*) cnt FROM saved_dishes GROUP BY user_id) t
        """)
        if saved_row:
            total = self.db.fetch_val("SELECT COUNT(*) FROM saved_dishes") or 0
            result["saved_dishes"] = {
                "total": total,
                "avg_per_user": _r(saved_row[0]),
                "std": _r(saved_row[1]),
            }

        return result

    def _moderation_stats(self) -> dict:
        by_status = self.db.fetch_all("""
            SELECT content_status, COUNT(*) FROM reviews
            GROUP BY content_status ORDER BY COUNT(*) DESC
        """)
        by_verdict = self.db.fetch_all("""
            SELECT status, COUNT(*) FROM system.moderation_results
            GROUP BY status ORDER BY COUNT(*) DESC
        """)
        return {
            "content_status": dict(by_status),
            "moderation_verdict": dict(by_verdict),
        }

    def _ncf_dataset_profile(self) -> dict:
        return {
            "distribution": self._ncf_distribution_metrics(),
            "filtered": self._ncf_filtered_stats(),
        }

    def _ncf_distribution_metrics(self) -> dict:
        restaurant_counts = self.db.fetch_all("""
            SELECT COUNT(*) as cnt FROM reviews GROUP BY restaurant_id ORDER BY cnt DESC
        """)
        dish_counts = self.db.fetch_all("""
            SELECT COUNT(*) as cnt FROM reviews GROUP BY dish_id ORDER BY cnt DESC
        """)

        rest_vals = [r[0] for r in restaurant_counts] if restaurant_counts else []
        dish_vals = [r[0] for r in dish_counts] if dish_counts else []

        rest_alpha, rest_r2 = _power_law_fit(rest_vals) if rest_vals else (0.0, 0.0)
        dish_alpha, dish_r2 = _power_law_fit(dish_vals) if dish_vals else (0.0, 0.0)

        total_reviews = sum(dish_vals)
        threshold = total_reviews * 0.8
        cumulative = 0
        dishes_for_80 = 0
        for cnt in dish_vals:
            cumulative += cnt
            dishes_for_80 += 1
            if cumulative >= threshold:
                break
        coverage_80 = _r(dishes_for_80 / len(dish_vals) * 100) if dish_vals else 0

        return {
            "zipf_restaurant": {"alpha": _r(rest_alpha, 4), "r_squared": _r(rest_r2, 4)},
            "zipf_dish": {"alpha": _r(dish_alpha, 4), "r_squared": _r(dish_r2, 4)},
            "configured_alpha": 1.5,
            "catalog_coverage_80pct": coverage_80,
        }

    def _ncf_filtered_stats(self) -> dict:
        row = self.db.fetch_one("""
            SELECT COUNT(*), COUNT(DISTINCT r.user_id), COUNT(DISTINCT r.dish_id)
            FROM reviews r
            JOIN users u ON r.user_id = u.user_id
            WHERE r.is_visible AND NOT r.is_deleted
                AND r.content_status != 'rejected'
                AND NOT u.is_deleted
        """)
        total = self.db.fetch_val("SELECT COUNT(*) FROM reviews") or 0
        breakdown = self.db.fetch_all("""
            SELECT
                COUNT(*) FILTER (WHERE NOT r.is_visible) as not_visible,
                COUNT(*) FILTER (WHERE r.is_deleted) as deleted,
                COUNT(*) FILTER (WHERE r.content_status = 'rejected') as rejected,
                COUNT(*) FILTER (WHERE u.is_deleted) as user_deleted
            FROM reviews r
            JOIN users u ON r.user_id = u.user_id
        """)
        result = {
            "eligible_reviews": row[0] if row else 0,
            "eligible_users": row[1] if row else 0,
            "eligible_dishes": row[2] if row else 0,
            "total_reviews": total,
            "filter_rate": _r((1 - row[0] / total) * 100) if row and total else 0,
        }
        if breakdown and breakdown[0]:
            b = breakdown[0]
            result["filtered_breakdown"] = {
                "not_visible": b[0],
                "deleted": b[1],
                "rejected": b[2],
                "user_deleted": b[3],
            }
        return result

    def _generator_validation(self) -> dict:
        return {
            "geographic": self._geographic_consistency(),
            "velocity": self._review_velocity(),
            "lifetime": self._user_lifetime_stats(),
            "visit_frequency": self._restaurant_visit_frequency(),
            "cuisine_diversity": self._cuisine_diversity(),
            "baseline_correlation": self._rating_baseline_correlation(),
            "price_vs_rating": self._price_level_vs_rating(),
            "day_of_week": self._day_of_week_distribution(),
            "text_length": self._review_text_length(),
        }

    def _geographic_consistency(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                COUNT(*) as total,
                COUNT(*) FILTER (WHERE u.secret_home_city_id = rest.city_id) as home_city
            FROM reviews r
            JOIN users u ON r.user_id = u.user_id
            JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
        """)
        if not row or not row[0]:
            return {}
        return {
            "total_reviews": row[0],
            "home_city_reviews": row[1],
            "home_city_pct": _r(row[1] / row[0] * 100),
        }

    def _review_velocity(self) -> dict:
        row = self.db.fetch_one("""
            SELECT AVG(monthly_rate)::float, STDDEV(monthly_rate)::float,
                   PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY monthly_rate)::float
            FROM (
                SELECT user_id,
                    COUNT(*)::float / GREATEST(
                        (MAX(visit_date) - MIN(visit_date))::float / 30.0,
                        1
                    ) as monthly_rate
                FROM reviews
                GROUP BY user_id HAVING COUNT(*) > 1
            ) t
        """)
        if not row:
            return {}
        return {
            "avg_reviews_per_month": _r(row[0]),
            "std": _r(row[1]),
            "median": _r(row[2]),
        }

    def _user_lifetime_stats(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(lifetime_days)::float,
                STDDEV(lifetime_days)::float,
                PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY lifetime_days)::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY lifetime_days)::float,
                PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY lifetime_days)::float,
                MIN(lifetime_days),
                MAX(lifetime_days)
            FROM (
                SELECT user_id,
                    (MAX(visit_date) - MIN(visit_date))::float as lifetime_days
                FROM reviews
                GROUP BY user_id HAVING COUNT(*) > 1
            ) t
        """)
        if not row:
            return {}
        return {
            "mean_days": _r(row[0]),
            "std_days": _r(row[1]),
            "p25_days": _r(row[2]),
            "median_days": _r(row[3]),
            "p75_days": _r(row[4]),
            "min_days": _r(row[5]),
            "max_days": _r(row[6]),
        }

    def _restaurant_visit_frequency(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(visit_count)::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY visit_count)::float,
                MAX(visit_count)
            FROM (
                SELECT user_id, restaurant_id, COUNT(*) as visit_count
                FROM reviews
                GROUP BY user_id, restaurant_id
            ) t
        """)
        if not row:
            return {}
        return {
            "avg_visits_per_restaurant": _r(row[0]),
            "median": _r(row[1]),
            "max": row[2],
        }

    def _cuisine_diversity(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(cuisine_count)::float,
                STDDEV(cuisine_count)::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY cuisine_count)::float
            FROM (
                SELECT r.user_id, COUNT(DISTINCT rest.cuisine_type) as cuisine_count
                FROM reviews r
                JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
                GROUP BY r.user_id
            ) t
        """)
        if not row:
            return {}
        return {
            "avg_cuisines_per_user": _r(row[0]),
            "std": _r(row[1]),
            "median": _r(row[2]),
        }

    def _rating_baseline_correlation(self) -> dict:
        row = self.db.fetch_one("""
            SELECT CORR(secret_rating_baseline, avg_rating)::float
            FROM (
                SELECT u.user_id, u.secret_rating_baseline,
                    AVG(r.dish_rating)::float as avg_rating
                FROM users u
                JOIN reviews r ON u.user_id = r.user_id
                GROUP BY u.user_id, u.secret_rating_baseline
            ) t
        """)
        return {
            "baseline_vs_actual_correlation":
                _r(row[0], 4) if row and row[0] is not None else None,
        }

    def _price_level_vs_rating(self) -> dict:
        rows = self.db.fetch_all("""
            SELECT rest.price_level, AVG(r.dish_rating)::float, COUNT(*)
            FROM reviews r
            JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
            GROUP BY rest.price_level
            ORDER BY rest.price_level
        """)
        if not rows:
            return {}
        return {
            str(pl): {"avg_rating": _r(avg), "review_count": cnt}
            for pl, avg, cnt in rows
        }

    def _day_of_week_distribution(self) -> dict:
        rows = self.db.fetch_all("""
            SELECT EXTRACT(DOW FROM visit_date)::int as dow, COUNT(*)
            FROM reviews
            GROUP BY EXTRACT(DOW FROM visit_date)
            ORDER BY EXTRACT(DOW FROM visit_date)
        """)
        day_names = [
            "Sunday", "Monday", "Tuesday", "Wednesday",
            "Thursday", "Friday", "Saturday",
        ]
        if not rows:
            return {}
        return {day_names[dow]: cnt for dow, cnt in rows}

    def _review_text_length(self) -> dict:
        row = self.db.fetch_one("""
            SELECT
                AVG(LENGTH(content))::float,
                STDDEV(LENGTH(content))::float,
                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY LENGTH(content))::float,
                COUNT(*) FILTER (WHERE content IS NOT NULL AND LENGTH(content) > 0),
                COUNT(*) FILTER (WHERE content IS NULL OR LENGTH(content) = 0),
                COUNT(*)
            FROM reviews
        """)
        if not row:
            return {}
        return {
            "avg_length": _r(row[0]),
            "std_length": _r(row[1]),
            "median_length": _r(row[2]),
            "with_text": row[3],
            "without_text": row[4],
            "pct_with_text": _r(row[3] / row[5] * 100) if row[5] else 0,
        }

    def _photo_pipeline_stats(self) -> dict:
        total_ing = self.db.fetch_val("SELECT COUNT(*) FROM ingredients") or 0
        ing_with_icon = self.db.fetch_val(
            "SELECT COUNT(*) FROM ingredients WHERE icon_url IS NOT NULL"
        ) or 0
        ing_with_blurhash = self.db.fetch_val(
            "SELECT COUNT(*) FROM ingredients WHERE icon_blurhash IS NOT NULL"
        ) or 0
        ing_placeholder = self.db.fetch_val(
            "SELECT COUNT(*) FROM ingredients WHERE icon_url LIKE '%%ui-avatars%%'"
        ) or 0

        total_dishes = self.db.fetch_val("SELECT COUNT(*) FROM dishes") or 0
        dish_with_img = self.db.fetch_val(
            "SELECT COUNT(*) FROM dishes WHERE image_url IS NOT NULL"
        ) or 0
        dish_with_blurhash = self.db.fetch_val(
            "SELECT COUNT(*) FROM dishes WHERE image_blurhash IS NOT NULL"
        ) or 0

        total_rest = self.db.fetch_val("SELECT COUNT(*) FROM restaurants") or 0
        rest_with_img = self.db.fetch_val(
            "SELECT COUNT(*) FROM restaurants WHERE image_url IS NOT NULL"
        ) or 0
        rest_with_blurhash = self.db.fetch_val(
            "SELECT COUNT(*) FROM restaurants WHERE image_blurhash IS NOT NULL"
        ) or 0

        total_users = self.db.fetch_val("SELECT COUNT(*) FROM users") or 0
        user_with_avatar = self.db.fetch_val(
            "SELECT COUNT(*) FROM users WHERE avatar_url IS NOT NULL"
        ) or 0
        user_with_blurhash = self.db.fetch_val(
            "SELECT COUNT(*) FROM users WHERE avatar_blurhash IS NOT NULL"
        ) or 0

        return {
            "ingredients": {
                "total": total_ing,
                "with_icon": ing_with_icon,
                "with_blurhash": ing_with_blurhash,
                "placeholder_count": ing_placeholder,
                "coverage_pct": _r(ing_with_icon / total_ing * 100) if total_ing else 0,
            },
            "dishes": {
                "total": total_dishes,
                "with_image": dish_with_img,
                "with_blurhash": dish_with_blurhash,
                "coverage_pct": _r(dish_with_img / total_dishes * 100) if total_dishes else 0,
            },
            "restaurants": {
                "total": total_rest,
                "with_image": rest_with_img,
                "with_blurhash": rest_with_blurhash,
                "coverage_pct": _r(rest_with_img / total_rest * 100) if total_rest else 0,
            },
            "users": {
                "total": total_users,
                "with_avatar": user_with_avatar,
                "with_blurhash": user_with_blurhash,
                "coverage_pct": _r(user_with_avatar / total_users * 100) if total_users else 0,
            },
        }

    def _temporal_stats(self) -> dict:
        rows = self.db.fetch_all("""
            SELECT TO_CHAR(DATE_TRUNC('month', visit_date), 'YYYY-MM') as month,
                   COUNT(*)
            FROM reviews
            GROUP BY DATE_TRUNC('month', visit_date)
            ORDER BY DATE_TRUNC('month', visit_date)
        """)
        return {"reviews_per_month": dict(rows)}

    def _integrity_checks(self) -> list[dict]:
        num_users = self.db.fetch_val("SELECT COUNT(*) FROM users") or 0
        num_restaurants = self.db.fetch_val("SELECT COUNT(*) FROM restaurants") or 0
        num_dishes = self.db.fetch_val("SELECT COUNT(*) FROM dishes") or 0
        num_reviews = self.db.fetch_val("SELECT COUNT(*) FROM reviews") or 0

        checks: list[tuple[str, str, str, str]] = [
            (
                "Restaurants without dishes",
                """SELECT COUNT(*) FROM restaurants r
                   LEFT JOIN dishes d ON r.restaurant_id = d.restaurant_id
                   WHERE d.dish_id IS NULL AND r.status = 'active'""",
                "0",
                "Every active restaurant should have at least one dish",
            ),
            (
                "Dishes without restaurant",
                """SELECT COUNT(*) FROM dishes d
                   LEFT JOIN restaurants r ON d.restaurant_id = r.restaurant_id
                   WHERE r.restaurant_id IS NULL""",
                "0",
                "Every dish must belong to an existing restaurant",
            ),
            (
                "Cities without restaurants",
                """SELECT COUNT(*) FROM cities c
                   LEFT JOIN restaurants r ON c.city_id = r.city_id
                   WHERE r.restaurant_id IS NULL""",
                "0",
                "Every city should have at least one restaurant",
            ),
            (
                "Reviews referencing missing dishes",
                """SELECT COUNT(*) FROM reviews r
                   LEFT JOIN dishes d ON r.dish_id = d.dish_id
                   WHERE d.dish_id IS NULL""",
                "0",
                "Every review must reference an existing dish",
            ),
            (
                "Reviews referencing missing users",
                """SELECT COUNT(*) FROM reviews r
                   LEFT JOIN users u ON r.user_id = u.user_id
                   WHERE u.user_id IS NULL""",
                "0",
                "Every review must reference an existing user",
            ),
            (
                "Restaurants with NULL postal_code",
                "SELECT COUNT(*) FROM restaurants WHERE postal_code IS NULL",
                "0",
                "All restaurants should have a postal code",
            ),
            (
                "Restaurants with NULL email",
                "SELECT COUNT(*) FROM restaurants WHERE email IS NULL",
                "0",
                "All restaurants should have a contact email",
            ),
            (
                "Cuisine types with NULL icon",
                "SELECT COUNT(*) FROM cuisine_types WHERE icon IS NULL",
                "0",
                "All cuisine types should have an emoji icon",
            ),
            (
                "Dishes with NULL image_url",
                "SELECT COUNT(*) FROM dishes WHERE image_url IS NULL",
                "0",
                "Every dish should have a photo URL",
            ),
            (
                "Users with wrong review_count",
                """SELECT COUNT(*) FROM (
                       SELECT u.user_id, u.review_count, COUNT(r.review_id) real_cnt
                       FROM users u
                       LEFT JOIN reviews r ON u.user_id = r.user_id
                       GROUP BY u.user_id, u.review_count
                       HAVING u.review_count != COUNT(r.review_id)
                   ) t""",
                "0",
                "users.review_count must match actual COUNT(reviews)",
            ),
            (
                "Dishes with wrong review_count",
                """SELECT COUNT(*) FROM (
                       SELECT d.dish_id, d.review_count, COUNT(r.review_id) real_cnt
                       FROM dishes d
                       LEFT JOIN reviews r ON d.dish_id = r.dish_id
                       GROUP BY d.dish_id, d.review_count
                       HAVING d.review_count != COUNT(r.review_id)
                   ) t""",
                "0",
                "dishes.review_count must match actual COUNT(reviews)",
            ),
            (
                "Users without any reviews",
                """SELECT COUNT(*) FROM users u
                   LEFT JOIN reviews r ON u.user_id = r.user_id
                   WHERE r.review_id IS NULL AND u.role = 'user'""",
                f"< {int(num_users * 0.01)}",
                f"At most ~1% of users ({int(num_users * 0.01):,}) should have zero reviews",
            ),
            (
                "Dishes never reviewed",
                """SELECT COUNT(*) FROM dishes d
                   LEFT JOIN reviews r ON d.dish_id = r.dish_id
                   WHERE r.review_id IS NULL""",
                "0",
                "Every dish should have at least one review",
            ),
            (
                "Duplicate dish slugs",
                """SELECT COUNT(*) FROM (
                       SELECT slug FROM dishes GROUP BY slug HAVING COUNT(*) > 1
                   ) t""",
                "0",
                "Dish slugs must be globally unique",
            ),
            (
                "Review likes count",
                "SELECT COUNT(*) FROM review_likes",
                f"> {int(num_reviews * 0.5)}",
                f"Should be ~5x reviews ({num_reviews * 5:,}), at least 50% of review count",
            ),
            (
                "Restaurants with NULL trending_score",
                """SELECT COUNT(*) FROM restaurants WHERE trending_score IS NULL""",
                f"< {int(num_restaurants * 0.05)}",
                "At most ~5% of restaurants should lack a trending score",
            ),
            (
                "Dishes with trending_score (coverage)",
                """SELECT COUNT(*) FROM dishes WHERE trending_score IS NOT NULL""",
                f"> {int(num_dishes * 0.5)}",
                f"At least 50% of dishes ({int(num_dishes * 0.5):,}) should have a trending score",
            ),
            (
                "Self-follows",
                """SELECT COUNT(*) FROM user_follows
                   WHERE follower_id = followed_id""",
                "0",
                "No user should follow themselves",
            ),
            (
                "Self-review-likes",
                """SELECT COUNT(*) FROM review_likes rl
                   JOIN reviews r ON rl.review_id = r.review_id
                   WHERE rl.user_id = r.user_id""",
                "0",
                "No user should like their own review",
            ),
            (
                "Visible reviews from deleted users (query-time filtered)",
                """SELECT COUNT(*) FROM reviews r
                   JOIN users u ON r.user_id = u.user_id
                   WHERE r.is_visible AND (u.is_deleted OR u.is_banned)""",
                f"< {int(num_reviews * 0.10)}",
                "App filters at query time; expect some visible reviews from deleted users",
            ),
            (
                "Reviews with future visit dates",
                """SELECT COUNT(*) FROM reviews
                   WHERE visit_date > CURRENT_DATE""",
                "< 10",
                "Reviews should not have visit dates in the future",
            ),
            (
                "Ratings out of range (1-10)",
                """SELECT COUNT(*) FROM reviews
                   WHERE dish_rating < 1 OR dish_rating > 10
                      OR service_rating < 1 OR service_rating > 10
                      OR cleanliness_rating < 1 OR cleanliness_rating > 10
                      OR ambiance_rating < 1 OR ambiance_rating > 10""",
                "0",
                "All ratings must be between 1 and 10",
            ),
            (
                "Restaurant role without restaurant_id",
                """SELECT COUNT(*) FROM users
                   WHERE role = 'restaurant' AND restaurant_id IS NULL""",
                "0",
                "Users with restaurant role must have a restaurant_id",
            ),
            (
                "Non-restaurant role with restaurant_id",
                """SELECT COUNT(*) FROM users
                   WHERE role != 'restaurant' AND restaurant_id IS NOT NULL""",
                "0",
                "Users without restaurant role should not have a restaurant_id",
            ),
            (
                "Rejected but visible reviews",
                """SELECT COUNT(*) FROM reviews
                   WHERE content_status = 'rejected' AND is_visible""",
                "0",
                "Rejected reviews should not be visible",
            ),
            (
                "Duplicate user-dish interactions",
                """SELECT COUNT(*) FROM (
                       SELECT user_id, dish_id FROM reviews
                       GROUP BY user_id, dish_id HAVING COUNT(*) > 1
                   ) t""",
                "0",
                "Each user should review each dish at most once",
            ),
            (
                "Ingredients with placeholder icon (ui-avatars)",
                """SELECT COUNT(*) FROM ingredients
                   WHERE icon_url LIKE '%%ui-avatars%%'""",
                "0",
                "All ingredients should have real R2 photos, not ui-avatars placeholders",
            ),
            (
                "Ingredients without icon_url",
                "SELECT COUNT(*) FROM ingredients WHERE icon_url IS NULL",
                "0",
                "All ingredients should have an icon URL",
            ),
            (
                "Ingredients without blurhash",
                "SELECT COUNT(*) FROM ingredients WHERE icon_blurhash IS NULL",
                "0",
                "All ingredients should have a blurhash for progressive loading",
            ),
            (
                "Dishes without blurhash",
                "SELECT COUNT(*) FROM dishes WHERE image_blurhash IS NULL AND image_url IS NOT NULL",
                "0",
                "All dishes with images should have a blurhash",
            ),
            (
                "Restaurants without blurhash",
                "SELECT COUNT(*) FROM restaurants WHERE image_blurhash IS NULL AND image_url IS NOT NULL",
                "0",
                "All restaurants with images should have a blurhash",
            ),
            (
                "Dish ingredients referencing missing ingredients",
                """SELECT COUNT(*) FROM dish_ingredients di
                   LEFT JOIN ingredients i ON di.ingredient_id = i.ingredient_id
                   WHERE i.ingredient_id IS NULL""",
                "0",
                "Every dish_ingredient must reference an existing ingredient",
            ),
            (
                "Dishes without menu section assignment",
                """SELECT COUNT(*) FROM dishes d
                   LEFT JOIN dish_section_assignments dsa ON d.dish_id = dsa.dish_id
                   WHERE dsa.dish_id IS NULL""",
                "0",
                "Every dish should be assigned to a menu section",
            ),
            (
                "Menu sections without dishes (empty sections)",
                """SELECT COUNT(*) FROM menu_sections ms
                   LEFT JOIN dish_section_assignments dsa ON ms.section_id = dsa.section_id
                   WHERE dsa.section_id IS NULL""",
                f"< {int(num_restaurants * 5)}",
                "Some empty menu sections are expected (not every restaurant uses every section)",
            ),
            (
                "Restaurant edit requests referencing missing restaurants",
                """SELECT COUNT(*) FROM restaurant_edit_requests r
                   LEFT JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
                   WHERE rest.restaurant_id IS NULL""",
                "0",
                "Restaurant edit requests must reference existing restaurants",
            ),
            (
                "Ingredient suggestions referencing missing users",
                """SELECT COUNT(*) FROM ingredient_suggestions s
                   LEFT JOIN users u ON s.user_id = u.user_id
                   WHERE u.user_id IS NULL""",
                "0",
                "Ingredient suggestions must reference existing users",
            ),
            (
                "Users with NULL failed_login_count",
                "SELECT COUNT(*) FROM users WHERE failed_login_count IS NULL",
                "0",
                "All users must have failed_login_count set (NOT NULL)",
            ),
            (
                "Reviews with wrong helpful_count",
                """SELECT COUNT(*) FROM (
                       SELECT r.review_id, r.helpful_count, COUNT(rl.user_id) real_cnt
                       FROM reviews r
                       LEFT JOIN review_likes rl ON r.review_id = rl.review_id
                       GROUP BY r.review_id, r.helpful_count
                       HAVING r.helpful_count != COUNT(rl.user_id)
                   ) t""",
                "0",
                "reviews.helpful_count must match actual COUNT(review_likes)",
            ),
        ]

        results = []
        for name, query, expected, description in checks:
            try:
                actual = self.db.fetch_val(query) or 0
                status = _evaluate_check(actual, expected)
            except Exception as e:
                actual = -1
                status = "error"
                self.db.rollback()
                logger.debug(f"Integrity check '{name}' failed: {e}")

            results.append({
                "name": name,
                "actual": actual,
                "expected": expected,
                "description": description,
                "status": status,
            })

        return results

    def print_report(self) -> None:
        if not self.stats:
            logger.warning("No statistics collected. Run collect_all() first.")
            return

        lines = [
            "",
            "=" * 80,
            "                    DATASET STATISTICS (NCF Training Data)",
            "=" * 80,
        ]

        rc = self.stats.get("row_counts", {})
        lines.append("")
        lines.append("ROW COUNTS")
        for table, count in rc.items():
            val = "ERROR" if count == -1 else f"{count:,}"
            lines.append(f"  {table:<28}: {val}")

        ratings = self.stats.get("ratings", {})
        if "dish_rating" in ratings:
            dr = ratings["dish_rating"]
            lines.append("")
            lines.append("RATING DISTRIBUTION (dish_rating, scale 1-10)")
            lines.append(f"  Mean: {dr['mean']}  |  Std: {dr['std']}  |  Median: {dr['median']}")
            lines.append(f"  P25: {dr['p25']}  |  P75: {dr['p75']}  |  Min: {dr['min']}  |  Max: {dr['max']}")
            dist = dr.get("distribution", {})
            if dist:
                max_count = max(dist.values()) if dist else 1
                bar_parts = []
                for rating in range(1, 11):
                    count = dist.get(str(rating), 0)
                    bar_len = int((count / max_count) * 20) if max_count else 0
                    bar_parts.append(f"{rating}:{'#' * bar_len}")
                lines.append(f"  Histogram: {' '.join(bar_parts)}")

        for col in ("service_rating", "cleanliness_rating", "ambiance_rating"):
            if col in ratings:
                r = ratings[col]
                label = col.replace("_rating", "").capitalize()
                lines.append(f"  {label}: mean={r['mean']} std={r['std']} median={r['median']}")

        ncf = self.stats.get("ncf_matrix", {})
        if ncf:
            lines.append("")
            lines.append("NCF INTERACTION MATRIX")
            lines.append(f"  Users: {ncf['users']:,}  |  Items (dishes): {ncf['items']:,}")
            lines.append(f"  Interactions: {ncf['interactions']:,}  |  Matrix size: {ncf['matrix_size']:,}")
            lines.append(f"  Sparsity: {ncf['sparsity'] * 100:.2f}%  |  Density: {ncf['density'] * 100:.4f}%")
            lines.append(f"  Avg ratings/user: {ncf['avg_per_user']}  |  Avg ratings/dish: {ncf['avg_per_item']}")

        cs = self.stats.get("cold_start", {})
        if cs:
            u = cs.get("users", {})
            i = cs.get("items", {})
            lines.append("")
            lines.append("COLD START ANALYSIS")
            lines.append(
                f"  Users:  1 review: {u.get('1_review', 0):,} | "
                f"2-5: {u.get('2_to_5', 0):,} | "
                f"6-10: {u.get('6_to_10', 0):,} | "
                f"10+: {u.get('over_10', 0):,}"
            )
            lines.append(
                f"  Items:  <3 reviews: {i.get('under_3', 0):,} | "
                f"3-10: {i.get('3_to_10', 0):,} | "
                f"10+: {i.get('over_10', 0):,}"
            )

        ua = self.stats.get("user_activity", {})
        if ua:
            lines.append("")
            lines.append("USER ACTIVITY (reviews per user)")
            lines.append(
                f"  Mean: {ua['mean']}  |  Std: {ua['std']}  |  Median: {ua['median']}"
                f"  |  Min: {ua['min']}  |  Max: {ua['max']}"
            )

        dp = self.stats.get("dish_popularity", {})
        if dp:
            lines.append("DISH POPULARITY (reviews per dish)")
            lines.append(
                f"  Mean: {dp['mean']}  |  Std: {dp['std']}  |  Median: {dp['median']}"
                f"  |  Min: {dp['min']}  |  Max: {dp['max']}"
            )

        sg = self.stats.get("social_graph", {})
        if sg:
            lines.append("")
            lines.append("SOCIAL GRAPH")
            if "follows" in sg:
                f = sg["follows"]
                lines.append(
                    f"  Follows: {f['total']:,} (avg {f['avg_per_user']}/user, std {f['std']}, max {f['max']})"
                )
            if "review_likes" in sg:
                rl = sg["review_likes"]
                lines.append(
                    f"  Review likes: {rl['total']:,} "
                    f"(avg {rl['avg_per_review']}/review, std {rl['std']}, max {rl['max']})"
                )
            if "favorites" in sg:
                fv = sg["favorites"]
                lines.append(f"  Favorites: {fv['total']:,} (avg {fv['avg_per_user']}/user, std {fv['std']})")
            if "saved_dishes" in sg:
                sd = sg["saved_dishes"]
                lines.append(f"  Saved dishes: {sd['total']:,} (avg {sd['avg_per_user']}/user, std {sd['std']})")

        rd = self.stats.get("restaurant_distribution", {})
        if rd:
            lines.append("")
            lines.append("RESTAURANT DISTRIBUTION")
            by_city = rd.get("by_city", {})
            if by_city:
                top5 = list(by_city.items())[:5]
                cities_str = ", ".join(f"{c}: {n}" for c, n in top5)
                lines.append(f"  Cities ({len(by_city)}): {cities_str}, ...")
            by_pl = rd.get("by_price_level", {})
            if by_pl:
                total = sum(by_pl.values())
                pl_str = ", ".join(f"L{pl}: {cnt} ({cnt / total * 100:.0f}%)" for pl, cnt in by_pl.items())
                lines.append(f"  Price levels: {pl_str}")
            avg_r = rd.get("avg_restaurant_rating", {})
            if avg_r.get("mean"):
                lines.append(f"  Avg restaurant rating: {avg_r['mean']} (std {avg_r['std']})")

        mod = self.stats.get("moderation", {})
        if mod:
            lines.append("")
            lines.append("MODERATION")
            cs_data = mod.get("content_status", {})
            if cs_data:
                total = sum(cs_data.values())
                parts = [f"{s}: {c} ({c / total * 100:.1f}%)" for s, c in cs_data.items()]
                lines.append(f"  Content status: {', '.join(parts)}")

        nd = self.stats.get("ncf_dataset", {})
        if nd:
            lines.append("")
            lines.append("NCF DATASET PROFILE")
            dist = nd.get("distribution", {})
            if dist:
                zr = dist.get("zipf_restaurant", {})
                zd = dist.get("zipf_dish", {})
                cfg_alpha = dist.get("configured_alpha", "?")
                lines.append(
                    f"  Zipf fit (configured alpha={cfg_alpha}): "
                    f"restaurants alpha={zr.get('alpha')} R2={zr.get('r_squared')}  |  "
                    f"dishes alpha={zd.get('alpha')} R2={zd.get('r_squared')}"
                )
                lines.append(f"  Catalog coverage (80% reviews): {dist.get('catalog_coverage_80pct')}% of dishes")
            filt = nd.get("filtered", {})
            if filt:
                lines.append(
                    f"  NCF-eligible: {filt.get('eligible_reviews', 0):,} reviews "
                    f"({filt.get('eligible_users', 0):,} users, "
                    f"{filt.get('eligible_dishes', 0):,} dishes)  |  "
                    f"filter_rate={filt.get('filter_rate', 0)}%"
                )
                bd = filt.get("filtered_breakdown", {})
                if bd:
                    lines.append(
                        f"  Filter breakdown: not_visible={bd.get('not_visible', 0):,} "
                        f"deleted={bd.get('deleted', 0):,} "
                        f"rejected={bd.get('rejected', 0):,} "
                        f"user_deleted={bd.get('user_deleted', 0):,}"
                    )

        photo = self.stats.get("photo_pipeline", {})
        if photo:
            lines.append("")
            lines.append("PHOTO PIPELINE HEALTH")
            for section_name, section_data in photo.items():
                total = section_data.get("total", 0)
                coverage = section_data.get("coverage_pct", 0)
                if section_name == "ingredients":
                    with_icon = section_data.get("with_icon", 0)
                    with_bh = section_data.get("with_blurhash", 0)
                    placeholder = section_data.get("placeholder_count", 0)
                    lines.append(
                        f"  Ingredients: {with_icon}/{total} with icon ({coverage}%), "
                        f"{with_bh} blurhash, {placeholder} placeholders"
                    )
                elif section_name == "users":
                    with_avatar = section_data.get("with_avatar", 0)
                    with_bh = section_data.get("with_blurhash", 0)
                    lines.append(
                        f"  Users: {with_avatar}/{total} with avatar ({coverage}%), "
                        f"{with_bh} blurhash"
                    )
                else:
                    with_img = section_data.get("with_image", 0)
                    with_bh = section_data.get("with_blurhash", 0)
                    label = section_name.capitalize()
                    lines.append(
                        f"  {label}: {with_img}/{total} with image ({coverage}%), "
                        f"{with_bh} blurhash"
                    )

        real = self.stats.get("generator_validation", {})
        if real:
            lines.append("")
            lines.append("GENERATOR VALIDATION")
            geo = real.get("geographic", {})
            if geo:
                lines.append(
                    f"  Geographic consistency: {geo.get('home_city_pct', 0)}% "
                    f"reviews in home city ({geo.get('home_city_reviews', 0):,}/{geo.get('total_reviews', 0):,})"
                )
            vel = real.get("velocity", {})
            if vel:
                lines.append(
                    f"  Review velocity: {vel.get('avg_reviews_per_month')} reviews/user/month "
                    f"(std={vel.get('std')}, median={vel.get('median')})"
                )
            lt = real.get("lifetime", {})
            if lt:
                lines.append(
                    f"  User lifetime: mean={lt.get('mean_days')}d "
                    f"median={lt.get('median_days')}d "
                    f"[{lt.get('p25_days')}d - {lt.get('p75_days')}d]  |  "
                    f"range: {lt.get('min_days')}d - {lt.get('max_days')}d"
                )
            vf = real.get("visit_frequency", {})
            if vf:
                lines.append(
                    f"  Restaurant visits: avg={vf.get('avg_visits_per_restaurant')} "
                    f"median={vf.get('median')} max={vf.get('max')}"
                )
            cd = real.get("cuisine_diversity", {})
            if cd:
                lines.append(
                    f"  Cuisine diversity: avg={cd.get('avg_cuisines_per_user')} "
                    f"cuisines/user (std={cd.get('std')}, median={cd.get('median')})"
                )
            bc = real.get("baseline_correlation", {})
            if bc:
                lines.append(
                    f"  Baseline correlation: "
                    f"CORR(secret_baseline, avg_rating)={bc.get('baseline_vs_actual_correlation')}"
                )
            pvr = real.get("price_vs_rating", {})
            if pvr:
                parts = [f"L{pl}: {d['avg_rating']}" for pl, d in pvr.items()]
                lines.append(f"  Price level vs rating: {', '.join(parts)}")
            dow = real.get("day_of_week", {})
            if dow:
                dow_str = ", ".join(f"{d[:3]}={c:,}" for d, c in dow.items())
                lines.append(f"  Day of week: {dow_str}")
            tl = real.get("text_length", {})
            if tl:
                lines.append(
                    f"  Review text: avg_len={tl.get('avg_length')} "
                    f"median={tl.get('median_length')} "
                    f"std={tl.get('std_length')}  |  "
                    f"with_text={tl.get('pct_with_text')}% "
                    f"({tl.get('with_text', 0):,}/{tl.get('with_text', 0) + tl.get('without_text', 0):,})"
                )

        temporal = self.stats.get("temporal", {})
        rpm = temporal.get("reviews_per_month", {})
        if rpm:
            lines.append("")
            lines.append("TEMPORAL DISTRIBUTION (reviews per month)")
            for month, count in rpm.items():
                bar_len = int((count / max(rpm.values())) * 30) if rpm else 0
                lines.append(f"  {month}: {count:>8,}  {'#' * bar_len}")

        integrity = self.stats.get("integrity", [])
        if integrity:
            passed = sum(1 for c in integrity if c["status"] == "ok")
            total = len(integrity)
            lines.append("")
            lines.append(f"DATA INTEGRITY CHECKS ({passed}/{total} passed)")
            for check in integrity:
                icon = "ok" if check["status"] == "ok" else "FAIL"
                lines.append(
                    f"  [{icon:>4}] {check['name']}: "
                    f"actual={check['actual']:,}, expected {check['expected']}"
                )
                if check["status"] != "ok":
                    lines.append(f"         -> {check['description']}")

        lines.append("")
        lines.append("=" * 80)

        for line in lines:
            logger.info(line)

    def save_json(self, path: str = "data/dataset_stats.json") -> None:
        if not self.stats:
            logger.warning("No statistics collected. Run collect_all() first.")
            return

        output_path = Path(path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(self.stats, f, indent=2, ensure_ascii=False, default=str)

        logger.info(f"Statistics saved to {output_path}")

def _r(value: float | None, decimals: int = 2) -> float | None:
    if value is None:
        return None
    return round(value, decimals)

def _evaluate_check(actual: int, expected: str) -> str:
    expected = expected.strip()
    if expected.startswith("<"):
        threshold = int(expected[1:].strip())
        return "ok" if actual < threshold else "FAIL"
    if expected.startswith(">"):
        threshold = int(expected[1:].strip())
        return "ok" if actual > threshold else "FAIL"
    return "ok" if actual == int(expected) else "FAIL"

def _power_law_fit(counts: list[int]) -> tuple[float, float]:
    filtered = [(i + 1, c) for i, c in enumerate(sorted(counts, reverse=True)) if c > 0]
    if len(filtered) < 3:
        return 0.0, 0.0
    log_x = [math.log(rank) for rank, _ in filtered]
    log_y = [math.log(cnt) for _, cnt in filtered]
    n = len(log_x)
    sum_x = sum(log_x)
    sum_y = sum(log_y)
    sum_xy = sum(x * y for x, y in zip(log_x, log_y))
    sum_x2 = sum(x * x for x in log_x)
    denom = n * sum_x2 - sum_x * sum_x
    if denom == 0:
        return 0.0, 0.0
    slope = (n * sum_xy - sum_x * sum_y) / denom
    intercept = (sum_y - slope * sum_x) / n
    y_mean = sum_y / n
    ss_tot = sum((y - y_mean) ** 2 for y in log_y)
    ss_res = sum((y - (slope * x + intercept)) ** 2 for x, y in zip(log_x, log_y))
    r_squared = 1 - (ss_res / ss_tot) if ss_tot > 0 else 0
    return -slope, r_squared
