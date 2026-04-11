import csv
import sqlite3
import sys
from pathlib import Path

BLUEPRINTS_DIR = Path(__file__).parent
SCHEMA_FILE = BLUEPRINTS_DIR / "schema.sql"
DB_FILE = BLUEPRINTS_DIR / "blueprints.db"
DATA_DIR = BLUEPRINTS_DIR / "data"

TABLE_LOAD_ORDER = [
    "archetypes",
    "variants",
    "ingredients",
    "variant_ingredients",
    "sections",
    "themes",
    "theme_name_parts",
    "theme_sections",
    "theme_archetype_section",
    "dietary_keywords",
]


def rebuild():
    if DB_FILE.exists():
        DB_FILE.unlink()

    schema_sql = SCHEMA_FILE.read_text(encoding="utf-8")

    conn = sqlite3.connect(str(DB_FILE))
    conn.execute("PRAGMA foreign_keys = ON")
    conn.executescript(schema_sql)

    try:
        for table_name in TABLE_LOAD_ORDER:
            csv_path = DATA_DIR / f"{table_name}.csv"
            if not csv_path.exists():
                print(f"SKIP {table_name}.csv (not found)")
                continue

            with open(csv_path, encoding="utf-8", newline="") as f:
                reader = csv.DictReader(f)
                columns = reader.fieldnames
                placeholders = ",".join(["?"] * len(columns))
                col_names = ",".join(columns)
                sql = f"INSERT INTO {table_name} ({col_names}) VALUES ({placeholders})"

                row_count = 0
                for row_num, row in enumerate(reader, start=2):
                    values = []
                    for col in columns:
                        val = row[col]
                        if val == "":
                            values.append(None)
                        else:
                            values.append(val)
                    try:
                        conn.execute(sql, values)
                        row_count += 1
                    except sqlite3.IntegrityError as e:
                        print(f"FK VIOLATION in {table_name}.csv row {row_num}: {e}")
                        print(f"  Values: {dict(zip(columns, values))}")
                        conn.close()
                        DB_FILE.unlink()
                        sys.exit(1)

            print(f"  {table_name}: {row_count} rows")

        conn.commit()
    except Exception:
        conn.close()
        if DB_FILE.exists():
            DB_FILE.unlink()
        raise

    row_counts = {}
    for table_name in TABLE_LOAD_ORDER:
        count = conn.execute(f"SELECT COUNT(*) FROM {table_name}").fetchone()[0]
        row_counts[table_name] = count

    conn.close()

    print(f"\nRebuilt {DB_FILE.name}: {sum(row_counts.values())} total rows across {len(row_counts)} tables.")
    return row_counts


if __name__ == "__main__":
    print(f"Rebuilding {DB_FILE} from schema + CSV...")
    rebuild()
