import logging
import os
import sys
from typing import ClassVar

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class DatabaseManager:

    CLEANUP_TABLE_ORDER: ClassVar[list[str]] = [
        "system.tickets",
        "system.moderation_results",
        "system.job_progress",
        "system.ai_logs",
        "system.moderation_logs",
        "system.logs",
        "system.security_logs",
        "system.email_logs",
        "system.jobs",
        "system.refresh_tokens",
        "system.banned_identifiers",
        "system.forbidden_words",
        "system.files_to_delete",
        "system.service_accounts",
        "system.nodes",
        "system.config",
        "audit_logs",
        "verification_codes",
        "user_sessions",
        "user_notification_settings",
        "notifications",
        "search_histories",
        "ingredient_suggestions",
        "data_correction_requests",
        "report_reason_assignments",
        "reports",
        "restaurant_edit_requests",
        "user_follows",
        "review_likes",
        "saved_dishes",
        "favorite_restaurants",
        "media_assets",
        "restaurant_tags",
        "dish_tags",
        "tags",
        "reviews",
        "dish_ingredients",
        "dish_section_assignments",
        "menu_sections",
        "dishes",
        "dish_variants",
        "dish_archetypes",
        "restaurant_opening_hours",
        "rejection_reasons",
        "restaurants",
        "users",
        "report_reason_definitions",
        "ingredients",
        "cuisine_types",
        "cities",
    ]

    def __init__(self, db: DatabaseConnection):
        self.db = db

    def cleanup(self, confirm: bool = False, auto_confirm: bool = False) -> None:
        if not confirm:
            print("\n" + "=" * 60)
            print("WARNING: This will delete ALL data in the database.")
            print("=" * 60)
            print("Do you want to proceed? (yes/no): ", end="")

            if auto_confirm and os.getenv("AUTO_CONFIRM_CLEANUP") == "true":
                response = "yes"
                print("yes (auto-confirmed from env)")
            else:
                response = input().strip().lower()

            if response not in ("yes", "y"):
                print("Cleanup cancelled. Exiting.")
                sys.exit(0)

        try:
            self._cleanup_query_based()
        except Exception as e:
            logger.error(f"Database cleanup failed: {e}", exc_info=True)
            logger.warning("Falling back to cascade strategy...")
            try:
                self._cleanup_cascade()
            except Exception as e2:
                logger.error(f"Cascade cleanup also failed: {e2}", exc_info=True)
                raise

    def _cleanup_query_based(self) -> None:
        logger.info("Querying database schema for table list...")

        tables = self.db.fetch_all(
            """
            SELECT schemaname, tablename
            FROM pg_tables
            WHERE schemaname IN ('public', 'system')
              AND tablename NOT LIKE '\\_\\_%' ESCAPE '\\'
            ORDER BY tablename
            """
        )

        if not tables:
            logger.warning("No tables found in database!")
            return

        table_list = [f"{schema}.{table}" if schema == "system" else table for schema, table in tables]

        logger.info(f"Found {len(table_list)} tables to truncate")

        logger.debug("Disabling FK constraints...")
        self.db.execute_query("SET session_replication_role = 'replica';")

        try:
            for table in table_list:
                logger.debug(f"Truncating {table}...")
                self.db.execute_query(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")

        finally:
            logger.debug("Re-enabling FK constraints...")
            self.db.execute_query("SET session_replication_role = 'origin';")

        self.db.commit()
        logger.info("Database cleanup completed")

    def _cleanup_cascade(self) -> None:
        logger.info(f"Truncating {len(self.CLEANUP_TABLE_ORDER)} tables with CASCADE...")

        for table in self.CLEANUP_TABLE_ORDER:
            logger.debug(f"Truncating {table}...")
            self.db.execute_query(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")

        self.db.commit()
        logger.info("Database cleanup completed")

    def get_statistics(self) -> dict[str, int]:
        tables = [
            "users",
            "restaurants",
            "dishes",
            "reviews",
            "notifications",
            "media_assets",
            "system.tickets",
        ]

        stats = {}
        for table in tables:
            try:
                count = self.db.fetch_val(f"SELECT COUNT(*) FROM {table}")
                stats[table] = count or 0
            except Exception as e:
                logger.debug(f"Could not get count for {table}: {e}")
                stats[table] = -1

        return stats

    def print_statistics(self) -> None:
        logger.info("\n" + "=" * 40)
        logger.info("DATABASE STATISTICS")
        logger.info("=" * 40)

        stats = self.get_statistics()
        for table, count in stats.items():
            if count == -1:
                logger.warning(f"{table.ljust(20)}: ERROR")
            else:
                logger.info(f"{table.ljust(20)}: {count:,}")

    def table_exists(self, table_name: str) -> bool:
        if "." in table_name:
            schema, table = table_name.split(".", 1)
        else:
            schema = "public"
            table = table_name

        result = self.db.fetch_one(
            """
            SELECT EXISTS (
                SELECT FROM pg_tables
                WHERE schemaname = %s AND tablename = %s
            )
            """,
            (schema, table),
        )

        return result[0] if result else False

    def table_has_data(self, table_name: str) -> bool:
        try:
            count = self.db.fetch_val(f"SELECT COUNT(*) FROM {table_name}")
            return (count or 0) > 0
        except Exception:
            return False
