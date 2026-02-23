"""
Image Providers Module

Abstract base class and implementations for multiple image source providers
(Pixabay, Unsplash). Enables unified, multi-provider image fetching.
"""

import logging
import os
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass

import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

logger = logging.getLogger(__name__)

class RateLimitError(Exception):
    """Raised when API rate limit is exceeded."""

    def __init__(self, provider: str, retry_after: int = 300, message: str = ""):
        self.provider = provider
        self.retry_after = retry_after  # seconds to wait
        self.message = message
        super().__init__(f"{provider} rate limit hit. Retry after {retry_after}s. {message}")

@dataclass
class ImageResult:
    """Standardized image search result from any provider."""

    url: str
    provider: str
    provider_id: str
    width: int
    height: int
    credit: dict[str, str] | None = None  # {name, username, link}
    download_location: str | None = None  # Unsplash API: trigger download endpoint

class ImageProvider(ABC):
    """Abstract base for image providers."""

    @property
    @abstractmethod
    def name(self) -> str:
        """Provider name for logging."""
        pass

    @property
    @abstractmethod
    def enabled(self) -> bool:
        """Whether this provider is configured and usable."""
        pass

    @abstractmethod
    def search(self, query: str, count: int, orientation: str = "horizontal") -> list[ImageResult]:
        """
        Search for images matching query.

        Args:
            query: Search term
            count: Maximum number of results
            orientation: 'horizontal', 'vertical', or 'squarish'

        Returns:
            List of ImageResult objects

        Raises:
            RateLimitError: When API rate limit is exceeded
        """
        pass

    @abstractmethod
    def download(self, result: ImageResult, target_width: int | None = None) -> bytes | None:
        """
        Download image bytes.

        Args:
            result: ImageResult from search()
            target_width: Optional width hint for server-side resize

        Returns:
            Image bytes or None on failure
        """
        pass

class PixabayProvider(ImageProvider):
    """Pixabay API provider - primary source."""

    API_URL = "https://pixabay.com/api/"

    def __init__(self):
        self._api_key = os.getenv("PIXABAY_API_KEY", "")
        self._session = self._create_session()

    @property
    def name(self) -> str:
        return "pixabay"

    @property
    def enabled(self) -> bool:
        return bool(self._api_key)

    def _create_session(self) -> requests.Session:
        session = requests.Session()
        # Don't auto-retry 429 - we handle it manually
        retries = Retry(total=3, backoff_factor=0.5, status_forcelist=[500, 502, 503, 504])
        session.mount("https://", HTTPAdapter(max_retries=retries))
        return session

    def search(
        self, query: str, count: int, orientation: str = "horizontal", category: str | None = None
    ) -> list[ImageResult]:
        if not self.enabled:
            logger.warning("Pixabay API key not configured")
            return []

        params = {
            "key": self._api_key,
            "q": query,
            "image_type": "photo",
            "per_page": min(count, 200),
            "safesearch": "true",
            "orientation": orientation,
        }
        if category:
            params["category"] = category

        try:
            response = self._session.get(self.API_URL, params=params, timeout=10)

            # Check for rate limit
            if response.status_code == 429:
                retry_after = int(response.headers.get("Retry-After", 300))
                logger.debug(f"Pixabay rate limit hit, retry after {retry_after}s")
                raise RateLimitError("pixabay", retry_after, "Too many requests")

            if response.status_code == 403:
                # Pixabay returns 403 for rate limit sometimes
                raise RateLimitError("pixabay", 300, "Forbidden - likely rate limited")

            response.raise_for_status()
            data = response.json()

            logger.debug(
                f"Pixabay search: '{query}' -> {data.get('totalHits', 0)} total hits, returning {len(data.get('hits', []))} results"
            )

        except RateLimitError:
            raise  # Re-raise rate limit errors
        except Exception as e:
            logger.error(f"Pixabay search failed for '{query}': {e}")
            return []

        results = []
        for hit in data.get("hits", []):
            results.append(
                ImageResult(
                    url=hit.get("largeImageURL", hit.get("webformatURL", "")),
                    provider=self.name,
                    provider_id=str(hit.get("id", "")),
                    width=hit.get("imageWidth", 0),
                    height=hit.get("imageHeight", 0),
                    credit={"name": hit.get("user", ""), "link": hit.get("pageURL", "")},
                )
            )

        return results

    def download(self, result: ImageResult, target_width: int | None = None) -> bytes | None:
        try:
            response = self._session.get(result.url, timeout=30)
            response.raise_for_status()
            return response.content
        except Exception as e:
            logger.error(f"Pixabay download failed for {result.provider_id}: {e}")
            return None

class UnsplashProvider(ImageProvider):
    """
    Unsplash API provider - secondary source for diversity.

    Rate Limits:
    - Demo mode: 50 requests/hour
    - Production: 5,000 requests/hour
    - No daily limit, only hourly
    """

    API_URL = "https://api.unsplash.com"

    # Rate limit tracking (class-level for persistence)
    _rate_limit: int = 50  # Assume demo until we know
    _rate_remaining: int = 50
    _rate_reset: float = 0  # Unix timestamp when limit resets

    def __init__(self):
        self._api_key = os.getenv("UNSPLASH_ACCESS_KEY", "")
        self._session = self._create_session()

    @property
    def name(self) -> str:
        return "unsplash"

    @property
    def enabled(self) -> bool:
        return bool(self._api_key)

    @property
    def is_demo_mode(self) -> bool:
        """Check if using demo API (50/hour limit)."""
        return self._rate_limit <= 50

    def _create_session(self) -> requests.Session:
        session = requests.Session()
        retries = Retry(total=2, backoff_factor=1, status_forcelist=[500, 502, 503, 504])
        session.mount("https://", HTTPAdapter(max_retries=retries))
        return session

    def _update_rate_limit(self, response: requests.Response):
        """Update rate limit tracking from response headers."""
        UnsplashProvider._rate_limit = int(response.headers.get("X-Ratelimit-Limit", self._rate_limit))
        UnsplashProvider._rate_remaining = int(response.headers.get("X-Ratelimit-Remaining", self._rate_remaining))

        # Log rate status periodically
        if self._rate_remaining <= 5 or self._rate_remaining % 10 == 0:
            mode = "DEMO" if self.is_demo_mode else "PRODUCTION"
            logger.info(f"Unsplash [{mode}]: {self._rate_remaining}/{self._rate_limit} requests remaining")

    def _check_rate_limit(self):
        """Pre-emptively check if we're rate limited."""
        if self._rate_remaining <= 0 and self._rate_reset > 0 and time.time() < self._rate_reset:
            retry_after = int(self._rate_reset - time.time())
            raise RateLimitError("unsplash", retry_after, f"Limit exhausted. Resets in {retry_after}s")

    def search(self, query: str, count: int, orientation: str = "horizontal") -> list[ImageResult]:
        if not self.enabled:
            logger.debug("Unsplash API key not configured - skipping")
            return []

        # Note: Removed preemptive rate limit check - let actual request determine current status

        # Map orientation to Unsplash format
        orient_map = {"horizontal": "landscape", "vertical": "portrait", "squarish": "squarish"}

        headers = {"Authorization": f"Client-ID {self._api_key}"}
        params = {
            "query": query,
            "per_page": min(count, 30),  # Unsplash max is 30
            "orientation": orient_map.get(orientation, "landscape"),
        }

        try:
            response = self._session.get(f"{self.API_URL}/search/photos", headers=headers, params=params, timeout=10)

            # Update rate limit tracking from headers
            self._update_rate_limit(response)

            # Check for rate limit response
            if response.status_code == 403 or self._rate_remaining <= 0:
                # Calculate wait time until reset (top of next hour)
                retry_after = 3600 - (int(time.time()) % 3600)  # Seconds until next hour
                UnsplashProvider._rate_reset = time.time() + retry_after
                logger.debug(
                    f"Unsplash rate limit: {self._rate_remaining}/{self._rate_limit} remaining, retry in {retry_after}s"
                )
                raise RateLimitError(
                    "unsplash", retry_after, f"Rate limit hit ({self._rate_limit}/hour). Resets in {retry_after}s"
                )

            response.raise_for_status()
            data = response.json()

            logger.debug(
                f"Unsplash search: '{query}' -> {data.get('total', 0)} total, returning {len(data.get('results', []))} results"
            )

        except RateLimitError:
            raise  # Re-raise rate limit errors
        except Exception as e:
            logger.error(f"Unsplash search failed for '{query}': {e}")
            return []

        results = []
        for photo in data.get("results", []):
            urls = photo.get("urls", {})
            links = photo.get("links", {})
            results.append(
                ImageResult(
                    url=urls.get("raw", urls.get("full", "")),
                    provider=self.name,
                    provider_id=photo.get("id", ""),
                    width=photo.get("width", 0),
                    height=photo.get("height", 0),
                    credit={
                        "name": photo.get("user", {}).get("name", ""),
                        "username": photo.get("user", {}).get("username", ""),
                        "link": links.get("html", ""),
                    },
                    download_location=links.get("download_location"),  # For trigger download
                )
            )

        return results

    def download(self, result: ImageResult, target_width: int | None = None) -> bytes | None:
        """
        Download with optional server-side resize via Imgix params.

        Per Unsplash API Guidelines, triggers the download endpoint first
        to track usage statistics for photographers.
        """
        # Trigger download endpoint (Unsplash API requirement)
        # This notifies Unsplash that the image is being used
        if result.download_location:
            self._trigger_download(result.download_location)

        url = result.url

        # Unsplash uses Imgix - add resize params for efficiency
        if target_width and "unsplash.com" in url:
            separator = "&" if "?" in url else "?"
            url = f"{url}{separator}w={target_width}&q=80&fm=jpg"

        try:
            response = self._session.get(url, timeout=30)
            response.raise_for_status()
            return response.content
        except Exception as e:
            logger.error(f"Unsplash download failed for {result.provider_id}: {e}")
            return None

    def _trigger_download(self, download_location: str) -> None:
        """
        Trigger Unsplash download tracking endpoint.

        Per API Guidelines: "To abide by the API guidelines, you need to trigger
        a GET request to this endpoint every time your application performs
        something similar to a download."

        This is done asynchronously (fire-and-forget) to not slow down downloads.
        """
        headers = {"Authorization": f"Client-ID {self._api_key}"}

        try:
            # Fire-and-forget with short timeout
            response = self._session.get(download_location, headers=headers, timeout=5)
            if response.status_code == 200:
                logger.debug("✓ Triggered download for Unsplash photo")
            else:
                logger.debug(f"Trigger download returned {response.status_code}")
        except Exception as e:
            # Don't fail the actual download if trigger fails
            logger.debug(f"Trigger download failed (non-critical): {e}")

class ProviderManager:
    """
    Coordinates multiple image providers.
    Handles fallback, rate limit waiting, and result mixing for visual diversity.
    """

    def __init__(self):
        self.providers: list[ImageProvider] = []
        self._rate_limited: dict[str, float] = {}  # provider -> resume_time

        # Add Pixabay (primary)
        pixabay = PixabayProvider()
        if pixabay.enabled:
            self.providers.append(pixabay)
            logger.info("Pixabay provider enabled")

        # Add Unsplash (secondary)
        unsplash = UnsplashProvider()
        if unsplash.enabled:
            self.providers.append(unsplash)
            logger.info("Unsplash provider enabled")

        if not self.providers:
            logger.error("No image providers configured! Check API keys.")

    def get_provider(self, name: str) -> ImageProvider | None:
        """Get a specific provider by name (for direct access)."""
        for provider in self.providers:
            if provider.name == name:
                return provider
        return None

    def _is_rate_limited(self, provider_name: str) -> bool:
        """Check if provider is currently rate limited."""
        if provider_name in self._rate_limited:
            if time.time() < self._rate_limited[provider_name]:
                return True
            else:
                del self._rate_limited[provider_name]
        return False

    def _wait_for_rate_limit(self, error: RateLimitError):
        """
        Wait for rate limit to expire with smart polling.
        Checks every 5 minutes if limit has actually reset (rolling window).
        """
        import os

        import requests

        POLL_INTERVAL = 300  # Check every 5 minutes
        MAX_WAIT = 3900  # Max 65 minutes (to cover rolling window edge cases)

        logger.warning(f"Waiting: {error.provider} rate limit hit. Polling every 5 min to check reset...")

        elapsed = 0
        while elapsed < MAX_WAIT:
            # Wait for poll interval
            wait_chunk = min(POLL_INTERVAL, MAX_WAIT - elapsed)
            logger.info(f"Sleeping {wait_chunk}s before next check...")
            time.sleep(wait_chunk)
            elapsed += wait_chunk

            # Check if Unsplash limit has reset
            if error.provider == "unsplash":
                api_key = os.getenv("UNSPLASH_ACCESS_KEY", "")
                if api_key:
                    try:
                        r = requests.get(
                            "https://api.unsplash.com/search/photos",
                            headers={"Authorization": f"Client-ID {api_key}"},
                            params={"query": "test", "per_page": 1},
                            timeout=10,
                        )
                        remaining = int(r.headers.get("X-Ratelimit-Remaining", 0))
                        limit = int(r.headers.get("X-Ratelimit-Limit", 50))

                        if r.status_code == 200 and remaining > 0:
                            logger.info(f"OK: Unsplash limit reset! {remaining}/{limit} available.")
                            UnsplashProvider._rate_remaining = remaining
                            UnsplashProvider._rate_limit = limit
                            UnsplashProvider._rate_reset = 0
                            break
                        else:
                            logger.info(f"Still rate limited ({remaining}/{limit}). Waiting more...")
                    except Exception as e:
                        logger.debug(f"Check failed: {e}")

        # Clear rate limit flag
        if error.provider in self._rate_limited:
            del self._rate_limited[error.provider]

        logger.info(f"Resuming {error.provider} requests.")

    def search_mixed(
        self, query: str, total: int, orientation: str = "horizontal", pixabay_ratio: float = 0.6, max_retries: int = 3
    ) -> list[ImageResult]:
        """
        Search multiple providers and mix results.
        Waits and retries on rate limit errors.

        Args:
            query: Search term
            total: Total results needed
            orientation: Image orientation
            pixabay_ratio: Fraction from Pixabay (rest from Unsplash)
            max_retries: Maximum retries per provider on rate limit

        Returns:
            Mixed list of ImageResult from multiple providers
        """
        results: list[ImageResult] = []

        # Calculate split
        pixabay_count = int(total * pixabay_ratio)
        unsplash_count = total - pixabay_count

        for provider in self.providers:
            if provider.name == "pixabay":
                count = pixabay_count
            elif provider.name == "unsplash":
                count = unsplash_count
            else:
                count = total // len(self.providers)

            if count <= 0:
                continue

            # Skip if rate limited
            if self._is_rate_limited(provider.name):
                logger.debug(f"Skipping {provider.name} - rate limited")
                continue

            # Try with retries - always wait on rate limit
            for _attempt in range(max_retries):
                try:
                    provider_results = provider.search(query, count, orientation)
                    results.extend(provider_results)
                    logger.debug(f"{provider.name}: Got {len(provider_results)} results for '{query}'")
                    break  # Success

                except RateLimitError as e:
                    # Always wait for rate limit to expire
                    self._wait_for_rate_limit(e)
                    # After waiting, retry the request (loop continues)

        return results
