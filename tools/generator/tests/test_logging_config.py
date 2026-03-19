"""Tests for centralized logging configuration."""

import logging
from logging.handlers import RotatingFileHandler

from utils.logging_config import LoggingConfig

def test_logging_setup_default():
    """Test default logging setup."""
    logger = LoggingConfig.setup(level="INFO")
    assert logger.level == logging.INFO

def test_logging_setup_quiet():
    """Test quiet mode sets WARNING level."""
    logger = LoggingConfig.setup(level="INFO", quiet=True)
    assert logger.level == logging.WARNING
    assert LoggingConfig.is_quiet() is True

def test_logging_setup_debug():
    """Test debug mode sets DEBUG level."""
    logger = LoggingConfig.setup(level="DEBUG")
    assert logger.level == logging.DEBUG

def test_log_rotation_configured():
    """Test that rotating file handler is configured."""
    LoggingConfig.setup(level="INFO", log_file=True)
    root = logging.getLogger()

    # Find rotating file handler
    rotating_handlers = [
        h for h in root.handlers if isinstance(h, RotatingFileHandler)
    ]

    assert len(rotating_handlers) > 0
    handler = rotating_handlers[0]
    assert handler.maxBytes == 10 * 1024 * 1024  # 10MB
    assert handler.backupCount == 5

def test_console_handler_disabled_in_quiet():
    """Test console handler is not added in quiet mode."""
    LoggingConfig.setup(level="INFO", quiet=True, console=True)
    root = logging.getLogger()

    # In quiet mode, should only have file handler
    console_handlers = [
        h
        for h in root.handlers
        if isinstance(h, logging.StreamHandler)
        and hasattr(h.stream, "name")
        and h.stream.name == "<stdout>"
    ]

    assert len(console_handlers) == 0

def test_is_quiet_helper():
    """Test is_quiet() helper function."""
    LoggingConfig.setup(level="INFO", quiet=False)
    assert LoggingConfig.is_quiet() is False

    LoggingConfig.setup(level="INFO", quiet=True)
    assert LoggingConfig.is_quiet() is True

def test_get_level_helper():
    """Test get_level() helper function."""
    LoggingConfig.setup(level="INFO")
    assert LoggingConfig.get_level() == logging.INFO

    LoggingConfig.setup(level="DEBUG")
    assert LoggingConfig.get_level() == logging.DEBUG

def test_noisy_loggers_silenced():
    """Test that third-party loggers are silenced."""
    LoggingConfig.setup(level="DEBUG")

    # Check that noisy loggers are set to WARNING
    assert logging.getLogger("urllib3").level == logging.WARNING
    assert logging.getLogger("PIL").level == logging.WARNING
    assert logging.getLogger("blurhash").level == logging.WARNING
    assert logging.getLogger("boto3").level == logging.WARNING
