import logging

import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer

from config import Settings
from handlers.batch_mixin import BatchJobMixin
from handlers.protocol import JobMapping, ModelRequirement
from handlers.result import make_result
from models.model_manager import ModelManager

logger = logging.getLogger(__name__)

class TextModerator(BatchJobMixin):
    PHASE_NAME = "loading_herbert"
    MODELS = [
        ModelRequirement(name="herbert", hf_repo="allegro/herbert-base-cased", version_env_key="herbert_model_version"),
    ]
    JOB_MAPPINGS = [
        JobMapping("text_moderation", "handle_job"),
        JobMapping("text_moderation_batch", "handle_batch_job"),
    ]
    BATCH_INPUT_KEY = "text"

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

        return make_result(
            model_name="allegro/herbert-base-cased",
            model_version=self.settings.herbert_model_version,
            verdict=verdict,
            toxicity_score=toxicity_score,
        )

    def _apply_thresholds(self, score: float, config: dict) -> str:
        approve = float(config.get("toxicThresholdApprove", 0.3))
        reject = float(config.get("toxicThresholdReject", 0.8))

        if score <= approve:
            return "approved"
        if score >= reject:
            return "rejected"
        return "needs_review"
