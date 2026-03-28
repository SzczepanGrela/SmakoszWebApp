from utils.db_connection import DatabaseConnection

class CityDAO:

    @staticmethod
    def get_all_city_names(db: DatabaseConnection) -> list[tuple[str]]:
        return db.fetch_all("SELECT city_name FROM cities")
