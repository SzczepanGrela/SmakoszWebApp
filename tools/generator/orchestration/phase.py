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

    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    SKIPPED = "skipped"

@dataclass
class PhaseMetadata:

    phase_id: str
    display_name: str
    dependencies: list[str]
    required_tables: list[str]
    cleanup_tables: list[str]
    estimated_duration: int | None = None

@dataclass
class PhaseResult:

    phase_id: str
    status: PhaseStatus
    duration_seconds: float
    entities_generated: dict[str, Any]
    error: Exception | None = None

class BasePhase(ABC):

    @property
    @abstractmethod
    def metadata(self) -> PhaseMetadata:
        pass

    @abstractmethod
    def execute(self, context: "ExecutionContext") -> PhaseResult:
        pass

    def validate_prerequisites(self, context: "ExecutionContext", selective: bool = False) -> None:
        for dep_id in self.metadata.dependencies:
            if dep_id not in context.completed_phases:
                if selective:
                    logger.info(
                        f"[Selective] Assuming {dep_id} data exists for {self.metadata.phase_id}"
                    )
                else:
                    raise ValueError(
                        f"Phase {self.metadata.phase_id} requires {dep_id} "
                        f"but it hasn't been completed. "
                        f"Completed phases: {sorted(context.completed_phases)}"
                    )

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

    def __init__(self):
        self._phases: dict[str, BasePhase] = {}

    def register(self, phase: BasePhase) -> None:
        phase_id = phase.metadata.phase_id
        if phase_id in self._phases:
            raise ValueError(f"Phase {phase_id} is already registered")

        self._phases[phase_id] = phase
        logger.debug(f"Registered phase: {phase_id} ({phase.metadata.display_name})")

    def get(self, phase_id: str) -> BasePhase | None:
        return self._phases.get(phase_id)

    def get_all(self) -> list[BasePhase]:
        return list(self._phases.values())

    def resolve_dependencies(self, requested_phase_ids: list[str]) -> list[str]:
        for phase_id in requested_phase_ids:
            if phase_id not in self._phases:
                available = ", ".join(sorted(self._phases.keys()))
                raise ValueError(f"Unknown phase: {phase_id}. Available: {available}")

        all_needed = set()
        to_process = deque(requested_phase_ids)

        while to_process:
            phase_id = to_process.popleft()
            if phase_id in all_needed:
                continue

            all_needed.add(phase_id)
            phase = self._phases[phase_id]

            for dep_id in phase.metadata.dependencies:
                if dep_id not in self._phases:
                    raise ValueError(f"Phase {phase_id} depends on unknown phase {dep_id}")
                if dep_id not in all_needed:
                    to_process.append(dep_id)

        in_degree: dict[str, int] = defaultdict(int)
        adjacency: dict[str, list[str]] = defaultdict(list)

        for phase_id in all_needed:
            phase = self._phases[phase_id]
            if phase_id not in in_degree:
                in_degree[phase_id] = 0

            for dep_id in phase.metadata.dependencies:
                adjacency[dep_id].append(phase_id)
                in_degree[phase_id] += 1

        queue: deque = deque()
        result: list[str] = []

        for phase_id in all_needed:
            if in_degree[phase_id] == 0:
                queue.append(phase_id)

        while queue:
            current = queue.popleft()
            result.append(current)

            for neighbor in adjacency[current]:
                in_degree[neighbor] -= 1
                if in_degree[neighbor] == 0:
                    queue.append(neighbor)

        if len(result) != len(all_needed):
            remaining = all_needed - set(result)
            raise ValueError(f"Circular dependency detected involving phases: {sorted(remaining)}")

        logger.debug(f"Dependency resolution: {requested_phase_ids} -> {result}")
        return result

    def resolve_downstream(self, phase_ids: list[str]) -> list[str]:
        reverse_deps: dict[str, list[str]] = defaultdict(list)
        for phase in self._phases.values():
            for dep in phase.metadata.dependencies:
                reverse_deps[dep].append(phase.metadata.phase_id)

        downstream: set[str] = set()
        queue: deque[str] = deque(phase_ids)
        while queue:
            pid = queue.popleft()
            for child in reverse_deps.get(pid, []):
                if child not in downstream and child not in phase_ids:
                    downstream.add(child)
                    queue.append(child)
        return list(downstream)

    def sort_phases(self, phase_ids: list[str]) -> list[str]:
        for pid in phase_ids:
            if pid not in self._phases:
                available = ", ".join(sorted(self._phases.keys()))
                raise ValueError(f"Unknown phase: {pid}. Available: {available}")
        unique = list(dict.fromkeys(phase_ids))
        return sorted(unique, key=lambda pid: (int(pid.replace("phase", "").split("_")[0]), pid))

    def get_cleanup_tables_for_phases(self, phase_ids: list[str]) -> list[str]:
        tables: list[str] = []
        seen: set[str] = set()
        for pid in phase_ids:
            phase = self._phases.get(pid)
            if phase:
                for t in phase.metadata.cleanup_tables:
                    if t not in seen:
                        tables.append(t)
                        seen.add(t)
        return tables

    def get_dependency_graph(self) -> dict[str, list[str]]:
        return {phase.metadata.phase_id: phase.metadata.dependencies for phase in self._phases.values()}

    def __len__(self) -> int:
        return len(self._phases)

    def __contains__(self, phase_id: str) -> bool:
        return phase_id in self._phases
