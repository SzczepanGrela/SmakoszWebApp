import logging
from logging.handlers import RotatingFileHandler

from utils.logging_config import LoggingConfig

def test_logging_setup_default():
    logger = LoggingConfig.setup(level="INFO")
    assert logger.level == logging.INFO

def test_logging_setup_quiet():
    logger = LoggingConfig.setup(level="INFO", quiet=True)
    assert logger.level == logging.WARNING
    assert LoggingConfig.is_quiet() is True

def test_logging_setup_debug():
    logger = LoggingConfig.setup(level="DEBUG")
    assert logger.level == logging.DEBUG

def test_log_rotation_configured():
    LoggingConfig.setup(level="INFO", log_file=True)
    root = logging.getLogger()

    rotating_handlers = [h for h in root.handlers if isinstance(h, RotatingFileHandler)]

    assert len(rotating_handlers) > 0
    handler = rotating_handlers[0]
    assert handler.maxBytes == 10 * 1024 * 1024
    assert handler.backupCount == 5

def test_console_handler_disabled_in_quiet():
    LoggingConfig.setup(level="INFO", quiet=True, console=True)
    root = logging.getLogger()

    console_handlers = [
        h
        for h in root.handlers
        if isinstance(h, logging.StreamHandler) and hasattr(h.stream, "name") and h.stream.name == "<stdout>"
    ]

    assert len(console_handlers) == 0

def test_is_quiet_helper():
    LoggingConfig.setup(level="INFO", quiet=False)
    assert LoggingConfig.is_quiet() is False

    LoggingConfig.setup(level="INFO", quiet=True)
    assert LoggingConfig.is_quiet() is True

def test_get_level_helper():
    LoggingConfig.setup(level="INFO")
    assert LoggingConfig.get_level() == logging.INFO

    LoggingConfig.setup(level="DEBUG")
    assert LoggingConfig.get_level() == logging.DEBUG

def test_noisy_loggers_silenced():
    LoggingConfig.setup(level="DEBUG")

    assert logging.getLogger("urllib3").level == logging.WARNING
    assert logging.getLogger("PIL").level == logging.WARNING
    assert logging.getLogger("blurhash").level == logging.WARNING
    assert logging.getLogger("boto3").level == logging.WARNING
