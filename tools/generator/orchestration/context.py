from __future__ import annotations

from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any

from utils.db_connection import DatabaseConnection

if TYPE_CHECKING:
    from orchestration.phase import PhaseRegistry

@dataclass
class ExecutionContext:

    db: DatabaseConnection
    config: dict[str, Any]
    phase_registry: PhaseRegistry
    completed_phases: set[str] = field(default_factory=set)
    shared_cache: dict[str, Any] = field(default_factory=dict)

    def mark_completed(self, phase_id: str) -> None:
        self.completed_phases.add(phase_id)

    def is_completed(self, phase_id: str) -> bool:
        return phase_id in self.completed_phases

    def cache_get(self, key: str, default: Any = None) -> Any:
        return self.shared_cache.get(key, default)

    def cache_set(self, key: str, value: Any) -> None:
        self.shared_cache[key] = value

    def cache_clear(self) -> None:
        self.shared_cache.clear()
