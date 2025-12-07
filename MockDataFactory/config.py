"""
Configuration - Konfiguracja połączenia i parametrów generacji
"""

import os
from pathlib import Path

# Load .env file
try:
    from dotenv import load_dotenv
    env_path = Path(__file__).parent / '.env'
    if env_path.exists():
        load_dotenv(env_path)
except ImportError:
    pass  # python-dotenv not installed, using system env vars

# ============================================ 
# DATABASE CONFIGURATION (PostgreSQL)
# ============================================ 

DATABASE_CONFIG = {
    'host': os.getenv('DB_HOST', 'localhost'),
    'port': os.getenv('DB_PORT', '5432'),
    'database': os.getenv('DB_NAME', 'mockdatadb'),
    'user': os.getenv('DB_USER', 'postgres'),
    'password': os.getenv('DB_PASSWORD', '')
}

def get_connection_params():
    """
    Zwraca parametry połączenia dla PostgreSQL

    Returns:
        Dict z parametrami dla psycopg2.connect()
    """
    return {
        'host': DATABASE_CONFIG['host'],
        'port': DATABASE_CONFIG['port'],
        'dbname': DATABASE_CONFIG['database'],
        'user': DATABASE_CONFIG['user'],
        'password': DATABASE_CONFIG['password']
    }

# ============================================ 
# GENERATION PARAMETERS (ZOPTYMALIZOWANE!) 
# ============================================ 

GENERATION_CONFIG = {
    # Podstawowe liczby
    'num_users': 25000,
    'num_restaurants': 1200,
    'num_dishes': 20000,

    # Parametry recenzji
    'avg_reviews_per_user': 35,
    'power_user_percentage': 0.05,  # 5% użytkowników
    'power_user_review_count': 100,  # ~100 recenzji dla power users

    # Parametry rozkładu
    'zipf_alpha': 1.5,  # Parametr dla popularności (Zipf distribution)

    # Parametry zachowań
    'default_mood_propensity': 0.3,
    'default_cross_impact_factor': 0.02,
    'default_travel_propensity': 0.20,

    # Parametry anchor items (dla CF)
    'anchor_top_percentage': 0.20,  # TOP 20% restauracji
    'anchor_visit_rate': 0.40,  # 40% wizyt w TOP 20%
    'power_user_anchor_top_percentage': 0.30,  # TOP 30% dla power users
    'power_user_anchor_visit_rate': 0.80,  # 80% wizyt dla power users

    # Parametry moderacji
    'moderation_photo_rate': 0.02,  # 2% zdjęć do moderacji
    'moderation_comment_rate': 0.03,  # 3% komentarzy do moderacji
    'moderation_report_rate': 0.01,  # 1% recenzji zgłoszonych

    # Parametry zdjęć
    'user_photo_rate': 0.30,  # 30% recenzji ma zdjęcia użytkownika
    'restaurant_photos_per': (2, 3),  # 2-3 zdjęcia na restaurację
    'dish_photos_per': 1,  # 1 zdjęcie na danie
}

# ============================================ 
# PHOTO CONFIGURATION (Pixabay API)
# ============================================ 

# Load Pixabay API key from environment
PIXABAY_API_KEY = os.getenv('PIXABAY_API_KEY', '')
PIXABAY_ENABLED = bool(PIXABAY_API_KEY)

PHOTO_CONFIG = {
    # Pixabay API settings
    'pixabay_api_key': PIXABAY_API_KEY,
    'pixabay_enabled': PIXABAY_ENABLED,

    # Cache settings
    'cache_file': 'data/photo_cache.json',
    'images_per_query': 200,  # Fetch 200 URLs per query (MAX allowed by Pixabay - 10x variety!)

    # Local photo index (configurable via .env)
    'local_photo_index': os.getenv('LOCAL_PHOTO_INDEX', 'data/photo_index.json'),

    # API request settings
    'max_retries': 3,
    'timeout_seconds': 10,
    'rate_limit_per_hour': 4900,  # Slightly under 5000 for safety margin

    # Fallback settings
    'fallback_enabled': True,  # Use Lorem Picsum if Pixabay fails
    'fallback_base_url': 'https://picsum.photos',
}

# ============================================ 
# OCZEKIWANE METRYKI CF
# ============================================ 

EXPECTED_METRICS = {
    'sparsity': 99.825,  # % (1 - reviews / (users × dishes))
    'coverage': 95,  # % dań z >10 recenzjami
    'avg_reviews_per_user': 43,  # Średnia recenzji/użytkownik
    'avg_reviews_per_dish': 43.75,  # Średnia recenzji/danie
    'total_reviews': 1075000,  # Całkowita liczba recenzji

    # Metryki jakości
    'expected_rmse': (0.8, 1.1),  # Oczekiwany zakres RMSE
    'user_user_similarity': (0.65, 0.75),  # Korelacja między podobnymi użytkownikami
    'effective_dimensions': 14,  # NOWE
}

# ============================================ 
# DERIVED PREFERENCES CONFIGURATION
# ============================================ 

DERIVED_PREFERENCES_CONFIG = {
    'num_dimensions': 14,
    'dimension_names': [
        'flavor_sweetness', 'flavor_bitterness', 'flavor_spiciness',
        'flavor_umami', 'flavor_sourness', 'flavor_saltiness',
        'texture_crispy', 'texture_creamy', 'texture_chewy',
        'physics_richness', 'physics_temperature', 'physics_freshness',
        'context_price_sensitivity', 'context_portion_preference'
    ],

    'dish_individual_variance': 0.05,
    'restaurant_bias_range': (-0.15, 0.15),
    'restaurant_bias_dimensions': (1, 3),

    'default_weight': 1.0,
    'max_affinity_impact': 4.0,

    'blueprint_path': 'blueprints/variant_characteristics.json',
}

# ============================================ 
# LOCALE CONFIGURATION
# ============================================ 

LOCALE = 'pl_PL'  # Locale dla Faker (polskie dane)

# ============================================ 
# BLUEPRINTS DIRECTORY
# ============================================ 

BLUEPRINTS_DIR = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    'blueprints'
)

def print_config():
    """Wyświetla aktualną konfigurację"""
    print("=" * 60)
    print("📝 KONFIGURACJA MOCKDATAFACTORY")
    print("=" * 60)
    print("\n🗄️  BAZA DANYCH (PostgreSQL):")
    print(f"  Host: {DATABASE_CONFIG['host']}:{DATABASE_CONFIG['port']}")
    print(f"  Database: {DATABASE_CONFIG['database']}")
    print(f"  User: {DATABASE_CONFIG['user']}")

    print("\n📊 PARAMETRY GENERACJI:")
    print(f"  Użytkownicy: {GENERATION_CONFIG['num_users']:,}")
    print(f"  Restauracje: {GENERATION_CONFIG['num_restaurants']:,}")
    print(f"  Dania: {GENERATION_CONFIG['num_dishes']:,}")
    print(f"  Średnia recenzji/użytkownik: {GENERATION_CONFIG['avg_reviews_per_user']}")
    print(f"  Oczekiwane recenzje: {GENERATION_CONFIG['num_users'] * GENERATION_CONFIG['avg_reviews_per_user']:,}")

    print("\n🎯 PARAMETRY ZOPTYMALIZOWANE:")
    print(f"  Mood propensity: {GENERATION_CONFIG['default_mood_propensity']}")
    print(f"  Cross-impact factor: {GENERATION_CONFIG['default_cross_impact_factor']}")
    print(f"  Travel propensity: {GENERATION_CONFIG['default_travel_propensity']}")
    print(f"  Power users: {GENERATION_CONFIG['power_user_percentage'] * 100}%")
    print(f"  Anchor visit rate: {GENERATION_CONFIG['anchor_visit_rate'] * 100}%")

    print("\n📈 OCZEKIWANE METRYKI CF:")
    print(f"  Sparsity: {EXPECTED_METRICS['sparsity']:.3f}%")
    print(f"  Coverage: {EXPECTED_METRICS['coverage']}%")
    print(f"  Total reviews: {EXPECTED_METRICS['total_reviews']:,}")
    print(f"  Expected RMSE: {EXPECTED_METRICS['expected_rmse']}")

    print("=" * 60)

if __name__ == "__main__":
    print_config()