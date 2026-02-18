"""
Execution Context for Data Generation Pipeline

Provides shared state and dependencies for all phases.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Any

from utils.db_connection import DatabaseConnection

if TYPE_CHECKING:
    from orchestration.phase import PhaseRegistry

@dataclass
class ExecutionContext:
    """
    Context object passed to all phases during execution.

    Contains:
    - Database connection
    - Configuration dictionary
    - Phase registry (for dependency lookups)
    - Set of completed phases
    - Shared cache for inter-phase data
    """

    db: DatabaseConnection
    config: dict[str, Any]
    phase_registry: PhaseRegistry  # Properly typed now
    completed_phases: set[str] = field(default_factory=set)
    shared_cache: dict[str, Any] = field(default_factory=dict)

    def mark_completed(self, phase_id: str) -> None:
        """Mark a phase as completed."""
        self.completed_phases.add(phase_id)

    def is_completed(self, phase_id: str) -> bool:
        """Check if a phase has been completed."""
        return phase_id in self.completed_phases

    def cache_get(self, key: str, default: Any = None) -> Any:
        """Get value from shared cache."""
        return self.shared_cache.get(key, default)

    def cache_set(self, key: str, value: Any) -> None:
        """Set value in shared cache."""
        self.shared_cache[key] = value

    def cache_clear(self) -> None:
        """Clear shared cache."""
        self.shared_cache.clear()
