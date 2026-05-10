import io
from unittest import mock

import httpx
import pytest
from PIL import Image

from inference import image_moderator
from inference.image_moderator import (
    BACKOFF_SECONDS,
    MAX_DOWNLOAD_ATTEMPTS,
    USER_AGENT,
    ImageModerator,
)


def _make_png_response(status_code: int = 200) -> mock.Mock:
    buf = io.BytesIO()
    Image.new("RGB", (8, 8), color=(255, 0, 0)).save(buf, format="PNG")
    resp = mock.Mock(spec=httpx.Response)
    resp.status_code = status_code
    resp.content = buf.getvalue()
    resp.raise_for_status = mock.Mock()
    if status_code >= 400:
        resp.raise_for_status.side_effect = httpx.HTTPStatusError(
            message="error", request=mock.Mock(), response=resp,
        )
    return resp


@pytest.fixture
def moderator() -> ImageModerator:
    return ImageModerator.__new__(ImageModerator)


def test_download_succeeds_first_attempt(moderator: ImageModerator) -> None:
    url = "https://assets.smakosz.xyz/photo.jpg"
    with mock.patch.object(image_moderator.httpx, "get", return_value=_make_png_response()) as get_mock, \
         mock.patch.object(image_moderator.time, "sleep") as sleep_mock:
        img = moderator._download_image(url)

    assert isinstance(img, Image.Image)
    assert get_mock.call_count == 1
    assert sleep_mock.call_count == 0
    _, kwargs = get_mock.call_args
    assert kwargs["headers"]["User-Agent"] == USER_AGENT


def test_download_retries_then_succeeds(moderator: ImageModerator) -> None:
    url = "https://assets.smakosz.xyz/photo.jpg"
    side_effects = [
        httpx.ConnectError("SSL: UNEXPECTED_EOF_WHILE_READING"),
        httpx.ConnectError("SSL: UNEXPECTED_EOF_WHILE_READING"),
        _make_png_response(),
    ]
    with mock.patch.object(image_moderator.httpx, "get", side_effect=side_effects) as get_mock, \
         mock.patch.object(image_moderator.time, "sleep") as sleep_mock:
        img = moderator._download_image(url)

    assert isinstance(img, Image.Image)
    assert get_mock.call_count == 3
    assert sleep_mock.call_args_list == [
        mock.call(BACKOFF_SECONDS[0]),
        mock.call(BACKOFF_SECONDS[1]),
    ]


def test_download_exhausts_retries_raises_connecterror(moderator: ImageModerator) -> None:
    url = "https://assets.smakosz.xyz/photo.jpg"
    with mock.patch.object(image_moderator.httpx, "get", side_effect=httpx.ConnectError("SSL EOF")) as get_mock, \
         mock.patch.object(image_moderator.time, "sleep"):
        with pytest.raises(httpx.ConnectError):
            moderator._download_image(url)

    assert get_mock.call_count == MAX_DOWNLOAD_ATTEMPTS


def test_download_http_404_raises_valueerror_no_retry(moderator: ImageModerator) -> None:
    url = "https://assets.smakosz.xyz/missing.jpg"
    with mock.patch.object(image_moderator.httpx, "get", return_value=_make_png_response(status_code=404)) as get_mock, \
         mock.patch.object(image_moderator.time, "sleep") as sleep_mock:
        with pytest.raises(ValueError, match="404"):
            moderator._download_image(url)

    assert get_mock.call_count == 1
    assert sleep_mock.call_count == 0


def test_download_timeout_raises_valueerror_no_retry(moderator: ImageModerator) -> None:
    url = "https://assets.smakosz.xyz/slow.jpg"
    with mock.patch.object(image_moderator.httpx, "get", side_effect=httpx.TimeoutException("timed out")) as get_mock, \
         mock.patch.object(image_moderator.time, "sleep") as sleep_mock:
        with pytest.raises(ValueError, match="Timeout"):
            moderator._download_image(url)

    assert get_mock.call_count == 1
    assert sleep_mock.call_count == 0
