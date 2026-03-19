-- ========================================
-- DATA INTEGRITY & SECURITY TRIGGERS v4.3
-- ========================================
-- This file implements:
--   1. Universal audit logging for critical tables
--   2. Opening hours validation (time logic + overlap prevention)
--   3. Phone number normalization (E.164)
--   4. Review photo limit enforcement (Spam prevention)
--   5. Orphaned report cleanup
--   6. Verification code cleanup (Anti-Spam)
--   7. File Cleanup Queue (R2 Reaper)
--
-- Dependencies:
--   - sql/modules/03_audit_system.sql (audit_logs table and log_audit_event function)
--   - sql/modules/01_tables.sql (core tables)
--   - sql/modules/05_infrastructure.sql (system.files_to_delete)
-- ========================================

-- ========================================
-- PART 1: AUDIT LOGGING TRIGGERS
-- ========================================
-- Attach universal audit logging to critical tables

CREATE TRIGGER trg_audit_restaurants
AFTER INSERT OR UPDATE OR DELETE ON restaurants
FOR EACH ROW
EXECUTE FUNCTION log_audit_event('restaurant_id');

CREATE TRIGGER trg_audit_users
AFTER INSERT OR UPDATE OR DELETE ON users
FOR EACH ROW
EXECUTE FUNCTION log_audit_event('user_id');

CREATE TRIGGER trg_audit_dishes
AFTER INSERT OR UPDATE OR DELETE ON dishes
FOR EACH ROW
EXECUTE FUNCTION log_audit_event('dish_id');

CREATE TRIGGER trg_audit_reviews
AFTER INSERT OR UPDATE OR DELETE ON reviews
FOR EACH ROW
EXECUTE FUNCTION log_audit_event('review_id');

-- ========================================
-- PART 2: OPENING HOURS VALIDATION
-- ========================================

CREATE OR REPLACE FUNCTION validate_opening_hours()
RETURNS TRIGGER AS $$
DECLARE
    overlap_count INT;
BEGIN
    IF NEW.is_closed = TRUE THEN
        RETURN NEW;
    END IF;

    -- RULE A: Open != Close
    IF NEW.open_time = NEW.close_time THEN
        RAISE EXCEPTION 'Nieprawidłowy czas otwarcia: open_time nie może być równe close_time.';
    END IF;

    -- RULE B: No Overlap
    SELECT COUNT(*) INTO overlap_count
    FROM restaurant_opening_hours
    WHERE restaurant_id = NEW.restaurant_id
      AND day_of_week = NEW.day_of_week
      AND is_closed = FALSE
      AND hours_id != COALESCE(NEW.hours_id, -1)
      AND (
          (NEW.open_time < close_time AND NEW.close_time > open_time)
      );

    IF overlap_count > 0 THEN
        RAISE EXCEPTION 'Konflikt czasu otwarcia: Zakresy nachodzą na siebie.';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_validate_opening_hours
BEFORE INSERT OR UPDATE ON restaurant_opening_hours
FOR EACH ROW
EXECUTE FUNCTION validate_opening_hours();

-- ========================================
-- PART 3: PHONE NUMBER NORMALIZATION
-- ========================================

CREATE OR REPLACE FUNCTION normalize_phone_number()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.phone IS NOT NULL THEN
        -- Remove non-digits/plus
        NEW.phone := REGEXP_REPLACE(NEW.phone, '[ \-\(\)]', '', 'g');

        -- Assume +48 for 9-digit
        IF NEW.phone ~ '^[0-9]{9}$' THEN
            NEW.phone := '+48' || NEW.phone;
        END IF;

        -- Convert 00 to +
        IF NEW.phone ~ '^00' THEN
            NEW.phone := '+' || SUBSTRING(NEW.phone, 3);
        END IF;

        -- E.164 Validation
        IF NEW.phone !~ '^\+[0-9]{7,15}$' THEN
            RAISE EXCEPTION 'Nieprawidłowy format numeru telefonu. Wymagany E.164.';
        END IF;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_normalize_user_phone
BEFORE INSERT OR UPDATE OF phone ON users
FOR EACH ROW
WHEN (NEW.phone IS NOT NULL)
EXECUTE FUNCTION normalize_phone_number();

CREATE TRIGGER trg_normalize_restaurant_phone
BEFORE INSERT OR UPDATE OF phone ON restaurants
FOR EACH ROW
WHEN (NEW.phone IS NOT NULL)
EXECUTE FUNCTION normalize_phone_number();

-- ========================================
-- PART 4: REVIEW PHOTO LIMIT (Anti-Spam)
-- ========================================
-- Business Rule: Max 5 photos per review.

CREATE OR REPLACE FUNCTION check_review_photo_limit()
RETURNS TRIGGER AS $$
DECLARE
    v_photo_count INT;
BEGIN
    -- Only relevant for review photos
    IF NEW.entity_type = 'review' THEN
        SELECT COUNT(*) INTO v_photo_count
        FROM media_assets
        WHERE entity_type = 'review'
          AND entity_id = NEW.entity_id; -- entity_id is review_id here

        IF v_photo_count >= 5 THEN
            RAISE EXCEPTION 'Limit 5 zdjęć na recenzję został osiągnięty.';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_check_review_photo_limit
BEFORE INSERT ON media_assets
FOR EACH ROW
WHEN (NEW.entity_type = 'review')
EXECUTE FUNCTION check_review_photo_limit();

COMMENT ON TRIGGER trg_check_review_photo_limit ON media_assets IS
'Enforces limit of 5 photos per review. Fires before insert to media_assets.';

-- ========================================
-- PART 5: ORPHANED REPORTS CLEANUP
-- ========================================

CREATE OR REPLACE FUNCTION cleanup_related_reports()
RETURNS TRIGGER AS $$
DECLARE
    v_entity_type VARCHAR(20);
    v_entity_id INT;
BEGIN
    CASE TG_TABLE_NAME
        WHEN 'reviews' THEN
            v_entity_type := 'review';
            v_entity_id := OLD.review_id;
        WHEN 'media_assets' THEN
            v_entity_type := 'user_photo'; -- Matches 'reports' constraint
            v_entity_id := OLD.asset_id;
        WHEN 'users' THEN
            v_entity_type := 'user';
            v_entity_id := OLD.user_id;
        ELSE
            RETURN OLD;
    END CASE;

    DELETE FROM reports 
    WHERE entity_type = v_entity_type 
      AND entity_id = v_entity_id;

    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_cleanup_review_reports
AFTER DELETE ON reviews
FOR EACH ROW
EXECUTE FUNCTION cleanup_related_reports();

CREATE TRIGGER trg_cleanup_photo_reports
AFTER DELETE ON media_assets
FOR EACH ROW
EXECUTE FUNCTION cleanup_related_reports();

CREATE TRIGGER trg_cleanup_user_reports
AFTER DELETE ON users
FOR EACH ROW
EXECUTE FUNCTION cleanup_related_reports();

-- ========================================
-- PART 6: VERIFICATION CODE CLEANUP
-- ========================================
-- Business Rule: A user can have only ONE active code per type.
-- Generating a new code (e.g. resending reset email) invalidates all previous ones.

CREATE OR REPLACE FUNCTION invalidate_previous_verification_codes()
RETURNS TRIGGER AS $$
BEGIN
    -- Delete any existing codes of the same type for this user
    DELETE FROM verification_codes 
    WHERE user_id = NEW.user_id 
      AND type = NEW.type;
      
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_invalidate_previous_codes
BEFORE INSERT ON verification_codes
FOR EACH ROW
EXECUTE FUNCTION invalidate_previous_verification_codes();

COMMENT ON TRIGGER trg_invalidate_previous_codes ON verification_codes IS
'Ensures only one active verification code per user and type. 
Automatically removes old codes when a new one is requested.';

-- ========================================
-- PART 7: FILE CLEANUP QUEUE (R2 Reaper)
-- ========================================
-- Automatically queues file URLs for deletion when a media record is deleted from DB.

CREATE OR REPLACE FUNCTION queue_file_deletion()
RETURNS TRIGGER AS $$
BEGIN
    -- Only queue if URL exists and is not an external link (optional check)
    IF OLD.url IS NOT NULL THEN
        INSERT INTO system.files_to_delete (r2_key, source_entity)
        VALUES (OLD.url, TG_TABLE_NAME);
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_queue_media_deletion
AFTER DELETE ON media_assets
FOR EACH ROW
EXECUTE FUNCTION queue_file_deletion();

COMMENT ON TRIGGER trg_queue_media_deletion ON media_assets IS
'Ensures that when a media record is deleted, its file URL is added to the cleanup queue for the R2 Reaper script.';
