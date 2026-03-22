import json
import logging
from typing import Any

logger = logging.getLogger(__name__)

def safe_json_loads(value: str | dict | None, default: Any = None) -> Any:
    if value is None or value == "":
        return default if default is not None else {}

    if isinstance(value, dict):
        return value

    try:
        return json.loads(value)
    except (json.JSONDecodeError, TypeError) as e:
        logger.warning(f"JSON parse error: {e}, value: {value}")
        return default if default is not None else {}

def safe_divide(numerator: float, denominator: float, default: float = 1.0) -> float:
    if denominator is None or denominator == 0:
        return default
    try:
        return numerator / denominator
    except (TypeError, ZeroDivisionError):
        return default
