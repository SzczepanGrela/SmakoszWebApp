"""
Phase 5 - Generowanie recenzji (~875,000) - NAJBARDZIEJ KRYTYCZNE!
"""

import logging
import random
import json
import sys
import os
from datetime import timedelta

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.date_generator import DateGenerator
from utils.text_generator import ReviewTextGenerator
from utils.photo_pools import PhotoPools
from algorithms.rating_engine import calculate_review_ratings
from algorithms.restaurant_selector import select_restaurants_for_user
from algorithms.dish_selector import select_dish_from_menu

logger = logging.getLogger(__name__)

def safe_json_loads(value, default=None):
    """
    Bezpieczne parsowanie JSON z obsługą NULL i pustych wartości

    Args:
        value: Wartość do sparsowania (str, dict, lub None)
        default: Wartość domyślna jeśli value jest None/puste

    Returns:
        Sparsowany obiekt lub default
    """
    if value is None or value == '':
        return default if default is not None else {}

    # FIXED: PostgreSQL może zwracać JSON jako już sparsowany dict
    if isinstance(value, dict):
        return value

    try:
        return json.loads(value)
    except (json.JSONDecodeError, TypeError) as e:
        logger.warning(f"JSON parse error: {e}, value: {value}")
        return default if default is not None else {}

def safe_divide(numerator, denominator, default=1.0):
    """
    Bezpieczne dzielenie z zabezpieczeniem przed zerem

    Args:
        numerator: Licznik
        denominator: Mianownik
        default: Wartość domyślna jeśli dzielenie niemożliwe

    Returns:
        Wynik dzielenia lub default
    """
    if denominator is None or denominator == 0:
        return default
    try:
        return numerator / denominator
    except (TypeError, ZeroDivisionError):
        return default

def safe_float(value, default=0.0):
    """
    Bezpieczna konwersja do float

    Args:
        value: Wartość do konwersji
        default: Wartość domyślna jeśli konwersja niemożliwa

    Returns:
        Float lub default
    """
    if value is None or value == '':
        return default
    try:
        return float(value)
    except (ValueError, TypeError):
        return default

def generate_review_title(dish_rating: int, dish_name: str) -> str:
    """
    Generuje krótki tytuł recenzji (do 100 znaków) na podstawie oceny

    Args:
        dish_rating: Ocena dania (1-10)
        dish_name: Nazwa dania

    Returns:
        Tytuł recenzji po polsku
    """
    if dish_rating >= 9:
        templates = [
            "Wyśmienite! {dish_name} na najwyższym poziomie",
            "Przepyszne {dish_name} - polecam!",
            "Rewelacyjne {dish_name}",
            "Najlepsze {dish_name} jakie jadłem!",
            "Perfekcyjne {dish_name}"
        ]
    elif dish_rating >= 7:
        templates = [
            "Bardzo dobre {dish_name}",
            "Solidne {dish_name}, polecam",
            "{dish_name} - warte spróbowania",
            "Smaczne {dish_name}",
            "Dobre {dish_name}, wrócę tu"
        ]
    elif dish_rating >= 5:
        templates = [
            "{dish_name} - w porządku",
            "Przeciętne {dish_name}",
            "{dish_name} - nic specjalnego",
            "Okej, ale bez rewelacji",
            "{dish_name} - średnio"
        ]
    elif dish_rating >= 3:
        templates = [
            "{dish_name} - rozczarowanie",
            "Słabe {dish_name}",
            "{dish_name} - nie polecam",
            "Niestety {dish_name} nie smakowało",
            "{dish_name} - poniżej oczekiwań"
        ]
    else:
        templates = [
            "{dish_name} - tragedia",
            "Okropne {dish_name}",
            "{dish_name} - nie jedzcie tego!",
            "Fatalne {dish_name}",
            "{dish_name} - kompletna porażka"
        ]

    title = random.choice(templates).format(dish_name=dish_name)
    return title[:100]  # Max 100 chars

def generate_reviews(db: DatabaseConnection):
    """
    Generuje ~875,000 recenzji używając algorytmu oceniania

    Proces:
    1. Dla każdego użytkownika:
       a. Wygeneruj daty recenzji (dates_with_spacing)
       b. Dla każdej daty:
          - Wybierz miasto (80% home city, 20% travel)
          - Wybierz restaurację (używa restaurant_selector.py)
          - Wybierz danie (używa dish_selector.py)
          - OBLICZ OCENY (używa rating_engine.py) ← KLUCZOWE!
          - Wygeneruj komentarz (text_generator.py)
          - Insert do Reviews
          - 30% szans: dodaj zdjęcie użytkownika
          - 2-3% szans: do kolejki moderacji
    """
    logger.info(" Generowanie recenzji...")

    # Pobierz wszystkich users
    users = db.fetch_all("""
        SELECT user_id, home_city_id, secret_total_review_count, secret_travel_propensity,
               secret_enjoyed_archetypes, secret_ingredient_preferences,
               secret_price_preference_range, secret_price_tolerance_above, secret_price_tolerance_below,
               secret_spice_preference, secret_richness_preference, secret_texture_preference,
               secret_cleanliness_preference, secret_preferred_ambiance,
               secret_mood_propensity, secret_cross_impact_factor,
               secret_chance_dine_random, secret_chance_pick_random_dish,
               account_created_at
        FROM users
    """)

    # Pobierz wszystkie restauracje
    all_restaurants = db.fetch_all("""
        SELECT restaurant_id, city_id, cuisine_type, created_at,
               secret_price_multiplier, secret_overall_food_quality,
               secret_service_quality, secret_cleanliness_score,
               secret_ambiance_type, secret_ambiance_quality
        FROM restaurants
    """)

    # Pobierz wszystkie miasta
    cities = db.fetch_all("SELECT city_id FROM cities")
    city_ids = [c[0] for c in cities]

    # FIXED: Validate that required data exists
    if not users:
        logger.error(" Brak użytkowników w bazie! Nie można wygenerować recenzji.")
        return

    if not all_restaurants:
        logger.error(" Brak restauracji w bazie! Nie można wygenerować recenzji.")
        return

    if not city_ids:
        logger.error(" Brak miast w bazie! Nie można wygenerować recenzji.")
        return

    logger.info(f" Dane wejściowe: {len(users)} użytkowników, {len(all_restaurants)} restauracji, {len(city_ids)} miast")

    date_gen = DateGenerator()
    text_gen = ReviewTextGenerator()
    photo_pools = PhotoPools()

    total_reviews = 0
    skipped_reviews_temporal = 0  # Counter for reviews skipped due to temporal validation
    log_interval = 50000  # Log every 50000 reviews

    # Track review dates per user for last_login_at calculation
    user_review_dates = {}

    for idx, user in enumerate(users):
        user_id = user[0]
        home_city_id = user[1]
        num_reviews = user[2]
        travel_prop = user[3]

        # Parse JSON strings (FIXED: używa safe_json_loads zamiast hacka z .replace())
        user_data = {
            'user_id': user_id,
            'city_id': home_city_id,
            'secret_total_review_count': num_reviews,
            'travel_propensity': travel_prop,
            'secret_enjoyed_archetypes': safe_json_loads(user[4], {}),
            'secret_ingredient_preferences': safe_json_loads(user[5], {}),
            'secret_price_preference_range': safe_float(user[6], 35.0),  # FIXED: safe conversion
            'secret_price_tolerance_above': user[7] if user[7] else 2.0,  # Separate column
            'secret_price_tolerance_below': user[8] if user[8] else 0.5,  # Separate column
            'secret_spice_preference': user[9],
            'secret_richness_preference': user[10],
            'secret_texture_preference': user[11],
            'secret_cleanliness_preference': safe_json_loads(user[12], {}),
            'secret_preferred_ambiance': user[13],
            'secret_mood_propensity': user[14],
            'secret_cross_impact_factor': user[15],
            'secret_chance_dine_random': user[16] if user[16] is not None else 0.1,
            'secret_chance_pick_random_dish': user[17] if user[17] is not None else 0.05,
            'join_date': user[18]
        }

        # Generuj daty recenzji
        review_dates = date_gen.generate_dates_with_spacing(
            count=num_reviews,
            start_date=user_data['join_date'],
            min_days=3,
            max_days=14
        )

        for review_date in review_dates:
            # Wybierz miasto (80% home, 20% travel)
            if random.random() < travel_prop:
                city_id = random.choice(city_ids)
            else:
                city_id = home_city_id

            # Filtruj restauracje w tym mieście
            city_restaurants = [
                {
                    'restaurant_id': r[0],
                    'city_id': r[1],
                    'cuisine_type': r[2],
                    'created_at': r[3],
                    'secret_price_multiplier': r[4],
                    'secret_overall_food_quality': r[5],
                    'secret_service_quality': r[6],
                    'secret_cleanliness_score': r[7],
                    'secret_ambiance_type': r[8],
                    'secret_ambiance_quality': r[9]
                }
                for r in all_restaurants if r[1] == city_id
            ]

            if not city_restaurants:
                continue

            # Wybierz restaurację (używa restaurant_selector.py)
            selected_restaurant_ids = select_restaurants_for_user(
                user_data, city_restaurants, city_id, count=1
            )

            if not selected_restaurant_ids:
                continue

            restaurant_id = selected_restaurant_ids[0]
            # FIXED: Safe next() with default None to prevent StopIteration
            restaurant = next((r for r in city_restaurants if r['restaurant_id'] == restaurant_id), None)
            if not restaurant:
                logger.warning(f"⚠️ Restauracja {restaurant_id} nie znaleziona w liście miasta")
                continue

            # TEMPORAL VALIDATION: Review date must be >= restaurant created_at
            # This ensures users can't review restaurants that didn't exist yet
            if review_date < restaurant['created_at']:
                skipped_reviews_temporal += 1
                continue  # Skip this review - restaurant didn't exist at review_date

            # Pobierz dania restauracji
            dishes = db.fetch_all("""
                SELECT dish_id, dish_name, secret_archetype, price,
                       secret_base_price, secret_quality, secret_spiciness,
                       secret_richness, secret_texture_score, secret_popularity_factor
                FROM dishes
                WHERE restaurant_id = %s
            """, (restaurant_id,))

            if not dishes:
                continue

            # Konwertuj do dict i załaduj składniki
            # FIXED N+1 PROBLEM: Pobierz WSZYSTKIE składniki dla WSZYSTKICH dań naraz
            dish_ids = [d[0] for d in dishes]
            ingredients_by_dish = {}

            if dish_ids:
                placeholders = ','.join(['%s'] * len(dish_ids))
                all_ingredients = db.fetch_all(f"""
                    SELECT dil.dish_id, i.ingredient_name
                    FROM dish_ingredients_link dil
                    JOIN ingredients i ON dil.ingredient_id = i.ingredient_id
                    WHERE dil.dish_id IN ({placeholders})
                """, tuple(dish_ids))

                # Grupuj składniki per dish_id
                for dish_id, ingredient_name in all_ingredients:
                    if dish_id not in ingredients_by_dish:
                        ingredients_by_dish[dish_id] = []
                    ingredients_by_dish[dish_id].append(ingredient_name)

            # Teraz buduj dish_dicts używając zgrupowanych składników
            dish_dicts = []
            for d in dishes:
                dish_id = d[0]
                dish_dicts.append({
                    'dish_id': dish_id,
                    'dish_name': d[1],
                    'secret_archetype': d[2],
                    'price': d[3],
                    'secret_base_price': d[4],
                    'secret_quality': d[5],
                    'secret_spiciness': d[6],
                    'secret_richness': d[7],
                    'secret_texture_score': d[8],
                    'secret_popularity_factor': d[9],
                    'ingredients': ingredients_by_dish.get(dish_id, [])  # FIXED: Batch loaded
                })

            # Wybierz danie (używa dish_selector.py)
            selected_dish = select_dish_from_menu(user_data, dish_dicts)

            if not selected_dish:
                continue

            # OBLICZ OCENY (używa rating_engine.py) ← NAJWAŻNIEJSZE!
            ratings = calculate_review_ratings(user_data, selected_dish, restaurant)

            # FIXED: 60% recenzji ma komentarz, 40% bez komentarza
            has_comment = random.random() < 0.60
            comment = None
            comment_is_pending = False

            if has_comment:
                # Wygeneruj komentarz
                comment = text_gen.generate_review_comment(
                    rating=ratings['overall_rating'],
                    dish_name=selected_dish['dish_name'],
                    restaurant_name=f"Restaurant_{restaurant_id}",
                    city="City",
                    quality_score=selected_dish['secret_quality'],
                    price_ratio=safe_divide(selected_dish['price'], user_data['secret_price_preference_range'], 1.0),
                    service_score=restaurant['secret_service_quality'],
                    cleanliness_score=restaurant['secret_cleanliness_score'],
                    ambiance_score=restaurant['secret_ambiance_quality'] * 10
                )

                # FIXED: 20% komentarzy do moderacji
                comment_is_pending = random.random() < 0.20

            # FIXED: Single insert aby mieć prawdziwe review_id
            dish_rating_value = int(round(ratings['food_score']))
            review_data = {
                'user_id': user_id,
                'restaurant_id': restaurant_id,
                'dish_id': selected_dish['dish_id'],
                'dish_rating': dish_rating_value,  # FIXED: food_score -> dish_rating
                'service_rating': int(round(ratings['service_score'])),  # FIXED: nazwy kolumn
                'cleanliness_rating': int(round(ratings['cleanliness_score'])),
                'ambiance_rating': int(round(ratings['ambiance_score'])),
                'review_comment': None if comment_is_pending else comment,  # FIXED: NULL if pending
                'review_title': generate_review_title(dish_rating_value, selected_dish['dish_name']),
                'review_date': DateGenerator.to_sql_datetime(review_date)
            }

            review_id = db.insert_single("reviews", review_data)  # FIXED: Prawdziwe ID!
            total_reviews += 1

            # Track review date for last_login_at calculation
            if user_id not in user_review_dates:
                user_review_dates[user_id] = []
            user_review_dates[user_id].append(review_date)

            # FIXED: Jeśli komentarz pending, dodaj do pending_comments
            if comment_is_pending:
                # Wstaw do pending_comments
                pending_comment_id = db.insert_single("pending_comments", {
                    'review_id': review_id,
                    'submitted_by_user_id': user_id,
                    'comment_text': comment,
                    'status': 'pending'
                })

                # 50% szans: AI review, 50% szans: Admin review
                if random.random() < 0.5:
                    # -> AI review queue
                    db.insert_single("ai_review_comments", {
                        'pending_comment_id': pending_comment_id,
                        'submitted_by_user_id': user_id,
                        'status': 'pending'
                    })
                else:
                    # -> Admin review queue
                    db.insert_single("admin_review_comments", {
                        'pending_comment_id': pending_comment_id,
                        'submitted_by_user_id': user_id,
                        'status': 'pending'
                    })

            # FIXED: 30% szans na zdjęcie użytkownika, 20% zdjęć do moderacji
            if random.random() < 0.30:
                photo_url = photo_pools.get_user_photo_generic()

                # FIXED: 20% zdjęć do moderacji
                photo_is_pending = random.random() < 0.20

                # Wstaw do user_photos (is_approved=False jeśli pending)
                user_photo_id = db.insert_single("user_photos", {
                    'review_id': review_id,
                    'uploaded_by_user_id': user_id,
                    'photo_url': photo_url,
                    'is_approved': not photo_is_pending  # False if pending, True if auto-approved
                })

                # Jeśli pending, dodaj do review queue
                if photo_is_pending:
                    db.insert_single("pending_user_photos", {
                        'user_photo_id': user_photo_id,
                        'submitted_by_user_id': user_id,
                        'status': 'pending'
                    })

                    # 50% szans: AI review, 50% szans: Admin review
                    if random.random() < 0.5:
                        # -> AI review queue
                        db.insert_single("ai_review_photos", {
                            'user_photo_id': user_photo_id,
                            'submitted_by_user_id': user_id,
                            'status': 'pending'
                        })
                    else:
                        # -> Admin review queue
                        db.insert_single("admin_review_photos", {
                            'user_photo_id': user_photo_id,
                            'submitted_by_user_id': user_id,
                            'status': 'pending'
                        })

            # Log co log_interval recenzji
            if total_reviews % log_interval == 0:
                logger.info(f"   Wygenerowano {total_reviews} recenzji...")

        if (idx + 1) % 5000 == 0:
            logger.info(f"  Przetworzono {idx + 1}/{len(users)} użytkowników...")

    # ========================================
    # POST-PROCESSING: Update last_login_at based on actual review activity
    # ========================================
    logger.info(" Aktualizacja last_login_at na podstawie rzeczywistych recenzji...")

    users_updated = 0
    users_no_reviews = 0

    for user_id, review_dates in user_review_dates.items():
        if review_dates:
            # Set last_login to latest review + random offset (0-30 days)
            latest_review = max(review_dates)
            days_offset = random.randint(0, 30)
            hours_offset = random.randint(0, 23)
            minutes_offset = random.randint(0, 59)

            last_login = latest_review + timedelta(
                days=days_offset,
                hours=hours_offset,
                minutes=minutes_offset
            )

            db.execute_query(
                "UPDATE users SET last_login_at = %s WHERE user_id = %s",
                (DateGenerator.to_sql_datetime(last_login), user_id)
            )
            users_updated += 1
        else:
            # User has no reviews - leave last_login_at as NULL
            users_no_reviews += 1

    db.commit()

    logger.info(f" Zaktualizowano last_login_at dla {users_updated} użytkowników")
    if users_no_reviews > 0:
        logger.info(f"   • {users_no_reviews} użytkowników bez recenzji (last_login_at = NULL)")

    # ========================================
    # FINAL STATISTICS
    # ========================================
    logger.info(f" Wygenerowano {total_reviews} recenzji")
    if skipped_reviews_temporal > 0:
        logger.info(f"   • Pominięto {skipped_reviews_temporal} recenzji (walidacja temporalna: review_date < restaurant.created_at)")
    logger.info(" ✅ System moderacji zintegrowany:")
    logger.info("    • 60% recenzji ma komentarze -> 20% do moderacji")
    logger.info("    • 30% recenzji ma zdjęcia -> 20% do moderacji")
    logger.info("    • Pending items: 50% -> ai_review_*, 50% -> admin_review_*")
    logger.info(" ✅ Walidacja temporalna:")
    logger.info("    • review_date >= user.account_created_at (ZAWSZE)")
    logger.info("    • review_date >= restaurant.created_at (ZAWSZE)")
    logger.info("    • last_login_at = max(review_dates) + random(0-30 dni)")
