from models.domain import CityInfo
from utils.db_connection import DatabaseConnection

class CityDAO:

    @staticmethod
    def get_all_city_names(db: DatabaseConnection) -> list[tuple[str]]:
        return db.fetch_all("SELECT city_name FROM cities")

    @staticmethod
    def get_all_cities(db: DatabaseConnection) -> list[CityInfo]:
        rows = db.fetch_all("SELECT city_id, city_name FROM cities")
        return [CityInfo(city_id=row[0], city_name=row[1]) for row in rows]
