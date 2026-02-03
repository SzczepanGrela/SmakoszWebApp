import json
import logging
import random
import time
import uuid

from tqdm import tqdm

from config import GENERATION_CONFIG
from data_access import RestaurantDAO
from utils.blueprint_loader import BlueprintLoader
from utils.date_generator import DateGenerator
from utils.db_connection import DatabaseConnection
from utils.distributions import sample_beta
from utils.faker_instance import fake
from utils.photo_pools import PhotoPools
from utils.restaurant_helpers import RestaurantNameGenerator
from utils.text_generator import slugify

logger = logging.getLogger(__name__)

def generate_restaurant_archetype_modifiers(menu_blueprint: str) -> dict[str, dict[str, float]]:
    modifiers = {}

    primary_archetypes = {
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
        "French Bistro": ["French"],
    }

    relevant_archetypes = primary_archetypes.get(menu_blueprint, [])

    available_dims = [
        "physics_richness",
        "flavor_saltiness",
        "flavor_spiciness",
        "texture_crispy",
        "physics_freshness",
    ]

    for archetype in relevant_archetypes:
        num_mods = random.randint(1, 3)
        selected = random.sample(available_dims, min(num_mods, len(available_dims)))

        arch_mods = {}
        for dim in selected:
            offset = round(random.uniform(-0.15, 0.15), 3)
            arch_mods[dim] = offset

        modifiers[archetype] = arch_mods

    return modifiers

def generate_restaurants(db: DatabaseConnection, blueprints_dir: str = "blueprints", cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating restaurants...")

    if cleanup:
        logger.info("Cleaning up old Phase 2 data...")
        try:
            db.execute_query("TRUNCATE TABLE restaurants RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE restaurant_opening_hours RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE restaurant_tags RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE media_assets RESTART IDENTITY CASCADE")

            db.commit()
            logger.info("Cleanup complete.")

        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            raise e

    loader = BlueprintLoader(blueprints_dir)
    restaurant_rules = loader.load_blueprint("restaurant_types.json")
    city_config = loader.load_blueprint("cities.json").get("CITY_CONFIG", {})

    cities = db.fetch_all("SELECT city_id, city_name FROM cities")

    global_target = int(GENERATION_CONFIG["num_restaurants"])  # type: ignore

    total_weight = 0
    city_weights = {}

    for city_name, config in city_config.items():
        weight = config.get("target_restaurants", 50)
        city_weights[city_name] = weight
        total_weight += weight

    if total_weight == 0:
        total_weight = 1

    city_counts = {}
    allocated_sum = 0

    sorted_cities_by_weight = sorted(cities, key=lambda x: city_weights.get(x[1], 0), reverse=True)

    for _city_id, city_name in sorted_cities_by_weight:
        weight = city_weights.get(city_name, 0)
        count = int(global_target * (weight / total_weight))
        city_counts[city_name] = count
        allocated_sum += count

    remainder = global_target - allocated_sum
    if remainder > 0 and sorted_cities_by_weight:
        top_city_name = sorted_cities_by_weight[0][1]
        city_counts[top_city_name] += remainder

    logger.info(f"Target restaurants: {global_target}")

    date_gen = DateGenerator()
    photo_pools = PhotoPools()
    name_generator = RestaurantNameGenerator()

    restaurant_data = []
    menu_sections_data = []
    restaurant_id_counter = 1
    primary_photo_cache = {}  # Cache primary photo metadata for photos table

    generated_phones = set()

    # Load Tier Probabilities from Global Config (Problem 1)
    global_config = loader.load_blueprint("global_config.json")
    tier_config = global_config.get("RESTAURANT_TIER_PROBABILITIES", {})
    default_tier_probs = tier_config.get("__default__", {"Budget": 0.2, "Casual": 0.7, "Fine Dining": 0.1})

    # Extract themes from loaded rules
    available_themes = list(restaurant_rules.get("RESTAURANT_THEMES", {}).keys())

    for city_id, city_name in tqdm(cities, desc="Generating restaurants", unit=" city", mininterval=1.0):
        num_restaurants = city_counts.get(city_name, 0)
        city_info = city_config.get(city_name, {})

        base_coords = city_info.get("coords", {"lat": 52.0, "lon": 19.0})

        for _ in range(num_restaurants):
            # Select theme based on distribution chance if available, else random
            # Here we use weighted random if 'distribution_chance' exists in themes
            theme_data = restaurant_rules.get("RESTAURANT_THEMES", {})
            weights = [theme_data.get(t, {}).get("distribution_chance", 0.05) for t in available_themes]
            theme = random.choices(available_themes, weights=weights, k=1)[0]
            
            theme_info = theme_data.get(theme, {})
            
            name = name_generator.generate_name(theme, city_name)
            created_date = date_gen.generate_restaurant_created_date()

            # Determine Tier (Mod 4)
            probs = tier_config.get(theme, default_tier_probs)
            tier = random.choices(list(probs.keys()), weights=list(probs.values()), k=1)[0]

            # Set Price Multiplier based on Tier (Mod 5)
            if tier == "Budget":
                secret_price_multiplier = random.uniform(0.6, 0.9)
                price_level = 1
                base_quality_mean = 0.4
            elif tier == "Casual":
                secret_price_multiplier = random.uniform(0.9, 1.3)
                price_level = 2
                base_quality_mean = 0.65
            else: # Fine Dining
                secret_price_multiplier = random.uniform(1.4, 2.5)
                price_level = 3
                base_quality_mean = 0.85

            # Quality attributes based on Tier
            base_food_quality = max(0.1, min(1.0, random.gauss(base_quality_mean, 0.1)))
            
            secret_overall_food_quality = base_food_quality
            secret_service_quality = max(0.1, min(1.0, base_food_quality + random.gauss(0, 0.1)))
            secret_cleanliness_score = max(0.1, min(1.0, base_food_quality + random.gauss(0, 0.1)))
            
            # Boost Fine Dining service/cleanliness
            if tier == "Fine Dining":
                secret_service_quality = min(1.0, secret_service_quality + 0.1)
                secret_cleanliness_score = min(1.0, secret_cleanliness_score + 0.1)

            ambiance_types = ["Romantyczny", "Rodzinny", "Biznesowy", "Casual", "Energiczny", "Spokojny"]
            secret_ambiance_type = random.choice(ambiance_types)
            secret_ambiance_quality = sample_beta(4, 3, 0.4, 0.95)

            menu_blueprint = _get_menu_blueprint(theme)

            lat = base_coords["lat"] + random.uniform(-0.05, 0.05)
            lon = base_coords["lon"] + random.uniform(-0.05, 0.05)

            rand_status = random.random()
            if rand_status < 0.95:
                status = "active"
            elif rand_status < 0.98:
                status = "renovation"
            elif rand_status < 0.99:
                status = "suspended"
            else:
                status = "closed_permanently"

            # Unique Phone Logic (Mod 1 & Problem 3 Fix)
            phone = fake.phone_number()
            phone_attempts = 0
            while phone in generated_phones:
                if phone_attempts > 10:
                    # Fallback to ensure uniqueness if loop gets stuck
                    phone = f"{phone}-{random.randint(1000, 9999)}"
                    break
                phone = fake.phone_number()
                phone_attempts += 1
            generated_phones.add(phone)

            # Verification Status (B2B Security Model)
            # 95%: verified (active/ghost)
            # 5%: unverified (pending admin approval)
            is_verified = random.random() < 0.95

            # Get primary photo metadata
            primary_photo_metadata = photo_pools.get_restaurant_photo(theme, restaurant_id_counter)
            # Cache metadata for use in _assign_restaurant_photos
            primary_photo_cache[restaurant_id_counter] = primary_photo_metadata

            restaurant_data.append(
                {
                    "public_id": str(uuid.uuid4()),
                    "city_id": city_id,
                    "restaurant_name": name,
                    "cuisine_type": theme,
                    "price_level": price_level,
                    "address": f"{fake.street_address()}, {city_name}",
                    "latitude": round(lat, 6),
                    "longitude": round(lon, 6),
                    "phone": phone,
                    "website": f"https://{slugify(name)}.pl",
                    "description": _generate_description(theme, tier, city_name), # Typowane opisy (Mod 2)
                    "image_url": primary_photo_metadata["url"],
                    "image_blurhash": primary_photo_metadata.get("blurhash"),
                    "status": status,
                    "is_verified": is_verified,
                    "owner_id": None, # Will be set in Phase 4
                    "created_at": DateGenerator.to_sql_datetime(created_date),
                    "updated_at": DateGenerator.to_sql_datetime(created_date),  # Initially same as created_at
                    "secret_price_multiplier": round(secret_price_multiplier, 3),
                    "secret_overall_food_quality": round(secret_overall_food_quality, 3),
                    "secret_service_quality": round(secret_service_quality, 3),
                    "secret_cleanliness_score": round(secret_cleanliness_score, 2),
                    "secret_ambiance_type": secret_ambiance_type,
                    "secret_ambiance_quality": round(secret_ambiance_quality, 3),
                    "secret_menu_blueprint": menu_blueprint,
                    "secret_archetype_modifiers": json.dumps(generate_restaurant_archetype_modifiers(menu_blueprint)),
                }
            )
            
            # Generate Menu Sections (Mod 10)
            menu_config = theme_info.get("menu_config", {}).get("sections", [])
            display_order = 1
            for section_def in menu_config:
                if random.random() <= section_def.get("chance", 1.0):
                    menu_sections_data.append({
                        "restaurant_id": restaurant_id_counter,
                        "section_name": section_def["name"],
                        "display_order": display_order
                    })
                    display_order += 1

            restaurant_id_counter += 1

    logger.info(f"Inserting {len(restaurant_data)} restaurants into database...")
    # Use RETURNING to get actual database IDs
    actual_restaurant_ids = db.insert_bulk_returning("restaurants", restaurant_data, "restaurant_id")
    logger.info(f"Successfully inserted {len(actual_restaurant_ids)} restaurants")

    # Map counter IDs to actual database IDs
    counter_to_db_id = {i+1: actual_id for i, actual_id in enumerate(actual_restaurant_ids)}

    # Update menu_sections with actual database IDs
    if menu_sections_data:
        for section in menu_sections_data:
            section["restaurant_id"] = counter_to_db_id[section["restaurant_id"]]
        db.insert_bulk("menu_sections", menu_sections_data)
        logger.info(f"Generated {len(menu_sections_data)} menu sections")

    _assign_restaurant_tags(db)
    _assign_restaurant_photos(db, photo_pools, primary_photo_cache)

    duration = time.time() - start_time
    logger.info(f"Generated {len(restaurant_data)} restaurants in {duration:.2f}s")

def _generate_description(theme: str, tier: str, city_name: str) -> str:
    # Mod 2: Typowane opisy
    if theme == "Pizzeria":
        base = random.choice([
            f"Najlepsza pizza w mieście {city_name}.",
            "Prawdziwa włoska receptura i piec opalany drewnem.",
            "Rodzinna pizzeria z tradycjami.",
            "Chrupiące ciasto i świeże składniki."
        ])
    elif theme == "Burgerownia":
        base = random.choice([
            "Soczyste burgery ze 100% wołowiny.",
            "Kraftowe burgery i domowe frytki.",
            f"Prawdziwy amerykański klimat w sercu {city_name}.",
            "Autorskie sosy i bułki wypiekane na miejscu."
        ])
    elif theme == "Sushi Bar":
        base = random.choice([
            "Świeże ryby i autentyczne japońskie smaki.",
            "Mistrzowie sushi zapraszają na kulinarną podróż.",
            "Tradycja i nowoczesność na jednym talerzu.",
            "Najlepsze sushi w okolicy."
        ])
    elif tier == "Fine Dining":
        base = random.choice([
            "Elegancja i wyrafinowany smak.",
            "Autorska kuchnia szefa kuchni dla wymagających.",
            "Wyjątkowe doświadczenie kulinarne.",
            "Idealne miejsce na romantyczną kolację lub spotkanie biznesowe."
        ])
    elif tier == "Budget":
        base = random.choice([
            "Szybko, smacznie i tanio.",
            "Najlepszy stosunek jakości do ceny.",
            "Ulubione miejsce studentów i nie tylko.",
            "Domowe smaki w dobrej cenie."
        ])
    else:
        base = f"Restauracja {theme} w {city_name}. Oferujemy autentyczne dania przygotowane z najlepszych składników."
        
    return base

def _select_restaurant_theme(rules: dict) -> str:
    # This function is deprecated by logic in main loop but kept for compatibility if needed
    themes = list(rules.get("RESTAURANT_THEMES", {}).keys())
    if not themes:
         return "Pizzeria"
    return random.choice(themes)

def _get_menu_blueprint(theme: str) -> str:
    blueprints = {
        "Pizzeria": "Pizzeria",
        "Burgerownia": "Burger Bar",
        "Sushi Bar": "Sushi Bar",
        "Kuchnia Azjatycka": "Asian Fusion",
        "Kuchnia Wietnamska": "Asian Fusion",
        "Kuchnia Chińska": "Asian Fusion",
        "Ramen Bar": "Asian Fusion",
        "Steakhouse": "Steakhouse",
        "Kawiarnia": "Cafe",
        "Piekarnia z Kawiarnią": "Cafe", 
        "Bar Meksykański": "Mexican Restaurant",
        "Kuchnia Włoska": "Italian Restaurant",
        "Francuskie Bistro": "French Bistro",
        "Restauracja z Owocami Morza": "Seafood Restaurant",
        "Kebab": "Kebab Place", 
        "Kuchnia Polska": "Polish Restaurant",
        "Kuchnia Indyjska": "Indian Restaurant",
        "Grecka Taverna": "Greek Taverna",
        "Wędzarnia BBQ": "BBQ Smokehouse",
        "Korean BBQ": "Korean Restaurant",
        "Bar Tapas": "Tapas Bar",
        "Amerykański Diner": "American Diner",
        "Niemiecki Pub": "German Pub",
        "Kuchnia Bliskowschodnia": "Middle Eastern",
        "Kuchnia Turecka": "Middle Eastern",
        "Lodziarnia": "Ice Cream Shop",
        "Kanapkownia": "Sandwich Shop",
        "Wykwintna Restauracja": "Fine Dining",
    }

    return blueprints.get(theme, "General")

def _assign_restaurant_tags(db: DatabaseConnection):
    logger.info("Assigning tags...")

    restaurants = RestaurantDAO.get_restaurants_with_cuisine(db)
    tags = db.fetch_all("SELECT tag_id, tag_name, category FROM tags")

    tag_assignments = []

    for restaurant_id, _theme in tqdm(restaurants, desc="Assigning tags", unit=" restaurant", mininterval=1.0):
        num_tags = random.randint(2, 4)
        selected_tags = random.sample(tags, min(num_tags, len(tags)))

        for tag_id, _, _ in selected_tags:
            tag_assignments.append({"restaurant_id": restaurant_id, "tag_id": tag_id})

    if tag_assignments:
        db.insert_bulk("restaurant_tags", tag_assignments)
        logger.info(f"Assigned {len(tag_assignments)} tags")

def _assign_restaurant_photos(db: DatabaseConnection, photo_pools: PhotoPools, primary_photo_cache: dict):
    logger.info("Adding restaurant photos...")

    restaurants = RestaurantDAO.get_restaurants_with_images(db)

    photo_data = []

    for restaurant_id, theme, primary_image_url in tqdm(
        restaurants, desc="Adding photos", unit=" restaurant", mininterval=1.0
    ):
        # Retrieve cached primary photo metadata (includes blurhash, width, height)
        primary_metadata = primary_photo_cache.get(restaurant_id, {})

        photo_data.append(
            {
                "public_id": str(uuid.uuid4()),
                "entity_type": "restaurant",
                "entity_id": restaurant_id,
                "url": primary_image_url,
                "blurhash": primary_metadata.get("blurhash"),
                "width": primary_metadata.get("width"),
                "height": primary_metadata.get("height"),
                "is_primary": True,
                "status": "approved",
            }
        )

        num_additional_photos = random.randint(1, 2)
        for _ in range(num_additional_photos):
            additional_photo_metadata = photo_pools.get_restaurant_photo(theme, restaurant_id)
            photo_data.append(
                {
                    "public_id": str(uuid.uuid4()),
                    "entity_type": "restaurant",
                    "entity_id": restaurant_id,
                    "url": additional_photo_metadata["url"],
                    "blurhash": additional_photo_metadata["blurhash"],
                    "width": additional_photo_metadata["width"],
                    "height": additional_photo_metadata["height"],
                    "is_primary": False,
                    "status": "approved",
                }
            )

    db.insert_bulk("media_assets", photo_data)
    logger.info(f"Added {len(photo_data)} photos")

    _assign_opening_hours(db)

def _assign_opening_hours(db: DatabaseConnection):
    logger.info("Generating opening hours...")

    restaurants = RestaurantDAO.get_restaurants_with_cuisine(db)
    hours_data = []

    for restaurant_id, theme in tqdm(restaurants, desc="Generating hours", unit=" restaurant", mininterval=1.0):
        schedule = _get_schedule_for_theme(theme)

        for day in range(1, 8):  # ISO 8601: 1=Mon, 7=Sun
            open_time, close_time = schedule["weekday"]

            if day in (5, 6):  # 5=Fri, 6=Sat
                open_time, close_time = schedule["weekend"]
            elif day == 7:  # 7=Sun
                open_time, close_time = schedule.get("sunday", schedule["weekday"])

            hours_data.append(
                {
                    "restaurant_id": restaurant_id,
                    "day_of_week": day,
                    "open_time": open_time,
                    "close_time": close_time,
                    "is_closed": False,
                }
            )

    db.insert_bulk("restaurant_opening_hours", hours_data)
    logger.info(f"Added {len(hours_data)} opening hour records")

def _get_schedule_for_theme(theme: str) -> dict:
    def random_time(start_hour_range, end_hour_range):
        h = random.randint(start_hour_range, end_hour_range)
        m = random.choice(["00", "30"])
        
        # Handle overflow for late night hours (e.g. 24 -> 00, 25 -> 01)
        if h >= 24:
            h = h - 24
            
        return f"{h:02d}:{m}"

    open_range = (11, 13)
    close_range = (21, 23)

    if theme in ["Pizzeria", "Italian", "Mexican", "Burger Bar"]:
        open_range = (11, 12)
        close_range = (22, 23)
    elif theme in ["Sushi Bar", "Asian Fusion", "Seafood"]:
        open_range = (12, 14)
        close_range = (22, 23)
    elif theme in ["Vegan Cafe", "French Bistro", "Breakfast Diner", "Bakery"]:
        open_range = (7, 10)
        close_range = (18, 21)
    elif theme in ["Steakhouse", "Fine Dining"]:
        open_range = (16, 17)
        close_range = (22, 23)
    elif theme in ["Kebab", "Fast Food"]:
        open_range = (10, 11)
        close_range = (23, 25) # Allow late night up to 01:00 (25)

    weekday_open = random_time(open_range[0], open_range[1])
    weekday_close = random_time(close_range[0], close_range[1])

    # Weekend logic
    weekend_open = weekday_open
    
    # Parse weekday close hour to extend for weekend
    try:
        wc_h, wc_m = map(int, weekday_close.split(":"))
    except ValueError:
        wc_h, wc_m = 22, 0 # Fallback
        
    # Extend by 1 hour for weekend
    we_close_h = wc_h + 1
    if we_close_h >= 24:
        we_close_h -= 24
        
    weekend_close = f"{we_close_h:02d}:{wc_m:02d}"

    # Sunday logic (usually earlier close)
    sunday_close_h = wc_h - 1
    if sunday_close_h < 0: # If was 00:00, becomes 23:00
        sunday_close_h += 24
    
    # Ensure Sunday doesn't close too early (e.g. before open) - simplified check
    if sunday_close_h < 12 and sunday_close_h > 4: # Assuming open is ~10-12
         sunday_close_h = 20 # Fallback to 8 PM
         
    sunday_close = f"{sunday_close_h:02d}:{wc_m:02d}"

    return {
        "weekday": (weekday_open, weekday_close),
        "weekend": (weekend_open, weekend_close),
        "sunday": (weekday_open, sunday_close),
    }

if __name__ == "__main__":
    import os
    import sys

    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

    from config import get_connection_params

    logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

    try:
        connection_params = get_connection_params()

        with DatabaseConnection(connection_params) as db:
            generate_restaurants(db, blueprints_dir="blueprints")
            logger.info("Phase 2 completed.")

    except Exception as e:
        logger.error(f"Error: {e}", exc_info=True)
        sys.exit(1)