import logging
import sys
from logging.handlers import RotatingFileHandler
from pathlib import Path
from typing import Literal

LogLevel = Literal["DEBUG", "INFO", "WARNING", "ERROR"]

_quiet_mode = False

class LoggingConfig:

    CONSOLE_FORMAT = "%(levelname)-8s | %(message)s"
    FILE_FORMAT = "%(asctime)s | %(name)-25s | %(levelname)-8s | %(message)s"
    DATE_FORMAT = "%Y-%m-%d %H:%M:%S"

    LOG_FILE = Path("mockdata_generation.log")
    MAX_BYTES = 10 * 1024 * 1024
    BACKUP_COUNT = 5

    @classmethod
    def setup(
        cls,
        level: LogLevel = "INFO",
        log_file: bool = True,
        console: bool = True,
        quiet: bool = False,
    ) -> logging.Logger:
        global _quiet_mode
        _quiet_mode = quiet

        if quiet:
            effective_level = logging.WARNING
        else:
            effective_level = getattr(logging, level.upper())

        root_logger = logging.getLogger()
        root_logger.setLevel(effective_level)
        root_logger.handlers.clear()

        if console and not quiet:
            console_handler = logging.StreamHandler(sys.stdout)
            console_handler.setLevel(effective_level)
            console_formatter = logging.Formatter(cls.CONSOLE_FORMAT, datefmt=cls.DATE_FORMAT)
            console_handler.setFormatter(console_formatter)
            root_logger.addHandler(console_handler)

        if log_file:
            file_handler = RotatingFileHandler(
                cls.LOG_FILE,
                maxBytes=cls.MAX_BYTES,
                backupCount=cls.BACKUP_COUNT,
                encoding="utf-8",
            )
            file_handler.setLevel(logging.DEBUG)
            file_formatter = logging.Formatter(cls.FILE_FORMAT, datefmt=cls.DATE_FORMAT)
            file_handler.setFormatter(file_formatter)
            root_logger.addHandler(file_handler)

        cls.silence_noisy_loggers()

        return root_logger

    @staticmethod
    def silence_noisy_loggers():
        logging.getLogger("urllib3").setLevel(logging.WARNING)
        logging.getLogger("PIL").setLevel(logging.WARNING)
        logging.getLogger("blurhash").setLevel(logging.WARNING)
        logging.getLogger("botocore").setLevel(logging.WARNING)
        logging.getLogger("boto3").setLevel(logging.WARNING)

    @staticmethod
    def is_quiet() -> bool:
        return _quiet_mode

    @staticmethod
    def get_level() -> int:
        return logging.getLogger().level
