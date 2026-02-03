-- ========================================
-- SCHEMA: VIEWS (v5.2 + NCF Training Data)
-- ========================================

-- 1. ACTIVE DISHES: UI-Ready listing with placeholders and city filtering
CREATE OR REPLACE VIEW vw_active_dishes AS
SELECT
    d.dish_id,
    d.dish_name,
    d.price,
    d.description,
    d.image_url,
    d.image_blurhash AS dish_blurhash,
    (
        SELECT STRING_AGG(ms.section_name, ', ')
        FROM dish_section_assignments dsa
        JOIN menu_sections ms ON dsa.section_id = ms.section_id
        WHERE dsa.dish_id = d.dish_id
    ) AS menu_sections,
    r.restaurant_id,
    r.restaurant_name,
    r.image_blurhash AS restaurant_blurhash,
    r.cuisine_type,
    r.city_id,
    c.city_name
FROM dishes d
JOIN restaurants r ON d.restaurant_id = r.restaurant_id
JOIN cities c ON r.city_id = c.city_id
WHERE d.is_available = TRUE AND r.status = 'active';

-- 2. USER STATS: Profile summary with reputation metrics (No secret_ fields)
CREATE OR REPLACE VIEW vw_user_stats AS
SELECT
    u.user_id,
    u.username,
    u.role,
    u.followers_count,
    u.following_count,
    COUNT(DISTINCT r.review_id) AS total_reviews,
    (
        SELECT COUNT(*) 
        FROM review_likes rl 
        JOIN reviews r2 ON rl.review_id = r2.review_id 
        WHERE r2.user_id = u.user_id
    ) AS total_likes_received,
    AVG(r.dish_rating::DOUBLE PRECISION) AS avg_rating_given
FROM users u
LEFT JOIN reviews r ON u.user_id = r.user_id
GROUP BY u.user_id, u.username, u.role, u.followers_count, u.following_count;

-- 3. UNIFIED ADMIN INBOX: Contextualized ticket list for Dashboard
CREATE OR REPLACE VIEW system.admin_tickets_view AS
SELECT
    t.ticket_id,
    t.ticket_type,
    t.reference_id,
    t.status,
    t.priority,
    t.assigned_admin_id,
    t.locked_at,
    t.created_at,
    -- Contextual description based on type
    CASE 
        WHEN t.ticket_type = 'review_content' THEN (SELECT LEFT(content, 50) FROM reviews WHERE review_id = t.reference_id)
        WHEN t.ticket_type = 'media_asset' THEN (SELECT url FROM media_assets WHERE asset_id = t.reference_id)
        WHEN t.ticket_type = 'report' THEN (SELECT entity_type || ': ' || LEFT(description, 50) FROM reports WHERE report_id = t.reference_id)
        WHEN t.ticket_type = 'restaurant_edit' THEN (SELECT 'Edit: ' || restaurant_name FROM restaurants r JOIN restaurant_edit_requests rer ON r.restaurant_id = rer.restaurant_id WHERE rer.request_id = t.reference_id)
        ELSE 'Brak opisu kontekstowego'
    END AS context_preview
FROM system.tickets t;

-- 4. MODERATION QUEUE STATS: High-performance stats from tickets table
CREATE OR REPLACE VIEW moderation_queue_stats AS
SELECT
    ticket_type AS queue_name,
    COUNT(*) AS total_items,
    COUNT(*) FILTER (WHERE status = 'open') AS open_count,
    COUNT(*) FILTER (WHERE status = 'in_progress') AS in_progress_count,
    COUNT(*) FILTER (WHERE status = 'resolved') AS resolved_count
FROM system.tickets
GROUP BY ticket_type;

COMMENT ON VIEW moderation_queue_stats IS
'Statystyki kolejek moderacji pobierane z tabeli ticketów.';

-- 5. RESTAURANT MENU: Detailed menu with sections (M:N relationship)
CREATE OR REPLACE VIEW vw_restaurant_menu AS
SELECT 
    r.restaurant_id,
    r.slug as restaurant_slug,
    ms.section_id,
    ms.section_name,
    ms.display_order as section_order,
    d.dish_id,
    d.public_id as dish_public_id,
    d.slug as dish_slug,
    d.dish_name,
    d.price,
    d.description,
    d.avg_rating,
    d.is_available,
    d.image_url,
    dsa.display_order as dish_order_in_section
FROM restaurants r
JOIN menu_sections ms ON ms.restaurant_id = r.restaurant_id
JOIN dish_section_assignments dsa ON dsa.section_id = ms.section_id
JOIN dishes d ON d.dish_id = dsa.dish_id
WHERE d.is_available = TRUE
ORDER BY r.restaurant_id, ms.display_order, dsa.display_order;

COMMENT ON VIEW vw_restaurant_menu IS 'Widok menu restauracji z sekcjami (relacja M:N). Używaj do wyświetlania menu pogrupowanego w sekcje.';

-- 6. NCF TRAINING DATA: Export for Neural Collaborative Filtering
-- No filtering applied - data scientists handle preprocessing
CREATE OR REPLACE VIEW vw_ncf_training_data AS
SELECT 
    user_id,
    dish_id,
    dish_rating AS rating,
    created_at
FROM reviews
WHERE is_deleted = FALSE;

COMMENT ON VIEW vw_ncf_training_data IS 
'Dane treningowe dla Neural Collaborative Filtering (NCF).
Eksport: SELECT user_id, dish_id, rating FROM vw_ncf_training_data;
Filtrowanie cold-start users odbywa się po stronie frontendu/ML pipeline.';

-- 7. RESTAURANT STATS: Summary with rating metrics
CREATE OR REPLACE VIEW vw_restaurant_stats AS
SELECT
    r.restaurant_id,
    r.public_id,
    r.restaurant_name,
    r.slug,
    r.cuisine_type,
    r.status,
    r.trending_score,
    r.avg_food_score,
    r.avg_service,
    r.avg_cleanliness,
    r.avg_ambiance,
    COUNT(DISTINCT d.dish_id) AS dish_count,
    COUNT(DISTINCT rv.review_id) AS review_count,
    c.city_name
FROM restaurants r
LEFT JOIN dishes d ON r.restaurant_id = d.restaurant_id AND d.is_available = TRUE
LEFT JOIN reviews rv ON r.restaurant_id = rv.restaurant_id AND rv.is_deleted = FALSE
JOIN cities c ON r.city_id = c.city_id
GROUP BY r.restaurant_id, r.public_id, r.restaurant_name, r.slug, r.cuisine_type, 
         r.status, r.trending_score, r.avg_food_score, r.avg_service, 
         r.avg_cleanliness, r.avg_ambiance, c.city_name;

COMMENT ON VIEW vw_restaurant_stats IS 'Statystyki restauracji dla widoku listy/karty.';
