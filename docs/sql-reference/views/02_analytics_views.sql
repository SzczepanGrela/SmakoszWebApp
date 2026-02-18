-- ========================================
-- SCHEMA: ANALYTICS VIEWS (v5.3)
-- ========================================
-- Widoki analityczne rozpakowujace struktury JSONB do plaskich kolumn.
-- Uzywane do weryfikacji i analizy wygenerowanych danych.

-- 1. USER PREFERENCES FLAT: Rozpakowane wektory preferencji uzytkownikow
CREATE OR REPLACE VIEW vw_user_preferences_flat AS
SELECT
    u.user_id,
    u.username,

    -- FLAVOR DIMENSIONS (6)
    (u.secret_characteristics_vector->'flavor_sweetness'->>'value')::numeric AS flavor_sweetness_target,
    (u.secret_characteristics_vector->'flavor_sweetness'->>'tolerance')::numeric AS flavor_sweetness_tol,

    (u.secret_characteristics_vector->'flavor_bitterness'->>'value')::numeric AS flavor_bitterness_target,
    (u.secret_characteristics_vector->'flavor_bitterness'->>'tolerance')::numeric AS flavor_bitterness_tol,

    (u.secret_characteristics_vector->'flavor_spiciness'->>'value')::numeric AS flavor_spiciness_target,
    (u.secret_characteristics_vector->'flavor_spiciness'->>'tolerance')::numeric AS flavor_spiciness_tol,

    (u.secret_characteristics_vector->'flavor_umami'->>'value')::numeric AS flavor_umami_target,
    (u.secret_characteristics_vector->'flavor_umami'->>'tolerance')::numeric AS flavor_umami_tol,

    (u.secret_characteristics_vector->'flavor_sourness'->>'value')::numeric AS flavor_sourness_target,
    (u.secret_characteristics_vector->'flavor_sourness'->>'tolerance')::numeric AS flavor_sourness_tol,

    (u.secret_characteristics_vector->'flavor_saltiness'->>'value')::numeric AS flavor_saltiness_target,
    (u.secret_characteristics_vector->'flavor_saltiness'->>'tolerance')::numeric AS flavor_saltiness_tol,

    -- TEXTURE DIMENSIONS (3)
    (u.secret_characteristics_vector->'texture_crispy'->>'value')::numeric AS texture_crispy_target,
    (u.secret_characteristics_vector->'texture_crispy'->>'tolerance')::numeric AS texture_crispy_tol,

    (u.secret_characteristics_vector->'texture_creamy'->>'value')::numeric AS texture_creamy_target,
    (u.secret_characteristics_vector->'texture_creamy'->>'tolerance')::numeric AS texture_creamy_tol,

    (u.secret_characteristics_vector->'texture_chewy'->>'value')::numeric AS texture_chewy_target,
    (u.secret_characteristics_vector->'texture_chewy'->>'tolerance')::numeric AS texture_chewy_tol,

    -- PHYSICS DIMENSIONS (3)
    (u.secret_characteristics_vector->'physics_richness'->>'value')::numeric AS physics_richness_target,
    (u.secret_characteristics_vector->'physics_richness'->>'tolerance')::numeric AS physics_richness_tol,

    (u.secret_characteristics_vector->'physics_temperature'->>'value')::numeric AS physics_temperature_target,
    (u.secret_characteristics_vector->'physics_temperature'->>'tolerance')::numeric AS physics_temperature_tol,

    (u.secret_characteristics_vector->'physics_freshness'->>'value')::numeric AS physics_freshness_target,
    (u.secret_characteristics_vector->'physics_freshness'->>'tolerance')::numeric AS physics_freshness_tol,

    -- CONTEXT DIMENSIONS (2)
    (u.secret_characteristics_vector->'context_price_sensitivity'->>'value')::numeric AS context_price_sensitivity_target,
    (u.secret_characteristics_vector->'context_price_sensitivity'->>'tolerance')::numeric AS context_price_sensitivity_tol,

    (u.secret_characteristics_vector->'context_portion_preference'->>'value')::numeric AS context_portion_preference_target,
    (u.secret_characteristics_vector->'context_portion_preference'->>'tolerance')::numeric AS context_portion_preference_tol

FROM users u
WHERE u.secret_characteristics_vector IS NOT NULL;

COMMENT ON VIEW vw_user_preferences_flat IS
'Widok analityczny: rozpakowane preferencje uzytkownikow (JSONB -> kolumny).';

-- 2. RESTAURANT QUALITY SUMMARY: Oceny jakosci restauracji na skali 1-10
CREATE OR REPLACE VIEW vw_restaurant_quality_summary AS
SELECT
    r.restaurant_id,
    r.restaurant_name,
    r.cuisine_type,
    r.price_level,
    r.city_id,

    -- Secret quality attributes (0.0-1.0 scale, converted to 1-10)
    ROUND((r.secret_overall_food_quality * 10)::numeric, 2) AS food_quality,
    ROUND((r.secret_service_quality * 10)::numeric, 2) AS service_quality,
    ROUND((r.secret_cleanliness_score * 10)::numeric, 2) AS cleanliness,
    ROUND((r.secret_ambiance_quality * 10)::numeric, 2) AS ambiance,

    ROUND(r.secret_price_multiplier::numeric, 2) AS price_multiplier,

    -- Overall quality score
    ROUND((((r.secret_overall_food_quality + r.secret_service_quality + r.secret_cleanliness_score + r.secret_ambiance_quality) / 4.0) * 10)::numeric, 2) AS overall_quality_score

FROM restaurants r;

COMMENT ON VIEW vw_restaurant_quality_summary IS
'Widok analityczny: podsumowanie jakosci restauracji na skali 1-10.';

-- 3. DISH CHARACTERISTICS FLAT: Rozpakowane wektory charakterystyk dan
CREATE OR REPLACE VIEW vw_dish_characteristics_flat AS
SELECT
    d.dish_id,
    d.dish_name,
    d.restaurant_id,

    -- FLAVOR DIMENSIONS (6)
    (d.secret_characteristics_vector->>'flavor_sweetness')::numeric AS flavor_sweetness,
    (d.secret_characteristics_vector->>'flavor_bitterness')::numeric AS flavor_bitterness,
    (d.secret_characteristics_vector->>'flavor_spiciness')::numeric AS flavor_spiciness,
    (d.secret_characteristics_vector->>'flavor_umami')::numeric AS flavor_umami,
    (d.secret_characteristics_vector->>'flavor_sourness')::numeric AS flavor_sourness,
    (d.secret_characteristics_vector->>'flavor_saltiness')::numeric AS flavor_saltiness,

    -- TEXTURE DIMENSIONS (3)
    (d.secret_characteristics_vector->>'texture_crispy')::numeric AS texture_crispy,
    (d.secret_characteristics_vector->>'texture_creamy')::numeric AS texture_creamy,
    (d.secret_characteristics_vector->>'texture_chewy')::numeric AS texture_chewy,

    -- PHYSICS DIMENSIONS (3)
    (d.secret_characteristics_vector->>'physics_richness')::numeric AS physics_richness,
    (d.secret_characteristics_vector->>'physics_temperature')::numeric AS physics_temperature,
    (d.secret_characteristics_vector->>'physics_freshness')::numeric AS physics_freshness,

    -- CONTEXT DIMENSIONS (2)
    (d.secret_characteristics_vector->>'context_price_sensitivity')::numeric AS context_price_sensitivity,
    (d.secret_characteristics_vector->>'context_portion_preference')::numeric AS context_portion_preference,

    -- Other useful attributes
    ROUND(d.secret_quality::numeric, 2) AS dish_quality,
    ROUND(d.price::numeric, 2) AS price

FROM dishes d
WHERE d.secret_characteristics_vector IS NOT NULL;

COMMENT ON VIEW vw_dish_characteristics_flat IS
'Widok analityczny: rozpakowane charakterystyki dan (JSONB -> kolumny).';

-- 4. REVIEW QUALITY ANALYSIS: Analiza ocen vs oczekiwana jakosc
CREATE OR REPLACE VIEW vw_review_quality_analysis AS
SELECT
    rv.review_id,
    rv.user_id,
    rv.dish_id,
    rv.restaurant_id,

    -- Restaurant context
    r.restaurant_name,
    r.cuisine_type,
    r.price_level,

    -- Review scores
    rv.dish_rating,
    rv.service_rating,
    rv.cleanliness_rating,
    rv.ambiance_rating,
    ROUND((rv.dish_rating + rv.service_rating + rv.cleanliness_rating + rv.ambiance_rating) / 4.0, 2) as implied_overall,

    -- Expected quality (from secret fields)
    ROUND((r.secret_overall_food_quality * 10)::numeric, 2) AS restaurant_expected_food,
    ROUND((r.secret_service_quality * 10)::numeric, 2) AS restaurant_expected_service,

    -- Deltas (actual vs expected)
    ROUND((rv.dish_rating - r.secret_overall_food_quality * 10)::numeric, 2) AS food_delta,
    ROUND((rv.service_rating - r.secret_service_quality * 10)::numeric, 2) AS service_delta,

    rv.created_at AS review_date

FROM reviews rv
JOIN restaurants r ON rv.restaurant_id = r.restaurant_id;

COMMENT ON VIEW vw_review_quality_analysis IS
'Widok analityczny: analiza review z kontekstem restauracji (actual vs expected quality).';
