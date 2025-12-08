"""
Worker modules for multiprocessing in data generation phases.

This package contains worker logic separated from main generator modules
to avoid RuntimeWarnings and ensure clean multiprocessing imports.
"""

from .phase6_worker import process_follows_chunk, worker_init_phase6

__all__ = ["process_follows_chunk", "worker_init_phase6"]
