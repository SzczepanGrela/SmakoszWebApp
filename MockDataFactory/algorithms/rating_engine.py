from typing import Any

from .rating_strategies import RatingAggregator

def calculate_review_ratings(
    user_data: dict[str, Any],
    dish: dict[str, Any],
    restaurant: dict[str, Any],
    user_variant_preference_vector: dict[str, float] | None = None,
    vectors_data: dict[str, Any] | None = None,
) -> dict[str, float]:
    """
    Calculates detailed review ratings using RatingAggregator.
    Facade function for backward compatibility.
    """
    aggregator = RatingAggregator()
    return aggregator.calculate_all(user_data, dish, restaurant, user_variant_preference_vector, vectors_data)
