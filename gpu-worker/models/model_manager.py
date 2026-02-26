import logging
import shutil
from pathlib import Path

import boto3
from botocore.exceptions import ClientError

from config import Settings

logger = logging.getLogger(__name__)

HF_MODEL_MAPPING = {
    "herbert": "allegro/herbert-base-cased",
    "nsfw": "Marqo/nsfw-image-detection-384",
    "clip": "openai/clip-vit-base-patch32",
}

class ModelManager:
    """Model management: R2 primary, HuggingFace fallback, local cache."""

    def __init__(self, settings: Settings, cache_dir: Path | None = None):
        self._settings = settings
        self._cache_dir = cache_dir or Path("model_cache")
        self._cache_dir.mkdir(parents=True, exist_ok=True)

        self._s3 = None
        if settings.r2_endpoint and settings.r2_access_key:
            try:
                self._s3 = boto3.client(
                    "s3",
                    endpoint_url=settings.r2_endpoint,
                    aws_access_key_id=settings.r2_access_key,
                    aws_secret_access_key=settings.r2_secret_key,
                    region_name="auto",
                )
            except Exception:
                logger.warning("Failed to initialize R2 client, will use HuggingFace only")

    def get_model_path(self, model_name: str, version: str) -> Path | str:
        """
        1. Check local cache: model_cache/{model_name}/{version}/
        2. If missing -> download from R2: {bucket}/{model_name}/{version}/
        3. If R2 fails -> download from HuggingFace (returns HF repo name for from_pretrained)
        4. Return path to cache or HF repo name
        """
        cache_path = self._cache_dir / model_name / version
        if cache_path.exists() and any(cache_path.iterdir()):
            logger.info("Using cached model: %s/%s", model_name, version)
            return cache_path

        r2_path = self._download_from_r2(model_name, version)
        if r2_path is not None:
            return r2_path

        return self._download_from_huggingface(model_name)

    def _download_from_r2(self, model_name: str, version: str) -> Path | None:
        if self._s3 is None:
            return None

        prefix = f"{model_name}/{version}/"
        cache_path = self._cache_dir / model_name / version

        try:
            response = self._s3.list_objects_v2(
                Bucket=self._settings.r2_bucket,
                Prefix=prefix,
            )
            contents = response.get("Contents", [])
            if not contents:
                logger.info("No model found in R2 at %s", prefix)
                return None

            cache_path.mkdir(parents=True, exist_ok=True)

            for obj in contents:
                key = obj["Key"]
                rel_path = key[len(prefix) :]
                if not rel_path:
                    continue
                local_file = cache_path / rel_path
                local_file.parent.mkdir(parents=True, exist_ok=True)
                logger.info("Downloading from R2: %s", key)
                self._s3.download_file(self._settings.r2_bucket, key, str(local_file))

            logger.info("Downloaded model from R2: %s/%s", model_name, version)
            return cache_path
        except ClientError as e:
            logger.warning("R2 download failed for %s/%s: %s", model_name, version, e)
            if cache_path.exists():
                shutil.rmtree(cache_path, ignore_errors=True)
            return None

    def _download_from_huggingface(self, model_name: str) -> str:
        hf_repo = HF_MODEL_MAPPING.get(model_name)
        if not hf_repo:
            raise ValueError(f"No HuggingFace mapping for model: {model_name}")
        logger.info("Using HuggingFace model: %s -> %s", model_name, hf_repo)
        return hf_repo
