from utils.db_connection import DatabaseConnection

class ReviewDAO:

    @staticmethod
    def get_all_reviews_basic(db: DatabaseConnection) -> list[tuple[int, int]]:
        return db.fetch_all("SELECT review_id, user_id FROM reviews")
