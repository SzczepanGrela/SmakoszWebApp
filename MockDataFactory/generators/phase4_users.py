"""
Phase 4 - Generowanie użytkowników (~25,000)
"""

import logging
import random
import sys
import os

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.db_connection import DatabaseConnection
from utils.statistical import sample_normal, sample_beta
from utils.date_generator import DateGenerator
from faker import Faker

logger = logging.getLogger(__name__)
fake = Faker('pl_PL')


def generate_users(db: DatabaseConnection, num_users: int = 25000):
    """
    Generuje ~25,000 użytkowników z ZOPTYMALIZOWANYMI secret attributes dla CF

    Secret Attributes (ZOPTYMALIZOWANE):
    - secret_total_review_count (25-150, z 5% power users ~100)
    - secret_enjoyed_archetypes ({"Pizza": 0.9, "Burger": 0.2})
    - secret_ingredient_preferences ({"pomidor": 0.85, "orzechy": 0.1})
    - secret_price_preference_range ({"mean": 35, "tolerance_above": 2.0})
    - secret_spice_preference (0-10)
    - secret_richness_preference (0.0-1.0)
    - secret_texture_preference (0.0-1.0)
    - secret_cleanliness_preference (city-dependent)
    - secret_preferred_ambiance ("Spokojny", "Energiczny", "Romantyczny")
    - secret_mood_propensity (0.3 ± 0.05) - ZOPTYMALIZOWANE!
    - secret_cross_impact_factor (0.02 ± 0.01) - ZOPTYMALIZOWANE!
    - travel_propensity (0.20 ± 0.05) - ZOPTYMALIZOWANE!
    """
    logger.info("👥 Generowanie użytkowników...")

    # Pobierz miasta
    cities = db.fetch_all("SELECT city_id, city_name FROM Cities")

    # Pobierz wszystkie składniki
    all_ingredients = db.fetch_all("SELECT ingredient_name FROM Ingredients")
    ingredient_names = [name for (name,) in all_ingredients]

    # Archetypy dań
    archetypes = ["Pizza", "Burger", "Sushi", "Pasta", "Ramen", "Steak", "Salad",
                  "Soup", "Dessert", "Tacos", "Kebab", "Pierogi", "Seafood", "BBQ"]

    date_gen = DateGenerator()

    user_data = []

    for i in range(num_users):
        # Podstawowe dane
        username = fake.user_name() + str(random.randint(100, 999))
        email = fake.email()
        city_id, city_name = random.choice(cities)
        join_date = date_gen.generate_user_join_date()

        # Czy power user? (5%)
        is_power_user = random.random() < 0.05

        if is_power_user:
            secret_total_review_count = random.randint(80, 120)  # ~100 średnio
            travel_propensity = sample_normal(0.25, 0.05, 0.15, 0.35)  # Wyższy
        else:
            secret_total_review_count = random.randint(25, 50)  # ~35 średnio
            travel_propensity = sample_normal(0.20, 0.05, 0.10, 0.30)  # Normalny

        # ZOPTYMALIZOWANE PARAMETRY
        secret_mood_propensity = sample_normal(0.3, 0.05, 0.20, 0.40)  # 0.3 średnio (było 0.6)
        secret_cross_impact_factor = sample_normal(0.02, 0.01, 0.01, 0.04)  # 0.02 średnio (było 0.05)

        # Preferencje archetyp (2-4 ulubione)
        enjoyed_archetypes = {}
        num_enjoyed = random.randint(2, 4)
        enjoyed_list = random.sample(archetypes, num_enjoyed)

        for archetype in archetypes:
            if archetype in enjoyed_list:
                enjoyed_archetypes[archetype] = round(random.uniform(0.7, 0.95), 2)  # Uwielbia
            else:
                enjoyed_archetypes[archetype] = round(random.uniform(0.1, 0.6), 2)  # Neutralny/nie lubi

        # Preferencje składnikowe (losowo 20-30 składników)
        ingredient_preferences = {}
        sampled_ingredients = random.sample(ingredient_names, min(30, len(ingredient_names)))

        for ingredient in sampled_ingredients:
            ingredient_preferences[ingredient] = round(random.uniform(0.0, 1.0), 2)

        # Preferencje cenowe
        mean_price = sample_normal(35.0, 10.0, 15.0, 80.0)
        price_preference_range = {
            "mean": round(mean_price, 2),
            "tolerance_above": round(random.uniform(1.5, 2.5), 2),
            "tolerance_below": round(random.uniform(0.4, 0.7), 2)
        }

        # Inne preferencje
        secret_spice_preference = round(random.uniform(0, 10), 1)
        secret_richness_preference = round(random.uniform(0.0, 1.0), 2)
        secret_texture_preference = round(random.uniform(0.0, 1.0), 2)

        # Czystość (zależy od miasta)
        cleanliness_expectations = {
            "Fine dining": round(random.uniform(8.0, 9.5), 1),
            "Casual": round(random.uniform(6.0, 8.0), 1),
            "Fast casual": round(random.uniform(5.0, 7.0), 1)
        }

        # Atmosfera
        ambiance_types = ["Spokojny", "Energiczny", "Romantyczny", "Rodzinny", "Biznesowy"]
        secret_preferred_ambiance = random.choice(ambiance_types)

        user_data.append({
            "username": username,
            "email": email,
            "city_id": city_id,
            "join_date": DateGenerator.to_sql_datetime(join_date),
            "secret_total_review_count": secret_total_review_count,
            "secret_enjoyed_archetypes": str(enjoyed_archetypes),  # JSON jako string
            "secret_ingredient_preferences": str(ingredient_preferences),
            "secret_price_preference_range": str(price_preference_range),
            "secret_spice_preference": secret_spice_preference,
            "secret_richness_preference": secret_richness_preference,
            "secret_texture_preference": secret_texture_preference,
            "secret_cleanliness_preference": str(cleanliness_expectations),
            "secret_preferred_ambiance": secret_preferred_ambiance,
            "secret_mood_propensity": round(secret_mood_propensity, 3),
            "secret_cross_impact_factor": round(secret_cross_impact_factor, 3),
            "travel_propensity": round(travel_propensity, 3)
        })

        if (i + 1) % 5000 == 0:
            logger.info(f"  Wygenerowano {i + 1}/{num_users} użytkowników...")

    db.insert_bulk("Users", user_data)
    logger.info(f"✅ Wygenerowano {len(user_data)} użytkowników")
    logger.info(f"  🌟 Power users: ~{int(num_users * 0.05)} (~5%)")

    # Przypisz Saved_Dishes (~2 na użytkownika)
    _assign_saved_dishes(db)


def _assign_saved_dishes(db: DatabaseConnection):
    """Przypisuje ulubione dania użytkownikom (~2 na użytkownika)"""
    logger.info("❤️  Przypisywanie ulubionych dań...")

    users = db.fetch_all("SELECT user_id FROM Users")
    all_dishes = db.fetch_all("SELECT dish_id FROM Dishes")

    if not all_dishes:
        logger.warning("⚠️  Brak dań - pomijam Saved_Dishes")
        return

    saved_data = []

    for (user_id,) in users:
        # Każdy user ma 0-3 ulubione dania
        num_saved = random.randint(0, 3)

        if num_saved > 0:
            sampled_dishes = random.sample(all_dishes, min(num_saved, len(all_dishes)))

            for (dish_id,) in sampled_dishes:
                saved_data.append({
                    "user_id": user_id,
                    "dish_id": dish_id
                })

    if saved_data:
        db.insert_bulk("Saved_Dishes", saved_data)
        logger.info(f"✅ Przypisano {len(saved_data)} ulubionych dań")
