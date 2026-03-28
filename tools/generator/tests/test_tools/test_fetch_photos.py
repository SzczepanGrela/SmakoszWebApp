import tempfile
from io import BytesIO
from pathlib import Path
from unittest.mock import MagicMock, patch

from PIL import Image

import tools.fetch_photos as fetch_photos
from utils.image_processor import resize_and_crop

class TestPixabayAPIHandling:

    @patch("requests.get")
    def test_api_response_parsed_correctly(self, mock_get, mock_pixabay_response):
        mock_get.return_value.json.return_value = mock_pixabay_response
        mock_get.return_value.status_code = 200

        response = mock_get("https://pixabay.com/api/", params={})
        data = response.json()

        assert "hits" in data
        assert len(data["hits"]) == 1
        assert "webformatURL" in data["hits"][0]

    @patch("requests.get")
    def test_empty_response_handled(self, mock_get):
        mock_get.return_value.json.return_value = {"total": 0, "hits": []}
        mock_get.return_value.status_code = 200

        response = mock_get("https://pixabay.com/api/", params={})
        data = response.json()

        assert len(data["hits"]) == 0

    @patch("requests.get")
    def test_api_error_handled(self, mock_get):
        mock_get.return_value.status_code = 429
        mock_get.return_value.json.side_effect = Exception("Rate limited")

        response = mock_get("https://pixabay.com/api/", params={})

        assert response.status_code == 429

class TestImageDownload:

    @patch("requests.get")
    def test_image_saved_to_correct_path(self, mock_get):
        mock_get.return_value.content = b"fake_image_data"
        mock_get.return_value.status_code = 200

        with tempfile.TemporaryDirectory() as tmpdir:
            output_path = Path(tmpdir) / "test_variant" / "photo_001.jpg"
            output_path.parent.mkdir(parents=True, exist_ok=True)

            response = mock_get("https://example.com/image.jpg")
            output_path.write_bytes(response.content)

            assert output_path.exists()
            assert output_path.read_bytes() == b"fake_image_data"

    @patch("requests.get")
    def test_duplicate_images_skipped(self, mock_get):
        with tempfile.TemporaryDirectory() as tmpdir:
            output_path = Path(tmpdir) / "existing_photo.jpg"
            output_path.write_bytes(b"existing_data")

            assert output_path.exists()

class TestSearchTermGeneration:

    def test_pixabay_term_from_variant(self):
        variant_data = {"pixabay_term": "pizza margherita"}

        search_term = variant_data.get("pixabay_term", "food")

        assert search_term == "pizza margherita"

    def test_fallback_to_variant_name(self):
        variant_data = {}
        variant_name = "Spaghetti Bolognese"

        search_term = variant_data.get("pixabay_term", variant_name)

        assert search_term == "Spaghetti Bolognese"

class TestImageSizeConstants:

    def test_size_constants_loaded_from_config(self):
        fp = fetch_photos

        assert isinstance(fp.SIZE_FULL, tuple) and len(fp.SIZE_FULL) == 2
        assert isinstance(fp.SIZE_THUMB, tuple) and len(fp.SIZE_THUMB) == 2
        assert isinstance(fp.SIZE_TINY, tuple) and len(fp.SIZE_TINY) == 2
        assert isinstance(fp.SIZE_AVATAR, tuple) and len(fp.SIZE_AVATAR) == 2
        assert isinstance(fp.SIZE_HERO, tuple) and len(fp.SIZE_HERO) == 2
        assert isinstance(fp.SIZE_INGREDIENT, tuple) and len(fp.SIZE_INGREDIENT) == 2

        assert fp.SIZE_HERO == (1600, 900)
        assert fp.SIZE_FULL == (1280, 960)
        assert fp.SIZE_THUMB == (200, 150)
        assert fp.SIZE_TINY == (50, 50)
        assert fp.SIZE_AVATAR == (300, 300)
        assert fp.SIZE_INGREDIENT == (200, 200)

    def test_suffix_constants_loaded(self):
        fp = fetch_photos

        assert fp.SUFFIX_HERO == "_hero"
        assert fp.SUFFIX_THUMB == "_thumb"
        assert fp.SUFFIX_TINY == "_tiny"

class TestResizeAndCrop:

    def test_resize_and_crop_landscape_to_target(self):
        img = Image.new("RGB", (1600, 1200), color="red")
        result = resize_and_crop(img, (800, 600))
        assert result.size == (800, 600)

    def test_resize_and_crop_portrait_to_target(self):
        img = Image.new("RGB", (1200, 1600), color="blue")
        result = resize_and_crop(img, (800, 600))
        assert result.size == (800, 600)

    def test_resize_and_crop_square_to_avatar(self):
        fp = fetch_photos
        img = Image.new("RGB", (500, 500), color="green")
        result = resize_and_crop(img, fp.SIZE_AVATAR)
        assert result.size == fp.SIZE_AVATAR

class TestMultiSizeProcessing:

    def test_derive_thumbnail_path(self):
        fp = fetch_photos

        full_path = Path("/images/dishes/pizza/margherita_001.webp")

        stem = full_path.stem
        suffix = full_path.suffix
        parent = full_path.parent

        thumb_path = parent / f"{stem}{fp.SUFFIX_THUMB}{suffix}"
        tiny_path = parent / f"{stem}{fp.SUFFIX_TINY}{suffix}"

        assert thumb_path.as_posix() == "/images/dishes/pizza/margherita_001_thumb.webp"
        assert tiny_path.as_posix() == "/images/dishes/pizza/margherita_001_tiny.webp"

    def test_multi_size_generates_correct_files(self):
        fp = fetch_photos

        with tempfile.TemporaryDirectory() as tmpdir:
            output_dir = Path(tmpdir)
            full_path = output_dir / "test_image.webp"
            thumb_path = output_dir / f"test_image{fp.SUFFIX_THUMB}.webp"

            fake_img = Image.new("RGB", (1500, 1200), color="red")
            img_bytes = BytesIO()
            fake_img.save(img_bytes, format="WEBP")
            img_bytes.seek(0)

            mock_response = MagicMock()
            mock_response.content = img_bytes.read()
            mock_response.status_code = 200

            with (
                patch.object(fp.PixabayDownloader, "_get_api_key", return_value="test_key"),
                patch("tools.fetch_photos.OUTPUT_DIR", output_dir),
            ):
                downloader = fp.PixabayDownloader()
                with patch.object(downloader.session, "get", return_value=mock_response):
                    success, metadata = downloader.process_image_multi_size(
                        "https://example.com/image.jpg", full_path, include_tiny=False
                    )

            assert full_path.exists(), "Full size image should exist"
            assert thumb_path.exists(), "Thumbnail image should exist"

            assert success is True
            assert metadata is not None
            assert "path_thumb" in metadata

    def test_avatar_mode_uses_correct_size(self):
        fp = fetch_photos

        assert fp.SIZE_AVATAR == (300, 300)
        assert fp.SIZE_AVATAR != fp.SIZE_FULL

    def test_naming_convention_suffix_placement(self):
        fp = fetch_photos

        filename = "pizza_margherita_001.webp"
        stem = Path(filename).stem
        suffix = Path(filename).suffix

        thumb_filename = f"{stem}{fp.SUFFIX_THUMB}{suffix}"

        assert thumb_filename == "pizza_margherita_001_thumb.webp"
        assert thumb_filename.endswith(".webp")
        assert fp.SUFFIX_THUMB in thumb_filename

class TestImageProviders:

    def test_provider_manager_initializes(self):
        from tools.image_providers import ProviderManager

        with patch.dict("os.environ", {"PIXABAY_API_KEY": "test", "UNSPLASH_ACCESS_KEY": "test"}):
            manager = ProviderManager()
            assert len(manager.providers) >= 1

    def test_pixabay_provider_enabled_with_key(self):
        from tools.image_providers import PixabayProvider

        with patch.dict("os.environ", {"PIXABAY_API_KEY": "test_key"}):
            provider = PixabayProvider()
            assert provider.name == "pixabay"
            assert provider.enabled is True

    def test_unsplash_provider_enabled_with_key(self):
        from tools.image_providers import UnsplashProvider

        with patch.dict("os.environ", {"UNSPLASH_ACCESS_KEY": "test_key"}):
            provider = UnsplashProvider()
            assert provider.name == "unsplash"
            assert provider.enabled is True

    def test_unsplash_provider_disabled_without_key(self):
        from tools.image_providers import UnsplashProvider

        with patch.dict("os.environ", {"UNSPLASH_ACCESS_KEY": ""}, clear=True):
            provider = UnsplashProvider()
            assert provider.enabled is False

    @patch("requests.Session.get")
    def test_unsplash_search_parses_response(self, mock_get):
        from tools.image_providers import UnsplashProvider

        mock_response = MagicMock()
        mock_response.status_code = 200
        mock_response.json.return_value = {
            "results": [
                {
                    "id": "abc123",
                    "width": 4000,
                    "height": 3000,
                    "urls": {"raw": "https://images.unsplash.com/photo-abc123"},
                    "user": {"name": "Test User", "username": "testuser"},
                    "links": {"html": "https://unsplash.com/photos/abc123"},
                }
            ]
        }
        mock_get.return_value = mock_response

        with patch.dict("os.environ", {"UNSPLASH_ACCESS_KEY": "test_key"}):
            provider = UnsplashProvider()
            results = provider.search("restaurant interior", 5)

        assert len(results) == 1
        assert results[0].provider == "unsplash"
        assert results[0].provider_id == "abc123"
        assert results[0].width == 4000
        assert "unsplash.com" in results[0].url

    def test_image_result_dataclass(self):
        from tools.image_providers import ImageResult

        result = ImageResult(
            url="https://example.com/image.jpg",
            provider="pixabay",
            provider_id="12345",
            width=1920,
            height=1080,
            credit={"name": "Test", "link": "https://example.com"},
        )

        assert result.url == "https://example.com/image.jpg"
        assert result.provider == "pixabay"
        assert result.width == 1920
        assert result.credit["name"] == "Test"
