"""
Phase 4 - Generowanie użytkowników (~25,000)
"""

import logging
import random
import json
from datetime import date, timedelta, datetime
import numpy as np
from scipy.stats import beta as beta_dist

from utils.db_connection import DatabaseConnection
from utils.statistical import sample_normal, sample_beta
from utils.date_generator import DateGenerator
from utils.blueprint_loader import BlueprintLoader
from utils.faker_instance import fake

logger = logging.getLogger(__name__)

def generate_user_characteristics_vector():
    """
    Generuje 14-wymiarowy wektor preferencji użytkownika z tolerancjami.
    
    UPDATED: Widened tolerances (0.1-0.7) to create more diverse and orthogonal user profiles.
    """
    vector = {}

    # FLAVOR (6)
    vector['flavor_sweetness'] = {
        'value': round(beta_dist.rvs(2.5, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['flavor_bitterness'] = {
        'value': round(beta_dist.rvs(1.5, 3.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['flavor_spiciness'] = {
        'value': round(beta_dist.rvs(2.0, 2.5), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['flavor_umami'] = {
        'value': round(beta_dist.rvs(3.0, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['flavor_sourness'] = {
        'value': round(beta_dist.rvs(2.0, 2.5), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['flavor_saltiness'] = {
        'value': round(beta_dist.rvs(2.5, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }

    # TEXTURE (3)
    vector['texture_crispy'] = {
        'value': round(beta_dist.rvs(3.0, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['texture_creamy'] = {
        'value': round(beta_dist.rvs(2.5, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['texture_chewy'] = {
        'value': round(beta_dist.rvs(2.0, 2.5), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }

    # PHYSICS (3)
    vector['physics_richness'] = {
        'value': round(beta_dist.rvs(2.0, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['physics_temperature'] = {
        'value': round(beta_dist.rvs(2.5, 2.5), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['physics_freshness'] = {
        'value': round(beta_dist.rvs(3.5, 1.5), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }

    # CONTEXT (2)
    vector['context_price_sensitivity'] = {
        'value': round(beta_dist.rvs(2.0, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }
    vector['context_portion_preference'] = {
        'value': round(beta_dist.rvs(2.5, 2.0), 3),
        'tolerance': round(random.uniform(0.1, 0.7), 3)
    }

    return vector

def generate_full_name() -> str:
    """Generuje polskie imię i nazwisko"""
    return fake.name()

def generate_phone() -> str:
    """Generuje polski numer telefonu (format: +48 XXX XXX XXX, max 20 znaków)"""
    return f"+48 {random.randint(500, 999)} {random.randint(100, 999)} {random.randint(100, 999)}"

def generate_avatar_url(full_name: str, user_id: int) -> str:
    """
    Generuje deterministyczny avatar URL z inicjałami i rotacją kolorów

    Args:
        full_name: Imię i nazwisko
        user_id: ID użytkownika (dla determinizmu kolorów)

    Returns:
        URL avatara z UI Avatars API (max 500 znaków dla VARCHAR(500))
    """
    names = full_name.split()[:2]  # Max 2 słowa dla bezpieczeństwa

    # Rotacja 6 kolorów
    colors = ['3498db', 'e74c3c', '2ecc71', 'f39c12', '9b59b6', '1abc9c']
    color = colors[user_id % len(colors)]

    url = f"https://ui-avatars.com/api/?name={'+'.join(names)}&background={color}&color=fff&size=200"
    return url[:500]  # Safety truncation dla VARCHAR(500)

def generate_date_of_birth() -> date:
    """
    Generuje realistyczną datę urodzenia (wiek 18-70, większość 25-45)
    Używa rozkładu beta dla realistycznej dystrybucji wieku

    Returns:
        Data urodzenia
    """
    # Beta distribution dla wieku (koncentracja wokół 25-45 lat)
    age = sample_beta(2, 3, 18, 70)  # Większość 25-45, ekstremum 18-70

    # Calculate birth date
    today = date.today()
    years_ago = int(age)
    days_variation = random.randint(0, 365)  # Random day within the year

    birth_date = today - timedelta(days=years_ago * 365 + days_variation)
    return birth_date

def allocate_users_to_cities(cities, num_users, blueprints_dir="blueprints"):
    """
    Alokuje użytkowników do miast używając weighted Gaussian distribution z blueprintu

    Formula: (city_weight / total_weight) * target_users * gaussian(1.0, variance)

    Args:
        cities: Lista tupli (city_id, city_name) z bazy danych
        num_users: Docelowa liczba użytkowników (np. 25000)
        blueprints_dir: Ścieżka do katalogu blueprints

    Returns:
        Lista (city_id, city_name) dla każdego użytkownika (długość = num_users)
    """
    # Wczytaj blueprint z population_weight
    blueprint_loader = BlueprintLoader(blueprints_dir)
    city_config = blueprint_loader.load_blueprint("01_city_rules.json")["CITY_CONFIG"]

    # Przygotuj mapowanie city_name -> (city_id, weight, variance)
    city_data = {}
    for city_id, city_name in cities:
        if city_name in city_config:
            config = city_config[city_name]
            weight = config["population_weight"]["base"]
            variance = config["population_weight"]["variance"]
            city_data[city_name] = (city_id, weight, variance)
        else:
            # Fallback dla miast bez konfiguracji
            logger.warning(f"⚠️  Miasto {city_name} nie ma population_weight w blueprincie - używam domyślnego")
            city_data[city_name] = (city_id, 500, 100)

    # Oblicz sumę wag
    total_weight = sum(data[1] for data in city_data.values())

    # Alokuj użytkowników z Gaussian noise
    city_allocations = {}
    allocated_total = 0

    for city_name, (city_id, weight, variance) in city_data.items():
        # Bazowa proporcja
        expected_users = (weight / total_weight) * num_users

        # Dodaj Gaussian noise (stdev jako procent variance/weight dla stabilności)
        noise_stdev = variance / weight  # Normalize variance
        gaussian_multiplier = np.random.normal(1.0, noise_stdev * 0.3)  # 30% variance factor
        gaussian_multiplier = max(0.5, min(1.5, gaussian_multiplier))  # Clip to [0.5, 1.5]

        # Finalna alokacja
        allocated = int(expected_users * gaussian_multiplier)
        allocated = max(1, allocated)  # Minimum 1 user per city

        city_allocations[city_name] = {
            'city_id': city_id,
            'allocated': allocated,
            'expected': expected_users,
            'weight': weight
        }
        allocated_total += allocated

    # Dostosuj różnicę (jeśli suma != num_users)
    difference = num_users - allocated_total

    if difference != 0:
        # Dodaj/odejmij różnicę proporcjonalnie do wagi
        cities_sorted = sorted(city_allocations.items(), key=lambda x: x[1]['weight'], reverse=True)

        for city_name, data in cities_sorted:
            if difference == 0:
                break

            adjustment = 1 if difference > 0 else -1
            if data['allocated'] + adjustment >= 1:  # Nie pozwól zejść poniżej 1
                data['allocated'] += adjustment
                allocated_total += adjustment
                difference -= adjustment

    # Loguj dystrybucję
    logger.info(" Weighted Gaussian Distribution:")
    logger.info(f"   Total weight sum: {total_weight:,}")
    for city_name, data in sorted(city_allocations.items(), key=lambda x: x[1]['allocated'], reverse=True):
        pct = (data['allocated'] / num_users) * 100
        expected_pct = (data['expected'] / num_users) * 100
        logger.info(f"   {city_name:25s}: {data['allocated']:5,} ({pct:5.2f}%) | Expected: {data['expected']:7.1f} ({expected_pct:5.2f}%)")

    # Stwórz listę przypisań (każdy user -> city)
    user_city_assignments = []
    for city_name, data in city_allocations.items():
        for _ in range(data['allocated']):
            user_city_assignments.append((data['city_id'], city_name))

    # Losowa kolejność dla lepszej dystrybucji
    random.shuffle(user_city_assignments)

    return user_city_assignments

def generate_users(db: DatabaseConnection, num_users: int = 25000):
    """
    Generuje ~25,000 użytkowników z ZOPTYMALIZOWANYMI secret attributes dla CF

    Secret Attributes (ZOPTYMALIZOWANE):
    - secret_total_review_count (25-150, z 5% power users ~100)
    - secret_characteristics_vector (JSONB - 14 dimensions) - NOWE
    - secret_ingredient_preferences ({"pomidor": 0.85, "orzechy": 0.1})
    - secret_cleanliness_preference (city-dependent)
    - secret_preferred_ambiance ("Spokojny", "Energiczny", "Romantyczny")
    - secret_mood_propensity (0.3 ± 0.05) - ZOPTYMALIZOWANE!
    - secret_cross_impact_factor (0.02 ± 0.01) - ZOPTYMALIZOWANE!
    - travel_propensity (0.20 ± 0.05) - ZOPTYMALIZOWANE!
    """
    logger.info(" Generowanie użytkowników...")

    # Cleanup old data
    logger.info("🧹 Czyszczenie starych danych Phase 4 (users, saved_dishes)...")
    try:
        # Use execute_query directly instead of manual cursor management
        db.execute_query("TRUNCATE TABLE users RESTART IDENTITY CASCADE")
        db.execute_query("TRUNCATE TABLE saved_dishes RESTART IDENTITY CASCADE")
        # user_variant_preferences is Phase 4b, handled there (or via CASCADE from users)
        
        db.commit()
        logger.info("✅ Wyczyszczono starych użytkowników i powiązane tabele.")
        
    except Exception as e:
        logger.error(f"❌ Błąd podczas cleanup Phase 4: {e}")
        db.rollback()
        raise e

    # Pobierz miasta
    cities = db.fetch_all("SELECT city_id, city_name FROM cities")

    if not cities:
        logger.error(" Brak miast w bazie! Najpierw uruchom Phase 1 (generate_cities)")
        raise ValueError("Cannot generate users without cities in database")

    # Pobierz wszystkie składniki
    all_ingredients = db.fetch_all("SELECT ingredient_name FROM ingredients")
    ingredient_names = [name for (name,) in all_ingredients]

    # Load Archetypes for preferences
    loader = BlueprintLoader("blueprints")
    variant_blueprints = loader.load_blueprint("variant_characteristics.json")
    all_archetypes = list(variant_blueprints.keys())

    date_gen = DateGenerator()

    # RBAC: Role allocation constants
    TOTAL_ADMINS = 1       # 1 administrator (predef test account)
    TOTAL_MODERATORS = 3   # 3 moderators (predef test accounts)

    # WEIGHTED GAUSSIAN DISTRIBUTION: Alokuj użytkowników do miast
    user_city_assignments = allocate_users_to_cities(cities, num_users, blueprints_dir="blueprints")

    # UPDATE: Set num_users to the actual allocated count (handling variance)
    actual_num_users = len(user_city_assignments)
    if actual_num_users != num_users:
        logger.info(f"  Gaussian Variance applied: Target {num_users} -> Actual {actual_num_users}")
        num_users = actual_num_users

    user_data = []

    for i in range(num_users):
        # ... (RBAC logic skipped for brevity, assuming context remains) ...
        # RBAC: Role assignment logic
        if i < TOTAL_ADMINS:
            role = 'admin'
            username = f"admin_{i+1}"
            email = f"admin_{i+1}@smakosz.pl"
        elif i < TOTAL_ADMINS + TOTAL_MODERATORS:
            role = 'moderator'
            mod_num = i - TOTAL_ADMINS + 1
            username = f"moderator_{mod_num}"
            email = f"moderator_{mod_num}@smakosz.pl"
        else:
            role = 'user'
            base_username = fake.user_name()
            username = f"{base_username}{i}"
            email = f"{base_username}{i}@example.com"

        # WEIGHTED GAUSSIAN: Użyj pre-alokowanego przypisania zamiast random.choice()
        city_id, city_name = user_city_assignments[i]
        join_date = date_gen.generate_user_join_date()

        # Czy power user? (5%)
        is_power_user = random.random() < 0.05

        if is_power_user:
            secret_total_review_count = random.randint(80, 120)  # ~100 średnio
            travel_propensity = sample_normal(0.25, 0.05, 0.15, 0.35)  # Wyższy
            # Power users have 20% chance to be influencers
            is_influencer = random.random() < 0.20
        else:
            secret_total_review_count = random.randint(25, 50)  # ~35 średnio
            travel_propensity = sample_normal(0.20, 0.05, 0.10, 0.30)  # Normalny
            # Regular users have small chance (0.5%) to be external influencers (e.g. Instagram celebs)
            is_influencer = random.random() < 0.005

        # Admins/Mods are likely influencers
        if role in ['admin', 'moderator']:
            is_influencer = random.random() < 0.50

        # ZOPTYMALIZOWANE PARAMETRY
        secret_mood_propensity = sample_normal(0.3, 0.05, 0.20, 0.40)  # 0.3 średnio (było 0.6)
        secret_cross_impact_factor = sample_normal(0.02, 0.01, 0.01, 0.04)  # 0.02 średnio (było 0.05)

        # Preferencje składnikowe (losowo 20-30 składników)
        ingredient_preferences = {}
        sampled_ingredients = random.sample(ingredient_names, min(30, len(ingredient_names)))

        for ingredient in sampled_ingredients:
            ingredient_preferences[ingredient] = round(random.uniform(0.0, 1.0), 2)

        # GENERATE ENJOYED ARCHETYPES (Affinity to cuisines/types)
        # Pick 3-7 favorites (affinity 0.7-1.0)
        num_favorites = random.randint(3, 7)
        favorites = random.sample(all_archetypes, min(num_favorites, len(all_archetypes)))
        
        # Pick 1-3 dislikes (affinity 0.1-0.3)
        remaining = [a for a in all_archetypes if a not in favorites]
        num_dislikes = random.randint(1, 3)
        dislikes = random.sample(remaining, min(num_dislikes, len(remaining)))
        
        enjoyed_archetypes = {}
        for arch in favorites:
            enjoyed_archetypes[arch] = round(random.uniform(0.7, 1.0), 2)
        for arch in dislikes:
            enjoyed_archetypes[arch] = round(random.uniform(0.1, 0.3), 2)
            
        # Czystość (zależy od miasta)
        cleanliness_expectations = {
            "Fine dining": round(random.uniform(8.0, 9.5), 1),
            "Casual": round(random.uniform(6.0, 8.0), 1),
            "Fast casual": round(random.uniform(5.0, 7.0), 1)
        }

        # Atmosfera
        ambiance_types = ["Spokojny", "Energiczny", "Romantyczny", "Rodzinny", "Biznesowy"]
        secret_preferred_ambiance = random.choice(ambiance_types)

        # Generate public profile fields
        full_name = generate_full_name()
        phone = generate_phone()
        avatar_url = generate_avatar_url(full_name, i)  # Use loop index as proxy for user_id
        date_of_birth = generate_date_of_birth()

        # Determine logical flags
        is_verified = True 
        newsletter = random.random() < 0.40
        
        # Status flags
        is_banned = random.random() < 0.002

        is_active = True
        is_deleted = False
        deleted_at = None

        if random.random() < 0.05:
            is_active = False
            is_deleted = True
            days_active = random.randint(1, 300)
            deletion_date = join_date + timedelta(days=days_active)
            if deletion_date > datetime.now():
                deletion_date = datetime.now()
            deleted_at = DateGenerator.to_sql_datetime(deletion_date)

        # ========== RATING PERSONALITY (Baseline Bias) ==========
        # Generate user's inherent rating tendency (independent of actual quality)
        # This creates variance in review ratings for the same restaurant
        #
        # Distribution (optimized for ML training):
        # - 15% Critics: Harsh raters (baseline ~4.0) - "Everything is mediocre"
        # - 60% Realists: Neutral raters (baseline ~6.0) - "Fair is fair"
        # - 25% Fans: Enthusiastic raters (baseline ~8.0) - "I love everything!"

        personality_roll = random.random()

        if personality_roll < 0.15:
            # CRITIC (15%): Baseline 4.0 ± 0.5
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(4.0, 0.5)))
        elif personality_roll < 0.75:  # 0.15 + 0.60 = 0.75
            # REALIST (60%): Baseline 6.0 ± 0.5
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(6.0, 0.5)))
        else:
            # FAN (25%): Baseline 8.0 ± 0.5
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(8.0, 0.5)))

        secret_rating_baseline = round(secret_rating_baseline, 2)
        # =========================================================

        user_data.append({
            "username": username,
            "email": email,
            "email_verified": is_verified,
            "newsletter_consent": newsletter,
            "password_hash": "mock_password_hash_123",
            "role": role,
            "home_city_id": city_id,
            "account_created_at": DateGenerator.to_sql_datetime(join_date),
            "last_login_at": None, # Will be updated by reviews
            "is_active": is_active, # NEW
            "is_banned": is_banned, # NEW
            "is_deleted": is_deleted, # NEW
            "deleted_at": deleted_at, # NEW
            "full_name": full_name,
            "phone": phone,
            "avatar_url": avatar_url,
            "date_of_birth": date_of_birth.isoformat(),  # Convert date to ISO format string
            "secret_total_review_count": secret_total_review_count,
            "secret_travel_propensity": round(travel_propensity, 3),
            "secret_enjoyed_archetypes": json.dumps(enjoyed_archetypes), # NEW: Generated affinities
            "secret_chance_dine_random": 0.1,  # Default value
            "secret_chance_pick_random_dish": 0.05,  # Default value
            "secret_cross_impact_factor": round(secret_cross_impact_factor, 3),
            "secret_mood_propensity": round(secret_mood_propensity, 3),
            "secret_is_influencer": is_influencer, # NEW
            "secret_rating_baseline": secret_rating_baseline, # NEW: Rating personality (Critic/Realist/Fan)
            "secret_characteristics_vector": json.dumps(generate_user_characteristics_vector()), # NEW
            "secret_ingredient_preferences": json.dumps(ingredient_preferences),
            "secret_cleanliness_preference": json.dumps(cleanliness_expectations),
            "secret_preferred_ambiance": secret_preferred_ambiance,
        })

        if (i + 1) % 5000 == 0:
            logger.info(f"  Wygenerowano {i + 1}/{num_users} użytkowników...")

    db.insert_bulk("users", user_data)
    logger.info(f" Wygenerowano {len(user_data)} użytkowników")
    logger.info(f"   • Administratorzy: {TOTAL_ADMINS}")
    logger.info(f"   • Moderatorzy: {TOTAL_MODERATORS}")
    logger.info(f"   • Power users: ~{int(num_users * 0.05)} (~5%)")
    logger.info(f"   • Użytkownicy standardowi: {num_users - TOTAL_ADMINS - TOTAL_MODERATORS}")

    # Przypisz Saved_Dishes (~2 na użytkownika)
    _assign_saved_dishes(db)

def _assign_saved_dishes(db: DatabaseConnection):
    """
    Przypisuje ulubione dania użytkownikom.

    Pokrycie:
    - 85% użytkowników ma zapisane dania (vs poprzednie ~75%)
    - Zwykli użytkownicy: 3-10 ulubionych dań (średnio ~6.5)
    - Power users (5%): 15-30 ulubionych dań (średnio ~22.5)

    Oczekiwana liczba zapisów: ~175,000 dla 25k użytkowników
    """
    logger.info(" Przypisywanie ulubionych dań...")

    # Pobierz użytkowników z secret_total_review_count (power users mają > 80)
    users = db.fetch_all("SELECT user_id, secret_total_review_count FROM users")
    all_dishes = db.fetch_all("SELECT dish_id FROM dishes")

    if not all_dishes:
        logger.warning("⚠️  Brak dań - pomijam Saved_Dishes")
        return

    saved_data = []
    dish_list = [d[0] for d in all_dishes]

    for user_id, review_count in users:
        # Power user detection: secret_total_review_count > 80 (power users mają 80-120)
        is_power_user = review_count is not None and review_count > 80

        # 15% użytkowników nie ma żadnych zapisanych dań
        if random.random() < 0.15:
            continue

        if is_power_user:
            # Power users: 15-30 ulubionych dań
            num_saved = random.randint(15, 30)
        else:
            # Zwykli użytkownicy: 3-10 ulubionych dań
            num_saved = random.randint(3, 10)

        # Upewnij się, że nie przekraczamy liczby dostępnych dań
        num_saved = min(num_saved, len(dish_list))

        sampled_dishes = random.sample(dish_list, num_saved)

        for dish_id in sampled_dishes:
            saved_data.append({
                "user_id": user_id,
                "dish_id": dish_id
            })

    if saved_data:
        db.insert_bulk("saved_dishes", saved_data)
        users_with_saved = len(set(s["user_id"] for s in saved_data))
        avg_per_user = len(saved_data) / users_with_saved if users_with_saved > 0 else 0
        logger.info(f" Przypisano {len(saved_data):,} ulubionych dań")
        logger.info(f"   Użytkownicy z ulubionymi: {users_with_saved:,} ({100*users_with_saved/len(users):.1f}%)")
        logger.info(f"   Średnio na użytkownika: {avg_per_user:.1f}")