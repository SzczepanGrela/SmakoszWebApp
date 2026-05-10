from unittest import mock

import pytest

import app as gateway
import config


@pytest.fixture
def client(monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(config, "API_TOKEN", "test-token")
    monkeypatch.setattr(config, "GPU_WORKER_MAC", "AA:BB:CC:DD:EE:FF")
    gateway.app.testing = True
    return gateway.app.test_client()


def test_health_returns_ok(client):
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.get_json() == {"status": "ok", "service": "rbpi-gateway"}


def test_wake_without_token_returns_401(client):
    resp = client.post("/wake")
    assert resp.status_code == 401


def test_wake_with_token_no_mac_returns_500(client, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(config, "GPU_WORKER_MAC", "")
    resp = client.post("/wake", headers={"X-API-Token": "test-token"})
    assert resp.status_code == 500


def test_wake_with_token_and_mac_sends_magic_packet(client):
    with mock.patch.object(gateway, "send_magic_packet") as send_mock, \
         mock.patch.object(gateway.threading, "Thread") as thread_mock:
        resp = client.post("/wake", headers={"X-API-Token": "test-token"})

    assert resp.status_code == 200
    assert resp.get_json() == {"status": "sent", "mac": "AA:BB:CC:DD:EE:FF"}
    send_mock.assert_called_once_with("AA:BB:CC:DD:EE:FF")
    thread_mock.assert_called_once()
