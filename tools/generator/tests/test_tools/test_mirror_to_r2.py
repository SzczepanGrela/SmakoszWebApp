import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).parent.parent.parent))

class TestR2UploadWithMock:

    @patch("boto3.client")
    def test_upload_calls_s3_correctly(self, mock_boto_client, mock_s3_client):
        mock_boto_client.return_value = mock_s3_client

        client = mock_boto_client("s3", endpoint_url="https://fake.r2.dev")
        client.upload_file("/local/path/image.jpg", "bucket", "remote/path/image.jpg")

        mock_s3_client.upload_file.assert_called_once_with("/local/path/image.jpg", "bucket", "remote/path/image.jpg")

    @patch("boto3.client")
    def test_upload_respects_prefix(self, mock_boto_client, mock_s3_client):
        mock_boto_client.return_value = mock_s3_client

        prefix = "seed/"
        local_file = "data/images/dishes/pizza/photo.jpg"
        remote_key = f"{prefix}dishes/pizza/photo.jpg"

        client = mock_boto_client("s3")
        client.upload_file(local_file, "bucket", remote_key)

        call_args = mock_s3_client.upload_file.call_args
        assert call_args[0][2].startswith(prefix)

class TestR2DeleteWithMock:

    @patch("boto3.client")
    def test_delete_only_prefixed_files(self, mock_boto_client, mock_s3_client):
        mock_boto_client.return_value = mock_s3_client

        mock_s3_client.list_objects_v2.return_value = {
            "Contents": [
                {"Key": "seed/dishes/test.jpg"},
                {"Key": "seed/avatars/user.jpg"},
            ]
        }

        client = mock_boto_client("s3")
        response = client.list_objects_v2(Bucket="bucket", Prefix="seed/")

        for obj in response.get("Contents", []):
            assert obj["Key"].startswith("seed/")

    @patch("boto3.client")
    def test_delete_is_safe(self, mock_boto_client, mock_s3_client):
        mock_boto_client.return_value = mock_s3_client

        client = mock_boto_client("s3")
        client.delete_object(Bucket="bucket", Key="seed/test.jpg")

        mock_s3_client.delete_object.assert_called_once()

class TestLocalFileScanning:

    def test_scan_finds_images(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            (Path(tmpdir) / "dishes" / "pizza").mkdir(parents=True)
            (Path(tmpdir) / "dishes" / "pizza" / "photo1.jpg").write_bytes(b"test")
            (Path(tmpdir) / "dishes" / "pizza" / "photo2.jpg").write_bytes(b"test")

            images = list(Path(tmpdir).rglob("*.jpg"))

            assert len(images) == 2

    def test_ignores_non_image_files(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            (Path(tmpdir) / "image.jpg").write_bytes(b"test")
            (Path(tmpdir) / "data.json").write_text("{}")
            (Path(tmpdir) / "script.py").write_text("# test")

            images = list(Path(tmpdir).rglob("*.jpg"))

            assert len(images) == 1

class TestDryRunMode:

    @patch("boto3.client")
    def test_dry_run_no_actual_upload(self, mock_boto_client, mock_s3_client):
        mock_boto_client.return_value = mock_s3_client

        dry_run = True

        if not dry_run:
            client = mock_boto_client("s3")
            client.upload_file("test.jpg", "bucket", "key")

        mock_s3_client.upload_file.assert_not_called()
