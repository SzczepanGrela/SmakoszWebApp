import logging
import os
import random
import time
from dataclasses import dataclass
from datetime import datetime, timezone

import numpy as np

logger = logging.getLogger(__name__)

@dataclass
class WorkerContext:

    db_params: dict[str, str]
    user_ids: list[int]
    users_by_city: dict[int, list[int]]
    top_1_percent: list[int]
    top_10_percent: list[int]
    username_map: dict[int, str]

_worker_ctx: WorkerContext | None = None

def worker_init_phase6(ctx: WorkerContext) -> None:
    global _worker_ctx

    _worker_ctx = ctx

    random.seed(os.getpid() + time.time())
    np.random.seed(os.getpid() + int(time.time() * 1000) % (2**32))

def process_follows_chunk(user_chunk):
    ctx = _worker_ctx
    assert ctx is not None, "worker_init_phase6() must be called before process_follows_chunk()"

    follows_data = []

    num_users_chunk = len(user_chunk)

    follow_counts = np.random.normal(25, 10, size=num_users_chunk).astype(int)
    follow_counts = np.clip(follow_counts, 0, 150)

    for idx, (follower_id, _follower_username, city_id) in enumerate(user_chunk):
        num_following = int(follow_counts[idx])

        if num_following == 0:
            continue

        is_local = np.random.random(num_following) < 0.70
        num_local = int(is_local.sum())
        num_global = num_following - num_local

        targets: set[int] = set()

        if num_local > 0:
            local_peers = ctx.users_by_city.get(city_id, [])
            if len(local_peers) > 1:
                local_candidates = [u for u in local_peers if u != follower_id]
                if local_candidates:
                    local_targets = np.random.choice(
                        local_candidates, size=min(num_local, len(local_candidates) * 3), replace=True
                    )
                    targets.update(int(t) for t in local_targets[:num_local])

        if num_global > 0 or len(targets) < num_following:
            remaining = num_following - len(targets)
            if remaining > 0:
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

        for followed_id in targets:
            follows_data.append(
                {
                    "follower_id": int(follower_id),
                    "followed_id": int(followed_id),
                    "created_at": datetime.now(timezone.utc),
                }
            )

    return {"follows": follows_data}
