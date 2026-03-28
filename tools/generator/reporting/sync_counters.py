import logging

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class CounterSync:
    def __init__(self, db: DatabaseConnection):
        self.db = db

    def sync_all(self):
        self._sync_user_review_count()
        self._sync_user_follow_counts()
        self._sync_user_photo_count()
        self._sync_dish_review_count()
        self._sync_review_helpful_count()
        self._sync_restaurant_avg_ratings()
        self._sync_dish_avg_rating()
        self._sync_dish_trending_scores()
        self._sync_restaurant_trending_scores()
        self._sync_site_stats()
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

    def _sync_user_photo_count(self):
        logger.info("Syncing users.photo_count...")
        self.db.execute_query("""
            UPDATE users u SET photo_count = sub.cnt
            FROM (
                SELECT uploaded_by, COUNT(*) cnt
                FROM media_assets
                WHERE uploaded_by IS NOT NULL
                GROUP BY uploaded_by
            ) sub
            WHERE u.user_id = sub.uploaded_by
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

    def _sync_dish_trending_scores(self):
        logger.info("Syncing dishes.trending_score (Bayesian Average)...")
        self.db.execute_query("""
            WITH time_ref AS (
                SELECT MAX(created_at) - INTERVAL '30 days' AS cutoff FROM reviews
            ),
            global AS (
                SELECT COALESCE(AVG(dish_rating), 5.0)::decimal AS avg_r
                FROM reviews, time_ref
                WHERE created_at > time_ref.cutoff
                  AND content_status = 'approved' AND is_visible = true
            )
            UPDATE dishes d SET trending_score = sub.score
            FROM (
                SELECT r.dish_id,
                    CASE WHEN COUNT(*) >= 3 THEN
                        (COUNT(*)::decimal / (COUNT(*) + 5)) * AVG(r.dish_rating)
                        + (5::decimal / (COUNT(*) + 5)) * g.avg_r
                    ELSE NULL END AS score
                FROM reviews r, time_ref t, global g
                WHERE r.created_at > t.cutoff
                  AND r.content_status = 'approved' AND r.is_visible = true
                GROUP BY r.dish_id, g.avg_r
            ) sub
            WHERE d.dish_id = sub.dish_id
        """)

    def _sync_restaurant_trending_scores(self):
        logger.info("Syncing restaurants.trending_score (Bayesian Average)...")
        self.db.execute_query("""
            WITH time_ref AS (
                SELECT MAX(created_at) - INTERVAL '30 days' AS cutoff FROM reviews
            ),
            global AS (
                SELECT COALESCE(AVG(dish_rating), 5.0)::decimal AS avg_r
                FROM reviews, time_ref
                WHERE created_at > time_ref.cutoff
                  AND content_status = 'approved' AND is_visible = true
            )
            UPDATE restaurants res SET trending_score = sub.score
            FROM (
                SELECT r.restaurant_id,
                    CASE WHEN COUNT(*) >= 3 THEN
                        (COUNT(*)::decimal / (COUNT(*) + 5)) * AVG(r.dish_rating)
                        + (5::decimal / (COUNT(*) + 5)) * g.avg_r
                    ELSE NULL END AS score
                FROM reviews r, time_ref t, global g
                WHERE r.created_at > t.cutoff
                  AND r.content_status = 'approved' AND r.is_visible = true
                GROUP BY r.restaurant_id, g.avg_r
            ) sub
            WHERE res.restaurant_id = sub.restaurant_id
        """)

    def _sync_site_stats(self):
        logger.info("Syncing system.site_stats...")
        self.db.execute_query("""
            INSERT INTO system.site_stats (id, total_dishes, total_restaurants, total_reviews,
                total_users, total_photos, reviews_this_week, new_users_this_month,
                avg_dish_rating, avg_restaurant_food_score,
                most_popular_cuisine, most_active_city, updated_at)
            VALUES (1,
                (SELECT COUNT(*) FROM dishes),
                (SELECT COUNT(*) FROM restaurants WHERE status = 0),
                (SELECT COUNT(*) FROM reviews WHERE is_deleted = false),
                (SELECT COUNT(*) FROM users WHERE is_active AND NOT is_deleted),
                (SELECT COUNT(*) FROM media_assets WHERE status = 'approved'),
                (SELECT COUNT(*) FROM reviews WHERE NOT is_deleted
                    AND created_at >= NOW() - INTERVAL '7 days'),
                (SELECT COUNT(*) FROM users WHERE created_at >= NOW() - INTERVAL '30 days'),
                COALESCE((SELECT AVG(avg_rating) FROM dishes WHERE avg_rating IS NOT NULL), 0),
                COALESCE((SELECT AVG(avg_food_score) FROM restaurants
                    WHERE avg_food_score IS NOT NULL), 0),
                (SELECT cuisine_type FROM restaurants
                    WHERE status = 0 AND cuisine_type IS NOT NULL
                    GROUP BY cuisine_type ORDER BY COUNT(*) DESC LIMIT 1),
                (SELECT c.city_name FROM reviews r
                    JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
                    JOIN cities c ON rest.city_id = c.city_id
                    WHERE NOT r.is_deleted
                    GROUP BY c.city_name ORDER BY COUNT(*) DESC LIMIT 1),
                NOW()
            )
            ON CONFLICT (id) DO UPDATE SET
                total_dishes = EXCLUDED.total_dishes,
                total_restaurants = EXCLUDED.total_restaurants,
                total_reviews = EXCLUDED.total_reviews,
                total_users = EXCLUDED.total_users,
                total_photos = EXCLUDED.total_photos,
                reviews_this_week = EXCLUDED.reviews_this_week,
                new_users_this_month = EXCLUDED.new_users_this_month,
                avg_dish_rating = EXCLUDED.avg_dish_rating,
                avg_restaurant_food_score = EXCLUDED.avg_restaurant_food_score,
                most_popular_cuisine = EXCLUDED.most_popular_cuisine,
                most_active_city = EXCLUDED.most_active_city,
                updated_at = EXCLUDED.updated_at
        """)
