"""
CounterSync - bulk synchronization of denormalized counters.

The generator bypasses per-row SQL triggers for performance (millions of rows).
After all phases complete, this module recalculates all denormalized counters
using bulk UPDATE queries (equivalent to what triggers would do, but in one pass).
"""

import logging

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class CounterSync:
    def __init__(self, db: DatabaseConnection):
        self.db = db

    def sync_all(self):
        """Synchronize all denormalized counters in correct order."""
        self._sync_user_review_count()
        self._sync_user_follow_counts()
        self._sync_dish_review_count()
        self._sync_review_helpful_count()
        self._sync_restaurant_avg_ratings()
        self._sync_dish_avg_rating()
        self.db.commit()
        logger.info("All denormalized counters synchronized.")

    def _sync_user_review_count(self):
        logger.info("Syncing users.review_count...")
        self.db.execute_query("""
            UPDATE users u SET review_count = sub.cnt
            FROM (SELECT user_id, COUNT(*) cnt FROM reviews GROUP BY user_id) sub
            WHERE u.user_id = sub.user_id
        """)

    def _sync_user_follow_counts(self):
        logger.info("Syncing users.followers_count...")
        self.db.execute_query("""
            UPDATE users u SET followers_count = COALESCE(sub.cnt, 0)
            FROM (SELECT followed_id, COUNT(*) cnt FROM user_follows GROUP BY followed_id) sub
            WHERE u.user_id = sub.followed_id
        """)

        logger.info("Syncing users.following_count...")
        self.db.execute_query("""
            UPDATE users u SET following_count = COALESCE(sub.cnt, 0)
            FROM (SELECT follower_id, COUNT(*) cnt FROM user_follows GROUP BY follower_id) sub
            WHERE u.user_id = sub.follower_id
        """)

    def _sync_dish_review_count(self):
        logger.info("Syncing dishes.review_count...")
        self.db.execute_query("""
            UPDATE dishes d SET review_count = sub.cnt
            FROM (SELECT dish_id, COUNT(*) cnt FROM reviews GROUP BY dish_id) sub
            WHERE d.dish_id = sub.dish_id
        """)

    def _sync_review_helpful_count(self):
        logger.info("Syncing reviews.helpful_count...")
        self.db.execute_query("""
            UPDATE reviews r SET helpful_count = sub.cnt
            FROM (SELECT review_id, COUNT(*) cnt FROM review_likes GROUP BY review_id) sub
            WHERE r.review_id = sub.review_id
        """)

    def _sync_restaurant_avg_ratings(self):
        logger.info("Syncing restaurants avg ratings...")
        self.db.execute_query("""
            UPDATE restaurants r SET
                avg_food_score = sub.avg_food,
                avg_service = sub.avg_svc,
                avg_cleanliness = sub.avg_clean,
                avg_ambiance = sub.avg_amb
            FROM (
                SELECT restaurant_id,
                    AVG(dish_rating)::float avg_food,
                    AVG(service_rating)::float avg_svc,
                    AVG(cleanliness_rating)::float avg_clean,
                    AVG(ambiance_rating)::float avg_amb
                FROM reviews GROUP BY restaurant_id
            ) sub
            WHERE r.restaurant_id = sub.restaurant_id
        """)

    def _sync_dish_avg_rating(self):
        logger.info("Syncing dishes.avg_rating...")
        self.db.execute_query("""
            UPDATE dishes d SET avg_rating = sub.avg_r
            FROM (SELECT dish_id, AVG(dish_rating)::float avg_r FROM reviews GROUP BY dish_id) sub
            WHERE d.dish_id = sub.dish_id
        """)
