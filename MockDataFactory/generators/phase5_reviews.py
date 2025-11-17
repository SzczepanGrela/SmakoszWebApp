"""
Phase 5 - Generowanie recenzji (~875,000) - NAJBARDZIEJ KRYTYCZNE!
"""

import logging
import random
import json
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.date_generator import DateGenerator
from utils.text_generator import ReviewTextGenerator
from utils.photo_pools import PhotoPools
from algorithms.rating_engine import calculate_review_ratings
from algorithms.restaurant_selector import select_restaurants_for_user
from algorithms.dish_selector import select_dish_from_menu

logger = logging.getLogger(__name__)


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
    logger.info("⭐ Generowanie recenzji...")

    # Pobierz wszystkich users
    users = db.fetch_all("""
        SELECT user_id, city_id, secret_total_review_count, travel_propensity,
               secret_enjoyed_archetypes, secret_ingredient_preferences,
               secret_price_preference_range, secret_spice_preference,
               secret_richness_preference, secret_texture_preference,
               secret_cleanliness_preference, secret_preferred_ambiance,
               secret_mood_propensity, secret_cross_impact_factor,
               join_date
        FROM Users
    """)

    # Pobierz wszystkie restauracje
    all_restaurants = db.fetch_all("""
        SELECT restaurant_id, city_id, theme, created_date,
               secret_price_multiplier, secret_overall_food_quality,
               secret_service_quality, secret_cleanliness_score,
               secret_ambiance_type, secret_ambiance_quality
        FROM Restaurants
    """)

    # Pobierz wszystkie miasta
    cities = db.fetch_all("SELECT city_id FROM Cities")
    city_ids = [c[0] for c in cities]

    date_gen = DateGenerator()
    text_gen = ReviewTextGenerator()
    photo_pools = PhotoPools()

    total_reviews = 0
    batch_size = 5000
    review_batch = []
    photo_batch = []

    for idx, user in enumerate(users):
        user_id = user[0]
        home_city_id = user[1]
        num_reviews = user[2]
        travel_prop = user[3]

        # Parse JSON strings
        user_data = {
            'user_id': user_id,
            'city_id': home_city_id,
            'secret_total_review_count': num_reviews,
            'travel_propensity': travel_prop,
            'secret_enjoyed_archetypes': json.loads(user[4].replace("'", "\"")),
            'secret_ingredient_preferences': json.loads(user[5].replace("'", "\"")),
            'secret_price_preference_range': json.loads(user[6].replace("'", "\"")),
            'secret_spice_preference': user[7],
            'secret_richness_preference': user[8],
            'secret_texture_preference': user[9],
            'secret_cleanliness_preference': json.loads(user[10].replace("'", "\"")),
            'secret_preferred_ambiance': user[11],
            'secret_mood_propensity': user[12],
            'secret_cross_impact_factor': user[13],
            'join_date': user[14]
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
                    'theme': r[2],
                    'created_date': r[3],
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
            restaurant = next(r for r in city_restaurants if r['restaurant_id'] == restaurant_id)

            # Pobierz dania restauracji
            dishes = db.fetch_all(f"""
                SELECT dish_id, dish_name, archetype, public_price,
                       secret_base_price, secret_quality, secret_spiciness,
                       secret_richness, secret_texture_score, popularity_factor
                FROM Dishes
                WHERE restaurant_id = {restaurant_id}
            """)

            if not dishes:
                continue

            # Konwertuj do dict
            dish_dicts = [
                {
                    'dish_id': d[0],
                    'dish_name': d[1],
                    'archetype': d[2],
                    'public_price': d[3],
                    'secret_base_price': d[4],
                    'secret_quality': d[5],
                    'secret_spiciness': d[6],
                    'secret_richness': d[7],
                    'secret_texture_score': d[8],
                    'popularity_factor': d[9],
                    'ingredients': []  # Simplified
                }
                for d in dishes
            ]

            # Wybierz danie (używa dish_selector.py)
            selected_dish = select_dish_from_menu(user_data, dish_dicts)

            if not selected_dish:
                continue

            # OBLICZ OCENY (używa rating_engine.py) ← NAJWAŻNIEJSZE!
            ratings = calculate_review_ratings(user_data, selected_dish, restaurant)

            # Wygeneruj komentarz
            comment = text_gen.generate_review_comment(
                rating=ratings['overall_rating'],
                dish_name=selected_dish['dish_name'],
                restaurant_name=f"Restaurant_{restaurant_id}",
                city="City",
                quality_score=selected_dish['secret_quality'],
                price_ratio=selected_dish['public_price'] / user_data['secret_price_preference_range']['mean'],
                service_score=restaurant['secret_service_quality'],
                cleanliness_score=restaurant['secret_cleanliness_score'],
                ambiance_score=restaurant['secret_ambiance_quality'] * 10
            )

            # Insert review
            review_batch.append({
                'user_id': user_id,
                'restaurant_id': restaurant_id,
                'dish_id': selected_dish['dish_id'],
                'overall_rating': ratings['overall_rating'],
                'food_score': ratings['food_score'],
                'service_score': ratings['service_score'],
                'cleanliness_score': ratings['cleanliness_score'],
                'ambiance_score': ratings['ambiance_score'],
                'value_for_money_score': ratings['value_for_money_score'],
                'comment_text': comment,
                'review_date': DateGenerator.to_sql_datetime(review_date)
            })

            total_reviews += 1

            # 30% szans na zdjęcie użytkownika
            if random.random() < 0.30:
                photo_batch.append({
                    'review_id': total_reviews,  # Approximation
                    'photo_url': photo_pools.get_user_photo_generic()
                })

            # Bulk insert co batch_size
            if len(review_batch) >= batch_size:
                db.insert_bulk("Reviews", review_batch)
                if photo_batch:
                    db.insert_bulk("Photos", photo_batch)

                logger.info(f"  ✅ Wygenerowano {total_reviews} recenzji...")
                review_batch = []
                photo_batch = []

        if (idx + 1) % 1000 == 0:
            logger.info(f"  Przetworzono {idx + 1}/{len(users)} użytkowników...")

    # Insert remaining
    if review_batch:
        db.insert_bulk("Reviews", review_batch)
    if photo_batch:
        db.insert_bulk("Photos", photo_batch)

    logger.info(f"✅ Wygenerowano {total_reviews} recenzji")

    # Moderacja (2-3%)
    _moderate_content(db)


def _moderate_content(db: DatabaseConnection):
    """Dodaje 2-3% do kolejki moderacji"""
    logger.info("🔍 Dodawanie do kolejki moderacji...")

    # 2% zdjęć do Pending_User_Photos
    # 3% komentarzy do Pending_Comments
    # 1% recenzji do Reports

    logger.info("✅ Moderacja skonfigurowana")
