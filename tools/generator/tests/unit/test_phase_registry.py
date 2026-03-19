"""
Unit tests for PhaseRegistry and dependency resolution.

Tests:
- Phase registration
- Duplicate registration detection
- Dependency resolution (topological sort)
- Circular dependency detection
- Unknown phase handling
"""

import pytest

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseRegistry, PhaseResult, PhaseStatus

class MockPhaseNoDeps(BasePhase):
    """Mock phase with no dependencies."""

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase0_mock",
            display_name="Mock Phase 0",
            dependencies=[],
            required_tables=["mock_table_0"],
            cleanup_tables=["mock_table_0"],
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        return PhaseResult(
            phase_id="phase0_mock",
            status=PhaseStatus.COMPLETED,
            duration_seconds=0.1,
            entities_generated={"mock": 10},
        )

class MockPhase1(BasePhase):
    """Mock phase with dependency on phase0."""

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase1_mock",
            display_name="Mock Phase 1",
            dependencies=["phase0_mock"],
            required_tables=["mock_table_1"],
            cleanup_tables=["mock_table_1"],
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        return PhaseResult(
            phase_id="phase1_mock",
            status=PhaseStatus.COMPLETED,
            duration_seconds=0.1,
            entities_generated={"mock": 20},
        )

class MockPhase2(BasePhase):
    """Mock phase with dependency on phase1."""

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase2_mock",
            display_name="Mock Phase 2",
            dependencies=["phase1_mock"],
            required_tables=["mock_table_2"],
            cleanup_tables=["mock_table_2"],
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        return PhaseResult(
            phase_id="phase2_mock",
            status=PhaseStatus.COMPLETED,
            duration_seconds=0.1,
            entities_generated={"mock": 30},
        )

class MockPhaseCircularA(BasePhase):
    """Mock phase for circular dependency test (A depends on B)."""

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase_a",
            display_name="Circular A",
            dependencies=["phase_b"],
            required_tables=["table_a"],
            cleanup_tables=["table_a"],
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        return PhaseResult(
            phase_id="phase_a",
            status=PhaseStatus.COMPLETED,
            duration_seconds=0.1,
            entities_generated={},
        )

class MockPhaseCircularB(BasePhase):
    """Mock phase for circular dependency test (B depends on A)."""

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase_b",
            display_name="Circular B",
            dependencies=["phase_a"],
            required_tables=["table_b"],
            cleanup_tables=["table_b"],
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        return PhaseResult(
            phase_id="phase_b",
            status=PhaseStatus.COMPLETED,
            duration_seconds=0.1,
            entities_generated={},
        )

def test_phase_registration():
    """Test that phases can be registered."""
    registry = PhaseRegistry()
    phase = MockPhaseNoDeps()

    registry.register(phase)

    assert len(registry) == 1
    assert registry.get("phase0_mock") == phase
    assert "phase0_mock" in registry

def test_duplicate_registration_fails():
    """Test that registering duplicate phase_id raises ValueError."""
    registry = PhaseRegistry()
    phase1 = MockPhaseNoDeps()
    phase2 = MockPhaseNoDeps()  # Same phase_id

    registry.register(phase1)

    with pytest.raises(ValueError, match="already registered"):
        registry.register(phase2)

def test_get_nonexistent_phase():
    """Test getting phase that doesn't exist returns None."""
    registry = PhaseRegistry()

    result = registry.get("nonexistent_phase")
    assert result is None

def test_dependency_resolution_simple():
    """Test topological sort with simple linear dependency chain."""
    registry = PhaseRegistry()

    # Setup: phase2 -> phase1 -> phase0
    registry.register(MockPhaseNoDeps())
    registry.register(MockPhase1())
    registry.register(MockPhase2())

    # Request only phase2
    result = registry.resolve_dependencies(["phase2_mock"])

    # Should include all dependencies in correct order
    assert result == ["phase0_mock", "phase1_mock", "phase2_mock"]

def test_dependency_resolution_multiple_requested():
    """Test requesting multiple phases resolves correctly."""
    registry = PhaseRegistry()

    registry.register(MockPhaseNoDeps())
    registry.register(MockPhase1())
    registry.register(MockPhase2())

    # Request both phase1 and phase2
    result = registry.resolve_dependencies(["phase1_mock", "phase2_mock"])

    # Should include phase0 as dependency
    assert result == ["phase0_mock", "phase1_mock", "phase2_mock"]

def test_dependency_resolution_no_duplicates():
    """Test that dependencies are not duplicated in result."""
    registry = PhaseRegistry()

    registry.register(MockPhaseNoDeps())
    registry.register(MockPhase1())
    registry.register(MockPhase2())

    # Request same phase twice
    result = registry.resolve_dependencies(["phase2_mock", "phase2_mock"])

    # Should appear only once
    assert result.count("phase2_mock") == 1
    assert len(result) == 3  # phase0, phase1, phase2

def test_circular_dependency_detected():
    """Test that circular dependencies raise ValueError."""
    registry = PhaseRegistry()

    # Setup: A depends on B, B depends on A
    registry.register(MockPhaseCircularA())
    registry.register(MockPhaseCircularB())

    with pytest.raises(ValueError, match="Circular dependency"):
        registry.resolve_dependencies(["phase_a"])

def test_unknown_phase_in_request():
    """Test that requesting unknown phase raises ValueError."""
    registry = PhaseRegistry()

    registry.register(MockPhaseNoDeps())

    with pytest.raises(ValueError, match="Unknown phase"):
        registry.resolve_dependencies(["nonexistent_phase"])

def test_unknown_dependency():
    """Test that phase with unknown dependency raises ValueError."""

    class PhaseWithBadDep(BasePhase):
        @property
        def metadata(self) -> PhaseMetadata:
            return PhaseMetadata(
                phase_id="phase_bad",
                display_name="Bad Dependency",
                dependencies=["nonexistent"],
                required_tables=[],
                cleanup_tables=[],
            )

        def execute(self, context: ExecutionContext) -> PhaseResult:
            return PhaseResult(
                phase_id="phase_bad",
                status=PhaseStatus.COMPLETED,
                duration_seconds=0.1,
                entities_generated={},
            )

    registry = PhaseRegistry()
    registry.register(PhaseWithBadDep())

    with pytest.raises(ValueError, match="depends on unknown phase"):
        registry.resolve_dependencies(["phase_bad"])

def test_get_all_phases():
    """Test getting all registered phases."""
    registry = PhaseRegistry()

    registry.register(MockPhaseNoDeps())
    registry.register(MockPhase1())
    registry.register(MockPhase2())

    all_phases = registry.get_all()

    assert len(all_phases) == 3
    assert all(isinstance(p, BasePhase) for p in all_phases)

def test_get_dependency_graph():
    """Test getting dependency graph."""
    registry = PhaseRegistry()

    registry.register(MockPhaseNoDeps())
    registry.register(MockPhase1())
    registry.register(MockPhase2())

    graph = registry.get_dependency_graph()

    assert graph["phase0_mock"] == []
    assert graph["phase1_mock"] == ["phase0_mock"]
    assert graph["phase2_mock"] == ["phase1_mock"]

def test_contains_operator():
    """Test __contains__ operator (in keyword)."""
    registry = PhaseRegistry()
    registry.register(MockPhaseNoDeps())

    assert "phase0_mock" in registry
    assert "nonexistent" not in registry
