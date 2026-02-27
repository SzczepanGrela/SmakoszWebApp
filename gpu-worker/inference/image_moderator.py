import io
import json
import logging

import httpx
import torch
from PIL import Image
from transformers import (
    AutoImageProcessor,
    AutoModelForImageClassification,
    CLIPModel,
    CLIPProcessor,
)

from api.client import WorkerApiClient
from config import Settings
from models.model_manager import ModelManager

logger = logging.getLogger(__name__)

FOOD_PROMPTS = [
    "a photo of food",
    "a restaurant dish",
    "a meal on a plate",
    "a delicious dish",
]

GENERIC_PROMPTS = [
    "a random photo",
    "not food related",
    "a landscape",
    "a selfie",
]

class ImageModerator:
    """NSFW detection + CLIP on-topic scoring."""

    def __init__(self, model_manager: ModelManager, settings: Settings, device: torch.device):
        self.device = device
        self.settings = settings

        # NSFW model
        nsfw_path = model_manager.get_model_path("nsfw", settings.nsfw_model_version)
        logger.info("Loading NSFW model from: %s", nsfw_path)
        self.nsfw_processor = AutoImageProcessor.from_pretrained(nsfw_path)
        self.nsfw_model = AutoModelForImageClassification.from_pretrained(nsfw_path)
        self.nsfw_model.to(device).eval()

        # CLIP model
        clip_path = model_manager.get_model_path("clip", settings.clip_model_version)
        logger.info("Loading CLIP model from: %s", clip_path)
        self.clip_model = CLIPModel.from_pretrained(clip_path)
        self.clip_processor = CLIPProcessor.from_pretrained(clip_path)
        self.clip_model.to(device).eval()

        logger.info("Image moderation models loaded on %s", device)

    def _download_image(self, url: str) -> Image.Image:
        try:
            resp = httpx.get(url, timeout=30.0, follow_redirects=True)
            resp.raise_for_status()
            return Image.open(io.BytesIO(resp.content)).convert("RGB")
        except httpx.HTTPStatusError as e:
            raise ValueError(f"Failed to download image ({e.response.status_code}): {url}") from e
        except httpx.TimeoutException:
            raise ValueError(f"Timeout downloading image: {url}")
        except Exception as e:
            raise ValueError(f"Failed to process image from {url}: {e}") from e

    def _predict_nsfw(self, image: Image.Image) -> float:
        inputs = self.nsfw_processor(images=image, return_tensors="pt").to(self.device)
        with torch.no_grad():
            outputs = self.nsfw_model(**inputs)
            probs = torch.softmax(outputs.logits, dim=-1)[0]

        label_names = self.nsfw_model.config.id2label
        nsfw_score = 0.0
        for idx, label in label_names.items():
            if "nsfw" in label.lower() or "porn" in label.lower() or "hentai" in label.lower() or "sexy" in label.lower():
                nsfw_score += probs[idx].item()

        return round(nsfw_score, 4)

    def _predict_on_topic(self, image: Image.Image) -> float:
        all_prompts = FOOD_PROMPTS + GENERIC_PROMPTS
        inputs = self.clip_processor(
            text=all_prompts,
            images=image,
            return_tensors="pt",
            padding=True,
        ).to(self.device)

        with torch.no_grad():
            outputs = self.clip_model(**inputs)
            logits = outputs.logits_per_image[0]
            probs = torch.softmax(logits, dim=-1)

        food_score = probs[: len(FOOD_PROMPTS)].sum().item()
        return round(food_score, 4)

    def predict(self, image_url: str, config: dict) -> dict:
        image = self._download_image(image_url)

        nsfw_score = self._predict_nsfw(image)
        on_topic_score = self._predict_on_topic(image)

        verdict = self._apply_thresholds(nsfw_score, on_topic_score, config)

        return {
            "nsfw_score": nsfw_score,
            "on_topic_score": on_topic_score,
            "verdict": verdict,
            "model_version": f"nsfw-{self.settings.nsfw_model_version}_clip-{self.settings.clip_model_version}",
        }

    def _apply_thresholds(self, nsfw_score: float, on_topic_score: float, config: dict) -> str:
        nsfw_reject = float(config.get("nsfwThresholdReject", 0.7))
        nsfw_approve = float(config.get("nsfwThresholdApprove", 0.2))
        on_topic_threshold = float(config.get("onTopicThreshold", 0.3))

        if nsfw_score >= nsfw_reject:
            return "rejected"
        if on_topic_score < on_topic_threshold:
            return "rejected"
        if nsfw_score <= nsfw_approve:
            return "approved"
        return "needs_review"

    def handle_job(self, job: dict, api: WorkerApiClient) -> dict:
        payload = json.loads(job["payload"])
        image_url = payload["image_url"]
        config = api.get_config()
        return self.predict(image_url, config)
