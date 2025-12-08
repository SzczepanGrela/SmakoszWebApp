import logging
from typing import Any

import psycopg2
from psycopg2.extras import execute_values

class DatabaseConnection:
    def __init__(self, connection_params: dict[str, str]):
        self.connection_params = connection_params
        self.connection: psycopg2.extensions.connection | None = None
        self.cursor: psycopg2.extensions.cursor | None = None
        self.logger = logging.getLogger(__name__)

    def connect(self) -> None:
        try:
            self.connection = psycopg2.connect(**self.connection_params)
            self.cursor = self.connection.cursor()
            self.logger.info("Connected to PostgreSQL")
        except psycopg2.Error as e:
            self.logger.error(f"Connection error: {e}")
            raise

    def execute_query(self, sql_query: str, params: tuple = ()) -> None:
        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")
        try:
            self.cursor.execute(sql_query, params)
        except psycopg2.Error as e:
            self.logger.error(f"Query error: {e}")
            self.logger.error(f"SQL: {sql_query}")
            self.logger.error(f"Params: {params}")
            raise

    def insert_single(self, table: str, data: dict[str, Any], id_column: str | None = None) -> int:
        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")

        columns = ", ".join(data.keys())
        placeholders = ", ".join(["%s"] * len(data))

        if id_column is None:
            table_lower = table.lower()
            id_map = {
                "cities": "city_id",
                "dishes": "dish_id",
                "ingredients": "ingredient_id",
                "restaurants": "restaurant_id",
                "users": "user_id",
                "reviews": "review_id",
                "tags": "tag_id",
                "reports": "report_id",
                "restaurant_opening_hours": "hours_id",
                "search_history": "search_id",
                "data_correction_requests": "request_id",
                "verification_codes": "verification_code_id",
                "media_assets": "asset_id",
            }
            id_column = id_map.get(table_lower, table_lower.rstrip("s") + "_id")

        sql_query = f"INSERT INTO {table} ({columns}) VALUES ({placeholders}) RETURNING {id_column}"

        self.execute_query(sql_query, tuple(data.values()))
        result = self.cursor.fetchone()
        self.commit()

        return result[0] if result else 0

    def insert_bulk(self, table: str, data_list: list[dict[str, Any]]) -> None:
        if not data_list:
            return

        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")

        columns = list(data_list[0].keys())
        columns_str = ", ".join(columns)

        values = [tuple(d.values()) for d in data_list]

        try:
            sql_query = f"INSERT INTO {table} ({columns_str}) VALUES %s"
            execute_values(self.cursor, sql_query, values)
            self.commit()
            if len(data_list) >= 1000:
                self.logger.debug(f"Inserted {len(data_list)} rows into {table}")
        except psycopg2.Error as e:
            self.logger.error(f"Bulk insert error: {e}")
            self.rollback()
            raise

    def insert_bulk_returning(self, table: str, data_list: list[dict[str, Any]], id_column: str) -> list[int]:
        if not data_list:
            return []

        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")

        columns = list(data_list[0].keys())
        columns_str = ", ".join(columns)
        values = [tuple(d.values()) for d in data_list]

        try:
            sql_query = f"INSERT INTO {table} ({columns_str}) VALUES %s RETURNING {id_column}"
            result = execute_values(self.cursor, sql_query, values, fetch=True)
            self.commit()
            if len(data_list) >= 1000:
                self.logger.debug(f"Inserted {len(data_list)} rows into {table}")
            # execute_values returns a list of tuples even for single column
            return [row[0] for row in result] if result else []
        except psycopg2.Error as e:
            self.logger.error(f"Bulk insert error: {e}")
            self.rollback()
            raise

    def fetch_all(self, sql_query: str, params: tuple = ()) -> list[tuple]:
        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")
        self.execute_query(sql_query, params)
        res = self.cursor.fetchall()
        return res if res is not None else []

    def fetch_one(self, sql_query: str, params: tuple = ()) -> tuple | None:
        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")
        self.execute_query(sql_query, params)
        return self.cursor.fetchone()

    def fetch_val(self, sql_query: str, params: tuple = ()) -> Any | None:
        if not self.cursor:
            raise RuntimeError("Database cursor is not connected. Call connect() first.")
        self.execute_query(sql_query, params)
        res = self.cursor.fetchone()
        return res[0] if res else None

    def commit(self) -> None:
        if self.connection:
            self.connection.commit()

    def rollback(self) -> None:
        if self.connection:
            self.connection.rollback()

    def close(self) -> None:
        if self.cursor:
            self.cursor.close()
        if self.connection:
            self.connection.close()
        self.logger.info("Connection closed")

    def __enter__(self):
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        if exc_type:
            self.rollback()
        else:
            self.commit()
        self.close()
