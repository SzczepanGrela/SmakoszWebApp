import os

import psycopg2
from dotenv import load_dotenv

load_dotenv()

DB_HOST = os.getenv("DB_HOST", "localhost")
DB_PORT = os.getenv("DB_PORT", "5432")
DB_NAME = os.getenv("DB_NAME", "smakosz_db")
DB_USER = os.getenv("DB_USER", "postgres")
DB_PASSWORD = os.getenv("DB_PASSWORD")

def seed_reference_data():
    """Seed reference data from JSON blueprints after schema creation."""
    print("Seeding reference data from blueprints...")

    import sys

    # Add project root to path for imports
    current_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(current_dir)
    sys.path.insert(0, project_root)

    from utils.blueprint_loader import BlueprintLoader
    from config.database import get_connection_params

    loader = BlueprintLoader(blueprints_dir=os.path.join(project_root, "blueprints"))

    # Get connection from environment
    connection_params = get_connection_params()

    try:
        conn = psycopg2.connect(**connection_params)
        cursor = conn.cursor()

        # Load rejection_reasons
        rejection_data = loader.load_blueprint("rejection_reasons.json")
        reasons_list = rejection_data.get("rejection_reasons", [])

        if reasons_list:
            # Validate required keys
            required_keys = ["reason_code", "category", "admin_label", "user_message_template"]
            for reason in reasons_list:
                loader.validate_required_keys(reason, required_keys, "rejection_reason")

            # Insert with ON CONFLICT handling
            for reason in reasons_list:
                sql = """
                    INSERT INTO rejection_reasons (reason_code, category, admin_label, user_message_template, is_active)
                    VALUES (%(reason_code)s, %(category)s, %(admin_label)s, %(user_message_template)s, %(is_active)s)
                    ON CONFLICT (reason_code) DO NOTHING
                """
                params = {
                    "reason_code": reason["reason_code"],
                    "category": reason["category"],
                    "admin_label": reason["admin_label"],
                    "user_message_template": reason["user_message_template"],
                    "is_active": reason.get("is_active", True)
                }
                cursor.execute(sql, params)

            conn.commit()
            print(f"  -> Seeded {len(reasons_list)} rejection_reasons")

        cursor.close()
        conn.close()

    except Exception as e:
        print(f"Error seeding reference data: {e}")
        if 'conn' in locals():
            conn.rollback()
            conn.close()
        raise

def apply_schema():
    print(f"Connecting to server on {DB_HOST}:{DB_PORT} to ensure DB exists...")
    try:
        # 1. Connect to default 'postgres' database to create the target DB if missing
        sys_conn = psycopg2.connect(
            host=DB_HOST, port=DB_PORT, dbname="postgres", user=DB_USER, password=DB_PASSWORD
        )
        sys_conn.autocommit = True
        sys_cursor = sys_conn.cursor()

        # Check if database exists
        sys_cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (DB_NAME,))
        exists = sys_cursor.fetchone()

        if exists:
            print(f"Database '{DB_NAME}' exists. Dropping...")
            # Terminate other connections first
            sys_cursor.execute(f"""
                SELECT pg_terminate_backend(pg_stat_activity.pid)
                FROM pg_stat_activity
                WHERE pg_stat_activity.datname = '{DB_NAME}'
                AND pid <> pg_backend_pid();
            """)
            sys_cursor.execute(f"DROP DATABASE {DB_NAME}")
            print(f"Database '{DB_NAME}' dropped.")

        print(f"Creating database '{DB_NAME}'...")
        sys_cursor.execute(f"CREATE DATABASE {DB_NAME}")
        print(f"Database '{DB_NAME}' created successfully.")

        sys_cursor.close()
        sys_conn.close()

        # 2. Connect to the target database
        print(f"Connecting to database {DB_NAME}...")
        conn = psycopg2.connect(host=DB_HOST, port=DB_PORT, dbname=DB_NAME, user=DB_USER, password=DB_PASSWORD)
        conn.autocommit = True
        cursor = conn.cursor()

        current_dir = os.path.dirname(os.path.abspath(__file__))
        project_root = os.path.dirname(current_dir)
        
        # Define schema files in execution order
        schema_files = [
            # "sql/modules/00_cleanup.sql", # Skipped (Fresh DB)
            "sql/modules/01_tables.sql",
            "sql/modules/02_moderation_system.sql",  # Draft/Published moderation workflow
            "sql/modules/03_audit_system.sql",  # Universal audit logging
            "sql/modules/04_search_optimization.sql",  # Full-text search (Polish)
            "sql/modules/05_infrastructure.sql",  # GPU nodes, task queue, user sessions
            "sql/modules/06_worker_api.sql",  # Worker API (v7.1)
            "sql/views/01_views.sql",
            "sql/views/02_analytics_views.sql",  # Analytics views (v5.3)
            "sql/functions/01_functions.sql",
            "sql/functions/02_transactions.sql",  # Stored procedures
            "sql/triggers/01_triggers.sql",
            "sql/triggers/02_business_logic.sql",  # Photo sync & review approval workflow
            "sql/triggers/03_moderation_triggers.sql",  # State machine moderation triggers
            "sql/triggers/04_integrity_triggers.sql",  # Integrity triggers (v7.1)
            "sql/triggers/06_review_restrictions.sql",  # Business rule: Only users can review
        ]

        print("Applying modular schema...")
        
        for rel_path in schema_files:
            file_path = os.path.join(project_root, rel_path)
            print(f"  -> Executing {rel_path}...")
            
            if not os.path.exists(file_path):
                print(f"Error: File not found: {file_path}")
                exit(1)

            with open(file_path, encoding="utf-8") as f:
                sql_content = f.read()
                
            cursor.execute(sql_content)

        print("Schema applied successfully.")

        cursor.close()
        conn.close()

        # Seed reference data from blueprints
        seed_reference_data()

    except Exception as e:
        print(f"Error applying schema: {e}")
        exit(1)

if __name__ == "__main__":
    apply_schema()
