import pytest

from inference.image_moderator import ImageModerator
from inference.text_moderator import TextModerator


@pytest.fixture
def text_mod() -> TextModerator:
    return TextModerator.__new__(TextModerator)


@pytest.fixture
def image_mod() -> ImageModerator:
    return ImageModerator.__new__(ImageModerator)


# Text config defaults: toxicThresholdApprove=0.3, toxicThresholdReject=0.8

def test_text_low_score_approved(text_mod: TextModerator) -> None:
    assert text_mod._apply_thresholds(0.1, {}) == "approved"


def test_text_boundary_at_approve_is_approved(text_mod: TextModerator) -> None:
    assert text_mod._apply_thresholds(0.3, {}) == "approved"


def test_text_high_score_rejected(text_mod: TextModerator) -> None:
    assert text_mod._apply_thresholds(0.9, {}) == "rejected"


def test_text_mid_score_needs_review(text_mod: TextModerator) -> None:
    assert text_mod._apply_thresholds(0.5, {}) == "needs_review"


def test_text_custom_config_overrides_defaults(text_mod: TextModerator) -> None:
    cfg = {"toxicThresholdApprove": 0.1, "toxicThresholdReject": 0.4}
    assert text_mod._apply_thresholds(0.5, cfg) == "rejected"


# Image config defaults: nsfwThresholdReject=0.7, nsfwThresholdApprove=0.2, onTopicThreshold=0.3

def test_image_high_nsfw_rejected(image_mod: ImageModerator) -> None:
    assert image_mod._apply_thresholds(0.9, 0.9, {}) == "rejected"


def test_image_off_topic_rejected(image_mod: ImageModerator) -> None:
    assert image_mod._apply_thresholds(0.0, 0.1, {}) == "rejected"


def test_image_clean_and_on_topic_approved(image_mod: ImageModerator) -> None:
    assert image_mod._apply_thresholds(0.1, 0.9, {}) == "approved"


def test_image_mid_nsfw_needs_review(image_mod: ImageModerator) -> None:
    assert image_mod._apply_thresholds(0.5, 0.9, {}) == "needs_review"
