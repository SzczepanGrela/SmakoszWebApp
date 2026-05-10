from handlers.protocol import JobHandler
from inference.image_moderator import ImageModerator
from inference.text_moderator import TextModerator
from training.ncf_trainer import NcfTrainer

HANDLER_CLASSES: list[type[JobHandler]] = [
    TextModerator,
    ImageModerator,
    NcfTrainer,
]
