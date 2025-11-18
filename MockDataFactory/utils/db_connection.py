"""
Database Connection Manager dla SQL Server przez pyodbc
"""

import pyodbc
import logging
from typing import List, Dict, Any, Optional

class DatabaseConnection:
    """
    Zarządza połączeniem z SQL Server przez pyodbc
    """

    def __init__(self, connection_string: str):
        """
        Args:
            connection_string: ODBC connection string
            Przykład: "Driver={ODBC Driver 17 for SQL Server};Server=localhost;Database=MockDataDB;Trusted_Connection=yes;"
        """
        self.connection_string = connection_string
        self.connection: Optional[pyodbc.Connection] = None
        self.cursor: Optional[pyodbc.Cursor] = None
        self.logger = logging.getLogger(__name__)

    def connect(self) -> None:
        """Nawiązuje połączenie z bazą danych"""
        try:
            self.connection = pyodbc.connect(self.connection_string)
            self.cursor = self.connection.cursor()
            self.logger.info("✅ Połączono z bazą danych")
        except pyodbc.Error as e:
            self.logger.error(f"❌ Błąd połączenia: {e}")
            raise

    def execute_query(self, sql: str, params: tuple = ()) -> pyodbc.Cursor:
        """
        Wykonuje zapytanie SQL

        Args:
            sql: Zapytanie SQL (może zawierać placeholders ?)
            params: Parametry do zapytania

        Returns:
            Cursor z wynikami
        """
        try:
            return self.cursor.execute(sql, params)
        except pyodbc.Error as e:
            self.logger.error(f"❌ Błąd zapytania: {e}")
            self.logger.error(f"SQL: {sql}")
            self.logger.error(f"Params: {params}")
            raise

    def insert_single(self, table: str, data: Dict[str, Any]) -> int:
        """
        Wstawia pojedynczy wiersz do tabeli

        Args:
            table: Nazwa tabeli
            data: Słownik {kolumna: wartość}

        Returns:
            ID wstawionego wiersza
        """
        columns = ", ".join(data.keys())
        placeholders = ", ".join(["?"] * len(data))
        sql = f"INSERT INTO {table} ({columns}) VALUES ({placeholders})"

        self.execute_query(sql, tuple(data.values()))
        self.commit()

        return self.get_last_insert_id()

    def insert_bulk(self, table: str, data_list: List[Dict[str, Any]]) -> None:
        """
        Wstawia wiele wierszy za jednym razem (SZYBSZE)

        Args:
            table: Nazwa tabeli
            data_list: Lista słowników [{kolumna: wartość}, ...]
        """
        if not data_list:
            return

        # Zakładamy, że wszystkie dict mają te same klucze
        columns = ", ".join(data_list[0].keys())
        placeholders = ", ".join(["?"] * len(data_list[0]))
        sql = f"INSERT INTO {table} ({columns}) VALUES ({placeholders})"

        # Przekształć listę dict na listę tuple
        values = [tuple(d.values()) for d in data_list]

        try:
            self.cursor.executemany(sql, values)
            self.commit()
            self.logger.info(f"✅ Wstawiono {len(data_list)} wierszy do {table}")
        except pyodbc.Error as e:
            self.logger.error(f"❌ Błąd bulk insert: {e}")
            self.rollback()
            raise

    def get_last_insert_id(self) -> int:
        """Zwraca ID ostatnio wstawionego wiersza"""
        self.cursor.execute("SELECT SCOPE_IDENTITY()")
        result = self.cursor.fetchone()
        if result and result[0] is not None:
            return int(result[0])
        return 0

    def fetch_all(self, sql: str, params: tuple = ()) -> List[tuple]:
        """Wykonuje SELECT i zwraca wszystkie wyniki"""
        self.execute_query(sql, params)
        return self.cursor.fetchall()

    def fetch_one(self, sql: str, params: tuple = ()) -> Optional[tuple]:
        """Wykonuje SELECT i zwraca jeden wynik"""
        self.execute_query(sql, params)
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
        self.logger.info("🔒 Zamknięto połączenie z bazą danych")

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
