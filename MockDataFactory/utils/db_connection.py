"""
Database Connection Manager dla PostgreSQL przez psycopg2
"""

import psycopg2
from psycopg2 import sql
from psycopg2.extras import execute_values
import logging
from typing import List, Dict, Any, Optional

class DatabaseConnection:
    """
    Zarządza połączeniem z PostgreSQL przez psycopg2
    """

    def __init__(self, connection_params: Dict[str, str]):
        """
        Args:
            connection_params: Słownik z parametrami połączenia
            Przykład: {'host': 'localhost', 'port': '5432', 'dbname': 'mockdatadb', 'user': 'postgres', 'password': ''}
        """
        self.connection_params = connection_params
        self.connection: Optional[psycopg2.extensions.connection] = None
        self.cursor: Optional[psycopg2.extensions.cursor] = None
        self.logger = logging.getLogger(__name__)

    def connect(self) -> None:
        """Nawiązuje połączenie z bazą danych"""
        try:
            self.connection = psycopg2.connect(**self.connection_params)
            self.cursor = self.connection.cursor()
            self.logger.info("Polaczono z baza danych PostgreSQL")
        except psycopg2.Error as e:
            self.logger.error(f"Blad polaczenia: {e}")
            raise

    def execute_query(self, sql_query: str, params: tuple = ()) -> None:
        """
        Wykonuje zapytanie SQL

        Args:
            sql_query: Zapytanie SQL (używa %s jako placeholders)
            params: Parametry do zapytania

        Returns:
            None (cursor zawiera wyniki)
        """
        try:
            self.cursor.execute(sql_query, params)
        except psycopg2.Error as e:
            self.logger.error(f"Blad zapytania: {e}")
            self.logger.error(f"SQL: {sql_query}")
            self.logger.error(f"Params: {params}")
            raise

    def insert_single(self, table: str, data: Dict[str, Any], id_column: str = None) -> int:
        """
        Wstawia pojedynczy wiersz do tabeli

        Args:
            table: Nazwa tabeli
            data: Słownik {kolumna: wartość}
            id_column: Nazwa kolumny ID do zwrócenia (opcjonalnie)

        Returns:
            ID wstawionego wiersza (jeśli id_column podany)
        """
        columns = ", ".join(data.keys())
        placeholders = ", ".join(["%s"] * len(data))

        # Automatyczne wykrycie kolumny ID jeśli nie podana
        if id_column is None:
            table_lower = table.lower()
            if table_lower == 'cities':
                id_column = 'city_id'
            elif table_lower == 'dishes':
                id_column = 'dish_id'
            elif table_lower == 'ingredients':
                id_column = 'ingredient_id'
            elif table_lower == 'restaurants':
                id_column = 'restaurant_id'
            elif table_lower == 'users':
                id_column = 'user_id'
            elif table_lower == 'reviews':
                id_column = 'review_id'
            elif table_lower == 'tags':
                id_column = 'tag_id'
            elif table_lower == 'photos':
                id_column = 'photo_id'
            elif table_lower == 'user_photos':
                id_column = 'user_photo_id'
            elif table_lower == 'reports':
                id_column = 'report_id'
            elif table_lower == 'pending_user_photos':
                id_column = 'pending_photo_id'
            elif table_lower == 'pending_comments':
                id_column = 'pending_comment_id'
            elif table_lower == 'restaurant_opening_hours':
                id_column = 'hours_id'
            elif table_lower == 'search_history':
                id_column = 'search_id'
            elif table_lower == 'data_correction_requests':
                id_column = 'request_id'
            elif table_lower == 'email_logs':
                id_column = 'email_log_id'
            elif table_lower == 'auth_tokens':
                id_column = 'token_id'
            elif table_lower == 'security_logs':
                id_column = 'log_id'
            elif table_lower == 'ai_review_photos':
                id_column = 'queue_id'
            elif table_lower == 'admin_review_photos':
                id_column = 'queue_id'
            elif table_lower == 'ai_review_comments':
                id_column = 'queue_id'
            elif table_lower == 'admin_review_comments':
                id_column = 'queue_id'
            else:
                # Fallback: singular_id
                id_column = table_lower.rstrip('s') + '_id'

        sql_query = f"INSERT INTO {table} ({columns}) VALUES ({placeholders}) RETURNING {id_column}"

        self.execute_query(sql_query, tuple(data.values()))
        result = self.cursor.fetchone()
        self.commit()

        return result[0] if result else 0

    def insert_bulk(self, table: str, data_list: List[Dict[str, Any]]) -> None:
        """
        Wstawia wiele wierszy za jednym razem (SZYBSZE z execute_values)

        Args:
            table: Nazwa tabeli
            data_list: Lista słowników [{kolumna: wartość}, ...]
        """
        if not data_list:
            return

        # Zakładamy, że wszystkie dict mają te same klucze
        columns = list(data_list[0].keys())
        columns_str = ", ".join(columns)

        # Przekształć listę dict na listę tuple
        values = [tuple(d.values()) for d in data_list]

        try:
            # Użyj execute_values dla znacznie lepszej wydajności
            sql_query = f"INSERT INTO {table} ({columns_str}) VALUES %s"
            execute_values(self.cursor, sql_query, values)
            self.commit()
            if len(data_list) >= 1000:
                self.logger.info(f"Wstawiono {len(data_list)} wierszy do {table}")
        except psycopg2.Error as e:
            self.logger.error(f"Blad bulk insert: {e}")
            self.rollback()
            raise

    def insert_bulk_returning(self, table: str, data_list: List[Dict[str, Any]], id_column: str) -> List[int]:
        """
        Wstawia wiele wierszy i zwraca ich ID

        Args:
            table: Nazwa tabeli
            data_list: Lista słowników [{kolumna: wartość}, ...]
            id_column: Nazwa kolumny ID do zwrócenia

        Returns:
            Lista ID wstawionych wierszy
        """
        if not data_list:
            return []

        columns = list(data_list[0].keys())
        columns_str = ", ".join(columns)
        values = [tuple(d.values()) for d in data_list]

        try:
            sql_query = f"INSERT INTO {table} ({columns_str}) VALUES %s RETURNING {id_column}"
            result = execute_values(self.cursor, sql_query, values, fetch=True)
            self.commit()
            if len(data_list) >= 1000:
                self.logger.info(f"Wstawiono {len(data_list)} wierszy do {table}")
            return [row[0] for row in result]
        except psycopg2.Error as e:
            self.logger.error(f"Blad bulk insert: {e}")
            self.rollback()
            raise

    def fetch_all(self, sql_query: str, params: tuple = ()) -> List[tuple]:
        """Wykonuje SELECT i zwraca wszystkie wyniki"""
        self.execute_query(sql_query, params)
        return self.cursor.fetchall()

    def fetch_one(self, sql_query: str, params: tuple = ()) -> Optional[tuple]:
        """Wykonuje SELECT i zwraca jeden wynik"""
        self.execute_query(sql_query, params)
        return self.cursor.fetchone()

    def commit(self) -> None:
        """Zatwierdza transakcję"""
        if self.connection:
            self.connection.commit()

    def rollback(self) -> None:
        """Cofa transakcję"""
        if self.connection:
            self.connection.rollback()

    def close(self) -> None:
        """Zamyka połączenie"""
        if self.cursor:
            self.cursor.close()
        if self.connection:
            self.connection.close()
        self.logger.info("Zamknieto polaczenie z baza danych")

    def __enter__(self):
        """Context manager entry"""
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit"""
        if exc_type:
            self.rollback()
        else:
            self.commit()
        self.close()
