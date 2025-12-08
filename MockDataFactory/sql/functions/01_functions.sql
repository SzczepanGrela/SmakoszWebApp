-- ========================================
-- SCHEMA: FUNCTIONS
-- ========================================

-- Lifecycle: Update Timestamp
CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Lifecycle: Soft Delete Propagation (Grace Period Mode)
CREATE OR REPLACE FUNCTION propagate_soft_delete_user()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.is_deleted = TRUE AND OLD.is_deleted = FALSE THEN
        -- 1. Soft Delete Content (Hide from public, but keep for restoration)
        UPDATE reviews SET is_deleted = TRUE WHERE user_id = NEW.user_id;
        
        -- 2. Security Wipe (Force Logout)
        -- User cannot log in during grace period, but data remains.
        DELETE FROM user_sessions WHERE user_id = NEW.user_id;
        DELETE FROM verification_codes WHERE user_id = NEW.user_id;
        
        -- 3. Social Data & Favorites -> PRESERVED
        -- We do NOT delete likes, follows, or saved items here.
        -- They will be removed by the 'Reaper' cron job after 30 days
        -- via ON DELETE CASCADE when the user record is physically deleted.
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Security: Update Last Login
CREATE OR REPLACE FUNCTION update_last_login_from_log()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.event_type = 'login_success' THEN
        UPDATE users SET last_login_at = NEW.created_at WHERE user_id = NEW.user_id;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Business Logic: Sync Dish Flags (INSERT)
CREATE OR REPLACE FUNCTION trg_refresh_dish_metadata_on_insert() RETURNS TRIGGER AS $$ BEGIN
    WITH affected AS (SELECT DISTINCT dish_id FROM new_table),
    aggs AS (
        SELECT ad.dish_id,
            COALESCE(jsonb_agg(jsonb_build_object('id',i.ingredient_id,'ingredient_name',i.ingredient_name,'is_allergen',i.is_allergen) ORDER BY i.ingredient_name), '[]'::jsonb) as json,
            COALESCE(BOOL_AND(i.is_vegan), TRUE) as veg,
            COALESCE(BOOL_AND(i.is_vegetarian), TRUE) as vege,
            COALESCE(BOOL_AND(i.is_gluten_free), TRUE) as gf,
            COALESCE(BOOL_AND(i.is_lactose_free), TRUE) as lf
        FROM affected ad LEFT JOIN dish_ingredients dil ON ad.dish_id=dil.dish_id LEFT JOIN ingredients i ON dil.ingredient_id=i.ingredient_id GROUP BY ad.dish_id
    ) UPDATE dishes d SET ingredients_json=a.json, is_vegan=a.veg, is_vegetarian=a.vege, is_gluten_free=a.gf, is_lactose_free=a.lf FROM aggs a WHERE d.dish_id=a.dish_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Business Logic: Sync Dish Flags (DELETE)
CREATE OR REPLACE FUNCTION trg_refresh_dish_metadata_on_delete() RETURNS TRIGGER AS $$ BEGIN
    WITH affected AS (SELECT DISTINCT dish_id FROM old_table),
    aggs AS (
        SELECT ad.dish_id,
            COALESCE(jsonb_agg(jsonb_build_object('id',i.ingredient_id,'ingredient_name',i.ingredient_name,'is_allergen',i.is_allergen) ORDER BY i.ingredient_name), '[]'::jsonb) as json,
            COALESCE(BOOL_AND(i.is_vegan), TRUE) as veg,
            COALESCE(BOOL_AND(i.is_vegetarian), TRUE) as vege,
            COALESCE(BOOL_AND(i.is_gluten_free), TRUE) as gf,
            COALESCE(BOOL_AND(i.is_lactose_free), TRUE) as lf
        FROM affected ad LEFT JOIN dish_ingredients dil ON ad.dish_id=dil.dish_id LEFT JOIN ingredients i ON dil.ingredient_id=i.ingredient_id GROUP BY ad.dish_id
    ) UPDATE dishes d SET ingredients_json=a.json, is_vegan=a.veg, is_vegetarian=a.vege, is_gluten_free=a.gf, is_lactose_free=a.lf FROM aggs a WHERE d.dish_id=a.dish_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Business Logic: Sync Dish Flags (UPDATE)
CREATE OR REPLACE FUNCTION trg_refresh_dish_metadata_on_update() RETURNS TRIGGER AS $$ BEGIN
    WITH affected AS (SELECT DISTINCT dish_id FROM new_table UNION SELECT DISTINCT dish_id FROM old_table),
    aggs AS (
        SELECT ad.dish_id,
            COALESCE(jsonb_agg(jsonb_build_object('id',i.ingredient_id,'ingredient_name',i.ingredient_name,'is_allergen',i.is_allergen) ORDER BY i.ingredient_name), '[]'::jsonb) as json,
            COALESCE(BOOL_AND(i.is_vegan), TRUE) as veg,
            COALESCE(BOOL_AND(i.is_vegetarian), TRUE) as vege,
            COALESCE(BOOL_AND(i.is_gluten_free), TRUE) as gf,
            COALESCE(BOOL_AND(i.is_lactose_free), TRUE) as lf
        FROM affected ad LEFT JOIN dish_ingredients dil ON ad.dish_id=dil.dish_id LEFT JOIN ingredients i ON dil.ingredient_id=i.ingredient_id GROUP BY ad.dish_id
    ) UPDATE dishes d SET ingredients_json=a.json, is_vegan=a.veg, is_vegetarian=a.vege, is_gluten_free=a.gf, is_lactose_free=a.lf FROM aggs a WHERE d.dish_id=a.dish_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Business Logic: Sync Dish Flags when Source Ingredient Changes
CREATE OR REPLACE FUNCTION trg_refresh_dish_metadata_on_ingredient_change() RETURNS TRIGGER AS $$
BEGIN
    -- Refresh metadata for all dishes containing the modified ingredient
    WITH affected AS (
        SELECT DISTINCT dish_id FROM dish_ingredients WHERE ingredient_id = NEW.ingredient_id
    ),
    aggs AS (
        SELECT ad.dish_id,
            COALESCE(jsonb_agg(jsonb_build_object('id',i.ingredient_id,'ingredient_name',i.ingredient_name,'is_allergen',i.is_allergen) ORDER BY i.ingredient_name), '[]'::jsonb) as json,
            COALESCE(BOOL_AND(i.is_vegan), TRUE) as veg,
            COALESCE(BOOL_AND(i.is_vegetarian), TRUE) as vege,
            COALESCE(BOOL_AND(i.is_gluten_free), TRUE) as gf,
            COALESCE(BOOL_AND(i.is_lactose_free), TRUE) as lf
        FROM affected ad 
        LEFT JOIN dish_ingredients dil ON ad.dish_id=dil.dish_id 
        LEFT JOIN ingredients i ON dil.ingredient_id=i.ingredient_id 
        GROUP BY ad.dish_id
    ) 
    UPDATE dishes d SET 
        ingredients_json=a.json, 
        is_vegan=a.veg, 
        is_vegetarian=a.vege, 
        is_gluten_free=a.gf, 
        is_lactose_free=a.lf 
    FROM aggs a 
    WHERE d.dish_id=a.dish_id;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ========================================
-- UPSERT NOTIFICATION WITH SMART PREFERENCES (v3.1)
-- ========================================
-- Push channels: configurable per-user via user_notification_settings.
-- Email channels: transactional only (always for security/B2B, never for social).

CREATE OR REPLACE FUNCTION upsert_notification(
    p_user_id INT,
    p_type VARCHAR(50),
    p_title VARCHAR(100),
    p_message VARCHAR(255),
    p_metadata JSONB DEFAULT '{}',
    p_group_key VARCHAR(200) DEFAULT NULL,
    p_severity VARCHAR(20) DEFAULT 'info',
    p_actor_id INT DEFAULT NULL
) RETURNS INT AS $$
DECLARE
    v_notification_id INT;
    v_send_email BOOLEAN := FALSE;
    v_send_push BOOLEAN := FALSE;
    v_settings RECORD;
BEGIN
    -- 1. Load User Push Preferences (if exist)
    SELECT * INTO v_settings FROM user_notification_settings WHERE user_id = p_user_id;

    -- Default fallback if no settings record found (e.g. new user)
    IF NOT FOUND THEN
        v_settings := (0, TRUE, TRUE, TRUE, NULL)::user_notification_settings;
    END IF;

    -- 2. Determine Channels based on Type & Settings
    -- Email is transactional-only (not configurable per-user).
    -- Push is configurable via user_notification_settings columns.
    CASE p_type
        WHEN 'like' THEN
            v_send_email := FALSE;
            v_send_push := COALESCE(v_settings.push_like, TRUE);

        WHEN 'follow' THEN
            v_send_email := FALSE;
            v_send_push := COALESCE(v_settings.push_follow, TRUE);

        WHEN 'review_comment' THEN
            v_send_email := FALSE;
            v_send_push := TRUE;

        WHEN 'system' THEN
            v_send_email := TRUE;
            v_send_push := COALESCE(v_settings.push_system, TRUE);

        -- CRITICAL EVENTS (Force Enabled)
        WHEN 'security_alert' THEN
            v_send_email := TRUE; -- Always send email for security
            v_send_push := TRUE;  -- Always send push for security

        WHEN 'correction_proposal' THEN
            v_send_email := TRUE; -- B2B Logic: Restaurant owner must know
            v_send_push := TRUE;

        ELSE
            -- Default for unknown types
            v_send_email := FALSE;
            v_send_push := TRUE;
    END CASE;

    -- 3. INSERT / UPSERT Logic
    IF p_group_key IS NULL THEN
        -- INSERT (Single)
        INSERT INTO notifications (
            user_id, actor_id, type, title, message,
            metadata, priority, group_key, counter, severity,
            send_email, email_status,
            send_push, push_status,
            is_read, is_deleted
        ) VALUES (
            p_user_id, p_actor_id, p_type, p_title, p_message,
            p_metadata, 1, NULL, 1, p_severity,
            v_send_email, CASE WHEN v_send_email THEN 'pending' ELSE 'none' END,
            v_send_push, CASE WHEN v_send_push THEN 'pending' ELSE 'none' END,
            FALSE, FALSE
        )
        RETURNING notification_id INTO v_notification_id;
    ELSE
        -- UPSERT (Aggregated)
        INSERT INTO notifications (
            user_id, actor_id, type, title, message,
            metadata, priority, group_key, counter, severity,
            send_email, email_status,
            send_push, push_status,
            is_read, is_deleted
        ) VALUES (
            p_user_id, p_actor_id, p_type, p_title, p_message,
            p_metadata, 1, p_group_key, 1, p_severity,
            v_send_email, CASE WHEN v_send_email THEN 'pending' ELSE 'none' END,
            v_send_push, CASE WHEN v_send_push THEN 'pending' ELSE 'none' END,
            FALSE, FALSE
        )
        ON CONFLICT (user_id, group_key)
        WHERE (is_read = FALSE AND is_deleted = FALSE AND group_key IS NOT NULL)
        DO UPDATE SET
            counter = notifications.counter + 1,
            message = EXCLUDED.message,
            actor_id = EXCLUDED.actor_id,
            metadata = notifications.metadata || EXCLUDED.metadata,
            updated_at = NOW(),
            -- Smart Resend Logic:
            -- If email/push was already sent for this group, don't send again immediately
            -- (Unless we want to spam "You have 5 likes", "You have 6 likes"...)
            -- Policy: Aggregation implies "Digest". We send only if previous failed or was reset.
            -- For simplicity here: preserve existing status (don't resend).
            send_email = notifications.send_email, -- Keep original decision
            send_push = notifications.send_push
        RETURNING notification_id INTO v_notification_id;
    END IF;

    RETURN v_notification_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION upsert_notification IS
'v3.1: Push per-user preferences (push_like/follow/system). Email transactional-only (system/security/B2B always, social never).';

-- Notifications: Owner (v2.0 with Routing)
CREATE OR REPLACE FUNCTION notify_owner_on_correction_request()
RETURNS TRIGGER AS $$
DECLARE
    v_restaurant_id INT;
    v_owner_user_id INT;
    v_restaurant_name VARCHAR(255);
    v_issue_type VARCHAR(50);
BEGIN
    v_restaurant_id := NEW.restaurant_id;
    v_issue_type := NEW.issue_type;

    -- Pobierz status restauracji (owner_id)
    SELECT owner_id, restaurant_name INTO v_owner_user_id, v_restaurant_name
    FROM restaurants WHERE restaurant_id = v_restaurant_id;

    IF NOT FOUND THEN
        RAISE WARNING 'Restaurant % not found for correction request %', v_restaurant_id, NEW.request_id;
        RETURN NULL;
    END IF;

    -- **ROUTING LOGIC**
    IF v_owner_user_id IS NOT NULL THEN
        -- **PATH A: CLAIMED -> Send to Owner**
        
        -- Individual notification (no aggregation)
        -- v3.0 Update: Removed p_send_email, handled by preferences inside upsert_notification
        PERFORM upsert_notification(
            p_user_id := v_owner_user_id,
            p_type := 'correction_proposal',
            p_title := 'Sugestia zmian danych',
            p_message := 'Użytkownik zgłosił propozycję korekty: ' || v_issue_type,
            p_metadata := json_build_object(
                'request_id', NEW.request_id,
                'target_type', 'correction_request',
                'restaurant_name', v_restaurant_name
            ),
            p_group_key := NULL,           -- No aggregation
            p_severity := 'info',
            p_actor_id := NEW.user_id
        );

    ELSE
        -- **PATH B: UNCLAIMED -> Do nothing (Passive)**
        -- We do NOT send notifications to admins.
        -- This request is stored in `data_correction_requests` and will automatically
        -- appear in the `system.admin_tickets` VIEW for all moderators/admins to pick up.
        RETURN NULL;
    END IF;

    RETURN NULL;

EXCEPTION
    WHEN OTHERS THEN
        RAISE WARNING 'Failed to send correction notification: %', SQLERRM;
        RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- Notifications: Like (BULK)
CREATE OR REPLACE FUNCTION trg_create_like_notifications_bulk() RETURNS TRIGGER AS $$ BEGIN
    INSERT INTO notifications (user_id, actor_id, type, title, message, metadata, priority, send_push, push_status, send_email, email_status)
    SELECT 
        r.user_id, 
        nt.user_id, 
        'like', 
        'Nowe polubienie', 
        'Użytkownik polubił Twoją recenzję.', 
        json_build_object(
            'review_id', nt.review_id,
            'target_type', 'review',
            'dish_name', d.dish_name,
            'restaurant_name', rest.restaurant_name
        ),
        1,
        TRUE, 'pending', FALSE, 'none' -- DEFAULT FOR BULK GENERATION (Optimization: Skip pref check for now, can be improved later)
    FROM new_table nt 
    JOIN reviews r ON nt.review_id = r.review_id 
    JOIN dishes d ON r.dish_id = d.dish_id
    JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
    WHERE r.user_id != nt.user_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Notifications: Follow (BULK)
CREATE OR REPLACE FUNCTION trg_create_follow_notifications_bulk() RETURNS TRIGGER AS $$ BEGIN
    INSERT INTO notifications (user_id, actor_id, type, title, message, metadata, priority, send_push, push_status, send_email, email_status)
    SELECT 
        nt.followed_id,
        nt.follower_id, 
        'follow', 
        'Nowy obserwujący', 
        'Użytkownik zaczął Cię obserwować.', 
        json_build_object(
            'follower_id', nt.follower_id,
            'target_type', 'user',
            'follower_name', u.username
        ),
        2,
        TRUE, 'pending', TRUE, 'pending' -- DEFAULT FOR BULK
    FROM new_table nt
    JOIN users u ON nt.follower_id = u.user_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Sync: Review Helpful Count (INSERT)
CREATE OR REPLACE FUNCTION sync_review_helpful_count_insert() RETURNS TRIGGER AS $$ BEGIN
    WITH c AS (SELECT review_id, COUNT(*) as cnt FROM new_table GROUP BY review_id)
    UPDATE reviews r SET helpful_count = r.helpful_count + c.cnt FROM c WHERE r.review_id = c.review_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Sync: Review Helpful Count (DELETE)
CREATE OR REPLACE FUNCTION sync_review_helpful_count_delete() RETURNS TRIGGER AS $$ BEGIN
    WITH c AS (SELECT review_id, COUNT(*) as cnt FROM old_table GROUP BY review_id)
    UPDATE reviews r SET helpful_count = r.helpful_count - c.cnt FROM c WHERE r.review_id = c.review_id;
    RETURN NULL; END; $$ LANGUAGE plpgsql;

-- Maintenance: Prune Notifications
CREATE OR REPLACE FUNCTION prune_notifications() RETURNS void AS $$ BEGIN
    DELETE FROM notifications WHERE notification_id IN (
        SELECT notification_id FROM (SELECT notification_id, ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY created_at DESC) as rn FROM notifications) t WHERE rn > 10
    );
END; $$ LANGUAGE plpgsql;

-- Maintenance: Update Averages & Trending Scores (Cron)
-- Calculates trending_score with time decay: (Reviews + Rating) / (1 + MonthAge)
CREATE OR REPLACE FUNCTION update_average_ratings() RETURNS void AS $$
BEGIN
    -- Update Dishes: Avg Rating
    UPDATE dishes d
    SET 
        avg_rating = sub.avg
    FROM (
        SELECT 
            dish_id, 
            AVG(dish_rating) as avg
        FROM reviews
        WHERE is_approved = TRUE AND is_deleted = FALSE
        GROUP BY dish_id
    ) sub
    WHERE d.dish_id = sub.dish_id;

    -- Update Restaurants: Avg Scores
    UPDATE restaurants r
    SET 
        avg_food_score = sub.avg
    FROM (
        SELECT 
            restaurant_id, 
            AVG(dish_rating) as avg
        FROM reviews
        WHERE is_approved = TRUE AND is_deleted = FALSE
        GROUP BY restaurant_id
    ) sub
    WHERE r.restaurant_id = sub.restaurant_id;

    -- Update sub-scores (service, cleanliness, ambiance)
    UPDATE restaurants r SET avg_service = sub.avg FROM (SELECT restaurant_id, AVG(service_rating) as avg FROM reviews WHERE is_approved = TRUE AND is_deleted = FALSE AND service_rating IS NOT NULL GROUP BY restaurant_id) sub WHERE r.restaurant_id = sub.restaurant_id;
    UPDATE restaurants r SET avg_cleanliness = sub.avg FROM (SELECT restaurant_id, AVG(cleanliness_rating) as avg FROM reviews WHERE is_approved = TRUE AND is_deleted = FALSE AND cleanliness_rating IS NOT NULL GROUP BY restaurant_id) sub WHERE r.restaurant_id = sub.restaurant_id;
    UPDATE restaurants r SET avg_ambiance = sub.avg FROM (SELECT restaurant_id, AVG(ambiance_rating) as avg FROM reviews WHERE is_approved = TRUE AND is_deleted = FALSE AND ambiance_rating IS NOT NULL GROUP BY restaurant_id) sub WHERE r.restaurant_id = sub.restaurant_id;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION update_average_ratings() IS
'Updates aggregate statistics (avg_rating, avg_food_score, etc.). Trending scores are handled by calculate_trending_scores().';

-- Maintenance: Sync Helpful Counts
CREATE OR REPLACE FUNCTION sync_helpful_counts() RETURNS void AS $$
BEGIN
    UPDATE reviews r SET helpful_count = sub.cnt FROM (SELECT review_id, COUNT(*) as cnt FROM review_likes GROUP BY review_id) sub WHERE r.review_id = sub.review_id;
END;
$$ LANGUAGE plpgsql;

-- Social Counters Sync
CREATE OR REPLACE FUNCTION update_follow_counts()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') THEN
        UPDATE users SET followers_count = followers_count + 1 WHERE user_id = NEW.followed_id;
        UPDATE users SET following_count = following_count + 1 WHERE user_id = NEW.follower_id;
        RETURN NEW;
    ELSIF (TG_OP = 'DELETE') THEN
        UPDATE users SET followers_count = followers_count - 1 WHERE user_id = OLD.followed_id;
        UPDATE users SET following_count = following_count - 1 WHERE user_id = OLD.follower_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- SEO & SLUG GENERATION
-- =============================================================================

CREATE OR REPLACE FUNCTION generate_slug(input_text TEXT) 
RETURNS TEXT AS $$
BEGIN
    RETURN LOWER(
        REGEXP_REPLACE(
            REGEXP_REPLACE(
                unaccent(TRIM(input_text)),  -- usuń polskie znaki
                '[^a-zA-Z0-9\s-]', '', 'g'   -- zostaw tylko litery, cyfry, spacje, myślniki
            ),
            '\s+', '-', 'g'                  -- zamień spacje na myślniki
        )
    );
END;
$$ LANGUAGE plpgsql IMMUTABLE;

COMMENT ON FUNCTION generate_slug IS 'Generuje URL-friendly slug z tekstu. Usuwa polskie znaki, znaki specjalne, zamienia spacje na myślniki.';

-- =============================================================================
-- TRENDING & BAYESIAN AVERAGE
-- =============================================================================

CREATE OR REPLACE FUNCTION calculate_trending_scores()
RETURNS void AS $$
DECLARE
    global_avg DECIMAL(10,4);
    min_reviews_threshold INT := 3;
    trust_parameter INT := 5;  -- 'm' w Bayesian Average
BEGIN
    -- Oblicz globalną średnią z ostatnich 7 dni
    SELECT COALESCE(AVG(dish_rating), 7.0) INTO global_avg
    FROM reviews
    WHERE created_at > NOW() - INTERVAL '7 days';
    
    -- Zresetuj trending_score dla wszystkich dań i restauracji
    UPDATE dishes SET trending_score = NULL;
    UPDATE restaurants SET trending_score = NULL;
    
    -- 1. Oblicz Bayesian Average dla DAŃ
    UPDATE dishes d SET trending_score = sub.bayesian_score
    FROM (
        SELECT 
            dish_id,
            ROUND(
                (review_count::DECIMAL / (review_count + trust_parameter)) * avg_rating +
                (trust_parameter::DECIMAL / (review_count + trust_parameter)) * global_avg,
                4
            ) as bayesian_score
        FROM (
            SELECT 
                dish_id,
                COUNT(*) as review_count,
                AVG(dish_rating) as avg_rating
            FROM reviews
            WHERE created_at > NOW() - INTERVAL '7 days'
            GROUP BY dish_id
            HAVING COUNT(*) >= min_reviews_threshold
        ) dish_stats
    ) sub
    WHERE d.dish_id = sub.dish_id;

    -- 2. Oblicz Bayesian Average dla RESTAURACJI
    UPDATE restaurants r SET trending_score = sub.bayesian_score
    FROM (
        SELECT 
            restaurant_id,
            ROUND(
                (review_count::DECIMAL / (review_count + trust_parameter)) * avg_rating +
                (trust_parameter::DECIMAL / (review_count + trust_parameter)) * global_avg,
                4
            ) as bayesian_score
        FROM (
            SELECT 
                restaurant_id,
                COUNT(*) as review_count,
                AVG(dish_rating) as avg_rating
            FROM reviews
            WHERE created_at > NOW() - INTERVAL '7 days'
            GROUP BY restaurant_id
            HAVING COUNT(*) >= min_reviews_threshold
        ) restaurant_stats
    ) sub
    WHERE r.restaurant_id = sub.restaurant_id;
    
    -- Log wykonania
    INSERT INTO system.logs (source, level, message, context)
    VALUES ('system-cron', 'INFO', 'Trending scores recalculated (Bayesian)', 
            jsonb_build_object(
                'global_avg', global_avg,
                'dishes_updated', (SELECT COUNT(*) FROM dishes WHERE trending_score IS NOT NULL),
                'restaurants_updated', (SELECT COUNT(*) FROM restaurants WHERE trending_score IS NOT NULL),
                'executed_at', NOW()
            ));
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION calculate_trending_scores IS 
'Przelicza trending_score dla dań i restauracji używając Bayesian Average.
Parametry: min_reviews=3, trust_parameter=5, window=7 dni.
Uruchamiane codziennie przez pg_cron o 04:00.';

-- =============================================================================
-- COUNTERS AUTOMATION (v6.5)
-- =============================================================================

CREATE OR REPLACE FUNCTION update_review_counts()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') THEN
        UPDATE users SET review_count = review_count + 1 WHERE user_id = NEW.user_id;
        UPDATE dishes SET review_count = review_count + 1 WHERE dish_id = NEW.dish_id;
        RETURN NEW;
    ELSIF (TG_OP = 'DELETE') THEN
        UPDATE users SET review_count = review_count - 1 WHERE user_id = OLD.user_id;
        UPDATE dishes SET review_count = review_count - 1 WHERE dish_id = OLD.dish_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION update_photo_counts()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') THEN
        IF NEW.uploaded_by IS NOT NULL THEN
            UPDATE users SET photo_count = photo_count + 1 WHERE user_id = NEW.uploaded_by;
        END IF;
        RETURN NEW;
    ELSIF (TG_OP = 'DELETE') THEN
        IF OLD.uploaded_by IS NOT NULL THEN
            UPDATE users SET photo_count = photo_count - 1 WHERE user_id = OLD.uploaded_by;
        END IF;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- USER SLUG GENERATION
-- =============================================================================

CREATE OR REPLACE FUNCTION trg_generate_user_slug()
RETURNS TRIGGER AS $$
DECLARE
    base_slug TEXT;
    final_slug TEXT;
    counter INT := 0;
BEGIN
    -- Generuj slug z username
    base_slug := generate_slug(NEW.username);
    final_slug := base_slug;
    
    -- Zapewnij unikalność
    WHILE EXISTS (SELECT 1 FROM users WHERE slug = final_slug AND user_id != COALESCE(NEW.user_id, -1)) LOOP
        counter := counter + 1;
        final_slug := base_slug || '-' || counter;
    END LOOP;
    
    NEW.slug := final_slug;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
