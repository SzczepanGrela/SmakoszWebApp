import time
from unittest import mock

import httpx
import pytest

from api.client import WorkerApiClient
from config import Settings


def _make_client() -> WorkerApiClient:
    client = WorkerApiClient.__new__(WorkerApiClient)
    client._settings = Settings()
    client._client = mock.Mock()
    client._config_cache = None
    client._config_cached_at = 0.0
    return client


def _resp(status_code: int, json_body: object = None, raises: Exception | None = None) -> mock.Mock:
    resp = mock.Mock()
    resp.status_code = status_code
    resp.json.return_value = json_body
    resp.raise_for_status = mock.Mock()
    if raises is not None:
        resp.raise_for_status.side_effect = raises
    return resp


def _http_error() -> httpx.HTTPStatusError:
    return httpx.HTTPStatusError("error", request=mock.Mock(), response=mock.Mock())


def test_get_next_job_204_returns_none() -> None:
    client = _make_client()
    client._client.get.return_value = _resp(204)
    assert client.get_next_job() is None


def test_get_next_job_200_returns_raw_json() -> None:
    client = _make_client()
    client._client.get.return_value = _resp(200, json_body={"jobId": 7, "type": "text_moderation"})
    assert client.get_next_job() == {"jobId": 7, "type": "text_moderation"}


def test_get_next_job_http_error_returns_none() -> None:
    client = _make_client()
    client._client.get.return_value = _resp(500, raises=_http_error())
    assert client.get_next_job() is None


def test_claim_job_204_returns_true() -> None:
    client = _make_client()
    client._client.put.return_value = _resp(204)
    assert client.claim_job(7) is True


def test_claim_job_409_returns_false() -> None:
    client = _make_client()
    client._client.put.return_value = _resp(409)
    assert client.claim_job(7) is False


def test_claim_job_http_error_returns_false() -> None:
    client = _make_client()
    client._client.put.return_value = _resp(500, raises=_http_error())
    assert client.claim_job(7) is False


def test_complete_job_reraises_on_http_error() -> None:
    client = _make_client()
    client._client.put.return_value = _resp(500, raises=_http_error())
    with pytest.raises(httpx.HTTPStatusError):
        client.complete_job(7, {"verdict": "approved"}, 123)


def test_fail_job_swallows_http_error() -> None:
    client = _make_client()
    client._client.put.return_value = _resp(500, raises=_http_error())
    client.fail_job(7, "boom", "trace", retryable=True)


def test_get_config_cache_hit_skips_request() -> None:
    client = _make_client()
    client._config_cache = {"toxicThresholdReject": 0.9}
    client._config_cached_at = time.monotonic()
    assert client.get_config() == {"toxicThresholdReject": 0.9}
    client._client.get.assert_not_called()


def test_get_config_cache_miss_fetches() -> None:
    client = _make_client()
    client._client.get.return_value = _resp(200, json_body={"toxicThresholdReject": 0.7})
    assert client.get_config() == {"toxicThresholdReject": 0.7}
    client._client.get.assert_called_once()


def test_get_config_error_returns_stale_cache() -> None:
    client = _make_client()
    client._config_cache = {"stale": True}
    client._config_cached_at = 0.0
    client._client.get.side_effect = httpx.ConnectError("unreachable")
    assert client.get_config() == {"stale": True}
