"""
Phase Abstraction and Registry for Data Generation Pipeline

Provides:
- BasePhase: Abstract base class for all phases
- PhaseMetadata: Explicit dependency declarations
- PhaseResult: Standardized execution results
- PhaseRegistry: Phase management and dependency resolution
"""

import logging
from abc import ABC, abstractmethod
from collections import defaultdict, deque
from dataclasses import dataclass
from enum import Enum
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from .context import ExecutionContext

logger = logging.getLogger(__name__)

class PhaseStatus(Enum):
    """Execution status of a phase."""

    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    SKIPPED = "skipped"

@dataclass
class PhaseMetadata:
    """
    Metadata for a generation phase.

    Explicitly declares:
    - Phase identifier and display name
    - Dependencies on other phases
    - Tables this phase populates
    - Tables to cleanup if running standalone
    - Estimated duration (for progress tracking)
    """

    phase_id: str  # e.g., "phase2_restaurants"
    display_name: str  # e.g., "Restaurant Generation"
    dependencies: list[str]  # e.g., ["phase1_cities"]
    required_tables: list[str]  # Tables this phase populates
    cleanup_tables: list[str]  # Tables to cleanup for standalone run
    estimated_duration: int | None = None  # seconds

@dataclass
class PhaseResult:
    """Result of phase execution."""

    phase_id: str
    status: PhaseStatus
    duration_seconds: float
    entities_generated: dict[str, Any]  # e.g., {"users": 50000}
    error: Exception | None = None

class BasePhase(ABC):
    """
    Abstract base class for all generation phases.

    Enforces:
    - Explicit metadata declaration (dependencies, tables)
    - Standardized execution interface
    - Isolation of responsibilities

    Benefits (SOLID):
    - SRP: Each phase has one job - generate its data
    - OCP: New phases added by inheritance
    - LSP: All BasePhase instances are substitutable
    - DIP: Pipeline depends on abstraction, not concrete classes
    """

    @property
    @abstractmethod
    def metadata(self) -> PhaseMetadata:
        """
        Return phase metadata including dependencies.

        Must be implemented by each phase.
        """
        pass

    @abstractmethod
    def execute(self, context: "ExecutionContext") -> PhaseResult:
        """
        Execute the phase with given context.

        Args:
            context: ExecutionContext with DB connection, config, etc.

        Returns:
            PhaseResult with status and statistics

        Raises:
            Exception: If phase execution fails
        """
        pass

    def validate_prerequisites(self, context: "ExecutionContext") -> None:
        """
        Validate that all prerequisites are satisfied.

        Checks:
        1. All dependency phases have been completed
        2. Required tables from dependencies have data

        Args:
            context: ExecutionContext with completion tracking

        Raises:
            ValueError: If prerequisites are not met
        """
        # Check dependency phases completed
        for dep_id in self.metadata.dependencies:
            if dep_id not in context.completed_phases:
                raise ValueError(
                    f"Phase {self.metadata.phase_id} requires {dep_id} "
                    f"but it hasn't been completed. "
                    f"Completed phases: {sorted(context.completed_phases)}"
                )

        # Check required tables have data (if dependencies exist)
        if self.metadata.dependencies:
            for dep_id in self.metadata.dependencies:
                dep_phase = context.phase_registry.get(dep_id)
                if dep_phase:
                    for table in dep_phase.metadata.required_tables:
                        try:
                            count = context.db.fetch_val(f"SELECT COUNT(*) FROM {table}")
                            if count == 0:
                                logger.warning(
                                    f"Table {table} (from {dep_id}) is empty. "
                                    f"This may cause issues for {self.metadata.phase_id}"
                                )
                        except Exception as e:
                            logger.debug(f"Could not check table {table}: {e}. Skipping validation.")

class PhaseRegistry:
    """
    Registry of available phases with dependency resolution.

    Provides:
    - Phase registration and lookup
    - Topological sort for dependency resolution
    - Circular dependency detection
    """

    def __init__(self):
        self._phases: dict[str, BasePhase] = {}

    def register(self, phase: BasePhase) -> None:
        """
        Register a phase.

        Args:
            phase: BasePhase instance to register

        Raises:
            ValueError: If phase_id already registered
        """
        phase_id = phase.metadata.phase_id
        if phase_id in self._phases:
            raise ValueError(f"Phase {phase_id} is already registered")

        self._phases[phase_id] = phase
        logger.debug(f"Registered phase: {phase_id} ({phase.metadata.display_name})")

    def get(self, phase_id: str) -> BasePhase | None:
        """Get phase by ID."""
        return self._phases.get(phase_id)

    def get_all(self) -> list[BasePhase]:
        """Get all registered phases."""
        return list(self._phases.values())

    def resolve_dependencies(self, requested_phase_ids: list[str]) -> list[str]:
        """
        Resolve dependencies and return topologically sorted phase IDs.

        Uses Kahn's algorithm for topological sorting:
        1. Build dependency graph
        2. Find nodes with no dependencies
        3. Process nodes, removing edges
        4. Detect cycles if any nodes remain

        Args:
            requested_phase_ids: List of phase IDs user wants to run

        Returns:
            Topologically sorted list of phase IDs (includes dependencies)

        Raises:
            ValueError: If unknown phase or circular dependency detected

        Example:
            >>> registry.resolve_dependencies(["phase2_restaurants"])
            ["phase1_cities", "phase2_restaurants"]
        """
        # Validate all requested phases exist
        for phase_id in requested_phase_ids:
            if phase_id not in self._phases:
                available = ", ".join(sorted(self._phases.keys()))
                raise ValueError(f"Unknown phase: {phase_id}. Available: {available}")

        # Collect all phases needed (requested + dependencies)
        all_needed = set()
        to_process = deque(requested_phase_ids)

        while to_process:
            phase_id = to_process.popleft()
            if phase_id in all_needed:
                continue

            all_needed.add(phase_id)
            phase = self._phases[phase_id]

            # Add dependencies to processing queue
            for dep_id in phase.metadata.dependencies:
                if dep_id not in self._phases:
                    raise ValueError(f"Phase {phase_id} depends on unknown phase {dep_id}")
                if dep_id not in all_needed:
                    to_process.append(dep_id)

        # Build dependency graph for topological sort
        # in_degree[node] = number of incoming edges
        in_degree: dict[str, int] = defaultdict(int)
        adjacency: dict[str, list[str]] = defaultdict(list)

        for phase_id in all_needed:
            phase = self._phases[phase_id]
            if phase_id not in in_degree:
                in_degree[phase_id] = 0

            for dep_id in phase.metadata.dependencies:
                adjacency[dep_id].append(phase_id)
                in_degree[phase_id] += 1

        # Kahn's algorithm: Topological sort
        queue: deque = deque()
        result: list[str] = []

        # Start with nodes that have no dependencies
        for phase_id in all_needed:
            if in_degree[phase_id] == 0:
                queue.append(phase_id)

        while queue:
            current = queue.popleft()
            result.append(current)

            # Remove edges from current node
            for neighbor in adjacency[current]:
                in_degree[neighbor] -= 1
                if in_degree[neighbor] == 0:
                    queue.append(neighbor)

        # Check for circular dependencies
        if len(result) != len(all_needed):
            remaining = all_needed - set(result)
            raise ValueError(f"Circular dependency detected involving phases: {sorted(remaining)}")

        logger.debug(f"Dependency resolution: {requested_phase_ids} -> {result}")
        return result

    def get_dependency_graph(self) -> dict[str, list[str]]:
        """
        Get dependency graph as adjacency list.

        Returns:
            Dictionary mapping phase_id -> list of dependency IDs
        """
        return {phase.metadata.phase_id: phase.metadata.dependencies for phase in self._phases.values()}

    def __len__(self) -> int:
        """Return number of registered phases."""
        return len(self._phases)

    def __contains__(self, phase_id: str) -> bool:
        """Check if phase is registered."""
        return phase_id in self._phases
