"""
Configuration - Konfiguracja połączenia i parametrów generacji
"""

import os

# ============================================
# DATABASE CONFIGURATION
# ============================================

DATABASE_CONFIG = {
    'server': os.getenv('DB_SERVER', 'localhost'),
    'database': os.getenv('DB_NAME', 'MockDataDB'),
    'driver': os.getenv('DB_DRIVER', 'ODBC Driver 17 for SQL Server'),
    'trusted_connection': os.getenv('DB_TRUSTED', 'yes')
}


def get_connection_string():
    """
    Zwraca ODBC connection string dla SQL Server

    Returns:
        Connection string dla pyodbc
    """
    return (
        f"Driver={{{DATABASE_CONFIG['driver']}}};"
        f"Server={DATABASE_CONFIG['server']};"
        f"Database={DATABASE_CONFIG['database']};"
        f"Trusted_Connection={DATABASE_CONFIG['trusted_connection']};"
    )


# ============================================
# GENERATION PARAMETERS (ZOPTYMALIZOWANE!)
# ============================================

GENERATION_CONFIG = {
    # Podstawowe liczby
    'num_users': 25000,  # +108% vs pierwotne 12000 (ZOPTYMALIZOWANE!)
    'num_restaurants': 1200,
    'num_dishes': 20000,

    # Parametry recenzji
    'avg_reviews_per_user': 35,  # +25% vs pierwotne 28 (ZOPTYMALIZOWANE!)
    'power_user_percentage': 0.05,  # 5% użytkowników
    'power_user_review_count': 100,  # ~100 recenzji dla power users

    # Parametry rozkładu
    'zipf_alpha': 1.5,  # Parametr dla popularności (Zipf distribution)

    # Parametry zachowań (ZOPTYMALIZOWANE!)
    'default_mood_propensity': 0.3,  # 0.3 średnio (było 0.6)
    'default_cross_impact_factor': 0.02,  # 0.02 średnio (było 0.05)
    'default_travel_propensity': 0.20,  # 0.20 średnio (było 0.15)

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
# OCZEKIWANE METRYKI CF
# ============================================

EXPECTED_METRICS = {
    'sparsity': 99.825,  # % (1 - reviews / (users × dishes))
    'coverage': 95,  # % dań z >10 recenzjami
    'avg_reviews_per_user': 35,  # Średnia recenzji/użytkownik
    'avg_reviews_per_dish': 43.75,  # Średnia recenzji/danie
    'total_reviews': 875000,  # Całkowita liczba recenzji

    # Metryki jakości
    'expected_rmse': (0.9, 1.2),  # Oczekiwany zakres RMSE
    'user_user_similarity': (0.6, 0.7),  # Korelacja między podobnymi użytkownikami
}


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
    print("\n🗄️  BAZA DANYCH:")
    print(f"  Server: {DATABASE_CONFIG['server']}")
    print(f"  Database: {DATABASE_CONFIG['database']}")
    print(f"  Driver: {DATABASE_CONFIG['driver']}")

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
