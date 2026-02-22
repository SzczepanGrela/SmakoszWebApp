CREATE OR REPLACE VIEW search_autocomplete AS
    SELECT DISTINCT
        'cuisine'::text AS type,
        0 AS id,
        cuisine_type AS name,
        'Kategoria'::text AS subtitle,
        NULL::text AS icon,
        f_unaccent(lower(cuisine_type)) AS name_normalized,
        1 AS priority
    FROM restaurants
    WHERE status = 'active' AND cuisine_type IS NOT NULL

    UNION ALL

    SELECT
        'restaurant'::text AS type,
        restaurant_id AS id,
        restaurant_name AS name,
        cuisine_type AS subtitle,
        image_url AS icon,
        f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) AS name_normalized,
        2 AS priority
    FROM restaurants
    WHERE status = 'active'

    UNION ALL

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
