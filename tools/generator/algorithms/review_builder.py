import random
import uuid
from datetime import datetime

from uuid6 import uuid7

from algorithms.on_the_fly_calculator import get_contextual_preferences
from algorithms.rating_strategies import calculate_review_ratings
from utils.date_generator import to_sql_datetime
from utils.helpers import safe_divide
from utils.photo_pools import PhotoPools
from utils.text_generator import ReviewTextGenerator

def generate_single_review(
    user: dict,
    restaurant: dict,
    dish: dict,
    review_date: datetime,
    vectors_data: dict,
    text_gen: ReviewTextGenerator,
    photo_pools: PhotoPools,
    user_variant_preference_vector: dict | None = None,
    simulation_today: datetime | None = None,
) -> dict:
    if not user_variant_preference_vector:
        user_variant_preference_vector = get_contextual_preferences(
            vectors_data, user, dish, dish.get("secret_variant_name", "Unknown"), dish.get("secret_archetype", "General")
        )

    ratings = calculate_review_ratings(
        user,
        dish,
        restaurant,
        user_variant_preference_vector=user_variant_preference_vector,
        vectors_data=vectors_data,
    )

    if simulation_today:
        review_date_date = review_date.date() if hasattr(review_date, "date") else review_date
        simulation_today_date = simulation_today.date() if hasattr(simulation_today, "date") else simulation_today
        review_age_days = (simulation_today_date - review_date_date).days
    else:
        review_age_days = 999

    is_recent_review = review_age_days <= 7

    has_comment = random.random() < 0.60
    comment = None

    if has_comment:
        comment = text_gen.generate_review_comment(
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
            content_status = "pending"
        else:
            content_status = "approved"
    else:
        content_status = "none"

    is_visible = not is_recent_review

    dish_rating_value = int(round(ratings["food_score"]))
    review_data = {
        "public_id": str(uuid7()),
        "user_id": user["user_id"],
        "restaurant_id": restaurant["restaurant_id"],
        "dish_id": dish["dish_id"],
        "visit_date": review_date.date() if hasattr(review_date, "date") else review_date,
        "dish_rating": dish_rating_value,
        "service_rating": int(round(ratings["service_score"])),
        "cleanliness_rating": int(round(ratings["cleanliness_score"])),
        "ambiance_rating": int(round(ratings["ambiance_score"])),
        "content": comment,
        "content_status": content_status,
        "is_visible": is_visible,
        "created_at": to_sql_datetime(review_date),
        "version": 1,
        "is_approved": True,
        "is_deleted": False,
    }

    user_photo_data = None

    if random.random() < 0.30:
        photo_metadata = photo_pools.get_review_photo(dish["secret_archetype"], dish["dish_name"])

        photo_status = "pending" if is_recent_review else "approved"

        user_photo_data = {
            "url": photo_metadata["url"],
            "blurhash": photo_metadata["blurhash"],
            "width": photo_metadata["width"],
            "height": photo_metadata["height"],
            "status": photo_status,
        }

    return {
        "review_data": review_data,
        "user_photo": user_photo_data,
    }
