"""
Worker functions for Phase 6 multiprocessing (social graph generation).

This module contains the worker initialization and chunk processing logic
for generating user follows and notifications in parallel.
"""

import logging
import os
import random
import time

import numpy as np

logger = logging.getLogger(__name__)

# Worker global variables

_WORKER_DB_PARAMS: dict[str, str] = {}

_WORKER_USER_IDS: list[int] = []

_WORKER_USERS_BY_CITY: dict[int, list[int]] = {}

_WORKER_TOP_1_PERCENT: list[int] = []

_WORKER_TOP_10_PERCENT: list[int] = []

_WORKER_USERNAME_MAP: dict[int, str] = {}

def worker_init_phase6(db_params, user_ids, users_by_city, top_1_percent, top_10_percent, username_map):
    """
    Initialize worker process with shared data.

    Args:
        db_params: Database connection parameters
        user_ids: List of all user IDs
        users_by_city: Dict mapping city_id to list of user_ids
        top_1_percent: List of top 1% influencer user IDs
        top_10_percent: List of top 10% influencer user IDs
        username_map: Dict mapping user_id to username
    """
    global \
        _WORKER_DB_PARAMS, \
        _WORKER_USER_IDS, \
        _WORKER_USERS_BY_CITY, \
        _WORKER_TOP_1_PERCENT, \
        _WORKER_TOP_10_PERCENT, \
        _WORKER_USERNAME_MAP

    _WORKER_DB_PARAMS = db_params
    _WORKER_USER_IDS = user_ids
    _WORKER_USERS_BY_CITY = users_by_city
    _WORKER_TOP_1_PERCENT = top_1_percent
    _WORKER_TOP_10_PERCENT = top_10_percent
    _WORKER_USERNAME_MAP = username_map

    # Seed random generator per worker
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
    follows_data = []
    notifications_data = []

    num_users_chunk = len(user_chunk)

    # Vectorized: Generate follow counts for chunk
    follow_counts = np.random.normal(25, 10, size=num_users_chunk).astype(int)
    follow_counts = np.clip(follow_counts, 0, 150)

    for idx, (follower_id, follower_username, city_id) in enumerate(user_chunk):
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
            local_peers = _WORKER_USERS_BY_CITY.get(city_id, [])
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
                    if r < 0.5 and _WORKER_TOP_1_PERCENT:
                        target = int(np.random.choice(_WORKER_TOP_1_PERCENT))
                    elif r < 0.8 and _WORKER_TOP_10_PERCENT:
                        target = int(np.random.choice(_WORKER_TOP_10_PERCENT))
                    else:
                        target = int(np.random.choice(_WORKER_USER_IDS))

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
