-- ========================================
-- SCHEMA: BUSINESS LOGIC TRIGGERS (v3.1)
-- ========================================
-- This file contains advanced business logic triggers for data consistency
-- and workflow automation in the MockDataFactory PostgreSQL schema.
--
-- Features:
--   1. Enforce Single Primary Photo per entity
--   2. Sync Restaurant Avatar to Owner Account
--   3. Review Approval Workflow (Auto-Lock based on pending moderation)
-- ========================================

-- ========================================
-- FEATURE 1: ENFORCE SINGLE PRIMARY PHOTO
-- ========================================
-- Goal: Ensure only ONE photo per entity (restaurant/dish) can be is_primary = TRUE
-- Strategy: When a photo becomes primary, demote all other photos for that entity

CREATE OR REPLACE FUNCTION enforce_primary_photo()
RETURNS TRIGGER AS $$
BEGIN
    -- Only act if the new photo is being set as primary
    IF NEW.is_primary = TRUE THEN
        -- Demote all OTHER photos for the same entity to non-primary
        UPDATE media_assets
        SET is_primary = FALSE
        WHERE entity_type = NEW.entity_type
          AND entity_id = NEW.entity_id
          AND asset_id != NEW.asset_id
          AND is_primary = TRUE;  -- Optimization: only update rows that are currently primary
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger: Fire BEFORE INSERT/UPDATE to ensure constraint is enforced atomically
-- This ensures Feature 2 (avatar sync) sees the correct primary photo state
DROP TRIGGER IF EXISTS trg_enforce_primary_photo ON media_assets;
CREATE TRIGGER trg_enforce_primary_photo
BEFORE INSERT OR UPDATE OF is_primary ON media_assets
FOR EACH ROW
WHEN (NEW.is_primary = TRUE)
EXECUTE FUNCTION enforce_primary_photo();

COMMENT ON FUNCTION enforce_primary_photo() IS
'Ensures only one photo per entity (restaurant/dish) can be marked as primary.
Automatically demotes other photos when a new primary is set.';

-- ========================================
-- FEATURE 2: SYNC RESTAURANT AVATAR
-- ========================================
-- Goal: When a restaurant's primary photo changes, update the owner's user avatar
-- Note: The function sync_restaurant_owner_avatar() already exists in 01_functions.sql
-- and has a trigger trg_sync_avatar that fires AFTER INSERT OR UPDATE on photos.
--
-- This feature works in conjunction with Feature 1:
-- 1. Feature 1 (BEFORE trigger) ensures only one primary photo exists
-- 2. Existing trg_sync_avatar (AFTER trigger) syncs to user.avatar_url
--
-- The existing implementation is correct and requires no changes.
-- The trigger chain is: INSERT/UPDATE -> enforce_primary_photo() -> sync_restaurant_owner_avatar()

-- ========================================
-- FEATURE 4: SYNC PRIMARY PHOTO TO ENTITY
-- ========================================
-- Goal: When a photo is marked as primary and approved, sync it back to the entity table
-- This ensures that restaurants.image_url, dishes.image_url, and users.avatar_url
-- are automatically updated with their primary photo (including BlurHash).
--
-- Note: This replaces manual photo assignment and ensures data consistency across tables.

CREATE OR REPLACE FUNCTION sync_primary_photo_to_entity()
RETURNS TRIGGER AS $$
BEGIN
    -- Only sync if photo is both primary AND approved
    IF NEW.is_primary = TRUE AND NEW.status = 'approved' THEN

        -- Route to appropriate entity table based on entity_type
        CASE NEW.entity_type
            WHEN 'restaurant' THEN
                -- 1. Update Restaurant Profile
                UPDATE restaurants
                SET
                    image_url = NEW.url,
                    image_blurhash = NEW.blurhash
                WHERE restaurant_id = NEW.entity_id;

                -- 2. Update Restaurant Owner (Consolidated Logic)
                -- Business Rule: Owner's avatar = Restaurant's primary photo
                UPDATE users
                SET avatar_url = NEW.url
                WHERE restaurant_id = NEW.entity_id;

            WHEN 'dish' THEN
                UPDATE dishes
                SET
                    image_url = NEW.url,
                    image_blurhash = NEW.blurhash
                WHERE dish_id = NEW.entity_id;

            WHEN 'user' THEN
                UPDATE users
                SET
                    avatar_url = NEW.url,
                    avatar_blurhash = NEW.blurhash
                WHERE user_id = NEW.entity_id;

            ELSE
                -- Unknown entity_type (should never happen due to CHECK constraint)
                RAISE WARNING 'Unknown entity_type in sync_primary_photo_to_entity: %', NEW.entity_type;
        END CASE;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger: Fire AFTER INSERT/UPDATE to sync primary photos to entity tables
DROP TRIGGER IF EXISTS trg_sync_primary_photo ON media_assets;
CREATE TRIGGER trg_sync_primary_photo
AFTER INSERT OR UPDATE OF is_primary, status, url, blurhash ON media_assets
FOR EACH ROW
WHEN (NEW.is_primary = TRUE AND NEW.status = 'approved')
EXECUTE FUNCTION sync_primary_photo_to_entity();

COMMENT ON FUNCTION sync_primary_photo_to_entity() IS
'Syncs primary approved photos back to entity tables (restaurants, dishes, users).
Automatically updates image_url/avatar_url and blurhash fields when a photo
is marked as primary and approved.
Also syncs restaurant photo to the owner''s avatar.';

COMMENT ON TRIGGER trg_sync_primary_photo ON media_assets IS
'Triggers automatic synchronization of primary photos to entity tables.
Ensures restaurants.image_url, dishes.image_url, and users.avatar_url
stay in sync with the media_assets table.';

-- ========================================
-- FEATURE 5: AUTO-LOG MODERATION DECISIONS
-- ========================================

CREATE OR REPLACE FUNCTION auto_log_photo_moderation()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.status = 'approved' AND OLD.status != 'approved' THEN
        INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes)
        VALUES ('photo', NEW.asset_id, 'admin', 'approve', ARRAY[]::VARCHAR[]);
    ELSIF NEW.status = 'rejected' AND OLD.status != 'rejected' THEN
        INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note)
        VALUES ('photo', NEW.asset_id, 'admin', 'reject', ARRAY['PHOTO_OFFENSIVE']::VARCHAR[], NEW.rejection_reason);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_auto_log_photo_moderation ON media_assets;
CREATE TRIGGER trg_auto_log_photo_moderation
AFTER UPDATE OF status ON media_assets
FOR EACH ROW
WHEN ((NEW.status = 'approved' AND OLD.status != 'approved') OR (NEW.status = 'rejected' AND OLD.status != 'rejected'))
EXECUTE FUNCTION auto_log_photo_moderation();

CREATE OR REPLACE FUNCTION auto_log_content_moderation()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.content_status = 'approved' AND OLD.content_status != 'approved' THEN
        INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes)
        VALUES ('content', NEW.review_id, 'admin', 'approve', ARRAY[]::VARCHAR[]);
    ELSIF NEW.content_status = 'rejected' AND OLD.content_status != 'rejected' THEN
        INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note)
        VALUES ('content', NEW.review_id, 'admin', 'reject', ARRAY['TEXT_PROFANITY']::VARCHAR[], NEW.content_rejection_reason);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_auto_log_content_moderation ON reviews;
CREATE TRIGGER trg_auto_log_content_moderation
AFTER UPDATE OF content_status ON reviews
FOR EACH ROW
WHEN ((NEW.content_status = 'approved' AND OLD.content_status != 'approved') OR (NEW.content_status = 'rejected' AND OLD.content_status != 'rejected'))
EXECUTE FUNCTION auto_log_content_moderation();

COMMENT ON FUNCTION auto_log_photo_moderation() IS
'Automatically logs all photo moderation decisions to moderation_logs for audit trail.';

COMMENT ON FUNCTION auto_log_content_moderation() IS
'Automatically logs all content moderation decisions to moderation_logs for audit trail.';

-- ========================================
-- FEATURE 6: CASCADE RESTAURANT STATUS TO DISHES
-- ========================================
-- Goal: When a restaurant is closed (renovation, suspended, closed_permanently),
-- automatically mark all its dishes as unavailable.
-- Note: We DO NOT automatically re-enable dishes when restaurant re-opens,
-- as the menu might have changed.

CREATE OR REPLACE FUNCTION cascade_restaurant_status_to_dishes()
RETURNS TRIGGER AS $$
BEGIN
    -- Only act if status changed AND new status is NOT 'active'
    IF OLD.status = 'active' AND NEW.status IN ('renovation', 'closed_permanently', 'suspended') THEN
        
        UPDATE dishes
        SET is_available = FALSE
        WHERE restaurant_id = NEW.restaurant_id;
        
        RAISE NOTICE 'Cascaded restaurant closure to dishes for restaurant % (Status: %)', NEW.restaurant_id, NEW.status;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_cascade_restaurant_status_to_dishes ON restaurants;
CREATE TRIGGER trg_cascade_restaurant_status_to_dishes
AFTER UPDATE OF status ON restaurants
FOR EACH ROW
WHEN (OLD.status = 'active' AND NEW.status IN ('renovation', 'closed_permanently', 'suspended'))
EXECUTE FUNCTION cascade_restaurant_status_to_dishes();

COMMENT ON FUNCTION cascade_restaurant_status_to_dishes() IS
'Automatically marks dishes as unavailable when restaurant status changes to closed/suspended.
Does NOT re-enable dishes upon reactivation (safety feature).';

COMMENT ON TRIGGER trg_cascade_restaurant_status_to_dishes ON restaurants IS
'Cascades restaurant closure to dishes (is_available=FALSE).
Fires when status changes from active to non-active.';

-- ========================================
-- FEATURE 5: SEO SLUG AUTO-GENERATION
-- ========================================

-- Trigger function for dishes
CREATE OR REPLACE FUNCTION trg_generate_dish_slug()
RETURNS TRIGGER AS $$
DECLARE
    restaurant_name TEXT;
    base_slug TEXT;
    final_slug TEXT;
    counter INT := 0;
BEGIN
    SELECT r.restaurant_name INTO restaurant_name 
    FROM restaurants r WHERE r.restaurant_id = NEW.restaurant_id;
    
    base_slug := generate_slug(NEW.dish_name || ' ' || COALESCE(restaurant_name, ''));
    final_slug := base_slug;
    
    -- Zapewnij unikalność
    WHILE EXISTS (SELECT 1 FROM dishes WHERE slug = final_slug AND dish_id != COALESCE(NEW.dish_id, -1)) LOOP
        counter := counter + 1;
        final_slug := base_slug || '-' || counter;
    END LOOP;
    
    NEW.slug := final_slug;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_dishes_slug ON dishes;
CREATE TRIGGER trg_dishes_slug
    BEFORE INSERT OR UPDATE OF dish_name, restaurant_id ON dishes
    FOR EACH ROW
    EXECUTE FUNCTION trg_generate_dish_slug();

-- Trigger function for restaurants
CREATE OR REPLACE FUNCTION trg_generate_restaurant_slug()
RETURNS TRIGGER AS $$
DECLARE
    city_name TEXT;
    base_slug TEXT;
    final_slug TEXT;
    counter INT := 0;
BEGIN
    SELECT c.city_name INTO city_name 
    FROM cities c WHERE c.city_id = NEW.city_id;
    
    base_slug := generate_slug(NEW.restaurant_name || ' ' || COALESCE(city_name, ''));
    final_slug := base_slug;
    
    WHILE EXISTS (SELECT 1 FROM restaurants WHERE slug = final_slug AND restaurant_id != COALESCE(NEW.restaurant_id, -1)) LOOP
        counter := counter + 1;
        final_slug := base_slug || '-' || counter;
    END LOOP;
    
    NEW.slug := final_slug;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_restaurants_slug ON restaurants;
CREATE TRIGGER trg_restaurants_slug
    BEFORE INSERT OR UPDATE OF restaurant_name, city_id ON restaurants
    FOR EACH ROW
    EXECUTE FUNCTION trg_generate_restaurant_slug();