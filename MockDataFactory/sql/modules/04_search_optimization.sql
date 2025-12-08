-- ========================================
-- SEARCH ENGINE v5.1 (Trigram Autocomplete + Cuisine Categories)
-- ========================================
-- Optimized for: LIKE '%query%', typo tolerance, prefix matching.
-- Includes separate priorities for Cuisines > Restaurants > Dishes.

-- ========================================
-- 1. EXTENSIONS
-- ========================================
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

-- ========================================
-- 2. IMMUTABLE UNACCENT WRAPPER
-- ========================================
CREATE OR REPLACE FUNCTION f_unaccent(text)
  RETURNS text AS
$func$
SELECT public.unaccent('public.unaccent', $1)
$func$  LANGUAGE sql IMMUTABLE;

-- ========================================
-- 3. TRIGRAM INDEXES (AUTOCOMPLETE)
-- ========================================

-- Restaurant Names & Cuisine (Concatenated search support)
CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_name 
ON restaurants 
USING GIN (f_unaccent(lower(restaurant_name)) gin_trgm_ops);

-- Fast distinct cuisine lookup for autocomplete
CREATE INDEX IF NOT EXISTS idx_restaurants_cuisine_btree ON restaurants(cuisine_type) WHERE status = 'active';

-- Support for concatenated search (Name + Cuisine)
CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_full_search
ON restaurants 
USING GIN (f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) gin_trgm_ops);

-- Dish Names
CREATE INDEX IF NOT EXISTS trgm_idx_dishes_name 
ON dishes 
USING GIN (f_unaccent(lower(dish_name)) gin_trgm_ops);

-- Usernames
CREATE INDEX IF NOT EXISTS trgm_idx_users_username
ON users
USING GIN (f_unaccent(lower(username)) gin_trgm_ops);

-- Exact Match Indexes
CREATE INDEX IF NOT EXISTS idx_users_email_lower ON users (lower(email));
CREATE INDEX IF NOT EXISTS idx_users_username_lower ON users (lower(username));

-- ========================================
-- 4. AUTOCOMPLETE VIEW (The "Smart" List)
-- ========================================
-- Structure:
--   - Type: 'cuisine' (1), 'restaurant' (2), 'dish' (3)
--   - Priority: Forces Cuisines to top
--   - Search Vector: Combined text for filtering

CREATE OR REPLACE VIEW search_autocomplete AS
    -- 1. CUISINES (Categories) - Priority 1
    SELECT DISTINCT
        'cuisine'::text AS type,
        0 AS id, -- Dummy ID
        cuisine_type AS name,
        'Kategoria'::text AS subtitle,
        NULL::text AS icon,
        f_unaccent(lower(cuisine_type)) AS name_normalized,
        1 AS priority
    FROM restaurants
    WHERE status = 'active' AND cuisine_type IS NOT NULL

    UNION ALL

    -- 2. RESTAURANTS - Priority 2
    SELECT
        'restaurant'::text AS type,
        restaurant_id AS id,
        restaurant_name AS name,
        cuisine_type AS subtitle,
        image_url AS icon,
        -- Search in both Name AND Cuisine Type
        f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) AS name_normalized,
        2 AS priority
    FROM restaurants
    WHERE status = 'active'

    UNION ALL

    -- 3. DISHES - Priority 3
    SELECT
        'dish'::text AS type,
        d.dish_id AS id,
        d.dish_name AS name,
        r.restaurant_name AS subtitle,
        d.image_url AS icon,
        f_unaccent(lower(d.dish_name)) AS name_normalized,
        3 AS priority
    FROM dishes d
    JOIN restaurants r ON d.restaurant_id = r.restaurant_id
    WHERE d.is_available = TRUE AND r.status = 'active';

COMMENT ON VIEW search_autocomplete IS
'Unified autocomplete source. 
Priorities: 1=Cuisine, 2=Restaurant, 3=Dish.
Query: SELECT * FROM search_autocomplete WHERE name_normalized LIKE ''%x%'' ORDER BY priority ASC, similarity DESC';