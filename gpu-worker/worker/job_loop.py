import logging
import time
import traceback
from collections.abc import Callable

import httpx
import torch

from api.client import WorkerApiClient
from config import Settings

logger = logging.getLogger(__name__)

def drain_jobs(
    api: WorkerApiClient,
    job_type: str,
    handler_fn: Callable,
    first_job: dict,
    settings: Settings,
) -> int:
    """Process all pending jobs of given type. Returns count processed."""
    processed = 0
    job = first_job

    while job is not None:
        job_id = job["jobId"]
        logger.info("Processing %s job %d", job_type, job_id)

        if not api.claim_job(job_id):
            logger.info("Job %d already claimed, skipping", job_id)
            job = api.get_next_job(job_type=job_type)
            continue

        try:
            start = time.monotonic()
            result = handler_fn(job, api)
            elapsed_ms = int((time.monotonic() - start) * 1000)
            api.complete_job(job_id, result, elapsed_ms)
            processed += 1
            logger.info("Job %d completed in %dms", job_id, elapsed_ms)
        except Exception as e:
            logger.exception("Job %d failed", job_id)
            api.fail_job(
                job_id,
                str(e),
                traceback.format_exc(),
                retryable=is_retryable(e),
            )

        time.sleep(settings.poll_interval_busy)
        job = api.get_next_job(job_type=job_type)

    return processed

def is_retryable(e: Exception) -> bool:
    if isinstance(e, torch.cuda.OutOfMemoryError):
        return True
    if isinstance(e, httpx.TimeoutException):
        return True
    if isinstance(e, OSError):
        return True
    return False

def run_loop(
    api: WorkerApiClient,
    handlers: dict[str, Callable],
    settings: Settings,
) -> None:
    logger.info("Entering polling loop (idle=%ds, busy=%ds)", settings.poll_interval_idle, settings.poll_interval_busy)

    while True:
        try:
            job = api.get_next_job()
        except Exception:
            logger.exception("Error polling for jobs")
            time.sleep(settings.poll_interval_idle)
            continue

        if job is None:
            time.sleep(settings.poll_interval_idle)
            continue

        job_id = job["jobId"]
        job_type = job["type"]
        logger.info("Found job %d (type=%s)", job_id, job_type)

        if not api.claim_job(job_id):
            logger.info("Job %d already claimed, skipping", job_id)
            continue

        handler = handlers.get(job_type)
        if handler is None:
            logger.error("Unknown job type: %s", job_type)
            api.fail_job(job_id, f"Unknown job type: {job_type}", "", retryable=False)
            continue

        try:
            start = time.monotonic()
            result = handler(job, api)
            elapsed_ms = int((time.monotonic() - start) * 1000)
            api.complete_job(job_id, result, elapsed_ms)
            logger.info("Job %d completed in %dms", job_id, elapsed_ms)
        except Exception as e:
            logger.exception("Job %d failed", job_id)
            api.fail_job(
                job_id,
                str(e),
                traceback.format_exc(),
                retryable=is_retryable(e),
            )

        time.sleep(settings.poll_interval_busy)
