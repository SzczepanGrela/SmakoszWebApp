"""
Trigger Management Tool for Data Generation Performance

This module provides TriggerManager class to selectively disable/enable database triggers
during bulk data generation, significantly improving performance.

Usage:
    from tools.toggle_triggers import TriggerManager

    manager = TriggerManager(db_connection)
    manager.disable_heavy_triggers()  # Before bulk insert
    # ... generate data ...
    manager.enable_heavy_triggers()   # After generation
"""

import logging

logger = logging.getLogger(__name__)

class TriggerManager:
    """
    Manages database triggers for optimal data generation performance.

    Disables heavy triggers (audit, notifications) during bulk inserts
    to avoid massive performance overhead, then re-enables them.
    """

    # Trigger patterns to disable (high overhead during bulk operations)
    HEAVY_TRIGGER_PATTERNS = [
        'trg_audit_%',              # Audit logging triggers (v4.1)
        'trg_notify_%',             # Notification triggers (v4.0, v5.0 - rejection notifications)
        'trg_auto_queue_photo',     # Photo moderation queue (v4.0)
        'trg_review_%_status_change',  # Review visibility moderation triggers (v5.0 - state machine)
    ]

    # Triggers to NEVER disable (critical for data integrity)
    PROTECTED_TRIGGERS = [
        'sync_dish_%',                    # Dish dietary flag sync
        'trg_normalize_%',                # Phone number normalization
        'trg_enforce_primary_photo',      # Primary photo enforcement
        'trg_enforce_single_active_dish_photo',  # Highlander rule
        'trg_check_review_photo_limit',   # Spam prevention
        'trg_validate_opening_hours',     # Business logic validation
        'trg_update_timestamp',           # Timestamp maintenance
    ]

    def __init__(self, db_connection):
        """
        Initialize TriggerManager with database connection.

        Args:
            db_connection: DatabaseConnection instance from utils.db_connection
        """
        self.db = db_connection
        self.disabled_triggers: list[dict[str, str]] = []

    def disable_heavy_triggers(self) -> None:
        """
        Disable heavy triggers for bulk data generation performance.

        Disables:
            - Audit logging (trg_audit_*)
            - Notifications (trg_notify_* - including rejection notifications)
            - Photo moderation queue (trg_auto_queue_photo)
            - Review visibility moderation (trg_review_*_status_change - v5.0 state machine)

        Preserves:
            - Data integrity triggers (sync_dish_*, trg_normalize_*)
            - Business validation triggers
            - Structural triggers

        Performance Impact:
            Typical speedup: 5-10x faster for bulk inserts
            Example: Phase 5 (2.5M reviews): 20min -> 5min

        Note (v5.0):
            During bulk generation with time-based pending logic (last 7 days = pending),
            most reviews are >7 days old and inserted as 'approved' directly.
            Disabling visibility triggers avoids unnecessary evaluate_review_visibility()
            calls on millions of already-approved records.
        """
        logger.info("Analyzing database triggers...")

        # Query all triggers matching heavy patterns
        # Note: Escape % as %% for psycopg2 (% is parameter placeholder)
        query = """
            SELECT
                t.tgname AS trigger_name,
                c.relname AS table_name,
                n.nspname AS schema_name
            FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE n.nspname = 'public'
              AND NOT t.tgisinternal
              AND (
                  {patterns}
              )
            ORDER BY c.relname, t.tgname;
        """.format(
            patterns=" OR ".join([
                f"t.tgname LIKE '{pattern.replace('%', '%%')}'"
                for pattern in self.HEAVY_TRIGGER_PATTERNS
            ])
        )

        triggers = self.db.fetch_all(query)

        # Filter out protected triggers
        triggers_to_disable = [
            t for t in triggers
            if not any(
                t[0].startswith(pattern.rstrip('%')) # Access by index 0 (trigger_name)
                for pattern in self.PROTECTED_TRIGGERS
            )
        ]

        if not triggers_to_disable:
            logger.warning("WARNING: No heavy triggers found to disable.")
            return

        logger.info(f"Found {len(triggers_to_disable)} heavy triggers to disable:")

        # Disable each trigger
        disabled_count = 0
        for trigger in triggers_to_disable:
            trigger_name = trigger[0] # index 0
            table_name = trigger[1] # index 1

            try:
                disable_sql = f"""
                    ALTER TABLE {table_name}
                    DISABLE TRIGGER {trigger_name};
                """
                self.db.execute_query(disable_sql)

                # Track disabled triggers for re-enabling later
                self.disabled_triggers.append({
                    'trigger_name': trigger_name,
                    'table_name': table_name
                })

                disabled_count += 1
                logger.debug(f"  [OK] Disabled {trigger_name} on {table_name}")

            except Exception as e:
                logger.error(f"  [FAIL] Failed to disable {trigger_name}: {e}")

        self.db.commit()

        logger.info(f"Successfully disabled {disabled_count}/{len(triggers_to_disable)} triggers")
        logger.info("Performance mode activated - bulk inserts will be much faster!")

    def enable_heavy_triggers(self) -> None:
        """
        Re-enable all triggers that were disabled by disable_heavy_triggers().

        Should be called after bulk data generation is complete to restore
        normal database behavior.

        Note: Only re-enables triggers that were explicitly disabled by this
        instance to avoid accidentally enabling manually disabled triggers.
        """
        if not self.disabled_triggers:
            logger.warning("WARNING: No triggers to re-enable (none were disabled).")
            return

        logger.info(f"Re-enabling {len(self.disabled_triggers)} triggers...")

        enabled_count = 0
        failed_count = 0

        for trigger in self.disabled_triggers:
            trigger_name = trigger['trigger_name']
            table_name = trigger['table_name']

            try:
                enable_sql = f"""
                    ALTER TABLE {table_name}
                    ENABLE TRIGGER {trigger_name};
                """
                self.db.execute_query(enable_sql)

                enabled_count += 1
                logger.debug(f"  [OK] Enabled {trigger_name} on {table_name}")

            except Exception as e:
                logger.error(f"  [FAIL] Failed to enable {trigger_name}: {e}")
                failed_count += 1

        self.db.commit()

        if failed_count == 0:
            logger.info(f"Successfully re-enabled all {enabled_count} triggers")
        else:
            logger.warning(
                f"WARNING: Re-enabled {enabled_count} triggers, "
                f"but {failed_count} failed"
            )

        # Clear tracking list
        self.disabled_triggers = []

        logger.info("Normal database mode restored")

    def get_trigger_status(self) -> dict[str, list[str]]:
        """
        Get current status of all triggers in the database.

        Returns:
            dict: {
                'enabled': [list of enabled trigger names],
                'disabled': [list of disabled trigger names]
            }

        Useful for debugging and monitoring trigger state.
        """
        query = """
            SELECT
                t.tgname AS trigger_name,
                c.relname AS table_name,
                CASE
                    WHEN t.tgenabled = 'O' THEN 'enabled'
                    WHEN t.tgenabled = 'D' THEN 'disabled'
                    ELSE 'unknown'
                END AS status
            FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE n.nspname = 'public'
              AND NOT t.tgisinternal
            ORDER BY c.relname, t.tgname;
        """

        triggers = self.db.fetch_all(query)

        enabled = []
        disabled = []

        for trigger in triggers:
            # trigger is a tuple: (trigger_name, table_name, status)
            name = f"{trigger[1]}.{trigger[0]}"
            if trigger[2] == 'enabled':
                enabled.append(name)
            elif trigger[2] == 'disabled':
                disabled.append(name)

        return {
            'enabled': enabled,
            'disabled': disabled
        }

# Example usage
if __name__ == '__main__':
    from config import get_connection_params
    from utils.db_connection import DatabaseConnection

    # Setup logging
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(levelname)s - %(message)s'
    )

    # Connect to database
    db = DatabaseConnection(get_connection_params())

    try:
        # Create trigger manager
        manager = TriggerManager(db)

        # Get initial status
        status = manager.get_trigger_status()
        print("\nInitial state:")
        print(f"  Enabled triggers: {len(status['enabled'])}")
        print(f"  Disabled triggers: {len(status['disabled'])}")

        # Disable heavy triggers
        print("\nDisabling heavy triggers...")
        manager.disable_heavy_triggers()

        # Get status after disable
        status = manager.get_trigger_status()
        print("\nAfter disable:")
        print(f"  Enabled triggers: {len(status['enabled'])}")
        print(f"  Disabled triggers: {len(status['disabled'])}")

        # Re-enable triggers
        print("\nRe-enabling triggers...")
        manager.enable_heavy_triggers()

        # Get final status
        status = manager.get_trigger_status()
        print("\nFinal state:")
        print(f"  Enabled triggers: {len(status['enabled'])}")
        print(f"  Disabled triggers: {len(status['disabled'])}")

    finally:
        db.close()
