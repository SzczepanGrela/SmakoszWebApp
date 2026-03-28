import os
from pathlib import Path

try:
    from dotenv import load_dotenv

    root_env = Path(__file__).parent.parent.parent.parent / ".env"
    load_dotenv(root_env)
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
    return {
        "host": DATABASE_CONFIG["host"],
        "port": DATABASE_CONFIG["port"],
        "dbname": DATABASE_CONFIG["database"],
        "user": DATABASE_CONFIG["user"],
        "password": DATABASE_CONFIG["password"],
    }
