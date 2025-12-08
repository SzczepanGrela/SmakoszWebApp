import json
import os
import sys
from pathlib import Path
from collections import defaultdict
import logging

# Set up logging
logging.basicConfig(level=logging.INFO, format="%(message)s")
logger = logging.getLogger("BlueprintVerifier")

def load_json_blueprint(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        logger.error(f"ERROR: Failed to load {filepath}: {e}")
        return None

def verify_blueprints():
    root_dir = Path(__file__).parent.parent
    blueprints_dir = root_dir / "blueprints"
    
    logger.info("Starting: Starting COMPREHENSIVE Blueprint Verification...\n")
    
    # 1. Load All Blueprints
    restaurant_types = load_json_blueprint(blueprints_dir / "restaurant_types.json")
    dishes = load_json_blueprint(blueprints_dir / "dishes.json")
    ingredients_list = load_json_blueprint(blueprints_dir / "ingredients_list.json")
    
    if not restaurant_types or not dishes or not ingredients_list:
        logger.error("ERROR: Critical: Missing blueprint files.")
        return

    ingredients_set = set(ingredients_list)

    # Definitions to sync with code
    # These mirrors the logic in generators/phase2_restaurants.py (CURRENT STATE)
    theme_mapping = {
        "Pizzeria": "Pizzeria",
        "Burgerownia": "Burger Bar",
        "Sushi Bar": "Sushi Bar",
        "Kuchnia Azjatycka": "Asian Fusion",
        "Kuchnia Wietnamska": "Asian Fusion",
        "Kuchnia Chińska": "Asian Fusion",
        "Ramen Bar": "Asian Fusion",
        "Steakhouse": "Steakhouse",
        "Kawiarnia": "Vegan Cafe", 
        "Bar Meksykański": "Mexican Restaurant",
        "Kuchnia Włoska": "Italian Restaurant",
        "Francuskie Bistro": "French Bistro",
        "Restauracja z Owocami Morza": "Seafood Restaurant",
        "Kebab": "General",
        "Kuchnia Polska": "General",
        # Note: Any theme NOT here falls back to General in the script
    }

    # These mirrors the logic in generators/phase3_dishes.py (CURRENT STATE)
    menu_configs = {
        "Pizzeria": ["Pizza", "Pasta", "Salad", "Deser"],
        "Burger Bar": ["Burger", "Steak", "Salad"],
        "Sushi Bar": ["Sushi", "Soup", "Salad"],
        "Asian Fusion": ["Ramen", "Noodles", "Dim Sum", "Pho", "Curry", "Sushi", "Kanapka", "Danie Azjatyckie"],
        "Steakhouse": ["Steak", "BBQ", "Burger", "Salad"],
        "Vegan Cafe": ["Vegan", "Salad", "Soup", "Smoothie Bowl"],
        "Mexican Restaurant": ["Tacos", "Quesadilla", "Nachos", "Burrito"],
        "Italian Restaurant": ["Pizza", "Pasta", "Risotto", "Gnocchi", "Deser"],
        "French Bistro": ["Steak", "Soup", "Fondue", "Deser"],
        "Seafood Restaurant": ["Seafood", "Sushi", "Oysters", "Fish"],
        "General": ["Pizza", "Burger", "Pasta", "Salad", "Kebab"],
    }
    
    # 1. Restaurant Types & Sections Coverage
    logger.info("--- 1. Restaurant Themes Analysis ---")
    themes = restaurant_types.get("RESTAURANT_THEMES", {})
    
    themes_without_specific_mapping = []
    
    for theme, data in themes.items():
        # Check mapping
        mapped_profile = theme_mapping.get(theme, "General")
        is_fallback = (theme not in theme_mapping) or (mapped_profile == "General" and theme not in ["Kebab", "Kuchnia Polska"])
        
        status_icon = "WARNING:" if is_fallback else "OK:"
        status_text = f"Fallback to General (Pizza/Burger)" if is_fallback else f"Maps to {mapped_profile}"
        
        # Check sections
        sections = data.get("menu_config", {}).get("sections", [])
        if not sections:
            logger.error(f"ERROR: {theme}: No Sections Defined!")
        
        if is_fallback:
             themes_without_specific_mapping.append(theme)
             # logger.warning(f"  {status_icon} {theme}: {status_text}")

    if themes_without_specific_mapping:
        logger.warning(f"\nWARNING:  {len(themes_without_specific_mapping)} Themes are falling back to 'General' (Missing specific menu):")
        for t in themes_without_specific_mapping:
            logger.warning(f"   - {t}")
        logger.info("   -> Recommendations: Create new mappings in phase2_restaurants.py and new menu profiles in phase3_dishes.py")

    # 2. Dish Reachability (Orphans)
    logger.info("\n--- 2. Dish Reachability Analysis ---")
    
    defined_archetypes = set()
    for category, content in dishes.items():
        if isinstance(content, dict) and content.get("variants"):
            defined_archetypes.add(category)

    reachable_archetypes = set()
    for profile, archetypes_list in menu_configs.items():
        for arch in archetypes_list:
            reachable_archetypes.add(arch)
            
    orphans = defined_archetypes - reachable_archetypes
    if orphans:
        logger.error(f"ERROR: Found {len(orphans)} ORPHANED Dish Categories (Never Generated):")
        for o in orphans:
             possible_match = [k for k in menu_configs.keys() if k.lower() in o.lower()] 
             hint = f"(Maybe add to {possible_match[0]}?)" if possible_match else ""
             logger.error(f"   - {o} {hint}")
    else:
        logger.info("OK: All dish categories are reachable.")

    # 3. Ingredient Integrity
    logger.info("\n--- 3. Ingredient Integrity Check ---")
    
    unknown_ingredients = defaultdict(list)
    
    for category, content in dishes.items():
        if not isinstance(content, dict): continue
        variants = content.get("variants", {})
        for v_name, v_data in variants.items():
            if not isinstance(v_data, dict): continue
            ingredients = v_data.get("ingredients", [])
            for ing in ingredients:
                if ing not in ingredients_set:
                    unknown_ingredients[ing].append(f"{category}/{v_name}")
                    
    if unknown_ingredients:
        logger.warning(f"WARNING: Found {len(unknown_ingredients)} ingredients used in dishes but NOT in ingredients_list.json:")
        sorted_unknown = sorted(unknown_ingredients.items(), key=lambda x: len(x[1]), reverse=True)
        for ing, usage in sorted_unknown[:10]: # Top 10
            logger.warning(f"   - '{ing}': used in {len(usage)} dishes (e.g. {usage[0]})")
        if len(unknown_ingredients) > 10:
            logger.warning(f"   ... and {len(unknown_ingredients) - 10} more.")
    else:
        logger.info("OK: All ingredients in dishes are valid.")

    # 4. Logic & Structure Check (with Inheritance)
    logger.info("\n--- 4. Logic & Structure Check (Inheritance) ---")
    
    for category, content in dishes.items():
        if isinstance(content, dict):
             # 1. Base Price
             bp = content.get("base_price", {})
             if not bp or "mean" not in bp:
                 logger.error(f"ERROR: {category}: Missing base_price configuration")
             
             # 2. Inheritable Base Characteristics
             archetype_base = content.get("archetype_base", {})
             base_chars = archetype_base.get("characteristics", {})
             if not base_chars:
                 logger.warning(f"WARNING: {category}: 'archetype_base.characteristics' is empty. Variants will need to define all stats.")

             # 3. Variants
             variants = content.get("variants", {})
             if not variants:
                 logger.warning(f"WARNING: {category}: No variants defined (Empty category)")
             
             for v_name, v_data in variants.items():
                 # Check pixabay term
                 if not v_data.get("pixabay_term"):
                     logger.warning(f"WARNING: {category}/{v_name}: Missing 'pixabay_term' (Photos will fail)")

                 # Check Characteristics Inheritance
                 variant_chars = v_data.get("characteristics", {})
                 
                 # Simulate Merge (Conceptual)
                 # We consider it "Valid" if either Base OR Variant provides data.
                 # If both are empty, the dish will be "Blank" (likely random gen in Phase 3).
                 if not base_chars and not variant_chars:
                     logger.warning(f"WARNING: {category}/{v_name}: No characteristics in Base AND No characteristics in Variant. Dish will be purely random.")

    logger.info("\n--- Verification Complete ---")

if __name__ == "__main__":
    verify_blueprints()
