"""
Photo Pools - Zarządzanie lokalnymi zdjęciami (Warianty + Restauracje)
"""

import json
import logging
import random
from pathlib import Path
from typing import Dict, List, Set
import sys
import os

# Import config - note: uses sys.path.append because utils can't use relative imports for config
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from config import PHOTO_CONFIG

logger = logging.getLogger(__name__)
INDEX_FILE = Path(PHOTO_CONFIG['local_photo_index'])

class PhotoPools:
    def __init__(self):
        self.index = self._load_index()
        # { restaurant_id: { 'dishes': set(), 'interior': set() } }
        self.usage_history: Dict[int, Dict[str, Set[str]]] = {}
        logger.info(f"✓ PhotoPools: Initialized (Variant Aware).")

    def _load_index(self) -> Dict:
        if not INDEX_FILE.exists():
            return {"dishes": {}, "restaurants": {}}
        try:
            with open(INDEX_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception:
            return {"dishes": {}, "restaurants": {}}

    def _get_used(self, res_id: int, type_key: str) -> Set[str]:
        if res_id not in self.usage_history:
            self.usage_history[res_id] = {'dishes': set(), 'interior': set()}
        return self.usage_history[res_id][type_key]

    def get_dish_photo(self, category: str, variant: str, restaurant_id: int) -> str:
        """
        Pobiera zdjęcie dla KONKRETNEGO wariantu (np. Pizza -> Margherita).
        Fallback: Inny wariant z tej samej kategorii -> Losowe zdjęcie z kategorii -> Placeholder.
        """
        cat_data = self.index.get("dishes", {}).get(category, {})
        
        # 1. Try specific variant
        photos = cat_data.get(variant, [])
        
        # 2. Fallback: Flatten all photos from this category
        if not photos:
            photos = [p for sublist in cat_data.values() for p in sublist]
            
        if not photos:
            return "/images/mock/placeholder.webp"

        # Uniqueness check
        used = self._get_used(restaurant_id, 'dishes')
        unused = [p for p in photos if p not in used]
        
        if unused:
            selected = random.choice(unused)
        else:
            selected = random.choice(photos)
            
        used.add(selected)
        return f"/images/mock/{selected}"

    def get_restaurant_photo(self, theme: str, restaurant_id: int) -> str:
        photos = self.index.get("restaurants", {}).get(theme, [])
        if not photos:
            return "/images/mock/restaurant_placeholder.webp"
            
        used = self._get_used(restaurant_id, 'interior')
        unused = [p for p in photos if p not in used]
        
        if unused:
            selected = random.choice(unused)
        else:
            selected = random.choice(photos)
            
        used.add(selected)
        return f"/images/mock/{selected}"

    def get_user_photo_generic(self) -> str:
        return "https://ui-avatars.com/api/?name=User&background=random"
