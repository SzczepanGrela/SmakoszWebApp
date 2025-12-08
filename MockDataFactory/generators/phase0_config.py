import json
import logging
import time
from pathlib import Path

from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

def generate_system_config(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    start_time = time.time()
    logger.info("Phase 0: Initializing System Configuration...")

    if cleanup:
        logger.info("Cleaning up system.config table...")
        # We don't TRUNCATE to preserve manual overrides if needed, 
        # but for fresh mock generation, TRUNCATE is safer.
        db.execute_query("TRUNCATE TABLE system.config CASCADE")

    config_path = Path(blueprints_dir) / "system_config.json"
    
    try:
        with open(config_path, encoding='utf-8') as f:
            data = json.load(f)
            config_items = data.get("SYSTEM_CONFIG", {})
    except FileNotFoundError:
        logger.error(f"Config file not found: {config_path}")
        return

    insert_data = []
    for key, details in config_items.items():
        insert_data.append({
            "key": key,
            "value": details.get("value"),
            "description": details.get("description"),
            "is_secret": False, # Default, can be overridden if needed
            "is_public": details.get("is_public", False)
        })

    if insert_data:
        # We use insert_bulk which maps to INSERT ... VALUES
        # Since we truncated, bulk insert is fine.
        db.insert_bulk("system.config", insert_data)

    duration = time.time() - start_time
    logger.info(f"Initialized {len(insert_data)} system settings in {duration:.2f}s")
