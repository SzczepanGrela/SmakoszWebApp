"""Centralized logging configuration for MockDataFactory."""

import logging
import sys
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Literal

LogLevel = Literal["DEBUG", "INFO", "WARNING", "ERROR"]

# Module-level flag for quiet mode (used by tqdm)
_quiet_mode = False

class LoggingConfig:
    """Centralized logging configuration manager."""

    # Standard format for all scripts
    CONSOLE_FORMAT = "%(levelname)-8s | %(message)s"
    FILE_FORMAT = "%(asctime)s | %(name)-25s | %(levelname)-8s | %(message)s"
    DATE_FORMAT = "%Y-%m-%d %H:%M:%S"

    # Log file settings
    LOG_FILE = Path("mockdata_generation.log")
    MAX_BYTES = 10 * 1024 * 1024  # 10 MB
    BACKUP_COUNT = 5

    @classmethod
    def setup(
        cls,
        level: LogLevel = "INFO",
        log_file: bool = True,
        console: bool = True,
        quiet: bool = False,
    ) -> logging.Logger:
        """
        Setup logging with specified configuration.

        Args:
            level: Log level (DEBUG, INFO, WARNING, ERROR)
            log_file: Enable file logging with rotation
            console: Enable console logging
            quiet: Quiet mode (only WARNING/ERROR, disables progress bars)

        Returns:
            Configured root logger
        """
        global _quiet_mode
        _quiet_mode = quiet

        # Determine effective level
        if quiet:
            effective_level = logging.WARNING
        else:
            effective_level = getattr(logging, level.upper())

        # Root logger configuration
        root_logger = logging.getLogger()
        root_logger.setLevel(effective_level)
        root_logger.handlers.clear()  # Clear existing handlers

        # Console handler (stdout)
        if console and not quiet:
            console_handler = logging.StreamHandler(sys.stdout)
            console_handler.setLevel(effective_level)
            console_formatter = logging.Formatter(cls.CONSOLE_FORMAT, datefmt=cls.DATE_FORMAT)
            console_handler.setFormatter(console_formatter)
            root_logger.addHandler(console_handler)

        # File handler (rotating)
        if log_file:
            file_handler = RotatingFileHandler(
                cls.LOG_FILE,
                maxBytes=cls.MAX_BYTES,
                backupCount=cls.BACKUP_COUNT,
                encoding="utf-8",
            )
            file_handler.setLevel(logging.DEBUG)  # Always log DEBUG to file
            file_formatter = logging.Formatter(cls.FILE_FORMAT, datefmt=cls.DATE_FORMAT)
            file_handler.setFormatter(file_formatter)
            root_logger.addHandler(file_handler)

        # Silence noisy third-party loggers
        cls.silence_noisy_loggers()

        return root_logger

    @staticmethod
    def silence_noisy_loggers():
        """Silence verbose third-party libraries."""
        logging.getLogger("urllib3").setLevel(logging.WARNING)
        logging.getLogger("PIL").setLevel(logging.WARNING)
        logging.getLogger("blurhash").setLevel(logging.WARNING)
        logging.getLogger("botocore").setLevel(logging.WARNING)
        logging.getLogger("boto3").setLevel(logging.WARNING)

    @staticmethod
    def is_quiet() -> bool:
        """Check if quiet mode is enabled (for tqdm disable parameter)."""
        return _quiet_mode

    @staticmethod
    def get_level() -> int:
        """Get current root logger level."""
        return logging.getLogger().level
