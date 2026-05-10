import json
from unittest import mock

from handlers.batch_mixin import BatchJobMixin


class _StubModerator(BatchJobMixin):
    BATCH_INPUT_KEY = "image_url"

    def __init__(self, predict_side_effects: list) -> None:
        self._predict_mock = mock.Mock(side_effect=predict_side_effects)

    def predict(self, input_value, config):
        return self._predict_mock(input_value, config)


def _make_job(item_count: int) -> dict:
    items = [
        {
            "image_url": f"https://example.com/{i}.jpg",
            "entity_type": "Photo",
            "entity_id": i,
        }
        for i in range(item_count)
    ]
    return {"payload": json.dumps({"items": items})}


def _make_api() -> mock.Mock:
    api = mock.Mock()
    api.get_config.return_value = {}
    return api


def test_batch_continues_after_item_failure() -> None:
    moderator = _StubModerator([
        {"verdict": "approved", "model_name": "m", "model_version": "v"},
        ValueError("SSL EOF after 3 retries"),
        {"verdict": "approved", "model_name": "m", "model_version": "v"},
    ])

    result = moderator.handle_batch_job(_make_job(3), _make_api())

    assert len(result["results"]) == 3
    assert result["results"][0]["verdict"] == "approved"
    assert result["results"][0]["entity_id"] == 0
    assert result["results"][1]["verdict"] == "error"
    assert "SSL EOF" in result["results"][1]["error_message"]
    assert result["results"][1]["entity_id"] == 1
    assert result["results"][2]["verdict"] == "approved"
    assert result["results"][2]["entity_id"] == 2


def test_batch_all_items_fail() -> None:
    moderator = _StubModerator([ValueError("fail")] * 3)

    result = moderator.handle_batch_job(_make_job(3), _make_api())

    assert len(result["results"]) == 3
    for i, item_result in enumerate(result["results"]):
        assert item_result["verdict"] == "error"
        assert item_result["error_message"] == "fail"
        assert item_result["entity_id"] == i
