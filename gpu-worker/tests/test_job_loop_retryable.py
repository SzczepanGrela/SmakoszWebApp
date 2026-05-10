import httpx

from worker.job_loop import is_retryable


def test_httpx_connecterror_is_retryable() -> None:
    assert is_retryable(httpx.ConnectError("SSL EOF")) is True


def test_httpx_remoteprotocolerror_is_retryable() -> None:
    assert is_retryable(httpx.RemoteProtocolError("server disconnected")) is True


def test_httpx_readerror_is_retryable() -> None:
    assert is_retryable(httpx.ReadError("read error")) is True


def test_valueerror_is_not_retryable() -> None:
    assert is_retryable(ValueError("permanent failure")) is False
