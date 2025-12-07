"""
Phase 2 - Generowanie restauracji (~1200)
"""

import logging
import random
import json
import re
import unicodedata
from typing import Dict

from utils.db_connection import DatabaseConnection
from utils.blueprint_loader import BlueprintLoader
from utils.statistical import sample_beta, sample_normal
from utils.date_generator import DateGenerator
from utils.photo_pools import PhotoPools
from utils.faker_instance import fake

logger = logging.getLogger(__name__)

# FIXED: Global set to track used restaurant names and prevent UNIQUE constraint violations
_used_restaurant_names = set()
_name_counter = {}

def generate_restaurant_archetype_modifiers(menu_blueprint: str) -> Dict[str, Dict[str, float]]:
    modifiers = {}

    PRIMARY_ARCHETYPES = {
        "Italian Restaurant": ["Pizza", "Pasta", "Salad"],
        "Fast Food": ["Burger", "Fries", "Chicken"],
        "Ice Cream Shop": ["Ice Cream", "Dessert"],
        "Asian Fusion": ["Sushi", "Asian", "Soup"],
        "Steakhouse": ["Steak", "Salad", "Seafood"],
        "Pizzeria": ["Pizza"],
        "Sushi Bar": ["Sushi", "Asian"],
        "Vegan Cafe": ["Vegan", "Salad", "Soup"],
        "Breakfast Diner": ["Breakfast"],
        "Bakery": ["Dessert", "Breakfast", "Beverage"],
        "Seafood Restaurant": ["Seafood", "Salad"],
        "Mexican Restaurant": ["Mexican"],
        "Indian Restaurant": ["Indian"],
        "Chinese Restaurant": ["Chinese"],
        "Japanese Restaurant": ["Japanese", "Sushi"],
        "Thai Restaurant": ["Thai"],
        "American Diner": ["American", "Burger"],
        "Mediterranean Restaurant": ["Mediterranean", "Seafood"],
        "French Bistro": ["French"]
    }

    relevant_archetypes = PRIMARY_ARCHETYPES.get(menu_blueprint, [])

    AVAILABLE_DIMS = [
        "physics_richness", "flavor_saltiness", "flavor_spiciness",
        "texture_crispy", "physics_freshness"
    ]

    for archetype in relevant_archetypes:
        num_mods = random.randint(1, 3)
        selected = random.sample(AVAILABLE_DIMS, min(num_mods, len(AVAILABLE_DIMS)))

        arch_mods = {}
        for dim in selected:
            offset = round(random.uniform(-0.15, 0.15), 3)
            arch_mods[dim] = offset

        modifiers[archetype] = arch_mods

    return modifiers

def slugify(text: str) -> str:
    """
    Convert Polish text to URL-safe slug (ASCII-only, lowercase, hyphenated)

    Examples:
        "Pizzeria Królewska" -> "pizzeria-krolewska"
        "Sushi Bar Łódź" -> "sushi-bar-lodz"
    """
    # Manual mapping for Polish characters that NFKD doesn't handle
    polish_chars = {
        'ą': 'a', 'Ą': 'A',
        'ć': 'c', 'Ć': 'C',
        'ę': 'e', 'Ę': 'E',
        'ł': 'l', 'Ł': 'L',
        'ń': 'n', 'Ń': 'N',
        'ó': 'o', 'Ó': 'O',
        'ś': 's', 'Ś': 'S',
        'ź': 'z', 'Ź': 'Z',
        'ż': 'z', 'Ż': 'Z'
    }
    for polish, ascii_char in polish_chars.items():
        text = text.replace(polish, ascii_char)

    # Normalize remaining unicode characters
    text = unicodedata.normalize('NFKD', text)
    text = text.encode('ascii', 'ignore').decode('ascii')
    # Convert to lowercase and replace spaces with hyphens
    text = text.lower().replace(' ', '-')
    # Remove any remaining non-alphanumeric characters except hyphens
    text = re.sub(r'[^a-z0-9-]', '', text)
    # Remove consecutive hyphens
    text = re.sub(r'-+', '-', text)
    # Strip leading/trailing hyphens
    return text.strip('-')

def generate_restaurants(db: DatabaseConnection, blueprints_dir: str = "blueprints"):
    """
    Generuje ~1200 restauracji z secret attributes

    Secret Attributes:
    - secret_price_multiplier (0.8-1.3)
    - secret_overall_food_quality (0.0-1.0, Gaussian based on price_level)
    - secret_service_quality (0.0-1.0, Gaussian based on price_level)
    - secret_cleanliness_score (0.0-1.0, Gaussian based on price_level)
    - secret_ambiance_type ("Romantyczny", "Rodzinny", "Biznesowy")
    - secret_ambiance_quality (0.4-0.95)
    - secret_archetype_modifiers (JSONB)
    """
    logger.info(" Generowanie restauracji...")

    # Cleanup old data
    logger.info("🧹 Czyszczenie starych danych Phase 2 (restaurants, restaurant_opening_hours, restaurant_tags)...")
    try:
        # Use execute_query directly instead of manual cursor management
        db.execute_query("TRUNCATE TABLE restaurants RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE restaurant_opening_hours RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE restaurant_tags RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE photos RESTART IDENTITY CASCADE") # Clean photos too as they are linked
        
        db.commit()
        logger.info("✅ Wyczyszczono stare restauracje i powiązane tabele.")
        
    except Exception as e:
        logger.error(f"❌ Błąd podczas cleanup Phase 2: {e}")
        db.rollback()
        raise e

    loader = BlueprintLoader(blueprints_dir)
    restaurant_rules = loader.load_blueprint("02_restaurant_rules.json")
    city_config = loader.load_blueprint("01_city_rules.json").get("CITY_CONFIG", {})

    # Pobierz miasta
    cities = db.fetch_all("SELECT city_id, city_name FROM cities")

    date_gen = DateGenerator()
    photo_pools = PhotoPools()

    restaurant_data = []
    restaurant_id_counter = 1

    for city_id, city_name in cities:
        # Liczba restauracji per miasto (z restaurant_rules)
        num_restaurants = _get_restaurant_count_for_city(city_name, restaurant_rules)
        
        # Get city coordinates
        city_info = city_config.get(city_name, {})
        base_coords = city_info.get("coords", {"lat": 52.0, "lon": 19.0})

        for _ in range(num_restaurants):
            # Wybierz typ restauracji
            theme = _select_restaurant_theme(restaurant_rules)

            # Generuj nazwę
            name = _generate_restaurant_name(theme, city_name)

            # Data otwarcia
            created_date = date_gen.generate_restaurant_created_date()

            # Secret attributes - STEP 1: Determine price multiplier and level
            secret_price_multiplier = random.uniform(0.8, 1.3)

            # Calculate price_level based on secret_price_multiplier (MOVED UP)
            if secret_price_multiplier < 0.9:
                price_level = 1
            elif secret_price_multiplier < 1.1:
                price_level = 2
            elif secret_price_multiplier < 1.25:
                price_level = 3
            else:
                price_level = 4

            # ========== STEP 2: BIMODAL QUALITY DISTRIBUTION (Polarized) ==========
            # PURPOSE: Eliminate "average" quality trap - restaurants are either good or bad
            # This creates distinct signals for ML model training (high variance)
            #
            # OLD APPROACH: Gaussian centered on price_level means -> clustering around 6-7/10
            # NEW APPROACH: Bimodal mix -> 30% are terrible (3/10), 70% are great (8/10)

            # STEP 2a: Determine restaurant quality tier (bimodal distribution)
            # 30% chance: Low Quality (Kitchen Nightmares - Gordon Ramsay would shut it down)
            # 70% chance: High Quality (Hidden Gems - Michelin potential)
            is_low_quality = random.random() < 0.30

            if is_low_quality:
                # BAD RESTAURANT (30%): Mean 0.3, StdDev 0.1
                # Result: food quality 0.1-0.5 (1-5 on 10-point scale)
                quality_tier = 'low'
                base_food_quality = max(0.1, min(0.5, random.gauss(0.3, 0.1)))
            else:
                # GOOD RESTAURANT (70%): Mean 0.8, StdDev 0.1
                # Result: food quality 0.6-1.0 (6-10 on 10-point scale)
                quality_tier = 'high'
                base_food_quality = max(0.6, min(1.0, random.gauss(0.8, 0.1)))

            # STEP 2b: Generate CORRELATED attributes (bad restaurants are bad at everything)
            # Correlation coefficient: ~0.8 (strong positive correlation)
            # If food is bad (0.3), service and cleanliness will also be bad (0.2-0.4)
            # If food is great (0.8), service and cleanliness will also be great (0.7-0.9)

            if quality_tier == 'low':
                # Low quality restaurant: all attributes suffer together
                # Add small variance (±0.08) but keep them consistently low
                secret_overall_food_quality = base_food_quality
                secret_service_quality = max(0.1, min(0.5, base_food_quality + random.gauss(0, 0.08)))
                secret_cleanliness_score = max(0.1, min(0.5, base_food_quality + random.gauss(0, 0.08)))
            else:
                # High quality restaurant: all attributes excel together
                # Add small variance (±0.08) but keep them consistently high
                secret_overall_food_quality = base_food_quality
                secret_service_quality = max(0.6, min(1.0, base_food_quality + random.gauss(0, 0.08)))
                secret_cleanliness_score = max(0.6, min(1.0, base_food_quality + random.gauss(0, 0.08)))

            # Final clamp to valid range [0.0, 1.0]
            secret_overall_food_quality = max(0.0, min(1.0, secret_overall_food_quality))
            secret_service_quality = max(0.0, min(1.0, secret_service_quality))
            secret_cleanliness_score = max(0.0, min(1.0, secret_cleanliness_score))
            # =====================================================================

            # Other attributes
            ambiance_types = ["Romantyczny", "Rodzinny", "Biznesowy", "Casual", "Energiczny", "Spokojny"]
            secret_ambiance_type = random.choice(ambiance_types)
            secret_ambiance_quality = sample_beta(4, 3, 0.4, 0.95)

            # Menu blueprint
            menu_blueprint = _get_menu_blueprint(theme)
                
            # Coordinates (City center +/- ~5km)
            lat = base_coords["lat"] + random.uniform(-0.05, 0.05)
            lon = base_coords["lon"] + random.uniform(-0.05, 0.05)
            
            # Restaurant Status
            rand_status = random.random()
            if rand_status < 0.95:
                status = 'active'
            elif rand_status < 0.98:
                status = 'renovation'
            elif rand_status < 0.99:
                status = 'suspended'
            else:
                status = 'closed_permanently'

            # FIXED: Updated column names
            restaurant_data.append({
                "city_id": city_id,
                "restaurant_name": name,
                "cuisine_type": theme,  # RENAMED
                "price_level": price_level,
                "address": f"{fake.street_address()}, {city_name}",
                "latitude": round(lat, 6),
                "longitude": round(lon, 6),
                "phone": fake.phone_number(),
                "website": f"https://{slugify(name)}.pl",
                "description": f"Restauracja {theme} w {city_name}. Oferujemy autentyczne dania przygotowane z najlepszych składników.",
                "image_url": photo_pools.get_restaurant_photo(theme, restaurant_id_counter),
                "status": status, # NEW: Replaces is_active
                "created_at": DateGenerator.to_sql_datetime(created_date),
                "secret_price_multiplier": round(secret_price_multiplier, 3),
                "secret_overall_food_quality": round(secret_overall_food_quality, 3),
                "secret_service_quality": round(secret_service_quality, 3),
                "secret_cleanliness_score": round(secret_cleanliness_score, 2),
                "secret_ambiance_type": secret_ambiance_type,
                "secret_ambiance_quality": round(secret_ambiance_quality, 3),
                "secret_menu_blueprint": menu_blueprint,
                "secret_archetype_modifiers": json.dumps(generate_restaurant_archetype_modifiers(menu_blueprint)), # NEW
            })

            restaurant_id_counter += 1

    db.insert_bulk("restaurants", restaurant_data)
    logger.info(f" Wygenerowano {len(restaurant_data)} restauracji")

    # Dodaj tagi i zdjęcia
    _assign_restaurant_tags(db)
    _assign_restaurant_photos(db, photo_pools)

def _get_restaurant_count_for_city(city_name: str, rules: dict) -> int:
    """
    Zwraca liczbę restauracji dla miasta z zastosowaniem rozkładu Gaussa.
    Baza: Wagi populacji z rules.
    Wariancja: ~10-15% bazy.
    """
    city_counts = {
        "Warszawa": 200,
        "Kraków": 150,
        "Wrocław": 120,
        "Poznań": 100,
        "Gdańsk": 90,
        "Szczecin": 70,
        "Lublin": 60,
        "Katowice": 80,
        "Bydgoszcz": 50,
        "Białystok": 40,
        "Olsztyn": 35,
        "Rzeszów": 40,
        "Toruń": 35,
        "Kielce": 30,
        "Gliwice": 40,
        "Zabrze": 35,
        "Radom": 25,
        "Łódź": 100
    }

    base_count = city_counts.get(city_name, 30)

    # Apply Gaussian noise (Variance)
    # Mean = base_count, StdDev = 15% of base_count
    sigma = base_count * 0.15
    varied_count = int(random.gauss(base_count, sigma))

    # Ensure valid minimum (at least 5 restaurants per city)
    return max(5, varied_count)

def _select_restaurant_theme(rules: dict) -> str:
    """Wybiera typ restauracji"""
    themes = ["Pizzeria", "Burger Bar", "Sushi Bar", "Asian Fusion", "Steakhouse",
              "Vegan Cafe", "Mexican", "Italian", "French Bistro", "Seafood"]
    return random.choice(themes)

def _generate_restaurant_name(theme: str, city: str) -> str:
    """
    Generuje UNIKALNĄ polską nazwę restauracji

    FIXED: Dodano mechanizm zapewniający unikalność nazw (UNIQUE constraint w DB)
    Format: "Bazowa Nazwa Miasto" lub "Bazowa Nazwa Miasto 2" przy kolizji
    """
    global _used_restaurant_names, _name_counter

    base_name = _generate_base_name(theme)

    # Pierwsza próba: nazwa + miasto
    candidate = f"{base_name} {city}"

    if candidate not in _used_restaurant_names:
        _used_restaurant_names.add(candidate)
        return candidate

    # Kolizja - dodaj numeryczny suffix
    counter_key = f"{base_name}_{city}"
    if counter_key not in _name_counter:
        _name_counter[counter_key] = 1

    while True:
        _name_counter[counter_key] += 1
        candidate = f"{base_name} {city} {_name_counter[counter_key]}"
        if candidate not in _used_restaurant_names:
            _used_restaurant_names.add(candidate)
            return candidate

def _generate_base_name(theme: str) -> str:
    """Generuje bazową nazwę restauracji (bez gwarancji unikalności)"""
    prefixes = ["Restauracja", "Bistro", "Gospoda", "Smaki", "Bar"]
    suffixes = ["Pod Aniołem", "Starówka", "Centrum", "Parkowa", "Królewska",
                "Na Rogu", "U Babci", "Smaczna", "Domowa", "Zielona"]

    if theme == "Pizzeria":
        return f"Pizzeria {random.choice(['Bella', 'Roma', 'Napoli', 'Milano', 'Toscana', 'Palermo', 'Venezia', 'Firenze'])}"
    elif theme == "Sushi Bar":
        return f"Sushi {random.choice(['Tokyo', 'Osaka', 'Sakura', 'Zen', 'Kyoto', 'Fuji', 'Samurai', 'Ninja'])}"
    elif theme == "Burger Bar":
        return f"{random.choice(['The', 'Big', 'Best', 'Prime', 'Classic'])} Burger {random.choice(['House', 'Bar', 'Kitchen', 'Joint', 'Spot'])}"
    elif theme == "Asian Fusion":
        return f"{random.choice(['Asian', 'Oriental', 'Golden', 'Dragon'])} {random.choice(['Fusion', 'Kitchen', 'Garden', 'Palace'])}"
    elif theme == "Steakhouse":
        return f"{random.choice(['Prime', 'Black', 'Gold', 'Royal'])} {random.choice(['Steakhouse', 'Grill', 'Meat', 'Beef'])}"
    elif theme == "Vegan Cafe":
        return f"{random.choice(['Green', 'Fresh', 'Pure', 'Organic'])} {random.choice(['Cafe', 'Kitchen', 'Garden', 'Bistro'])}"
    elif theme == "Mexican":
        return f"{random.choice(['El', 'La', 'Casa'])} {random.choice(['Taco', 'Burrito', 'Fiesta', 'Mexico', 'Cantina'])}"
    elif theme == "French Bistro":
        return f"{random.choice(['Le', 'La', 'Petit'])} {random.choice(['Bistro', 'Cafe', 'Paris', 'Provence'])}"
    elif theme == "Seafood":
        return f"{random.choice(['Ocean', 'Sea', 'Blue', 'Harbor'])} {random.choice(['Catch', 'Fish', 'Grill', 'Kitchen'])}"
    else:
        return f"{random.choice(prefixes)} {random.choice(suffixes)}"

def _get_menu_blueprint(theme: str) -> str:
    """Zwraca blueprint menu dla typu restauracji"""
    blueprints = {
        "Pizzeria": "Pizzeria",
        "Burger Bar": "Fast Food",
        "Sushi Bar": "Sushi Bar",
        "Asian Fusion": "Asian Fusion",
        "Steakhouse": "Steakhouse",
        "Vegan Cafe": "Vegan Cafe",
        "Mexican": "Mexican Restaurant",
        "Italian": "Italian Restaurant",
        "French Bistro": "French Bistro",
        "Seafood": "Seafood Restaurant"
    }
    return blueprints.get(theme, "General")

def _assign_restaurant_tags(db: DatabaseConnection):
    """Przypisuje tagi do restauracji"""
    logger.info(" Przypisywanie tagów do restauracji...")

    restaurants = db.fetch_all("SELECT restaurant_id, cuisine_type FROM restaurants")
    tags = db.fetch_all("SELECT tag_id, tag_name, tag_category FROM tags")  # FIXED: category -> tag_category

    tag_assignments = []

    for restaurant_id, theme in restaurants:
        # Przypisz 2-4 losowe tagi
        num_tags = random.randint(2, 4)
        selected_tags = random.sample(tags, min(num_tags, len(tags)))

        for tag_id, _, _ in selected_tags:
            tag_assignments.append({
                "restaurant_id": restaurant_id,
                "tag_id": tag_id
            })

    if tag_assignments:
        db.insert_bulk("restaurant_tags", tag_assignments)
        logger.info(f" Przypisano {len(tag_assignments)} tagów do restauracji")

def _assign_restaurant_photos(db: DatabaseConnection, photo_pools: PhotoPools):
    """
    Dodaje zdjęcia restauracji do tabeli photos

    FIXED: Synchronizacja z restaurants.image_url
    - Primary photo (is_primary=TRUE) = TO SAMO zdjęcie co restaurants.image_url (synchronized!)
    - Additional 1-2 photos (is_primary=FALSE) = dodatkowe zdjęcia do galerii
    """
    logger.info(" Dodawanie zdjęć restauracji...")

    # FIXED: Fetch image_url from restaurants table (primary photo)
    restaurants = db.fetch_all("SELECT restaurant_id, cuisine_type, image_url FROM restaurants")

    photo_data = []

    for restaurant_id, theme, primary_image_url in restaurants:
        # FIXED: Add PRIMARY photo (same as restaurants.image_url for synchronization)
        photo_data.append({
            "entity_type": "restaurant",
            "entity_id": restaurant_id,
            "photo_url": primary_image_url,  # FIXED: Same URL as restaurants.image_url (synchronized!)
            "is_primary": True
        })

        # FIXED: Add 1-2 ADDITIONAL photos to gallery (non-primary)
        num_additional_photos = random.randint(1, 2)
        for _ in range(num_additional_photos):
            # additional_photo_url is generated but not assigned to a variable before use, fixed here
            additional_photo_url = photo_pools.get_restaurant_photo(theme, restaurant_id)
            photo_data.append({
                "entity_type": "restaurant",
                "entity_id": restaurant_id,
                "photo_url": additional_photo_url,
                "is_primary": False
            })

    db.insert_bulk("photos", photo_data)
    logger.info(f" Dodano {len(photo_data)} zdjęć restauracji")

    # Generuj godziny otwarcia
    _assign_opening_hours(db)

def _assign_opening_hours(db: DatabaseConnection):
    """Generuje godziny otwarcia dla restauracji"""
    logger.info(" Generowanie godzin otwarcia...")

    restaurants = db.fetch_all("SELECT restaurant_id, cuisine_type FROM restaurants")
    hours_data = []

    for restaurant_id, theme in restaurants:
        schedule = _get_schedule_for_theme(theme)
        
        # 0=Sunday, 1=Monday, ..., 6=Saturday
        for day in range(7):
            is_weekend = day in [5, 6] # Fri(5)?? No, 0=Sun, 1=Mon, 5=Fri, 6=Sat
            # Correction: 0=Sun, 6=Sat. Weekend is usually Fri-Sat night or Sat-Sun day.
            # Let's assume 5=Fri, 6=Sat for late hours logic.
            
            open_time, close_time = schedule['weekday']
            
            if day in [5, 6]: # Fri, Sat
                open_time, close_time = schedule['weekend']
            elif day == 0: # Sun
                open_time, close_time = schedule.get('sunday', schedule['weekday'])

            # Random variation +/- 30 mins
            # Simulating simplified times for bulk insert
            
            hours_data.append({
                "restaurant_id": restaurant_id,
                "day_of_week": day,
                "open_time": open_time,
                "close_time": close_time,
                "is_closed": False
            })

    db.insert_bulk("restaurant_opening_hours", hours_data)
    logger.info(f" Dodano {len(hours_data)} rekordów godzin otwarcia")

def _get_schedule_for_theme(theme: str) -> dict:
    """
    Zwraca losowy harmonogram godzin dla typu kuchni w ramach realistycznych zakresów.
    """
    
    def random_time(start_hour_range, end_hour_range):
        """Helper: returns random time string 'HH:MM'"""
        h = random.randint(start_hour_range, end_hour_range)
        m = random.choice(['00', '30'])
        return f"{h:02d}:{m}"

    # Default ranges
    open_range = (11, 13)
    close_range = (21, 23)
    
    if theme in ['Pizzeria', 'Italian', 'Mexican', 'Burger Bar']:
        open_range = (11, 12)
        close_range = (22, 23)
    elif theme in ['Sushi Bar', 'Asian Fusion', 'Seafood']:
        open_range = (12, 14)
        close_range = (22, 23)
    elif theme in ['Vegan Cafe', 'French Bistro', 'Breakfast Diner', 'Bakery']:
        open_range = (7, 10)
        close_range = (18, 21)
    elif theme in ['Steakhouse', 'Fine Dining']:
        open_range = (16, 17) # Often open for dinner only
        close_range = (22, 23)
    elif theme in ['Kebab', 'Fast Food']:
        open_range = (10, 11)
        close_range = (23, 24) # Late night

    # Generate specific times for this restaurant instance
    weekday_open = random_time(open_range[0], open_range[1])
    weekday_close = random_time(close_range[0], close_range[1])
    
    # Weekends usually open same time or earlier, close later
    weekend_open = weekday_open 
    weekend_close_h = int(weekday_close.split(':')[0])
    weekend_close = f"{min(23, weekend_close_h + 1):02d}:00" if weekend_close_h < 23 else "23:59"

    # Sunday
    sunday_close_h = int(weekday_close.split(':')[0])
    sunday_close = f"{max(18, sunday_close_h - 1):02d}:00"

    return {
        'weekday': (weekday_open, weekday_close),
        'weekend': (weekend_open, weekend_close),
        'sunday':  (weekday_open, sunday_close)
    }