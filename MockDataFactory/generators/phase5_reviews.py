"""
Phase 5 - Generowanie recenzji (~875,000) - NAJBARDZIEJ KRYTYCZNE!
"""

import logging
import random
import json
from datetime import timedelta

from utils.db_connection import DatabaseConnection
from utils.date_generator import DateGenerator
from utils.text_generator import ReviewTextGenerator
from utils.photo_pools import PhotoPools
from utils.helpers import safe_json_loads, safe_divide, safe_float
from algorithms.rating_engine import calculate_review_ratings
from algorithms.restaurant_selector import select_restaurants_for_user
from algorithms.dish_selector import select_dish_from_menu

logger = logging.getLogger(__name__)

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

    # Cleanup old data
    logger.info("🧹 Czyszczenie starych danych Phase 5 (reviews, user_photos, pending_comments)...")
    try:
        # Use execute_query directly instead of manual cursor management
        # This handles connection and cursor internally
        db.execute_query("TRUNCATE TABLE reviews RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE user_photos RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE pending_comments RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE pending_user_photos RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE ai_review_photos RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE ai_review_comments RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE admin_review_photos RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE admin_review_comments RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE review_likes RESTART IDENTITY CASCADE")
        
        db.commit()
        logger.info("✅ Wyczyszczono stare recenzje i powiązane tabele.")
        
    except Exception as e:
        logger.error(f"❌ Błąd podczas cleanup Phase 5: {e}")
        db.rollback()
        raise e

    # Pobierz wszystkich users
    users = db.fetch_all("""
        SELECT user_id, home_city_id, secret_total_review_count, secret_travel_propensity,
               secret_enjoyed_archetypes, secret_ingredient_preferences,
               secret_cleanliness_preference, secret_preferred_ambiance,
               secret_mood_propensity, secret_cross_impact_factor,
               secret_chance_dine_random, secret_chance_pick_random_dish,
               account_created_at, secret_characteristics_vector,
               secret_rating_baseline
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
    
    if users:
        logger.info(f"DEBUG USER ROW [0]: {users[0]}")
        logger.info(f"DEBUG USER ROW LEN: {len(users[0])}")

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
        
        # --- ROBUSTNESS FIX START ---
        # Check for index shift or type mismatch
        # Indices shifted due to new column:
        # 12: account_created_at
        # 13: secret_characteristics_vector
        # 14: secret_rating_baseline
        raw_join_date = user[12]
        raw_vector = user[13]
        try:
            raw_baseline = user[14]
        except IndexError:
            raw_baseline = 6.0 # Fallback
        
        final_join_date = None
        final_pref_vector = {}
        
        from datetime import datetime
        # Detect shift logic (kept for safety, though fixed query should be stable)
        if isinstance(raw_vector, datetime):
            logger.warning(f"DATA SHIFT DETECTED for User {user_id} (idx {idx}). Adjusting...")
            final_join_date = raw_vector
            final_pref_vector = {}
        else:
            # Normal case
            final_join_date = raw_join_date
            final_pref_vector = safe_json_loads(raw_vector, {})

        # Final validation
        if not isinstance(final_join_date, datetime):
            logger.warning(f"Invalid join_date for User {user_id}. Fallback to 2020-01-01.")
            final_join_date = datetime(2020, 1, 1)
        # --- ROBUSTNESS FIX END ---

        pref_vector = final_pref_vector
        
        # Parse JSON strings (FIXED: używa safe_json_loads zamiast hacka z .replace())
        user_data = {
            'user_id': user_id,
            'city_id': home_city_id,
            'secret_total_review_count': num_reviews,
            'travel_propensity': travel_prop,
            'secret_enjoyed_archetypes': safe_json_loads(user[4], {}), # Now fetched from DB!
            'secret_ingredient_preferences': safe_json_loads(user[5], {}),
            # Removed columns handling - using defaults:
            'secret_price_preference_range': 35.0,
            'secret_price_tolerance_above': 2.0,
            'secret_price_tolerance_below': 0.5,
            # Extract individual preferences from vector if available, else defaults
            'secret_spice_preference': pref_vector.get('flavor_spiciness', 0.5),
            'secret_richness_preference': pref_vector.get('physics_richness', 0.5),
            'secret_texture_preference': pref_vector.get('texture_crispy', 0.5), # heuristic
            
            'secret_cleanliness_preference': safe_json_loads(user[6], {}),
            'secret_preferred_ambiance': user[7],
            'secret_mood_propensity': user[8],
            'secret_cross_impact_factor': user[9],
            'secret_chance_dine_random': user[10] if user[10] is not None else 0.1,
            'secret_chance_pick_random_dish': user[11] if user[11] is not None else 0.05,
            'join_date': final_join_date,
            'secret_characteristics_vector': pref_vector,
            'secret_rating_baseline': raw_baseline
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

            # OPTIMIZATION: Filter restaurants that existed at review_date
            # This prevents discarding reviews and ensures valid selection
            available_restaurants = [
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
                for r in all_restaurants 
                if r[1] == city_id and r[3] <= review_date
            ]

            if not available_restaurants:
                # No restaurants available in this city at this date (too early in timeline)
                # Try fallback to home city if we were traveling
                if city_id != home_city_id:
                    available_restaurants = [
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
                        for r in all_restaurants 
                        if r[1] == home_city_id and r[3] <= review_date
                    ]
                
                if not available_restaurants:
                    skipped_reviews_temporal += 1
                    continue

            # Wybierz restaurację (używa restaurant_selector.py)
            selected_restaurant_ids = select_restaurants_for_user(
                user_data, available_restaurants, city_id if city_id in [r['city_id'] for r in available_restaurants] else home_city_id, count=1
            )

            if not selected_restaurant_ids:
                continue

            restaurant_id = selected_restaurant_ids[0]
            # Get full restaurant object
            restaurant = next((r for r in available_restaurants if r['restaurant_id'] == restaurant_id), None)
            
            # No need for temporal check here anymore - filtered above

    # Track review dates per user for last_login_at calculation
    user_review_dates = {}

    for idx, user in enumerate(users):
        user_id = user[0]
        home_city_id = user[1]
        num_reviews = user[2]
        travel_prop = user[3]
        
        # Track reviewed dishes to prevent duplicates (user_id, dish_id)
        reviewed_dishes = set()
        
        # Extract JSONB preference vector early (Index 13)
        pref_vector = safe_json_loads(user[13], {})

        # Extract Baseline (Index 14) - Added in Schema Update
        try:
            raw_baseline = user[14]
        except IndexError:
            raw_baseline = 6.0

        # Parse JSON strings (FIXED: używa safe_json_loads zamiast hacka z .replace())
        user_data = {
            'user_id': user_id,
            'city_id': home_city_id,
            'secret_total_review_count': num_reviews,
            'travel_propensity': travel_prop,
            'secret_enjoyed_archetypes': safe_json_loads(user[4], {}),
            'secret_ingredient_preferences': safe_json_loads(user[5], {}),
            # Removed columns handling - using defaults:
            'secret_price_preference_range': 35.0,
            'secret_price_tolerance_above': 2.0,
            'secret_price_tolerance_below': 0.5,
            # Extract individual preferences from vector if available, else defaults
            'secret_spice_preference': pref_vector.get('flavor_spiciness', 0.5),
            'secret_richness_preference': pref_vector.get('physics_richness', 0.5),
            'secret_texture_preference': pref_vector.get('texture_crispy', 0.5), # heuristic
            
            'secret_cleanliness_preference': safe_json_loads(user[6], {}),
            'secret_preferred_ambiance': user[7],
            'secret_mood_propensity': user[8],
            'secret_cross_impact_factor': user[9],
            'secret_chance_dine_random': user[10] if user[10] is not None else 0.1,
            'secret_chance_pick_random_dish': user[11] if user[11] is not None else 0.05,
            'join_date': user[12],
            'secret_characteristics_vector': pref_vector,
            'secret_rating_baseline': raw_baseline # NEW: Critical for Rating Engine V5
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

            # OPTIMIZATION: Filter restaurants that existed at review_date
            available_restaurants = [
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
                for r in all_restaurants 
                if r[1] == city_id and r[3] <= review_date
            ]

            if not available_restaurants:
                # FALLBACK: TRAVEL MODE
                # If no restaurants in chosen city, try ALL cities (Force Travel)
                available_restaurants = [
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
                    for r in all_restaurants 
                    if r[3] <= review_date
                ]
                
                if available_restaurants:
                    # Pick a random city from available restaurants to simulate travel
                    random_res = random.choice(available_restaurants)
                    city_id = random_res['city_id']
                    # Filter again for this new city to keep context valid
                    available_restaurants = [r for r in available_restaurants if r['city_id'] == city_id]
                else:
                    skipped_reviews_temporal += 1
                    continue

            # Wybierz restaurację (używa restaurant_selector.py)
            selected_restaurant_ids = select_restaurants_for_user(
                user_data, available_restaurants, city_id, count=3 # Pick top 3 candidates to find unreviewed dish
            )

            if not selected_restaurant_ids:
                continue

            selected_dish = None
            restaurant = None
            restaurant_id = None

            # Try to find a restaurant with an unreviewed dish
            for r_id in selected_restaurant_ids:
                candidate_restaurant = next((r for r in available_restaurants if r['restaurant_id'] == r_id), None)
                if not candidate_restaurant: continue

                # Pobierz dania restauracji
                dishes = db.fetch_all("""
                    SELECT dish_id, dish_name, secret_archetype, price,
                           secret_base_price, secret_quality, secret_popularity_factor,
                           secret_characteristics_vector, secret_weights_vector, secret_variant_name
                    FROM dishes
                    WHERE restaurant_id = %s
                """, (r_id,))

                if not dishes: continue

                # Filter out already reviewed dishes
                unreviewed_dishes_raw = [d for d in dishes if d[0] not in reviewed_dishes]
                
                if not unreviewed_dishes_raw:
                    continue # Try next restaurant

                # Found a valid restaurant!
                restaurant = candidate_restaurant
                restaurant_id = r_id
                
                # Process dishes for selection
                # (Copy-paste logic from before, but now inside loop)
                dish_ids = [d[0] for d in unreviewed_dishes_raw]
                
                # Batch load ingredients for these dishes
                ingredients_by_dish = {}
                if dish_ids:
                    placeholders = ','.join(['%s'] * len(dish_ids))
                    all_ingredients = db.fetch_all(f"""
                        SELECT dil.dish_id, i.ingredient_name
                        FROM dish_ingredients_link dil
                        JOIN ingredients i ON dil.ingredient_id = i.ingredient_id
                        WHERE dil.dish_id IN ({placeholders})
                    """, tuple(dish_ids))
                    for d_id, i_name in all_ingredients:
                        if d_id not in ingredients_by_dish: ingredients_by_dish[d_id] = []
                        ingredients_by_dish[d_id].append(i_name)

                dish_dicts = []
                for d in unreviewed_dishes_raw:
                    d_id = d[0]
                    char_vector = safe_json_loads(d[7])
                    dish_dicts.append({
                        'dish_id': d_id,
                        'dish_name': d[1],
                        'secret_archetype': d[2],
                        'price': d[3],
                        'secret_base_price': d[4],
                        'secret_quality': d[5],
                        'secret_popularity_factor': d[6],
                        'secret_characteristics_vector': char_vector,
                        'secret_weights_vector': safe_json_loads(d[8]),
                        'secret_variant_name': d[9],
                        'ingredients': ingredients_by_dish.get(d_id, [])
                    })

                selected_dish = select_dish_from_menu(user_data, dish_dicts)
                if selected_dish:
                    break # Found dish, exit loop

            if not selected_dish:
                # User has reviewed EVERYTHING in selected restaurants or no dishes available
                # Skip this review slot (or could force re-review, but uniqueness is preferred)
                continue

            # Mark as reviewed
            reviewed_dishes.add(selected_dish['dish_id'])

            # ... continue with calculation ...
            variant_name = selected_dish.get('secret_variant_name')
            # ... (rest of the code remains same, just ensure indentation matches)

            # ===== OPTIMIZATION: Fetch pre-calculated preference vector =====
            # If user_variant_preferences table has this combination, use it for 3-5x speedup
            user_variant_preference_vector = None
            variant_name = selected_dish.get('secret_variant_name')

            if variant_name:
                try:
                    pref_result = db.fetch_one("""
                        SELECT preference_vector
                        FROM user_variant_preferences
                        WHERE user_id = %s AND variant_name = %s
                    """, (user_id, variant_name))

                    if pref_result:
                        user_variant_preference_vector = safe_json_loads(pref_result[0], None)
                except Exception as e:
                    # Fallback to on-the-fly calculation if table doesn't exist or query fails
                    # This ensures backward compatibility
                    pass

            # OBLICZ OCENY (używa rating_engine.py) ← NAJWAŻNIEJSZE!
            # Pass pre-calculated preference vector for optimization (if available)
            ratings = calculate_review_ratings(user_data, selected_dish, restaurant,
                                               user_variant_preference_vector=user_variant_preference_vector)

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
