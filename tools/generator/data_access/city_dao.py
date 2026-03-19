"""City Data Access Object."""

from utils.db_connection import DatabaseConnection

class CityDAO:
    """Handles SELECT queries for cities."""

    @staticmethod
    def get_all_city_names(db: DatabaseConnection) -> list[tuple[str]]:
        """Fetch all city names for search history generation."""
        return db.fetch_all("SELECT city_name FROM cities")

    @staticmethod
    def get_cities_with_ids(db: DatabaseConnection) -> list[tuple[int, str]]:
        """Fetch cities with their IDs."""
        return db.fetch_all("SELECT city_id, city_name FROM cities")
