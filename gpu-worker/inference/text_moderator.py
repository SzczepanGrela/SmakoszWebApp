import json
import logging

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer

from api.client import WorkerApiClient
from config import Settings
from models.model_manager import ModelManager

logger = logging.getLogger(__name__)

class TextModerator:
    """HerBERT-based toxicity detection."""

    def __init__(self, model_manager: ModelManager, settings: Settings, device: torch.device):
        self.device = device
        self.settings = settings

        model_path = model_manager.get_model_path("herbert", settings.herbert_model_version)
        logger.info("Loading HerBERT from: %s", model_path)

        self.tokenizer = AutoTokenizer.from_pretrained(model_path)
        self.model = AutoModelForSequenceClassification.from_pretrained(
            model_path,
            num_labels=2,
            ignore_mismatched_sizes=True,
        )
        self.model.to(self.device).eval()
        logger.info("HerBERT loaded on %s", self.device)

    def predict(self, text: str, config: dict) -> dict:
        inputs = self.tokenizer(
            text,
            max_length=self.settings.herbert_max_length,
            truncation=True,
            padding=True,
            return_tensors="pt",
        ).to(self.device)

        with torch.no_grad():
            outputs = self.model(**inputs)
            logits = outputs.logits
            probs = torch.sigmoid(logits[0])
            toxicity_score = probs[1].item() if probs.shape[0] > 1 else probs[0].item()

        toxicity_score = round(toxicity_score, 4)
        verdict = self._apply_thresholds(toxicity_score, config)

        return {
            "toxicity_score": toxicity_score,
            "verdict": verdict,
            "model_version": self.settings.herbert_model_version,
        }

    def _apply_thresholds(self, score: float, config: dict) -> str:
        approve = float(config.get("toxicThresholdApprove", 0.3))
        reject = float(config.get("toxicThresholdReject", 0.8))

        if score <= approve:
            return "approved"
        if score >= reject:
            return "rejected"
        return "needs_review"

    def handle_job(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        text = payload["text"]
        config = api.get_config()
        return self.predict(text, config)
