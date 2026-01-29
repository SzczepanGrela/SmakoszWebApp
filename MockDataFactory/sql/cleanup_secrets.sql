-- ========================================
-- CLEANUP SECRETS: DEV -> PROD MIGRATION
-- ========================================
-- Ten skrypt usuwa WSZYSTKIE sekretne atrybuty z bazy danych
-- Użyj go TYLKO podczas migracji z DEV (z sekretami) do PROD (bez sekretów)
--
-- UWAGA: Ta operacja jest NIEODWRACALNA!
-- Zalecane: Zrób backup przed uruchomieniem!
--
-- Jak użyć:
--   1. Skopiuj bazę DEV do PROD (pg_dump -> pg_restore)
--   2. Uruchom ten skrypt na bazie PROD
--   3. Wszystkie sekrety zostaną usunięte
-- ========================================

\echo '========================================='
\echo 'CLEANUP SECRETS: DEV -> PROD MIGRATION'
\echo '========================================='
\echo 'Usuwanie wszystkich sekretnych atrybutów...'
\echo ''

-- ========================================
-- 1. RESTAURANTS: Usuń 6 sekretnych pól
-- ========================================
\echo '🏪 Czyszczenie tabeli RESTAURANTS...'

ALTER TABLE restaurants
    DROP COLUMN IF EXISTS secret_price_multiplier,
    DROP COLUMN IF EXISTS secret_overall_food_quality,
    DROP COLUMN IF EXISTS secret_service_quality,
    DROP COLUMN IF EXISTS secret_cleanliness_score,
    DROP COLUMN IF EXISTS secret_ambiance_type,
    DROP COLUMN IF EXISTS secret_ambiance_quality,
    DROP COLUMN IF EXISTS secret_menu_blueprint;

\echo '✅ Usunięto 6 sekretnych pól z RESTAURANTS'

-- ========================================
-- 2. DISHES: Usuń 7 sekretnych pól
-- ========================================
\echo '🍽️  Czyszczenie tabeli DISHES...'

ALTER TABLE dishes
    DROP COLUMN IF EXISTS secret_base_price,
    DROP COLUMN IF EXISTS secret_quality,
    DROP COLUMN IF EXISTS secret_spiciness,
    DROP COLUMN IF EXISTS secret_archetype,
    DROP COLUMN IF EXISTS secret_richness,
    DROP COLUMN IF EXISTS secret_texture_score,
    DROP COLUMN IF EXISTS secret_popularity_factor;

\echo '✅ Usunięto 7 sekretnych pól z DISHES'

-- ========================================
-- 3. USERS: Usuń 17 sekretnych pól
-- ========================================
\echo '👤 Czyszczenie tabeli USERS...'

ALTER TABLE users
    DROP COLUMN IF EXISTS secret_total_review_count,
    DROP COLUMN IF EXISTS secret_travel_propensity,
    DROP COLUMN IF EXISTS secret_chance_dine_random,
    DROP COLUMN IF EXISTS secret_chance_pick_random_dish,
    DROP COLUMN IF EXISTS secret_chance_to_update_rating,
    DROP COLUMN IF EXISTS secret_cross_impact_factor,
    DROP COLUMN IF EXISTS secret_mood_propensity,
    DROP COLUMN IF EXISTS secret_price_preference_range,
    DROP COLUMN IF EXISTS secret_price_tolerance_above,
    DROP COLUMN IF EXISTS secret_price_tolerance_below,
    DROP COLUMN IF EXISTS secret_enjoyed_archetypes,
    DROP COLUMN IF EXISTS secret_ingredient_preferences,
    DROP COLUMN IF EXISTS secret_cleanliness_preference,
    DROP COLUMN IF EXISTS secret_preferred_ambiance,
    DROP COLUMN IF EXISTS secret_spice_preference,
    DROP COLUMN IF EXISTS secret_richness_preference,
    DROP COLUMN IF EXISTS secret_texture_preference;

\echo '✅ Usunięto 17 sekretnych pól z USERS'

-- ========================================
-- 4. USUŃ INDEX GIN (dla JSONB)
-- ========================================
\echo '🗑️  Usuwanie indeksu JSONB...'

DROP INDEX IF EXISTS idx_users_archetypes_gin;

\echo '✅ Usunięto index JSONB'

-- ========================================
-- PODSUMOWANIE
-- ========================================
\echo ''
\echo '========================================='
\echo '✅ CLEANUP ZAKOŃCZONY POMYŚLNIE'
\echo '========================================='
\echo 'Usunięte pola:'
\echo '  • RESTAURANTS: 6 sekretnych atrybutów'
\echo '  • DISHES: 7 sekretnych atrybutów'
\echo '  • USERS: 17 sekretnych atrybutów'
\echo '  • Razem: 30 sekretnych pól'
\echo ''
\echo 'Baza danych PROD jest gotowa do użycia!'
\echo '========================================='
