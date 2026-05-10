import json

from api.client import WorkerApiClient


class BatchJobMixin:
    BATCH_INPUT_KEY: str  # "text" / "image_url"

    def predict(self, input_value, config: dict) -> dict:
        raise NotImplementedError

    def handle_job(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        config = api.get_config()
        return self.predict(payload[self.BATCH_INPUT_KEY], config)

    def handle_batch_job(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        items = payload["items"]
        config = api.get_config()
        results = []
        for item in items:
            try:
                prediction = self.predict(item[self.BATCH_INPUT_KEY], config)
            except Exception as e:
                prediction = {
                    "verdict": "error",
                    "error_message": str(e),
                }
            prediction["entity_type"] = item["entity_type"]
            prediction["entity_id"] = item["entity_id"]
            results.append(prediction)
        return {"results": results}
