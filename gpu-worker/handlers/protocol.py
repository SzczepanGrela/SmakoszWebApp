from __future__ import annotations

import dataclasses
from typing import Protocol, runtime_checkable

import torch

from config import Settings
from models.model_manager import ModelManager


@dataclasses.dataclass(frozen=True)
class ModelRequirement:
    name: str
    hf_repo: str
    version_env_key: str
    version_default: str = "v1"


@dataclasses.dataclass(frozen=True)
class JobMapping:
    job_type: str
    method: str


@runtime_checkable
class JobHandler(Protocol):
    MODELS: list[ModelRequirement]
    JOB_MAPPINGS: list[JobMapping]
    PHASE_NAME: str

    def __init__(self, model_manager: ModelManager, settings: Settings, device: torch.device) -> None: ...
