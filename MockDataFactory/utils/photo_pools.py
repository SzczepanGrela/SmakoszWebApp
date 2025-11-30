"""
Photo Pools - Zdjęcia z Pixabay API dla dań, restauracji i użytkowników

Strategia:
1. Cache in-memory (dict) - instant access
2. Cache on-disk (photo_cache.json) - persistent across runs
3. Pixabay API call - fetch ALL queries per category for maximum variety
4. Fallback to Lorem Picsum - 100% success guarantee

Optymalizacja:
- Cache per CATEGORY (nie per query) - 1 API call per category
- Fetch ALL queries for category - 5x więcej URLs = większe zróżnicowanie!
- 48 dish categories × 1 call = 48 calls (zamiast ~240)
- Każda kategoria: ~100 URLs (5 queries × 20) zamiast 20
"""

import random
import json
import logging
import requests
import time
from typing import List, Optional, Dict
from datetime import datetime, timedelta
from pathlib import Path

logger = logging.getLogger(__name__)

class PhotoPools:
    """
    Zarządza URL-ami zdjęć z Pixabay API (z fallbackiem do Lorem Picsum)
    """

    # Queries dla dań (według kategorii/archetypu)
    DISH_QUERIES = {
        'Pizza': ['pizza', 'italian pizza', 'margherita', 'pepperoni pizza', 'pizza restaurant'],
        'Burger': ['burger', 'hamburger', 'cheeseburger', 'burger fries', 'gourmet burger'],
        'Sushi': ['sushi', 'sushi roll', 'japanese food', 'nigiri', 'sashimi'],
        'Pasta': ['pasta', 'spaghetti', 'italian pasta', 'carbonara', 'penne'],
        'Ramen': ['ramen', 'ramen bowl', 'japanese noodles', 'ramen soup'],
        'Steak': ['steak', 'beef steak', 'grilled steak', 'ribeye', 'meat'],
        'Salad': ['salad', 'fresh salad', 'green salad', 'caesar salad', 'vegetable'],
        'Soup': ['soup', 'soup bowl', 'hot soup', 'cream soup', 'vegetable soup'],
        'Dessert': ['dessert', 'cake', 'sweet', 'chocolate dessert', 'pastry'],
        'Ice Cream': ['ice cream', 'gelato', 'dessert ice', 'frozen dessert'],
        'Tacos': ['tacos', 'mexican food', 'taco plate', 'tortilla'],
        'Kebab': ['kebab', 'doner', 'shawarma', 'middle eastern food'],
        'Pierogi': ['pierogi', 'dumplings', 'polish food', 'stuffed dumplings'],
        'Seafood': ['seafood', 'fish', 'salmon', 'shrimp', 'lobster'],
        'BBQ': ['bbq', 'barbecue', 'ribs', 'grilled meat', 'smoked meat'],
        'Chicken': ['chicken', 'fried chicken', 'grilled chicken', 'chicken dish'],
        'Vegan': ['vegan', 'plant based', 'vegan food', 'vegetables'],
        'Breakfast': ['breakfast', 'eggs', 'pancakes', 'morning food', 'brunch'],
        'Sandwich': ['sandwich', 'sub', 'panini', 'deli sandwich'],
        'Noodles': ['noodles', 'asian noodles', 'stir fry', 'pad thai'],
        'Curry': ['curry', 'indian food', 'curry dish', 'thai curry'],
        'Dim Sum': ['dim sum', 'chinese food', 'steamed dumplings', 'dumpling'],
        'Pho': ['pho', 'vietnamese soup', 'pho bo', 'vietnamese food'],
        'Falafel': ['falafel', 'middle eastern', 'chickpea', 'mediterranean'],
        'Risotto': ['risotto', 'italian rice', 'mushroom risotto', 'creamy rice'],
        'Gnocchi': ['gnocchi', 'italian pasta', 'potato gnocchi'],
        'Biryani': ['biryani', 'indian rice', 'rice dish', 'biryani plate'],
        'Paella': ['paella', 'spanish food', 'rice seafood', 'paella pan'],
        'Nachos': ['nachos', 'tortilla chips', 'mexican snack', 'cheese nachos'],
        'Quesadilla': ['quesadilla', 'mexican food', 'cheese quesadilla', 'tortilla'],
        'Wrap': ['wrap', 'tortilla wrap', 'burrito', 'sandwich wrap'],
        'Spring Rolls': ['spring rolls', 'vietnamese rolls', 'rice paper', 'fresh rolls'],
        'Tempura': ['tempura', 'fried japanese', 'shrimp tempura', 'japanese food'],
        'Donuts': ['donuts', 'doughnuts', 'glazed donut', 'sweet pastry'],
        'Croissant': ['croissant', 'french pastry', 'butter croissant', 'bakery'],
        'Waffle': ['waffle', 'belgian waffle', 'breakfast waffle', 'sweet waffle'],
        'Smoothie Bowl': ['smoothie bowl', 'acai bowl', 'healthy bowl', 'fruit bowl'],
        'Poke Bowl': ['poke bowl', 'hawaiian food', 'fish bowl', 'rice bowl'],
        'Buddha Bowl': ['buddha bowl', 'healthy bowl', 'grain bowl', 'vegan bowl'],
        'Fondue': ['fondue', 'cheese fondue', 'swiss food', 'melted cheese'],
        'Tapas': ['tapas', 'spanish food', 'small plates', 'spanish appetizers'],
        'Antipasti': ['antipasti', 'italian appetizers', 'charcuterie', 'italian starters'],
        'Oysters': ['oysters', 'raw oysters', 'seafood platter', 'fresh oysters'],
        'Ceviche': ['ceviche', 'seafood ceviche', 'peruvian food', 'fish ceviche'],
        'Empanadas': ['empanadas', 'latin american', 'pastry pockets', 'savory pastry'],
        'Schnitzel': ['schnitzel', 'breaded meat', 'german food', 'fried cutlet'],
        'Goulash': ['goulash', 'hungarian stew', 'beef stew', 'paprika stew'],
        'Moussaka': ['moussaka', 'greek food', 'eggplant dish', 'mediterranean'],
        'Baklava': ['baklava', 'middle eastern dessert', 'phyllo pastry', 'sweet pastry'],
        'Tiramisu': ['tiramisu', 'italian dessert', 'coffee dessert', 'mascarpone'],
    }

    # Queries dla restauracji (według motywu)
    RESTAURANT_QUERIES = {
        'Italian': ['italian restaurant', 'pizzeria', 'trattoria', 'italian interior'],
        'Asian': ['asian restaurant', 'chinese restaurant', 'japanese restaurant', 'sushi bar'],
        'Mexican': ['mexican restaurant', 'taco bar', 'cantina', 'mexican interior'],
        'American': ['american diner', 'burger restaurant', 'cafe interior', 'casual dining'],
        'French': ['french restaurant', 'bistro', 'parisian cafe', 'french interior'],
        'Mediterranean': ['mediterranean restaurant', 'greek taverna', 'middle eastern restaurant'],
        'Steakhouse': ['steakhouse', 'grill restaurant', 'meat restaurant', 'fine dining'],
        'Seafood': ['seafood restaurant', 'fish restaurant', 'oyster bar', 'coastal restaurant'],
        'Vegan': ['vegan restaurant', 'plant based cafe', 'healthy restaurant', 'vegetarian cafe'],
        'Cafe': ['cafe', 'coffee shop', 'bakery cafe', 'cozy cafe'],
    }

    # Mapowanie polskich kategorii dań na angielskie klucze DISH_QUERIES
    DISH_CATEGORY_MAPPING = {
        # Polish -> English
        'Deser': 'Dessert',
        'Zupa': 'Soup',
        'Lody': 'Ice Cream',
        'Sałatka': 'Salad',
        'Makaron': 'Pasta',
        'Stek': 'Steak',
        'Śniadanie': 'Breakfast',
        'Kanapka': 'Sandwich',
        'Przystawka': 'Antipasti',
        'Ryby i Owoce Morza': 'Seafood',
        'Naleśniki': 'Breakfast',
        'Panini': 'Sandwich',
        'Miska': 'Buddha Bowl',
        'Dania BBQ': 'BBQ',
        'Danie Azjatyckie': 'Noodles',
        'Danie Bliskowschodnie': 'Falafel',
        'Danie Francuskie': 'Fondue',
        'Danie Greckie': 'Moussaka',
        'Danie Koreańskie': 'Noodles',
        'Danie Niemieckie': 'Schnitzel',
        'Danie Polskie': 'Pierogi',
        'Shake': 'Smoothie Bowl',
        'Sorbet': 'Ice Cream',
        'Frytki': 'Breakfast',  # fries (use generic breakfast/side)
        'Dodatek': 'Salad',  # side dish
        'Pieczywo': 'Croissant',  # bread
        'Naan': 'Breakfast',  # Indian bread
        'Inne Danie': 'Salad',  # other dish (generic)
        'Inne': 'Salad',  # other
        # FIXED: Add missing categories
        'Mexican': 'Tacos',  # Mexican cuisine
        'Taco': 'Tacos',  # Taco (singular) -> Tacos (plural in DISH_QUERIES)
        # Beverages (fallback to generic food)
        'Kawa': 'Breakfast',
        'Herbata': 'Breakfast',
        'Gorąca Czekolada': 'Dessert',
        'Koktajl Mleczny': 'Smoothie Bowl',
        'Koktajl Alkoholowy': 'Smoothie Bowl',
        'Napój Bezalkoholowy': 'Smoothie Bowl',
        'Piwo': 'Breakfast',
        'Wino': 'Breakfast',
    }

    # Mapowanie nazw motywów z generatorów na klucze RESTAURANT_QUERIES
    THEME_MAPPING = {
        'Pizzeria': 'Italian',
        'Burger Bar': 'American',
        'Sushi Bar': 'Asian',
        'Asian Fusion': 'Asian',
        'Steakhouse': 'Steakhouse',
        'Vegan Cafe': 'Vegan',
        'Mexican': 'Mexican',
        'Italian': 'Italian',
        'French Bistro': 'French',
        'Seafood': 'Seafood',
    }

    # Queries dla zdjęć użytkowników (generyczne)
    USER_PHOTO_QUERIES = [
        'portrait', 'person', 'face', 'people', 'man', 'woman', 'profile'
    ]

    def __init__(self):
        """
        Inicjalizacja PhotoPools z Pixabay API i cache
        """
        random.seed()

        # Load config
        from config import PHOTO_CONFIG
        self.config = PHOTO_CONFIG
        self.api_key = self.config['pixabay_api_key']
        self.enabled = self.config['pixabay_enabled']

        # Cache setup
        self.cache_file = Path(__file__).parent.parent / self.config['cache_file']
        self.cache: Dict[str, List[str]] = self._load_cache()

        # Rate limiting
        self.api_calls: List[datetime] = []
        self.rate_limit = self.config['rate_limit_per_hour']

        # Stats
        self.stats = {
            'cache_hits': 0,
            'api_calls': 0,
            'fallback_calls': 0
        }

        if self.enabled:
            logger.info("✓ PhotoPools: Pixabay API enabled")
        else:
            logger.warning("⚠️ PhotoPools: Pixabay API disabled (no API key), using fallback only")

    def get_dish_photo(self, dish_category: str) -> str:
        """
        Zwraca URL zdjęcia dania

        Args:
            dish_category: Kategoria dania (np. 'Pizza', 'Burger', 'Deser')

        Returns:
            URL zdjęcia (Pixabay lub Lorem Picsum)
        """
        # FIXED: Map Polish categories to English keys (like THEME_MAPPING for restaurants)
        mapped_category = self.DISH_CATEGORY_MAPPING.get(dish_category, dish_category)
        queries = self.DISH_QUERIES.get(mapped_category, ['food'])
        return self._get_photo_url_for_category(mapped_category, queries, 'dish')

    def get_restaurant_photo(self, restaurant_theme: str) -> str:
        """
        Zwraca URL zdjęcia restauracji

        Args:
            restaurant_theme: Motyw restauracji (np. 'Pizzeria', 'Italian')

        Returns:
            URL zdjęcia (Pixabay lub Lorem Picsum)
        """
        mapped_theme = self.THEME_MAPPING.get(restaurant_theme, restaurant_theme)
        queries = self.RESTAURANT_QUERIES.get(mapped_theme, ['restaurant'])
        return self._get_photo_url_for_category(mapped_theme, queries, 'restaurant')

    def get_user_photo_generic(self) -> str:
        """
        Zwraca URL generycznego zdjęcia użytkownika

        Returns:
            URL zdjęcia (Pixabay lub Lorem Picsum)
        """
        # For user photos, use generic category
        return self._get_photo_url_for_category('user_generic', self.USER_PHOTO_QUERIES, 'user')

    def get_random_dish_photo(self) -> str:
        """Zwraca losowe zdjęcie jedzenia"""
        category = random.choice(list(self.DISH_QUERIES.keys()))
        return self.get_dish_photo(category)

    def get_random_restaurant_photo(self) -> str:
        """Zwraca losowe zdjęcie restauracji"""
        theme = random.choice(list(self.RESTAURANT_QUERIES.keys()))
        return self.get_restaurant_photo(theme)

    def _get_photo_url_for_category(self, category: str, queries: List[str], photo_type: str) -> str:
        """
        Główna logika pobierania zdjęć z cache/API/fallback
        FIXED: Cache per category, fetch all queries for variety

        Args:
            category: Kategoria (np. 'Pizza', 'Italian', 'user_generic')
            queries: Lista fraz wyszukiwania dla tej kategorii
            photo_type: Typ zdjęcia ('dish', 'restaurant', 'user')

        Returns:
            URL zdjęcia
        """
        # 1. Sprawdź cache (per category!)
        cache_key = f"{photo_type}:{category}"
        cached_url = self._get_cached_photo(cache_key)
        if cached_url:
            self.stats['cache_hits'] += 1
            return cached_url

        # 2. Spróbuj Pixabay API (fetch ALL queries for category)
        if self.enabled:
            pixabay_url = self._fetch_all_queries_for_category(category, queries, photo_type)
            if pixabay_url:
                return pixabay_url

        # 3. Fallback do Lorem Picsum
        self.stats['fallback_calls'] += 1
        return self._generate_fallback_url(queries[0] if queries else category)

    def _get_cached_photo(self, cache_key: str) -> Optional[str]:
        """
        Pobiera losowe zdjęcie z cache dla danego cache_key

        Args:
            cache_key: Klucz cache (np. "dish:Pizza")

        Returns:
            URL z cache lub None
        """
        if cache_key in self.cache and self.cache[cache_key]:
            return random.choice(self.cache[cache_key])

        return None

    def _fetch_all_queries_for_category(self, category: str, queries: List[str], photo_type: str) -> Optional[str]:
        """
        OPTIMIZED: Fetch photos for ALL queries with order variation for maximum variety

        Args:
            category: Kategoria (np. 'Pizza')
            queries: Lista fraz (np. ['pizza', 'margherita', 'pepperoni pizza'])
            photo_type: Typ zdjęcia

        Returns:
            URL zdjęcia lub None
        """
        all_urls = []

        # Fetch photos for ALL queries with alternating order for variety
        for idx, query in enumerate(queries):
            # Tier 4: Alternate between "popular" and "latest" for temporal variety
            order = "popular" if idx < len(queries) // 2 else "latest"

            urls = self._fetch_pixabay_photo(query, photo_type, order=order)
            if urls:
                all_urls.extend(urls)

        if all_urls:
            # Tier 2: Remove duplicate URLs
            all_urls = list(set(all_urls))

            # Store all URLs in cache under category key
            cache_key = f"{photo_type}:{category}"
            self.cache[cache_key] = all_urls
            self._save_cache()

            logger.info(f"✓ Cached {len(all_urls)} unique URLs for {cache_key}")

            # Return random URL from fetched batch
            return random.choice(all_urls)

        return None

    def _fetch_pixabay_photo(self, query: str, photo_type: str, order: str = "popular") -> Optional[List[str]]:
        """
        OPTIMIZED: Fetches photos from Pixabay API with quality filters and order variation
        Stats tracking fixed: count ALL API calls, not just successful ones

        Args:
            query: Fraza wyszukiwania
            photo_type: Typ zdjęcia
            order: Sort order - "popular" or "latest" (for variety)

        Returns:
            Lista URL-i lub None w przypadku błędu
        """
        # Rate limiting
        self._wait_for_rate_limit()

        try:
            # OPTIMIZED: Quality filters + order variation
            params = {
                "key": self.api_key,
                "q": query,
                "image_type": "photo",
                "per_page": self.config['images_per_query'],  # Now 200!
                "safesearch": "true",
                "orientation": "horizontal",
                "order": order,  # Tier 4: "popular" or "latest"
                "min_width": 800,  # Tier 3: Quality filter
                "min_height": 600,  # Tier 3: Quality filter
            }

            # Add category only for dishes
            if photo_type == 'dish':
                params["category"] = "food"

            response = requests.get(
                "https://pixabay.com/api/",
                params=params,
                timeout=self.config['timeout_seconds']
            )

            # FIXED: Track API call ALWAYS (not just on success)
            self.api_calls.append(datetime.now())
            self.stats['api_calls'] += 1

            if response.status_code == 200:
                data = response.json()
                if data.get('hits') and len(data['hits']) > 0:
                    # Extract URLs
                    urls = [hit['webformatURL'] for hit in data['hits']]
                    return urls
                else:
                    logger.debug(f"No results for query '{query}'")
                    return None

            elif response.status_code == 429:
                logger.warning("⚠️ Pixabay: Rate limit exceeded")
                return None
            else:
                logger.warning(f"⚠️ Pixabay API error: {response.status_code}")
                return None

        except requests.RequestException as e:
            logger.warning(f"⚠️ Pixabay request failed: {e}")
            return None

    def _load_cache(self) -> Dict[str, List[str]]:
        """
        Ładuje cache z pliku JSON

        Returns:
            Dict z cache lub pusty dict
        """
        if self.cache_file.exists():
            try:
                with open(self.cache_file, 'r', encoding='utf-8') as f:
                    cache = json.load(f)
                    logger.info(f"✓ Cache loaded: {len(cache)} entries from {self.cache_file.name}")
                    return cache
            except Exception as e:
                logger.warning(f"⚠️ Failed to load cache: {e}")

        return {}

    def _save_cache(self):
        """
        Zapisuje cache do pliku JSON
        """
        try:
            with open(self.cache_file, 'w', encoding='utf-8') as f:
                json.dump(self.cache, f, indent=2, ensure_ascii=False)
        except Exception as e:
            logger.error(f"❌ Failed to save cache: {e}")

    def _wait_for_rate_limit(self):
        """
        Czeka jeśli zbliżamy się do limitu 4900 req/h
        """
        # Usuń wywołania starsze niż 1h
        one_hour_ago = datetime.now() - timedelta(hours=1)
        self.api_calls = [call for call in self.api_calls if call > one_hour_ago]

        # Sprawdź limit
        if len(self.api_calls) >= self.rate_limit:
            # Poczekaj do wygaśnięcia najstarszego wywołania
            oldest_call = min(self.api_calls)
            wait_until = oldest_call + timedelta(hours=1, seconds=10)  # +10s buffer
            wait_seconds = (wait_until - datetime.now()).total_seconds()

            if wait_seconds > 0:
                logger.warning(f"⚠️ Rate limit approaching, waiting {wait_seconds:.0f}s...")
                time.sleep(wait_seconds)

    def _generate_fallback_url(self, query: str, width: int = 800, height: int = 600) -> str:
        """
        Generuje fallback URL (Lorem Picsum) gdy Pixabay nie działa

        Args:
            query: Fraza (używana jako seed)
            width: Szerokość
            height: Wysokość

        Returns:
            URL Lorem Picsum
        """
        seed = random.randint(1, 10000)
        query_clean = query.replace(' ', '_').replace(',', '_')
        url = f"{self.config['fallback_base_url']}/seed/{query_clean}_{seed}/{width}/{height}"
        return url

    def print_stats(self):
        """Wyświetla statystyki użycia"""
        total = sum(self.stats.values())
        if total == 0:
            return

        logger.info("\n" + "=" * 60)
        logger.info("📸 PHOTO POOLS STATISTICS")
        logger.info("=" * 60)
        logger.info(f"  Cache hits: {self.stats['cache_hits']:,} ({100*self.stats['cache_hits']/total:.1f}%)")
        logger.info(f"  API calls: {self.stats['api_calls']:,} ({100*self.stats['api_calls']/total:.1f}%)")
        logger.info(f"  Fallback calls: {self.stats['fallback_calls']:,} ({100*self.stats['fallback_calls']/total:.1f}%)")
        logger.info(f"  Total photos: {total:,}")
        logger.info(f"  Cache entries: {len(self.cache)}")

        # Show variety stats
        if self.cache:
            urls_per_category = [len(urls) for urls in self.cache.values()]
            avg_urls = sum(urls_per_category) / len(urls_per_category) if urls_per_category else 0
            logger.info(f"  Avg URLs per category: {avg_urls:.1f}")

        logger.info("=" * 60 + "\n")
