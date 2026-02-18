"""
Services Package

This package contains business logic services that orchestrate complex operations
across multiple domains. Services encapsulate the "how" of business processes,
keeping generators focused on "what" to generate.
"""

from .review_service import ReviewGeneratorService

__all__ = ["ReviewGeneratorService"]
