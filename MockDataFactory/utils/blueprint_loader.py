"""
Blueprint Loader - Wczytywanie i walidacja plików JSON
"""

import json
import logging
from pathlib import Path
from typing import Dict, Any, List

class BlueprintLoader:
    """
    Wczytuje i waliduje pliki JSON z blueprintów
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        """
        Args:
            blueprints_dir: Ścieżka do folderu z blueprintami
        """
        self.blueprints_dir = Path(blueprints_dir)
        self.logger = logging.getLogger(__name__)

        if not self.blueprints_dir.exists():
            raise FileNotFoundError(f"Folder {blueprints_dir} nie istnieje!")

    def load_blueprint(self, filename: str) -> Dict[str, Any]:
        """
        Wczytuje pojedynczy blueprint

        Args:
            filename: Nazwa pliku (np. '01_city_rules.json')

        Returns:
            Słownik z danymi JSON
        """
        filepath = self.blueprints_dir / filename

        if not filepath.exists():
            raise FileNotFoundError(f"Blueprint {filename} nie istnieje!")

        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)

            self.logger.info(f"✅ Wczytano blueprint: {filename}")
            return data

        except json.JSONDecodeError as e:
            self.logger.error(f"❌ Błąd parsowania JSON w {filename}: {e}")
            raise
        except Exception as e:
            self.logger.error(f"❌ Błąd wczytywania {filename}: {e}")
            raise

    def load_all_blueprints(self) -> Dict[str, Dict[str, Any]]:
        """
        Wczytuje wszystkie blueprinty z folderu

        Returns:
            Słownik {nazwa_pliku: dane_json}
        """
        blueprints = {}

        for filepath in sorted(self.blueprints_dir.glob("*.json")):
            filename = filepath.name
            blueprints[filename] = self.load_blueprint(filename)

        self.logger.info(f"✅ Wczytano {len(blueprints)} blueprintów")
        return blueprints

    def get_blueprint_path(self, filename: str) -> Path:
        """Zwraca pełną ścieżkę do blueprintu"""
        return self.blueprints_dir / filename

    def validate_required_keys(self, data: Dict, required_keys: List[str], name: str) -> None:
        """
        Waliduje czy wymagane klucze istnieją w słowniku

        Args:
            data: Słownik do walidacji
            required_keys: Lista wymaganych kluczy
            name: Nazwa blueprintu (dla logów)
        """
        missing_keys = [key for key in required_keys if key not in data]

        if missing_keys:
            raise ValueError(f"Blueprint {name} brakuje kluczy: {missing_keys}")

        self.logger.info(f"✅ Walidacja {name} - OK")
