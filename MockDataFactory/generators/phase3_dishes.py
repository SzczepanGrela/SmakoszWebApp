"""
Phase 3 - Generowanie dań (~20,000)
"""

import logging
import random
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.blueprint_loader import BlueprintLoader
from utils.statistical import sample_beta, zipf_distribution
from utils.photo_pools import PhotoPools

logger = logging.getLogger(__name__)

def generate_dish_description(dish_name: str, archetype: str, ingredients: list, quality: float, spiciness: float) -> str:
    """
    Generuje apetyczny opis dania po polsku (max 500 znaków)

    Args:
        dish_name: Nazwa dania
        archetype: Archetyp dania (Pizza, Burger, etc.)
        ingredients: Lista składników
        quality: Jakość dania (0.0-1.0)
        spiciness: Ostrość (0-10)

    Returns:
        Opis dania po polsku
    """
    # Quality adjectives
    if quality >= 0.8:
        quality_adj = random.choice(['wyśmienite', 'wyjątkowe', 'premium', 'perfekcyjne'])
    elif quality >= 0.6:
        quality_adj = random.choice(['pyszne', 'smaczne', 'aromatyczne', 'smakowite'])
    else:
        quality_adj = random.choice(['dobre', 'klasyczne', 'tradycyjne', 'domowe'])

    # Base templates by archetype
    archetype_intros = {
        'Pizza': f'{quality_adj.capitalize()} pizza {dish_name}',
        'Burger': f'{quality_adj.capitalize()} burger {dish_name}',
        'Sushi': f'{quality_adj.capitalize()} sushi {dish_name}',
        'Pasta': f'{quality_adj.capitalize()} makaron {dish_name}',
        'Deser': f'{quality_adj.capitalize()} deser {dish_name}',
        'Zupa': f'{quality_adj.capitalize()} zupa {dish_name}',
        'Sałatka': f'{quality_adj.capitalize()} sałatka {dish_name}'
    }
    base = archetype_intros.get(archetype, f'{quality_adj.capitalize()} danie {dish_name}')

    # Spice level
    spice = ''
    if spiciness >= 7:
        spice = ', bardzo ostre'
    elif spiciness >= 5:
        spice = ', ostre'
    elif spiciness >= 3:
        spice = ', średnio ostre'

    # Key ingredients (max 4)
    key_ingredients = ', '.join(ingredients[:4]) if ingredients else 'świeże składniki'

    description = f"{base}. {quality_adj.capitalize()} danie{spice}. Składniki: {key_ingredients}."
    return description[:500]

def generate_dish_calories(archetype: str, price: float, richness: float) -> int:
    """
    Generuje realistyczną wartość kaloryczną dania

    Args:
        archetype: Archetyp dania (Pizza, Burger, etc.)
        price: Cena dania
        richness: Bogactwo dania (0.0-1.0)

    Returns:
        Kalorie (zaokrąglone do 10)
    """
    # Base calorie ranges by archetype (Updated for realism)
    archetype_calories = {
        'Pizza': (800, 1400),
        'Burger': (600, 1100),
        'Sushi': (300, 600), # Per portion/roll set
        'Pasta': (500, 900),
        'Deser': (300, 700),
        'Zupa': (200, 450),
        'Sałatka': (250, 600),
        'Steak': (600, 1000),
        'Ryba': (350, 700),
        'Kurczak': (400, 800),
        'Kebab': (700, 1200),
        'Ramen': (500, 900),
        'Tacos': (400, 800)
    }

    min_cal, max_cal = archetype_calories.get(archetype, (400, 800))
    base_calories = random.uniform(min_cal, max_cal)

    # Price adjustment (expensive = slightly larger/richer portions, but not always)
    if price > 60:
        base_calories *= 1.1

    # Richness multiplier
    richness_multiplier = 0.8 + (richness * 0.4)
    calories = base_calories * richness_multiplier

    # Round to nearest 10
    return round(calories / 10) * 10

def generate_dishes(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje ~20,000 dań z secret attributes

    FIXED: Now uses proper dish_id from database after INSERT
    """
    logger.info(" Generowanie dań...")

    loader = BlueprintLoader(blueprints_dir)
    dish_variants = loader.load_blueprint("dish_variants.json")

    # Pobierz restauracje
    restaurants = db.fetch_all("""
        SELECT restaurant_id, secret_menu_blueprint, secret_price_multiplier
        FROM restaurants
    """)

    # Pobierz wszystkie składniki
    all_ingredients = db.fetch_all("SELECT ingredient_id, ingredient_name FROM ingredients")
    ingredient_map = {name: id for id, name in all_ingredients}

    # Pobierz wszystkie tagi do przypisania dish_tags
    all_tags = db.fetch_all("SELECT tag_id, tag_name, tag_category FROM tags")
    tag_map = {name: tag_id for tag_id, name, _ in all_tags}
    tag_by_category = {}
    for tag_id, tag_name, tag_category in all_tags:
        if tag_category not in tag_by_category:
            tag_by_category[tag_category] = []
        tag_by_category[tag_category].append((tag_id, tag_name))

    photo_pools = PhotoPools()

    total_dishes = 0
    total_ingredients_links = 0
    total_photos = 0
    total_dish_tags = 0
    dish_tags_buffer = []  # Buffer for bulk insert

    for restaurant_id, menu_blueprint, price_multiplier in restaurants:
        # Wybierz dania dla tego typu menu
        menu_dishes = _select_dishes_for_menu(menu_blueprint, dish_variants)

        if not menu_dishes:
            continue

        # Zipf distribution dla popularności dań
        popularity_scores = zipf_distribution(len(menu_dishes), alpha=1.5)

        # FIXED: Insert pojedynczo aby mieć prawdziwe dish_id
        for i, variant in enumerate(menu_dishes):
            dish_name = variant.get("name", "Danie")
            archetype = variant.get("archetype", "Unknown")
            base_price = variant.get("price", 35.0)

            # Secret attributes
            secret_base_price = base_price
            
            # Price with Gaussian noise
            # base * multiplier * gaussian(1.0, 0.1)
            final_price = base_price * price_multiplier * random.gauss(1.0, 0.1)
            price = round(max(10.0, final_price), 2) # Ensure min price 10 PLN
            
            secret_quality = sample_beta(5, 2, 0.3, 0.95)
            secret_spiciness = random.uniform(0, 10) if "spicy" in variant.get("tags", []) else random.uniform(0, 3)
            secret_richness = random.uniform(0.0, 1.0)
            secret_texture_score = sample_beta(4, 2, 0.0, 1.0)

            # Extract ingredients early (needed for description)
            ingredients = variant.get("ingredients", [])

            # Generate public fields
            description = generate_dish_description(
                dish_name=dish_name,
                archetype=archetype,
                ingredients=ingredients,
                quality=secret_quality,
                spiciness=secret_spiciness
            )

            # FIXED: Generate primary photo ONCE (will be used in both dishes.image_url and photos table)
            primary_photo_url = photo_pools.get_dish_photo(archetype)

            calories = generate_dish_calories(
                archetype=archetype,
                price=price,
                richness=secret_richness
            )

            # Map archetype to menu_section
            menu_section_map = {
                'Pizza': 'Dania Główne',
                'Burger': 'Dania Główne',
                'Pasta': 'Dania Główne',
                'Steak': 'Dania Główne',
                'Sushi': 'Sushi & Sashimi',
                'Ramen': 'Dania Główne',
                'Curry': 'Dania Główne',
                'Seafood': 'Owoce Morza',
                'Fish': 'Owoce Morza',
                'Salad': 'Przystawki i Sałatki',
                'Soup': 'Zupy',
                'Dessert': 'Desery',
                'Ice Cream': 'Desery',
                'Tacos': 'Dania Główne',
                'Kebab': 'Dania Główne',
                'Dim Sum': 'Przystawki',
                'Noodles': 'Dania Główne',
                'Vegan': 'Dania Roślinne'
            }
            menu_section = menu_section_map.get(archetype, 'Dania Główne')

            # Calculate tags EARLY to determine is_vegan flag
            dish_tag_ids = _get_tags_for_dish(
                archetype=archetype,
                spiciness=secret_spiciness,
                ingredients=ingredients,
                tag_map=tag_map,
                tag_by_category=tag_by_category
            )
            
            # Determine flags
            is_spicy = secret_spiciness > 2.0
            is_vegan = False
            if "Wegańskie" in tag_map and tag_map["Wegańskie"] in dish_tag_ids:
                is_vegan = True

            # Insert dania i pobierz prawdziwe ID
            dish_data = {
                "restaurant_id": restaurant_id,
                "dish_name": dish_name,
                "secret_archetype": archetype,  # NEW column in schema
                "price": price, # RENAMED from public_price
                "description": description,
                "menu_section": menu_section, # NEW: Mapped section
                "is_vegan": is_vegan, # NEW: Denormalized flag
                "is_spicy": is_spicy, # NEW: Denormalized flag
                "secret_base_price": round(secret_base_price, 2),
                "secret_quality": round(secret_quality, 3),
                "secret_spiciness": round(secret_spiciness, 2),
                "secret_richness": round(secret_richness, 3),  # NEW column
                "secret_texture_score": round(secret_texture_score, 3),  # NEW column
                "secret_popularity_factor": round(popularity_scores[i], 4),  # NEW column
                "image_url": primary_photo_url,  # FIXED: Use Pixabay (same as primary in photos table)
                "calories": calories
            }

            # FIXED: Insert pojedynczo i pobierz prawdziwe ID
            dish_id = db.insert_single("dishes", dish_data)
            total_dishes += 1

            # Przypisz składniki (teraz z prawdziwym dish_id)
            ingredient_links = []
            for ingredient_name in ingredients:
                if ingredient_name in ingredient_map:
                    ingredient_links.append({
                        "dish_id": dish_id,  # FIXED: prawdziwe ID
                        "ingredient_id": ingredient_map[ingredient_name]
                    })

            if ingredient_links:
                db.insert_bulk("dish_ingredients_link", ingredient_links)
                total_ingredients_links += len(ingredient_links)

            # FIXED: Add PRIMARY photo (same as dishes.image_url for synchronization)
            db.insert_single("photos", {
                "entity_type": "dish",
                "entity_id": dish_id,
                "photo_url": primary_photo_url,  # FIXED: Same URL as dishes.image_url (synchronized!)
                "is_primary": True
            })
            total_photos += 1

            # FIXED: Add 1-2 ADDITIONAL photos to gallery (non-primary)
            num_additional_photos = random.randint(1, 2)
            for _ in range(num_additional_photos):
                additional_photo_url = photo_pools.get_dish_photo(archetype)
                db.insert_single("photos", {
                    "entity_type": "dish",
                    "entity_id": dish_id,
                    "photo_url": additional_photo_url,
                    "is_primary": False  # Additional gallery photo
                })
                total_photos += 1

            # Przypisz tagi do dania (dish_tags) - using pre-calculated list
            for tag_id in dish_tag_ids:
                dish_tags_buffer.append({
                    "dish_id": dish_id,
                    "tag_id": tag_id
                })

            # Bulk insert dish_tags co 5000 rekordów
            if len(dish_tags_buffer) >= 5000:
                db.insert_bulk("dish_tags", dish_tags_buffer)
                total_dish_tags += len(dish_tags_buffer)
                dish_tags_buffer = []

        if (total_dishes % 5000) == 0:
            logger.info(f"  Wygenerowano {total_dishes} dań...")

    # Insert remaining dish_tags
    if dish_tags_buffer:
        db.insert_bulk("dish_tags", dish_tags_buffer)
        total_dish_tags += len(dish_tags_buffer)

    logger.info(f" Wygenerowano {total_dishes} dań")
    logger.info(f" Przypisano {total_ingredients_links} składników do dań")
    logger.info(f" Dodano {total_photos} zdjęć dań")
    logger.info(f" Przypisano {total_dish_tags} tagów do dań")

def _get_tags_for_dish(archetype: str, spiciness: float, ingredients: list,
                       tag_map: dict, tag_by_category: dict) -> list:
    """
    Określa tagi dla dania na podstawie jego właściwości.

    Args:
        archetype: Typ dania (Pizza, Burger, Sushi, etc.)
        spiciness: Poziom ostrości (0-10)
        ingredients: Lista składników
        tag_map: Mapowanie nazwa_tagu -> tag_id
        tag_by_category: Mapowanie kategoria -> lista (tag_id, tag_name)

    Returns:
        Lista tag_id do przypisania
    """
    tag_ids = set()

    # 1. Mapowanie archetype -> cuisine tag
    archetype_cuisine_map = {
        "Pizza": "Włoska",
        "Pasta": "Włoska",
        "Risotto": "Włoska",
        "Gnocchi": "Włoska",
        "Burger": "Amerykańska",
        "Steak": "Amerykańska",
        "BBQ": "Amerykańska",
        "Sushi": "Japońska",
        "Ramen": "Japońska",
        "Pho": "Wietnamska",
        "Noodles": "Azjatycka",
        "Dim Sum": "Azjatycka",
        "Curry": "Indyjska",
        "Tacos": "Meksykańska",
        "Quesadilla": "Meksykańska",
        "Nachos": "Meksykańska",
        "Kebab": "Bliskowschodnia",
        "Salad": "Śródziemnomorska",
        "Seafood": "Śródziemnomorska",
        "Oysters": "Francuska",
        "Fondue": "Francuska",
        "Soup": "Polska",
        "Vegan": "Śródziemnomorska",
    }

    cuisine_tag = archetype_cuisine_map.get(archetype)
    if cuisine_tag and cuisine_tag in tag_map:
        tag_ids.add(tag_map[cuisine_tag])

    # 2. Mapowanie spiciness -> spice tag
    if spiciness <= 1:
        spice_tag = "Łagodne"
    elif spiciness <= 3:
        spice_tag = "Średnio ostre"
    elif spiciness <= 6:
        spice_tag = "Ostre"
    else:
        spice_tag = "Bardzo ostre"

    if spice_tag in tag_map:
        tag_ids.add(tag_map[spice_tag])

    # 3. Tagi dietetyczne na podstawie składników
    ingredients_lower = [i.lower() for i in ingredients]

    # Wegańskie - jeśli nie ma mięsa, nabiału, jaj
    meat_keywords = ['mięso', 'kurczak', 'wołowina', 'wieprzowina', 'boczek', 'szynka',
                     'kiełbasa', 'beef', 'chicken', 'pork', 'bacon', 'ham', 'sausage',
                     'ryba', 'fish', 'łosoś', 'salmon', 'tuńczyk', 'tuna', 'krewetki', 'shrimp']
    dairy_keywords = ['ser', 'cheese', 'mleko', 'milk', 'śmietana', 'cream', 'masło', 'butter']
    egg_keywords = ['jajko', 'egg', 'jaja']

    has_meat = any(any(kw in ing for kw in meat_keywords) for ing in ingredients_lower)
    has_dairy = any(any(kw in ing for kw in dairy_keywords) for ing in ingredients_lower)
    has_eggs = any(any(kw in ing for kw in egg_keywords) for ing in ingredients_lower)

    if not has_meat and not has_dairy and not has_eggs:
        if "Wegańskie" in tag_map:
            tag_ids.add(tag_map["Wegańskie"])
    elif not has_meat:
        if "Wegetariańskie" in tag_map:
            tag_ids.add(tag_map["Wegetariańskie"])

    # Bezglutenowe
    gluten_keywords = ['mąka', 'flour', 'chleb', 'bread', 'makaron', 'pasta', 'pszenica', 'wheat']
    has_gluten = any(any(kw in ing for kw in gluten_keywords) for ing in ingredients_lower)
    if not has_gluten and "Bezglutenowe" in tag_map:
        tag_ids.add(tag_map["Bezglutenowe"])

    # 4. Losowo dodaj 1-2 dodatkowe tagi (occasion/feature/mood)
    optional_categories = ['occasion', 'feature', 'mood']
    for category in random.sample(optional_categories, k=random.randint(1, 2)):
        if category in tag_by_category and tag_by_category[category]:
            random_tag = random.choice(tag_by_category[category])
            tag_ids.add(random_tag[0])  # tag_id

    return list(tag_ids)

def _select_dishes_for_menu(menu_blueprint: str, dish_variants: dict) -> list:
    """
    Wybiera dania odpowiednie dla danego typu menu

    FIXED: Handles nested JSON structure:
    {"Pizza": {"base_price": {...}, "variants": {"Margherita": {"ingredients": [...]}}}}
    """
    # FIXED: Build flat variant list from nested structure
    all_variants = []
    for category_name, category_data in dish_variants.items():
        if not isinstance(category_data, dict):
            continue

        # Get base price for this category
        base_price_info = category_data.get("base_price", {"mean": 35.0, "stdev": 5.0})
        base_price = base_price_info.get("mean", 35.0)

        variants = category_data.get("variants", {})
        for variant_name, variant_data in variants.items():
            if not isinstance(variant_data, dict):
                continue

            # Calculate final price
            price_mult_info = variant_data.get("price_multiplier", {"mean": 1.0})
            price_multiplier = price_mult_info.get("mean", 1.0)
            final_price = round(base_price * price_multiplier, 2)

            # Get spiciness
            spiciness_info = variant_data.get("spiciness", {"mean": 0})
            spiciness = spiciness_info.get("mean", 0)

            all_variants.append({
                "name": variant_name,
                "archetype": category_name,  # Category name becomes archetype
                "price": final_price,
                "ingredients": variant_data.get("ingredients", []),
                "tags": ["spicy"] if spiciness > 2 else [],
                "spiciness": spiciness
            })

    # Menu mappings (archetype -> menu types) and Base Sizes (Mean)
    # Sushi & Asian places usually have larger menus than Burger joints
    menu_configs = {
        "pizza_menu":     {"archetypes": ["Pizza", "Pasta", "Salad", "Deser"], "mean": 25, "sigma": 5},
        "burger_menu":    {"archetypes": ["Burger", "Steak", "Salad"], "mean": 15, "sigma": 3},
        "sushi_menu":     {"archetypes": ["Sushi", "Soup", "Salad"], "mean": 40, "sigma": 8},
        "asian_menu":     {"archetypes": ["Ramen", "Noodles", "Dim Sum", "Pho", "Curry", "Sushi"], "mean": 35, "sigma": 7},
        "steak_menu":     {"archetypes": ["Steak", "BBQ", "Burger", "Salad"], "mean": 20, "sigma": 4},
        "vegan_menu":     {"archetypes": ["Vegan", "Salad", "Soup", "Smoothie Bowl"], "mean": 22, "sigma": 5},
        "mexican_menu":   {"archetypes": ["Tacos", "Quesadilla", "Nachos", "Burrito"], "mean": 28, "sigma": 6},
        "italian_menu":   {"archetypes": ["Pizza", "Pasta", "Risotto", "Gnocchi", "Deser"], "mean": 30, "sigma": 6},
        "french_menu":    {"archetypes": ["Steak", "Soup", "Fondue", "Deser"], "mean": 20, "sigma": 4},
        "seafood_menu":   {"archetypes": ["Seafood", "Sushi", "Oysters", "Fish"], "mean": 25, "sigma": 5},
        "general_menu":   {"archetypes": ["Pizza", "Burger", "Pasta", "Salad", "Kebab"], "mean": 20, "sigma": 5}
    }

    config = menu_configs.get(menu_blueprint, menu_configs["general_menu"])
    archetypes = config["archetypes"]
    
    # Calculate target menu size using Gaussian distribution
    target_count = int(random.gauss(config["mean"], config["sigma"]))
    # Hard limits to prevent empty or massive menus
    target_count = max(8, min(target_count, 80))

    # Filter variants by archetype
    matching_dishes = [v for v in all_variants if v.get("archetype") in archetypes]

    # If no matches, use all variants as fallback
    if not matching_dishes:
        matching_dishes = all_variants

    # Select dishes
    if len(matching_dishes) > target_count:
        return random.sample(matching_dishes, target_count)
    else:
        return matching_dishes
