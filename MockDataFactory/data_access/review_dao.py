"""Review Data Access Object."""

from utils.db_connection import DatabaseConnection

class ReviewDAO:
    """Handles SELECT queries for reviews."""

    @staticmethod
    def get_all_reviews_basic(db: DatabaseConnection) -> list[tuple[int, int]]:
        """Fetch basic review information for social interactions."""
        return db.fetch_all("SELECT review_id, user_id FROM reviews")
