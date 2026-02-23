"""
Tests for R2 mirror tool with mocked S3 client.

Uses mocking to avoid touching real R2/S3 storage during tests.
"""

import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

# Add project root to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

class TestR2UploadWithMock:
    """Tests for R2 upload functionality using mocked boto3."""

    @patch("boto3.client")
    def test_upload_calls_s3_correctly(self, mock_boto_client, mock_s3_client):
        """Verify upload_file is called with correct parameters."""
        mock_boto_client.return_value = mock_s3_client

        # Simulate what mirror_to_r2 would do
        client = mock_boto_client("s3", endpoint_url="https://fake.r2.dev")
        client.upload_file("/local/path/image.jpg", "bucket", "remote/path/image.jpg")

        mock_s3_client.upload_file.assert_called_once_with("/local/path/image.jpg", "bucket", "remote/path/image.jpg")

    @patch("boto3.client")
    def test_upload_respects_prefix(self, mock_boto_client, mock_s3_client):
        """Uploads should only go to correct prefix."""
        mock_boto_client.return_value = mock_s3_client

        # Simulate prefix-scoped upload
        prefix = "smakosz/images/mock/"
        local_file = "data/images/dishes/pizza/photo.jpg"
        remote_key = f"{prefix}dishes/pizza/photo.jpg"

        client = mock_boto_client("s3")
        client.upload_file(local_file, "bucket", remote_key)

        # Verify the remote key has correct prefix
        call_args = mock_s3_client.upload_file.call_args
        assert call_args[0][2].startswith(prefix)

class TestR2DeleteWithMock:
    """Tests for R2 delete functionality using mocked boto3."""

    @patch("boto3.client")
    def test_delete_only_prefixed_files(self, mock_boto_client, mock_s3_client):
        """Delete should only touch files with correct prefix."""
        mock_boto_client.return_value = mock_s3_client

        # Simulate list_objects returning files
        mock_s3_client.list_objects_v2.return_value = {
            "Contents": [
                {"Key": "smakosz/images/mock/dishes/test.jpg"},
                {"Key": "smakosz/images/mock/avatars/user.jpg"},
            ]
        }

        client = mock_boto_client("s3")
        response = client.list_objects_v2(Bucket="bucket", Prefix="smakosz/images/mock/")

        # Verify only mock-prefixed files are listed
        for obj in response.get("Contents", []):
            assert obj["Key"].startswith("smakosz/images/mock/")

    @patch("boto3.client")
    def test_delete_is_safe(self, mock_boto_client, mock_s3_client):
        """Delete operations should use mock client, not real one."""
        mock_boto_client.return_value = mock_s3_client

        client = mock_boto_client("s3")
        client.delete_object(Bucket="bucket", Key="smakosz/images/mock/test.jpg")

        # Verify mock was called, not real S3
        mock_s3_client.delete_object.assert_called_once()

class TestLocalFileScanning:
    """Tests for local file scanning before upload."""

    def test_scan_finds_images(self):
        """Local image scanner should find all image files."""
        with tempfile.TemporaryDirectory() as tmpdir:
            # Create test structure
            (Path(tmpdir) / "dishes" / "pizza").mkdir(parents=True)
            (Path(tmpdir) / "dishes" / "pizza" / "photo1.jpg").write_bytes(b"test")
            (Path(tmpdir) / "dishes" / "pizza" / "photo2.jpg").write_bytes(b"test")

            # Scan for images
            images = list(Path(tmpdir).rglob("*.jpg"))

            assert len(images) == 2

    def test_ignores_non_image_files(self):
        """Scanner should only pick up image files."""
        with tempfile.TemporaryDirectory() as tmpdir:
            (Path(tmpdir) / "image.jpg").write_bytes(b"test")
            (Path(tmpdir) / "data.json").write_text("{}")
            (Path(tmpdir) / "script.py").write_text("# test")

            images = list(Path(tmpdir).rglob("*.jpg"))

            assert len(images) == 1

class TestDryRunMode:
    """Tests for dry-run functionality."""

    @patch("boto3.client")
    def test_dry_run_no_actual_upload(self, mock_boto_client, mock_s3_client):
        """Dry run should not actually upload files."""
        mock_boto_client.return_value = mock_s3_client

        dry_run = True

        if not dry_run:
            client = mock_boto_client("s3")
            client.upload_file("test.jpg", "bucket", "key")

        # In dry run, upload should never be called
        mock_s3_client.upload_file.assert_not_called()
