"""
Database Manager for Data Generation Pipeline

Provides:
- Database cleanup strategies (query-based, cascade)
- Statistics reporting
- Lifecycle management

Fixes bugs:
- cleanup_database() unassigned response variable
- Hardcoded 32-table list -> automatic discovery
"""

import logging
import os
import sys
from typing import ClassVar

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

class DatabaseCleanupStrategy:
    """Strategy pattern for different database cleanup approaches."""

    @staticmethod
    def query_based(db: DatabaseConnection) -> None:
        """
        Query information_schema for table list with automatic FK handling.

        Advantages:
        - No manual table maintenance
        - Survives schema changes
        - Automatic FK dependency resolution

        Implementation:
        1. Query pg_tables for all user tables
        2. Disable FK checks temporarily (session_replication_role trick)
        3. TRUNCATE all tables with RESTART IDENTITY CASCADE
        4. Re-enable FK checks

        Note: Uses PostgreSQL-specific session_replication_role feature
        """
        logger.info("Querying database schema for table list...")

        # Get all user tables from public and system schemas
        # Exclude EF Core migration history table
        tables = db.fetch_all(
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

        # Format table names (schema.table for system schema)
        table_list = [f"{schema}.{table}" if schema == "system" else table for schema, table in tables]

        logger.info(f"Found {len(table_list)} tables to truncate")

        # Disable FK checks temporarily (PostgreSQL trick for bulk operations)
        # session_replication_role = 'replica' bypasses triggers and FK checks
        logger.debug("Disabling FK constraints...")
        db.execute_query("SET session_replication_role = 'replica';")

        try:
            # Truncate all tables
            for table in table_list:
                logger.debug(f"Truncating {table}...")
                db.execute_query(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")

        finally:
            # ALWAYS restore FK checks (even if truncate fails)
            logger.debug("Re-enabling FK constraints...")
            db.execute_query("SET session_replication_role = 'origin';")

        db.commit()
        logger.info("Database cleanup completed")

    @staticmethod
    def cascade_truncate(db: DatabaseConnection, table_order: list[str]) -> None:
        """
        Truncate tables in specified order with CASCADE.

        Advantages:
        - Predictable order
        - Explicit control
        - Fast (no FK check overhead with CASCADE)

        Disadvantages:
        - Requires manual maintenance if schema changes
        - List can become outdated

        Args:
            db: Database connection
            table_order: List of tables in reverse FK dependency order
        """
        logger.info(f"Truncating {len(table_order)} tables with CASCADE...")

        for table in table_order:
            logger.debug(f"Truncating {table}...")
            db.execute_query(f"TRUNCATE TABLE {table} RESTART IDENTITY CASCADE")

        db.commit()
        logger.info("Database cleanup completed")

class DatabaseManager:
    """
    Manages database lifecycle for data generation.

    Responsibilities:
    - Database cleanup (query-based or cascade strategy)
    - Statistics collection
    - Table validation
    """

    # Fallback table order for cascade strategy (reverse FK dependency order)
    # This is kept as fallback if query-based fails
    CLEANUP_TABLE_ORDER: ClassVar[list[str]] = [
        # System tables
        "system.tickets",
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
        # Audit
        "audit_logs",
        # User-related
        "verification_codes",
        "user_sessions",
        "user_notification_settings",
        "notifications",
        "search_histories",
        # Reports and corrections
        "ingredient_suggestions",
        "data_correction_requests",
        "report_reason_assignments",
        "reports",
        "restaurant_edit_requests",
        # Social graph
        "user_follows",
        "review_likes",
        # Saved content
        "saved_dishes",
        "favorite_restaurants",
        # Media
        "media_assets",
        # Tags
        "restaurant_tags",
        "dish_tags",
        "tags",
        # Reviews
        "reviews",
        # Dishes
        "dish_ingredients",
        "dish_section_assignments",
        "menu_sections",
        "dishes",
        "dish_variants",
        "dish_archetypes",
        # Restaurants
        "restaurant_opening_hours",
        "rejection_reasons",
        "restaurants",
        # Users
        "users",
        # Base data
        "report_reason_definitions",
        "ingredients",
        "cuisine_types",
        "cities",
    ]

    def __init__(self, db: DatabaseConnection, strategy: str = "query_based"):
        """
        Initialize DatabaseManager.

        Args:
            db: Database connection
            strategy: Cleanup strategy ("query_based" or "cascade")
        """
        self.db = db
        self.strategy = strategy

        if strategy not in ("query_based", "cascade"):
            raise ValueError(f"Unknown cleanup strategy: {strategy}. Must be 'query_based' or 'cascade'")

    def cleanup(self, confirm: bool = False, auto_confirm: bool = False) -> None:
        """
        Clean all data from database.

        FIXES BUG from original cleanup_database():
        - response variable is now properly assigned from input()
        - Falls through correctly from env var check to user prompt

        Args:
            confirm: If True, skip confirmation prompt
            auto_confirm: If True, check AUTO_CONFIRM_CLEANUP env var

        Raises:
            SystemExit: If user cancels cleanup
        """
        # Confirmation logic
        if not confirm:
            print("\n" + "=" * 60)
            print("WARNING: This will delete ALL data in the database.")
            print("=" * 60)
            print("Do you want to proceed? (yes/no): ", end="")

            # Check env var first
            if auto_confirm and os.getenv("AUTO_CONFIRM_CLEANUP") == "true":
                response = "yes"
                print("yes (auto-confirmed from env)")
            else:
                # FIX: Actually read user input!
                response = input().strip().lower()

            if response not in ("yes", "y"):
                print("Cleanup cancelled. Exiting.")
                sys.exit(0)

        # Execute cleanup with selected strategy
        try:
            if self.strategy == "query_based":
                DatabaseCleanupStrategy.query_based(self.db)
            elif self.strategy == "cascade":
                DatabaseCleanupStrategy.cascade_truncate(self.db, self.CLEANUP_TABLE_ORDER)
        except Exception as e:
            logger.error(f"Database cleanup failed: {e}", exc_info=True)

            # If query-based failed, try cascade as fallback
            if self.strategy == "query_based":
                logger.warning("Falling back to cascade strategy...")
                try:
                    DatabaseCleanupStrategy.cascade_truncate(self.db, self.CLEANUP_TABLE_ORDER)
                except Exception as e2:
                    logger.error(f"Cascade cleanup also failed: {e2}", exc_info=True)
                    raise

    def get_statistics(self) -> dict[str, int]:
        """
        Get row counts for key tables.

        Returns:
            Dictionary mapping table name to row count
        """
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
                stats[table] = -1  # Indicate error

        return stats

    def print_statistics(self) -> None:
        """Print formatted database statistics."""
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
        """
        Check if a table exists.

        Args:
            table_name: Name of table (can include schema, e.g. "system.config")

        Returns:
            True if table exists, False otherwise
        """
        # Split schema and table if provided
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
        """
        Check if a table has any rows.

        Args:
            table_name: Name of table

        Returns:
            True if table has data, False otherwise
        """
        try:
            count = self.db.fetch_val(f"SELECT COUNT(*) FROM {table_name}")
            return (count or 0) > 0
        except Exception:
            return False
