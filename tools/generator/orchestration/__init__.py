"""
Orchestration Layer for MockDataFactory

Provides clean architecture for data generation pipeline:
- BasePhase: Abstract base class for all generation phases
- PhaseRegistry: Registry and dependency resolution
- ExecutionContext: Shared state between phases
- DataGenerationPipeline: Main orchestrator
- DatabaseManager: Database lifecycle management
- Config validation: Pydantic schemas
"""

from .context import ExecutionContext
from .database_manager import DatabaseCleanupStrategy, DatabaseManager
from .phase import BasePhase, PhaseMetadata, PhaseRegistry, PhaseResult, PhaseStatus
from .pipeline import DataGenerationPipeline, PipelineConfig, PipelineResult

__all__ = [
    # Core abstractions
    "BasePhase",
    "PhaseMetadata",
    "PhaseResult",
    "PhaseStatus",
    "PhaseRegistry",
    # Context
    "ExecutionContext",
    # Database
    "DatabaseManager",
    "DatabaseCleanupStrategy",
    # Pipeline
    "DataGenerationPipeline",
    "PipelineConfig",
    "PipelineResult",
]
