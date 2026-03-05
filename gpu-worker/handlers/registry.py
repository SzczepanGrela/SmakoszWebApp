from handlers.protocol import JobHandler
from inference.text_moderator import TextModerator
from inference.image_moderator import ImageModerator
from training.ncf_trainer import NcfTrainer

# Order = phase order in batch_run
HANDLER_CLASSES: list[type[JobHandler]] = [
    TextModerator,
    ImageModerator,
    NcfTrainer,
]
