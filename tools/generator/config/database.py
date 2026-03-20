"""
Database Configuration

This module contains database connection settings and related utilities.
Loads configuration from environment variables using .env file.
"""

import os
from pathlib import Path

try:
    from dotenv import load_dotenv

    # Root monorepo (.env) -> fallback na lokalny (.env w tools/generator/)
    root_env = Path(__file__).parent.parent.parent.parent / ".env"
    local_env = Path(__file__).parent.parent / ".env"

    if root_env.exists():
        load_dotenv(root_env)
    elif local_env.exists():
        load_dotenv(local_env)
except ImportError:
    pass

DATABASE_CONFIG = {
    "host": os.getenv("DB_HOST", "localhost"),
    "port": os.getenv("DB_PORT", "5432"),
    "database": os.getenv("DB_NAME", "smakosz_db"),
    "user": os.getenv("DB_USER", "postgres"),
    "password": os.getenv("DB_PASSWORD", ""),
}

def get_connection_params():
    """
    Get database connection parameters in psycopg2 format.

    Returns:
        dict: Connection parameters with 'dbname' key (psycopg2 convention)
    """
    return {
        "host": DATABASE_CONFIG["host"],
        "port": DATABASE_CONFIG["port"],
        "dbname": DATABASE_CONFIG["database"],
        "user": DATABASE_CONFIG["user"],
        "password": DATABASE_CONFIG["password"],
    }
