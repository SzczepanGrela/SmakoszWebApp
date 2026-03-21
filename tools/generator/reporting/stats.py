"""
Dataset Statistics for NCF Training Data Analysis.

Collects, prints and exports statistics about generated data.
Designed for engineering thesis on Neural Collaborative Filtering.
"""

import json
import logging
from datetime import datetime
from pathlib import Path

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class DatasetStatistics:
    """Collects and reports dataset statistics relevant for NCF model training."""

    def __init__(self, db: DatabaseConnection):
        self.db = db
        self.stats: dict = {}

    def collect_all(self) -> dict:
        """Run all statistics queries. Returns full stats dictionary."""
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
            "temporal": self._temporal_stats(),
        }

        logger.info("Statistics collection complete.")
        return self.stats

    # ------------------------------------------------------------------
    # Data collection methods
    # ------------------------------------------------------------------

    def _row_counts(self) -> dict[str, int]:
        tables = [
            "users", "restaurants", "dishes", "reviews",
            "user_follows", "review_likes", "notifications",
            "favorite_restaurants", "saved_dishes", "search_histories",
            "media_assets", "data_correction_requests", "reports",
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
                    "mean": _r(row[0]), "std": _r(row[1]),
                    "min": row[2], "max": row[3],
                    "p25": _r(row[4]), "median": _r(row[5]), "p75": _r(row[6]),
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
            "mean": _r(row[0]), "std": _r(row[1]), "median": _r(row[2]),
            "min": row[3], "max": row[4],
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
            "mean": _r(row[0]), "std": _r(row[1]), "median": _r(row[2]),
            "min": row[3], "max": row[4],
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
            "by_city": {city: cnt for city, cnt in by_city},
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

        return result

    def _moderation_stats(self) -> dict:
        by_status = self.db.fetch_all("""
            SELECT content_status, COUNT(*) FROM reviews
            GROUP BY content_status ORDER BY COUNT(*) DESC
        """)
        by_verdict = self.db.fetch_all("""
            SELECT ai_verdict, COUNT(*) FROM reviews
            WHERE ai_verdict IS NOT NULL
            GROUP BY ai_verdict ORDER BY COUNT(*) DESC
        """)
        return {
            "content_status": {s: c for s, c in by_status},
            "ai_verdict": {v: c for v, c in by_verdict},
        }

    def _temporal_stats(self) -> dict:
        rows = self.db.fetch_all("""
            SELECT TO_CHAR(DATE_TRUNC('month', visit_date), 'YYYY-MM') as month,
                   COUNT(*)
            FROM reviews
            GROUP BY DATE_TRUNC('month', visit_date)
            ORDER BY DATE_TRUNC('month', visit_date)
        """)
        return {"reviews_per_month": {m: c for m, c in rows}}

    # ------------------------------------------------------------------
    # Output methods
    # ------------------------------------------------------------------

    def print_report(self) -> None:
        """Print formatted report to console via logger."""
        if not self.stats:
            logger.warning("No statistics collected. Run collect_all() first.")
            return

        lines = [
            "",
            "=" * 80,
            "                    DATASET STATISTICS (NCF Training Data)",
            "=" * 80,
        ]

        # Row counts
        rc = self.stats.get("row_counts", {})
        lines.append("")
        lines.append("ROW COUNTS")
        for table, count in rc.items():
            val = "ERROR" if count == -1 else f"{count:,}"
            lines.append(f"  {table:<28}: {val}")

        # Ratings
        ratings = self.stats.get("ratings", {})
        if "dish_rating" in ratings:
            dr = ratings["dish_rating"]
            lines.append("")
            lines.append("RATING DISTRIBUTION (dish_rating, scale 1-10)")
            lines.append(
                f"  Mean: {dr['mean']}  |  Std: {dr['std']}  |  Median: {dr['median']}"
            )
            lines.append(
                f"  P25: {dr['p25']}  |  P75: {dr['p75']}  |  Min: {dr['min']}  |  Max: {dr['max']}"
            )
            dist = dr.get("distribution", {})
            if dist:
                max_count = max(dist.values()) if dist else 1
                bar_parts = []
                for rating in range(1, 11):
                    count = dist.get(str(rating), 0)
                    bar_len = int((count / max_count) * 20) if max_count else 0
                    bar_parts.append(f"{rating}:{'█' * bar_len}")
                lines.append(f"  Histogram: {' '.join(bar_parts)}")

        # Sub-ratings summary
        for col in ("service_rating", "cleanliness_rating", "ambiance_rating"):
            if col in ratings:
                r = ratings[col]
                label = col.replace("_rating", "").capitalize()
                lines.append(f"  {label}: mean={r['mean']} std={r['std']} median={r['median']}")

        # NCF matrix
        ncf = self.stats.get("ncf_matrix", {})
        if ncf:
            lines.append("")
            lines.append("NCF INTERACTION MATRIX")
            lines.append(
                f"  Users: {ncf['users']:,}  |  Items (dishes): {ncf['items']:,}"
            )
            lines.append(
                f"  Interactions: {ncf['interactions']:,}  |  "
                f"Matrix size: {ncf['matrix_size']:,}"
            )
            lines.append(
                f"  Sparsity: {ncf['sparsity'] * 100:.2f}%  |  "
                f"Density: {ncf['density'] * 100:.4f}%"
            )
            lines.append(
                f"  Avg ratings/user: {ncf['avg_per_user']}  |  "
                f"Avg ratings/dish: {ncf['avg_per_item']}"
            )

        # Cold start
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

        # User activity
        ua = self.stats.get("user_activity", {})
        if ua:
            lines.append("")
            lines.append("USER ACTIVITY (reviews per user)")
            lines.append(
                f"  Mean: {ua['mean']}  |  Std: {ua['std']}  |  Median: {ua['median']}"
                f"  |  Min: {ua['min']}  |  Max: {ua['max']}"
            )

        # Dish popularity
        dp = self.stats.get("dish_popularity", {})
        if dp:
            lines.append("DISH POPULARITY (reviews per dish)")
            lines.append(
                f"  Mean: {dp['mean']}  |  Std: {dp['std']}  |  Median: {dp['median']}"
                f"  |  Min: {dp['min']}  |  Max: {dp['max']}"
            )

        # Social graph
        sg = self.stats.get("social_graph", {})
        if sg:
            lines.append("")
            lines.append("SOCIAL GRAPH")
            if "follows" in sg:
                f = sg["follows"]
                lines.append(
                    f"  Follows: {f['total']:,} "
                    f"(avg {f['avg_per_user']}/user, std {f['std']}, max {f['max']})"
                )
            if "review_likes" in sg:
                rl = sg["review_likes"]
                lines.append(
                    f"  Review likes: {rl['total']:,} "
                    f"(avg {rl['avg_per_review']}/review, std {rl['std']}, max {rl['max']})"
                )
            if "favorites" in sg:
                fv = sg["favorites"]
                lines.append(
                    f"  Favorites: {fv['total']:,} "
                    f"(avg {fv['avg_per_user']}/user, std {fv['std']})"
                )

        # Restaurant distribution
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
                pl_str = ", ".join(
                    f"L{pl}: {cnt} ({cnt / total * 100:.0f}%)" for pl, cnt in by_pl.items()
                )
                lines.append(f"  Price levels: {pl_str}")
            avg_r = rd.get("avg_restaurant_rating", {})
            if avg_r.get("mean"):
                lines.append(
                    f"  Avg restaurant rating: {avg_r['mean']} (std {avg_r['std']})"
                )

        # Moderation
        mod = self.stats.get("moderation", {})
        if mod:
            lines.append("")
            lines.append("MODERATION")
            cs_data = mod.get("content_status", {})
            if cs_data:
                total = sum(cs_data.values())
                parts = [
                    f"{s}: {c} ({c / total * 100:.1f}%)" for s, c in cs_data.items()
                ]
                lines.append(f"  Content status: {', '.join(parts)}")

        # Temporal
        temporal = self.stats.get("temporal", {})
        rpm = temporal.get("reviews_per_month", {})
        if rpm:
            lines.append("")
            lines.append("TEMPORAL DISTRIBUTION (reviews per month)")
            for month, count in rpm.items():
                bar_len = int((count / max(rpm.values())) * 30) if rpm else 0
                lines.append(f"  {month}: {count:>8,}  {'█' * bar_len}")

        lines.append("")
        lines.append("=" * 80)

        for line in lines:
            logger.info(line)

    def save_json(self, path: str = "data/dataset_stats.json") -> None:
        """Save full stats dictionary to JSON file."""
        if not self.stats:
            logger.warning("No statistics collected. Run collect_all() first.")
            return

        output_path = Path(path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(self.stats, f, indent=2, ensure_ascii=False, default=str)

        logger.info(f"Statistics saved to {output_path}")

def _r(value: float | None, decimals: int = 2) -> float | None:
    """Round a float value, handling None."""
    if value is None:
        return None
    return round(value, decimals)
