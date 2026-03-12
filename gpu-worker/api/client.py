import json
import logging
import time
from typing import Any

import httpx

from config import Settings

logger = logging.getLogger(__name__)

def request_shutdown(settings: Settings) -> bool:
    try:
        resp = httpx.post(
            f"{settings.shutdown_api_url}/api/shutdown",
            headers={"X-API-Token": settings.shutdown_api_token},
            timeout=10.0,
        )
        if resp.status_code == 200:
            logger.info("Shutdown request accepted")
            return True
        if resp.status_code == 409:
            logger.warning("Shutdown blocked: %s", resp.text)
            return False
        resp.raise_for_status()
    except Exception:
        logger.exception("Failed to request shutdown")
    return False

class WorkerApiClient:
    def __init__(self, settings: Settings):
        self._settings = settings
        self._client = httpx.Client(
            base_url=settings.api_url,
            headers={
                "Authorization": f"Bearer {settings.api_key}",
                "X-Worker-Id": settings.worker_id,
                "Content-Type": "application/json",
            },
            timeout=30.0,
        )
        self._config_cache: dict | None = None
        self._config_cached_at: float = 0

    def get_next_job(self, job_type: str | None = None) -> dict | None:
        params = {}
        if job_type:
            params["type"] = job_type
        try:
            resp = self._client.get("/api/worker/jobs/next", params=params)
            if resp.status_code == 204:
                return None
            resp.raise_for_status()
            data = resp.json()
            return data.get("data")
        except httpx.HTTPStatusError as e:
            logger.error("Failed to get next job: %s", e)
            return None

    def claim_job(self, job_id: int) -> bool:
        try:
            resp = self._client.put(f"/api/worker/jobs/{job_id}/claim")
            if resp.status_code == 204:
                return True
            if resp.status_code == 409:
                logger.info("Job %d already claimed", job_id)
                return False
            resp.raise_for_status()
            return True
        except httpx.HTTPStatusError as e:
            logger.error("Failed to claim job %d: %s", job_id, e)
            return False

    def complete_job(self, job_id: int, result: dict, processing_time_ms: int) -> None:
        try:
            resp = self._client.put(
                f"/api/worker/jobs/{job_id}/complete",
                json={
                    "result": json.dumps(result),
                    "processingTimeMs": processing_time_ms,
                },
            )
            resp.raise_for_status()
        except httpx.HTTPStatusError as e:
            logger.error("Failed to complete job %d: %s", job_id, e)
            raise

    def fail_job(
        self, job_id: int, error: str, error_log: str, retryable: bool
    ) -> None:
        try:
            resp = self._client.put(
                f"/api/worker/jobs/{job_id}/fail",
                json={
                    "errorMessage": error,
                    "errorLog": error_log,
                    "retryable": retryable,
                },
            )
            resp.raise_for_status()
        except httpx.HTTPStatusError as e:
            logger.error("Failed to report job %d failure: %s", job_id, e)

    def report_progress(self, job_id: int, **kwargs: Any) -> None:
        try:
            resp = self._client.post(
                f"/api/worker/jobs/{job_id}/progress",
                json=kwargs,
            )
            resp.raise_for_status()
        except httpx.HTTPStatusError as e:
            logger.warning("Failed to report progress for job %d: %s", job_id, e)

    def send_heartbeat(self, gpu_info: dict) -> None:
        try:
            resp = self._client.post("/api/worker/heartbeat", json=gpu_info)
            resp.raise_for_status()
        except httpx.HTTPStatusError as e:
            logger.warning("Heartbeat failed: %s", e)
        except httpx.ConnectError:
            logger.warning("Heartbeat failed: API unreachable")

    def get_config(self) -> dict:
        now = time.monotonic()
        if self._config_cache is not None and (now - self._config_cached_at) < self._settings.config_cache_ttl:
            return self._config_cache

        try:
            resp = self._client.get("/api/worker/config")
            resp.raise_for_status()
            data = resp.json()
            self._config_cache = data.get("data", {})
            self._config_cached_at = now
            return self._config_cache
        except (httpx.HTTPStatusError, httpx.ConnectError) as e:
            logger.warning("Failed to fetch config: %s", e)
            return self._config_cache or {}

    def close(self) -> None:
        self._client.close()
