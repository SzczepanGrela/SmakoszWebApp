import os
import psycopg2
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# DB Configuration
DB_HOST = os.getenv("DB_HOST", "localhost")
DB_PORT = os.getenv("DB_PORT", "5432")
DB_NAME = os.getenv("DB_NAME", "mockdatadb")
DB_USER = os.getenv("DB_USER", "postgres")
DB_PASSWORD = os.getenv("DB_PASSWORD")

def apply_schema():
    print(f"Connecting to database {DB_NAME} on {DB_HOST}:{DB_PORT}...")
    try:
        conn = psycopg2.connect(
            host=DB_HOST,
            port=DB_PORT,
            dbname=DB_NAME,
            user=DB_USER,
            password=DB_PASSWORD
        )
        conn.autocommit = True
        cursor = conn.cursor()
        
        # Determine project root directory (parent of 'tools')
        current_dir = os.path.dirname(os.path.abspath(__file__))
        project_root = os.path.dirname(current_dir)
        schema_path = os.path.join(project_root, "sql", "schema_postgresql.sql")
        
        print(f"Reading schema from {schema_path}...")
        
        with open(schema_path, "r", encoding="utf-8") as f:
            schema_sql = f.read()
            
        print("Applying schema (this may take a moment)...")
        cursor.execute(schema_sql)
        
        print("Schema applied successfully.")
        
        cursor.close()
        conn.close()
        
    except Exception as e:
        print(f"Error applying schema: {e}")
        exit(1)

if __name__ == "__main__":
    apply_schema()
