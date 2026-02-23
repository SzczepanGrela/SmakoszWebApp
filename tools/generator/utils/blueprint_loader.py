import json
import logging
from pathlib import Path
from typing import Any

class BlueprintLoader:
    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = Path(blueprints_dir)
        self.logger = logging.getLogger(__name__)

        if not self.blueprints_dir.exists():
            raise FileNotFoundError(f"Directory {blueprints_dir} does not exist!")

    def load_blueprint(self, filename: str) -> dict[str, Any]:
        filepath = self.blueprints_dir / filename

        if not filepath.exists():
            raise FileNotFoundError(f"Blueprint {filename} does not exist!")

        try:
            with open(filepath, encoding="utf-8") as f:
                data = json.load(f)

            self.logger.debug(f"Loaded blueprint: {filename}")
            return data

        except json.JSONDecodeError as e:
            self.logger.error(f"JSON parse error in {filename}: {e}")
            raise
        except Exception as e:
            self.logger.error(f"Error loading {filename}: {e}")
            raise

    def load_all_blueprints(self) -> dict[str, dict[str, Any]]:
        blueprints = {}

        for filepath in sorted(self.blueprints_dir.glob("*.json")):
            filename = filepath.name
            blueprints[filename] = self.load_blueprint(filename)

        self.logger.info(f"Loaded {len(blueprints)} blueprints")
        return blueprints

    def get_blueprint_path(self, filename: str) -> Path:
        return self.blueprints_dir / filename

    def validate_required_keys(self, data: dict, required_keys: list[str], name: str) -> None:
        missing_keys = [key for key in required_keys if key not in data]

        if missing_keys:
            raise ValueError(f"Blueprint {name} missing keys: {missing_keys}")

        self.logger.debug(f"Validation {name} - OK")
