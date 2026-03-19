"""
Worker functions for Phase 6 multiprocessing (social graph generation).

This module contains the worker initialization and chunk processing logic
for generating user follows and notifications in parallel.
"""

import logging
import os
import random
import time
from dataclasses import dataclass

import numpy as np

logger = logging.getLogger(__name__)

@dataclass
class WorkerContext:
    """
    Immutable context shared across all workers in the multiprocessing pool.

    Passed as a single object to worker_init_phase6() so that the Pool
    initializer has a clean, typed interface instead of 6 positional arguments.

    Fields mirror the original individual globals but are now grouped and
    named, making testing straightforward:

        ctx = WorkerContext(db_params={...}, user_ids=[...], ...)
        generators.workers.phase6_worker._worker_ctx = ctx
        result = process_follows_chunk(chunk)
    """

    db_params: dict[str, str]
    user_ids: list[int]
    users_by_city: dict[int, list[int]]
    top_1_percent: list[int]
    top_10_percent: list[int]
    username_map: dict[int, str]

# Single module-level reference - set once per worker process by worker_init_phase6().
_worker_ctx: WorkerContext | None = None

def worker_init_phase6(ctx: WorkerContext) -> None:
    """
    Initialize worker process with shared data.

    Args:
        ctx: WorkerContext holding all shared read-only data for the worker.
    """
    global _worker_ctx

    _worker_ctx = ctx

    # Seed random generator per worker to avoid correlated sequences.
    random.seed(os.getpid() + time.time())
    np.random.seed(os.getpid() + int(time.time() * 1000) % (2**32))

def process_follows_chunk(user_chunk):
    """
    Process a chunk of users to generate follows and notifications.

    Args:
        user_chunk: List of tuples (user_id, username, city_id)

    Returns:
        Dict with 'follows' and 'notifications' lists.
    """
    ctx = _worker_ctx
    assert ctx is not None, "worker_init_phase6() must be called before process_follows_chunk()"

    follows_data = []

    num_users_chunk = len(user_chunk)

    # Vectorized: Generate follow counts for chunk
    follow_counts = np.random.normal(25, 10, size=num_users_chunk).astype(int)
    follow_counts = np.clip(follow_counts, 0, 150)

    for idx, (follower_id, _follower_username, city_id) in enumerate(user_chunk):
        num_following = int(follow_counts[idx])  # Cast to native int

        if num_following == 0:
            continue

        # Vectorized: Decide local vs global for all follows at once
        is_local = np.random.random(num_following) < 0.70
        num_local = int(is_local.sum())  # Cast to native int
        num_global = num_following - num_local

        targets: set[int] = set()

        # Local follows (same city)
        if num_local > 0:
            local_peers = ctx.users_by_city.get(city_id, [])
            if len(local_peers) > 1:
                # Remove self from local peers
                local_candidates = [u for u in local_peers if u != follower_id]
                if local_candidates:
                    # Sample with replacement to handle small pools
                    local_targets = np.random.choice(
                        local_candidates, size=min(num_local, len(local_candidates) * 3), replace=True
                    )
                    targets.update(int(t) for t in local_targets[:num_local])  # Cast to native int

        # Global follows (prefer influencers)
        if num_global > 0 or len(targets) < num_following:
            remaining = num_following - len(targets)
            if remaining > 0:
                # Vectorized: Decide influencer tier for each global follow
                rand_global = np.random.random(remaining)

                global_targets = []
                for r in rand_global:
                    target: int
                    if r < 0.5 and ctx.top_1_percent:
                        target = int(np.random.choice(ctx.top_1_percent))
                    elif r < 0.8 and ctx.top_10_percent:
                        target = int(np.random.choice(ctx.top_10_percent))
                    else:
                        target = int(np.random.choice(ctx.user_ids))

                    if target != follower_id:
                        global_targets.append(target)

                targets.update(global_targets)

        # Generate follow records
        for followed_id in targets:
            follows_data.append(
                {
                    "follower_id": int(follower_id),  # Cast to native int
                    "followed_id": int(followed_id),  # Cast to native int
                }
            )

    return {"follows": follows_data}
