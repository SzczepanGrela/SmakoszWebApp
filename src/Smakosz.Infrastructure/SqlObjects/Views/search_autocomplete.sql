DROP VIEW IF EXISTS search_autocomplete;

CREATE VIEW search_autocomplete AS
    SELECT DISTINCT
        'cuisine'::text AS type,
        ct.cuisine_type_id AS id,
        ct.display_name AS name,
        NULL::text AS slug,
        'Kategoria'::text AS subtitle,
        ct.icon,
        NULL::text AS image_blurhash,
        f_unaccent(lower(ct.display_name)) AS name_normalized,
        1 AS priority
    FROM restaurants r
    JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
    WHERE r.status = 'active'

    UNION ALL

    SELECT
        'restaurant'::text AS type,
        r.restaurant_id AS id,
        r.restaurant_name AS name,
        r.slug,
        ct.display_name AS subtitle,
        r.image_url AS icon,
        r.image_blurhash,
        f_unaccent(lower(r.restaurant_name || ' ' || COALESCE(ct.display_name, ''))) AS name_normalized,
        2 AS priority
    FROM restaurants r
    LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
    WHERE r.status = 'active'

    UNION ALL

    SELECT
        'dish'::text AS type,
        d.dish_id AS id,
        d.dish_name AS name,
        d.slug,
        r.restaurant_name AS subtitle,
        d.image_url AS icon,
        d.image_blurhash,
        f_unaccent(lower(d.dish_name)) AS name_normalized,
        3 AS priority
    FROM dishes d
    JOIN restaurants r ON d.restaurant_id = r.restaurant_id
    WHERE d.is_available = TRUE AND r.status = 'active';
