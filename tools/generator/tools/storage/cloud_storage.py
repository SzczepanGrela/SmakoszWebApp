"""
Cloud Storage Provider abstraction.

Provides a stable interface for cloud object storage operations so that
mirror_to_r2.py (and future tools) can work with any S3-compatible provider
(Cloudflare R2, AWS S3, GCS, MinIO) without modification.

Usage:
    # Default - uses environment variables for R2:
    provider = R2Provider.from_env()

    # Explicit configuration:
    provider = R2Provider(
        endpoint_url="https://<account>.r2.cloudflarestorage.com",
        access_key_id="...",
        secret_access_key="...",
        bucket_name="my-bucket",
    )

    # Dependency injection in tests:
    class MockProvider(CloudStorageProvider):
        def list_keys(self, prefix=""): return set()
        def upload_file(self, local_path, remote_key): return True
        def delete_batch(self, keys): return len(keys)
"""

from __future__ import annotations

import logging
import mimetypes
import os
from abc import ABC, abstractmethod
from pathlib import Path

logger = logging.getLogger(__name__)

class CloudStorageProvider(ABC):
    """
    Abstract base class for cloud object storage providers.

    Implement this interface to add support for a new storage backend
    (AWS S3, Google Cloud Storage, MinIO, etc.) without modifying
    any existing mirror or sync code.
    """

    @abstractmethod
    def list_keys(self, prefix: str = "") -> set[str]:
        """
        Return all object keys in the bucket, optionally filtered by prefix.

        Args:
            prefix: Only return keys that start with this string.
                    Empty string returns all keys.

        Returns:
            Set of key strings found in the bucket.
        """
        ...

    @abstractmethod
    def upload_file(self, local_path: Path, remote_key: str) -> bool:
        """
        Upload a local file to the storage backend.

        Args:
            local_path: Absolute path to the local file.
            remote_key: Target key (path) in the bucket.

        Returns:
            True on success, False on failure.
        """
        ...

    @abstractmethod
    def delete_batch(self, keys: list[str]) -> int:
        """
        Delete multiple objects from the storage backend.

        Args:
            keys: List of object keys to delete.

        Returns:
            Number of objects successfully deleted.
        """
        ...

class R2Provider(CloudStorageProvider):
    """
    Cloudflare R2 implementation of CloudStorageProvider.

    Uses boto3 with the S3-compatible R2 endpoint.
    """

    def __init__(
        self,
        endpoint_url: str,
        access_key_id: str,
        secret_access_key: str,
        bucket_name: str,
    ):
        import boto3

        self.bucket_name = bucket_name
        self._client = boto3.client(
            service_name="s3",
            endpoint_url=endpoint_url,
            aws_access_key_id=access_key_id,
            aws_secret_access_key=secret_access_key,
        )

    @classmethod
    def from_env(cls) -> R2Provider:
        """
        Create an R2Provider from environment variables.

        Required env vars:
            R2_ENDPOINT_URL, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET_NAME
        """
        endpoint_url = os.getenv("R2_ENDPOINT_URL", "")
        access_key_id = os.getenv("R2_ACCESS_KEY_ID", "")
        secret_access_key = os.getenv("R2_SECRET_ACCESS_KEY", "")
        bucket_name = os.getenv("R2_BUCKET_NAME", "")

        if not all([endpoint_url, access_key_id, secret_access_key, bucket_name]):
            missing = [
                name for name, val in [
                    ("R2_ENDPOINT_URL", endpoint_url),
                    ("R2_ACCESS_KEY_ID", access_key_id),
                    ("R2_SECRET_ACCESS_KEY", secret_access_key),
                    ("R2_BUCKET_NAME", bucket_name),
                ]
                if not val
            ]
            raise ValueError(f"Missing R2 environment variables: {', '.join(missing)}")

        return cls(
            endpoint_url=endpoint_url,
            access_key_id=access_key_id,
            secret_access_key=secret_access_key,
            bucket_name=bucket_name,
        )

    def list_keys(self, prefix: str = "") -> set[str]:
        """List all object keys in the R2 bucket with optional prefix filter."""
        from botocore.exceptions import ClientError

        logger.info(f"Fetching object list from R2 (prefix={prefix!r})...")
        keys: set[str] = set()
        paginator = self._client.get_paginator("list_objects_v2")

        try:
            kwargs: dict = {"Bucket": self.bucket_name}
            if prefix:
                kwargs["Prefix"] = prefix
            for page in paginator.paginate(**kwargs):
                if "Contents" in page:
                    for obj in page["Contents"]:
                        keys.add(obj["Key"])
        except ClientError as e:
            logger.error(f"Failed to list objects: {e}")
            raise

        logger.info(f"Found {len(keys)} objects in R2.")
        return keys

    def upload_file(self, local_path: Path, remote_key: str) -> bool:
        """Upload a local file to R2 with appropriate Content-Type and cache headers."""
        from botocore.exceptions import ClientError

        mime_type, _ = mimetypes.guess_type(local_path)
        if not mime_type:
            mime_type = "application/octet-stream"

        try:
            self._client.upload_file(
                str(local_path),
                self.bucket_name,
                remote_key,
                ExtraArgs={
                    "ContentType": mime_type,
                    "CacheControl": "public, max-age=31536000",
                },
            )
            return True
        except ClientError as e:
            logger.error(f"Failed to upload {remote_key}: {e}")
            return False

    def delete_batch(self, keys: list[str]) -> int:
        """Delete objects in batches of 1000 (S3 API limit)."""
        from botocore.exceptions import ClientError

        if not keys:
            return 0

        deleted_count = 0
        chunk_size = 1000

        for i in range(0, len(keys), chunk_size):
            chunk = keys[i : i + chunk_size]
            delete_request = {"Objects": [{"Key": k} for k in chunk]}
            try:
                self._client.delete_objects(Bucket=self.bucket_name, Delete=delete_request)
                deleted_count += len(chunk)
            except ClientError as e:
                logger.error(f"Failed to delete batch: {e}")

        return deleted_count
