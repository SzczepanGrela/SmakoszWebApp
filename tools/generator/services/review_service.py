"""
Review Generator Service

This service encapsulates the business logic for generating a single review.
It handles rating calculations, text generation, and review data assembly,
separating these concerns from the multiprocessing orchestration logic.
"""

import random
import uuid
from datetime import datetime

from algorithms.on_the_fly_calculator import OnTheFlyCalculator
from algorithms.rating_engine import calculate_review_ratings
from utils.date_generator import DateGenerator
from utils.helpers import safe_divide
from utils.photo_pools import PhotoPools
from utils.text_generator import ReviewTextGenerator

class ReviewGeneratorService:
    """
    Service for generating individual review records.

    Encapsulates the complex logic of creating a review including:
    - Rating calculations based on user preferences and dish characteristics
    - Review text generation (content)
    - Photo attachment logic
    - Pending moderation queue assignments
    """

    def __init__(self):
        self.text_gen = ReviewTextGenerator()
        self.photo_pools = PhotoPools()

    def generate_single_review(
        self,
        user: dict,
        restaurant: dict,
        dish: dict,
        review_date: datetime,
        vectors_data: dict,
        user_variant_preference_vector: dict | None = None,
        simulation_today: datetime | None = None,
    ) -> dict:
        """
        Generate a complete review record with all associated data.

        Args:
            user: User data dictionary with preferences and settings
            restaurant: Restaurant data dictionary with attributes
            dish: Dish data dictionary with characteristics
            review_date: Date/time of the review
            vectors_data: Archetype vectors for rating calculations
            user_variant_preference_vector: Pre-calculated preference vector (optional)
            simulation_today: Simulation "today" date for time-based pending logic (optional)

        Returns:
            dict: Complete review data ready for database insertion, including:
                - review_data: Main review record with state machine fields
                - user_photo: Optional user photo data
        """
        if not user_variant_preference_vector:
             calc = OnTheFlyCalculator(vectors_data)
             user_variant_preference_vector = calc.get_contextual_preferences(
                 user, dish, dish.get("secret_variant_name", "Unknown"), dish.get("secret_archetype", "General")
             )

        ratings = calculate_review_ratings(
            user,
            dish,
            restaurant,
            user_variant_preference_vector=user_variant_preference_vector,
            vectors_data=vectors_data,
        )

        if simulation_today:
            review_date_date = review_date.date() if hasattr(review_date, 'date') else review_date
            simulation_today_date = simulation_today.date() if hasattr(simulation_today, 'date') else simulation_today
            review_age_days = (simulation_today_date - review_date_date).days
        else:
            # Fallback: treat as old review (approved by default)
            review_age_days = 999

        # Time-based pending logic: Last 7 days = pending
        is_recent_review = review_age_days <= 7

        # Generate review comment (60% of reviews have comments)
        has_comment = random.random() < 0.60
        comment = None

        if has_comment:
            comment = self.text_gen.generate_review_comment(
                rating=ratings["overall_rating"],
                dish_name=dish["dish_name"],
                restaurant_name=f"Restaurant_{restaurant['restaurant_id']}",
                city="City",
                quality_score=dish["secret_quality"],
                price_ratio=safe_divide(dish["price"], user["secret_price_preference_range"], 1.0),
                service_score=restaurant["secret_service_quality"],
                cleanliness_score=restaurant["secret_cleanliness_score"],
                ambiance_score=restaurant["secret_ambiance_quality"] * 10,
            )

        if has_comment:
            if is_recent_review:
                content_status = 'pending'
            else:
                content_status = 'approved'
        else:
            content_status = 'none'

        # Recent reviews (≤7 days) are hidden until approved
        is_visible = not is_recent_review

        # AI Moderation Simulation (~2% of reviews need manual review)
        is_uncertain = random.random() < 0.02  # 2% need manual review
        if is_uncertain:
            ai_toxicity_score = round(random.uniform(0.3, 0.7), 4)
            ai_spam_score = round(random.uniform(0.3, 0.7), 4)
            ai_verdict = 'needs_review'
        else:
            ai_toxicity_score = round(random.uniform(0.0, 0.1), 4)
            ai_spam_score = round(random.uniform(0.0, 0.05), 4)
            ai_verdict = 'approved'

        ai_model_version = 'mockHerbert-v1'
        ai_processed_at = DateGenerator.to_sql_datetime(review_date)

        dish_rating_value = int(round(ratings["food_score"]))
        review_data = {
            "public_id": str(uuid.uuid4()),
            "user_id": user["user_id"],
            "restaurant_id": restaurant["restaurant_id"],
            "dish_id": dish["dish_id"],
            "visit_date": review_date.date() if hasattr(review_date, 'date') else review_date,
            "dish_rating": dish_rating_value,
            "service_rating": int(round(ratings["service_score"])),
            "cleanliness_rating": int(round(ratings["cleanliness_score"])),
            "ambiance_rating": int(round(ratings["ambiance_score"])),
            "content": comment,
            "content_status": content_status,
            "is_visible": is_visible,
            "created_at": DateGenerator.to_sql_datetime(review_date),
            "version": 1,  # Optimistic Locking
            # AI Moderation Fields
            "ai_toxicity_score": ai_toxicity_score,
            "ai_spam_score": ai_spam_score,
            "ai_verdict": ai_verdict,
            "ai_model_version": ai_model_version,
            "ai_processed_at": ai_processed_at,
            "is_approved": True,
            "is_deleted": False,
        }

        user_photo_data = None

        if random.random() < 0.30:
            photo_metadata = self.photo_pools.get_review_photo(dish["secret_archetype"], dish["dish_name"])

            # Time-based pending: recent reviews have pending photos
            photo_status = "pending" if is_recent_review else "approved"

            # AI Moderation Simulation for photos (~2% need manual review)
            photo_uncertain = random.random() < 0.02
            if photo_uncertain:
                photo_ai_nsfw = round(random.uniform(0.3, 0.6), 4)
                photo_ai_on_topic = round(random.uniform(0.3, 0.5), 4)
                photo_ai_verdict = 'needs_review'
            else:
                photo_ai_nsfw = round(random.uniform(0.0, 0.05), 4)
                photo_ai_on_topic = round(random.uniform(0.8, 0.99), 4)
                photo_ai_verdict = 'approved'

            user_photo_data = {
                "url": photo_metadata["url"],
                "blurhash": photo_metadata["blurhash"],
                "width": photo_metadata["width"],
                "height": photo_metadata["height"],
                "status": photo_status,
                # AI Moderation Fields
                "ai_nsfw_score": photo_ai_nsfw,
                "ai_on_topic_score": photo_ai_on_topic,
                "ai_verdict": photo_ai_verdict,
                "ai_model_version": "mockNSFW-v1/mockCLIP-v1",
                "ai_processed_at": DateGenerator.to_sql_datetime(review_date),
            }

        return {
            "review_data": review_data,
            "user_photo": user_photo_data,
        }
