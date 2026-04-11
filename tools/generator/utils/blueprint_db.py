import json
import logging
import sqlite3
from pathlib import Path
from typing import Any

logger = logging.getLogger(__name__)

BLUEPRINTS_DIR = Path(__file__).parent.parent / "blueprints"
SCHEMA_FILE = BLUEPRINTS_DIR / "schema.sql"
DB_FILE = BLUEPRINTS_DIR / "blueprints.db"
DATA_DIR = BLUEPRINTS_DIR / "data"


def _needs_rebuild() -> bool:
    if not DB_FILE.exists():
        return True
    db_mtime = DB_FILE.stat().st_mtime
    if SCHEMA_FILE.stat().st_mtime > db_mtime:
        return True
    for csv_file in DATA_DIR.glob("*.csv"):
        if csv_file.stat().st_mtime > db_mtime:
            return True
    return False


def _ensure_db():
    if _needs_rebuild():
        logger.info("Blueprint DB missing or stale, rebuilding...")
        from blueprints.rebuild_sqlite import rebuild
        rebuild()


class BlueprintDB:
    def __init__(self):
        _ensure_db()
        self._conn = sqlite3.connect(str(DB_FILE))
        self._conn.row_factory = sqlite3.Row
        self._conn.execute("PRAGMA foreign_keys = ON")

    def close(self):
        self._conn.close()

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()

    def _query(self, sql: str, params: tuple = ()) -> list[sqlite3.Row]:
        return self._conn.execute(sql, params).fetchall()

    def _query_one(self, sql: str, params: tuple = ()) -> sqlite3.Row | None:
        return self._conn.execute(sql, params).fetchone()

    def get_archetype_names(self) -> list[str]:
        rows = self._query("SELECT name FROM archetypes ORDER BY id")
        return [r["name"] for r in rows]

    def get_archetype_by_name(self, name: str) -> dict[str, Any] | None:
        row = self._query_one("SELECT * FROM archetypes WHERE name = ?", (name,))
        if row is None:
            return None
        return dict(row)


    def get_variants_for_archetype(self, archetype_name: str) -> list[dict[str, Any]]:
        rows = self._query(
            "SELECT v.* FROM variants v "
            "JOIN archetypes a ON v.archetype_id = a.id "
            "WHERE a.name = ?",
            (archetype_name,),
        )
        result = []
        for r in rows:
            d = dict(r)
            d["characteristics"] = json.loads(d["characteristics"])
            if d["weights"]:
                d["weights"] = json.loads(d["weights"])
            result.append(d)
        return result

    def get_all_variants_with_details(self) -> list[dict[str, Any]]:
        rows = self._query(
            "SELECT v.*, a.name AS archetype_name, a.base_price_mean, a.base_price_stdev, "
            "a.pixabay_term AS archetype_pixabay_term, a.cuisine_tag "
            "FROM variants v "
            "JOIN archetypes a ON v.archetype_id = a.id "
            "ORDER BY a.id, v.id"
        )
        result = []
        for r in rows:
            d = dict(r)
            d["characteristics"] = json.loads(d["characteristics"])
            if d["weights"]:
                d["weights"] = json.loads(d["weights"])
            result.append(d)
        return result

    def get_variant_ingredients(self, variant_id: int) -> list[str]:
        rows = self._query(
            "SELECT i.name FROM variant_ingredients vi "
            "JOIN ingredients i ON vi.ingredient_id = i.id "
            "WHERE vi.variant_id = ?",
            (variant_id,),
        )
        return [r["name"] for r in rows]

    def get_variant_by_name(self, archetype_name: str, variant_name: str) -> dict[str, Any] | None:
        row = self._query_one(
            "SELECT v.*, a.name AS archetype_name, a.base_price_mean, a.base_price_stdev, "
            "a.cuisine_tag "
            "FROM variants v "
            "JOIN archetypes a ON v.archetype_id = a.id "
            "WHERE a.name = ? AND v.name = ?",
            (archetype_name, variant_name),
        )
        if row is None:
            return None
        d = dict(row)
        d["characteristics"] = json.loads(d["characteristics"])
        if d["weights"]:
            d["weights"] = json.loads(d["weights"])
        return d


    def get_all_ingredients(self) -> list[dict[str, Any]]:
        rows = self._query("SELECT * FROM ingredients ORDER BY id")
        return [dict(r) for r in rows]

    def get_ingredient_names(self) -> list[str]:
        rows = self._query("SELECT name FROM ingredients ORDER BY name")
        return [r["name"] for r in rows]

    def get_ingredient_dietary_flags(self, ingredient_names: list[str]) -> dict[str, dict[str, bool]]:
        if not ingredient_names:
            return {}
        placeholders = ",".join(["?"] * len(ingredient_names))
        rows = self._query(
            f"SELECT name, is_meat, is_dairy, is_egg, is_gluten "
            f"FROM ingredients WHERE name IN ({placeholders})",
            tuple(ingredient_names),
        )
        return {
            r["name"]: {
                "is_meat": bool(r["is_meat"]),
                "is_dairy": bool(r["is_dairy"]),
                "is_egg": bool(r["is_egg"]),
                "is_gluten": bool(r["is_gluten"]),
            }
            for r in rows
        }


    def get_theme_names(self) -> list[str]:
        rows = self._query("SELECT name FROM themes ORDER BY id")
        return [r["name"] for r in rows]

    def get_themes(self) -> list[dict[str, Any]]:
        rows = self._query("SELECT * FROM themes ORDER BY id")
        return [dict(r) for r in rows]

    def get_theme_by_name(self, name: str) -> dict[str, Any] | None:
        row = self._query_one("SELECT * FROM themes WHERE name = ?", (name,))
        if row is None:
            return None
        return dict(row)

    def get_theme_name_parts(self, theme_name: str) -> dict[int, list[dict[str, Any]]]:
        rows = self._query(
            "SELECT tnp.part, tnp.name, tnp.chance "
            "FROM theme_name_parts tnp "
            "JOIN themes t ON tnp.theme_id = t.id "
            "WHERE t.name = ? "
            "ORDER BY tnp.part, tnp.id",
            (theme_name,),
        )
        parts: dict[int, list[dict[str, Any]]] = {1: [], 2: []}
        for r in rows:
            parts[r["part"]].append({"name": r["name"], "chance": r["chance"]})
        return parts

    def get_theme_sections(self, theme_name: str) -> list[dict[str, Any]]:
        rows = self._query(
            "SELECT s.name, ts.chance, ts.limit_min, ts.limit_max "
            "FROM theme_sections ts "
            "JOIN themes t ON ts.theme_id = t.id "
            "JOIN sections s ON ts.section_id = s.id "
            "WHERE t.name = ?",
            (theme_name,),
        )
        return [dict(r) for r in rows]

    def get_dish_count_params(self, theme_name: str) -> dict[str, int]:
        row = self._query_one(
            "SELECT dish_count_mean, dish_count_sigma FROM themes WHERE name = ?",
            (theme_name,),
        )
        if row is None:
            return {"mean": 20, "sigma": 5}
        return {"mean": row["dish_count_mean"], "sigma": row["dish_count_sigma"]}

    def get_tier_probabilities(self, theme_name: str) -> dict[str, float]:
        row = self._query_one(
            "SELECT budget_prob, casual_prob, fine_dining_prob FROM themes WHERE name = ?",
            (theme_name,),
        )
        if row is None:
            return {"Budget": 0.2, "Casual": 0.7, "Fine Dining": 0.1}
        return {
            "Budget": row["budget_prob"],
            "Casual": row["casual_prob"],
            "Fine Dining": row["fine_dining_prob"],
        }


    def get_theme_archetypes(self, theme_name: str) -> list[str]:
        rows = self._query(
            "SELECT DISTINCT a.name "
            "FROM theme_archetype_section tas "
            "JOIN themes t ON tas.theme_id = t.id "
            "JOIN archetypes a ON tas.archetype_id = a.id "
            "WHERE t.name = ?",
            (theme_name,),
        )
        return [r["name"] for r in rows]

    def get_sections_for_dish(self, theme_name: str, archetype_name: str) -> list[str]:
        rows = self._query(
            "SELECT s.name "
            "FROM theme_archetype_section tas "
            "JOIN themes t ON tas.theme_id = t.id "
            "JOIN archetypes a ON tas.archetype_id = a.id "
            "JOIN sections s ON tas.section_id = s.id "
            "WHERE t.name = ? AND a.name = ?",
            (theme_name, archetype_name),
        )
        return [r["name"] for r in rows]


    def get_ingredient_pixabay_terms(self) -> dict[str, str | None]:
        rows = self._query("SELECT name, pixabay_term FROM ingredients")
        return {r["name"]: r["pixabay_term"] for r in rows}

    def get_theme_pixabay_terms(self) -> dict[str, str | None]:
        rows = self._query("SELECT name, pixabay_term FROM themes")
        return {r["name"]: r["pixabay_term"] for r in rows}

    def get_variant_pixabay_terms(self) -> list[dict[str, str | None]]:
        rows = self._query(
            "SELECT v.name AS variant_name, v.pixabay_term, "
            "a.name AS archetype_name, a.pixabay_term AS archetype_pixabay_term "
            "FROM variants v "
            "JOIN archetypes a ON v.archetype_id = a.id"
        )
        return [dict(r) for r in rows]


    def get_dietary_keywords(self) -> dict[str, list[str]]:
        rows = self._query("SELECT category, keyword FROM dietary_keywords ORDER BY category, keyword")
        result: dict[str, list[str]] = {}
        for r in rows:
            result.setdefault(r["category"], []).append(r["keyword"])
        return result
