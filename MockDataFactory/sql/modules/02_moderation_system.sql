-- ========================================
-- SCHEMA: MODERATION SYSTEM v4.6 (Unified & Fixed)
-- ========================================
-- Unified moderation queues replacing separate AI/Admin tables.
--
-- Features:
--   1. Unified Photo Moderation (Official & User)
--   2. Unified Comment Moderation (AI + Admin in one table)
--   3. Text Content Moderation (Restaurant Edits)
--   4. Ingredient Suggestions
-- ========================================

-- ========================================
-- PART 1: MEDIA MODERATION (Handled via media_assets.status)
-- ========================================
-- Note: Photo moderation is now handled directly through the media_assets table
-- using the status column ('pending', 'approved', 'rejected').
-- The pending_official_photos table has been removed in favor of this simpler approach.
--
-- For user-uploaded photos that need moderation, use the moderation_photos table (PART 4).
-- ========================================

-- ========================================
-- PART 2: RESTAURANT EDIT REQUESTS
-- ========================================

CREATE TABLE IF NOT EXISTS restaurant_edit_requests (
    request_id SERIAL PRIMARY KEY,
    restaurant_id INT NOT NULL REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    change_type VARCHAR(50) NOT NULL DEFAULT 'general',
    change_scope VARCHAR(50) NOT NULL DEFAULT 'restaurant',
    target_entity_id INT,
    payload JSONB NOT NULL DEFAULT '{}',
    new_name VARCHAR(255),
    new_description VARCHAR(1000),
    new_address VARCHAR(200),
    new_cuisine_type VARCHAR(100),
    new_phone VARCHAR(20),
    new_website VARCHAR(200),
    new_image_url VARCHAR(500),
    new_image_blurhash VARCHAR(50),
    ai_verdict VARCHAR(20),
    ai_confidence DECIMAL(5,4),
    ai_model_version VARCHAR(50),
    ai_processed_at TIMESTAMPTZ,
    auto_approved BOOLEAN DEFAULT FALSE,
    auto_approve_reason VARCHAR(255),
    reviewed_by INT REFERENCES users(user_id),
    reviewed_at TIMESTAMPTZ,
    rejection_reason TEXT,
    admin_note VARCHAR(500),
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    resolved_at TIMESTAMPTZ NULL,
    resolved_by_admin_id INT NULL REFERENCES users(user_id) ON DELETE SET NULL,
    version INT DEFAULT 1, -- Optimistic Locking
    CONSTRAINT chk_edit_request_status CHECK (status IN ('pending', 'approved', 'rejected'))
);

COMMENT ON COLUMN restaurant_edit_requests.change_type IS 'Typ zmiany: info_update, hours_update, dish_create, dish_update, dish_delete, section_create, section_update, section_delete, photo_upload';
COMMENT ON COLUMN restaurant_edit_requests.change_scope IS 'Zakres: restaurant, dish, section, photo';
COMMENT ON COLUMN restaurant_edit_requests.target_entity_id IS 'ID encji której dotyczy zmiana (dish_id, section_id). NULL dla zmian restauracji.';
COMMENT ON COLUMN restaurant_edit_requests.payload IS 'JSON z danymi zmiany, np. {"name": "Nowa nazwa", "price": 45.00}';
COMMENT ON COLUMN restaurant_edit_requests.ai_verdict IS 'Wynik AI: approved, rejected, needs_review';
COMMENT ON COLUMN restaurant_edit_requests.ai_confidence IS 'Pewność AI (0.0000-1.0000)';
COMMENT ON COLUMN restaurant_edit_requests.auto_approved IS 'Czy zmiana została automatycznie zatwierdzona (low-risk changes)';

COMMENT ON TABLE restaurant_edit_requests IS 
'Staging table dla zmian B2B. Reguły auto-approve:
- hours_update: zawsze auto-approve (walidacja formatu)
- info_update (tylko description): AI check, auto-approve jeśli confidence > 0.95
- dish_update (tylko price, zmiana ±20%): auto-approve
- Wszystkie inne: wymagają review przez moderatora/admina';

CREATE INDEX IF NOT EXISTS idx_edit_requests_status ON restaurant_edit_requests(status) WHERE status = 'pending';
CREATE INDEX idx_edit_requests_restaurant ON restaurant_edit_requests(restaurant_id, created_at DESC);
CREATE INDEX idx_edit_requests_type ON restaurant_edit_requests(change_type, status);

CREATE INDEX IF NOT EXISTS idx_restaurant_edit_requests_status_old
ON restaurant_edit_requests(status, created_at)
WHERE status = 'pending';

-- ========================================
-- PART 3: INGREDIENT SUGGESTIONS
-- ========================================

CREATE TABLE IF NOT EXISTS ingredient_suggestions (
    suggestion_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE SET NULL,
    restaurant_id INT NOT NULL REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    suggested_name VARCHAR(100) NOT NULL,
    icon_url VARCHAR(500) DEFAULT NULL,
    icon_blurhash VARCHAR(50) DEFAULT NULL,
    is_allergen BOOLEAN DEFAULT FALSE,
    is_vegetarian BOOLEAN DEFAULT TRUE,
    is_vegan BOOLEAN DEFAULT TRUE,
    is_gluten_free BOOLEAN DEFAULT TRUE,
    is_lactose_free BOOLEAN DEFAULT TRUE,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    admin_note TEXT,
    reviewed_by_admin_id INT REFERENCES users(user_id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    reviewed_at TIMESTAMPTZ NULL,
    merged_ingredient_id INT REFERENCES ingredients(ingredient_id) ON DELETE SET NULL,
    version INT DEFAULT 1, -- Optimistic Locking
    CONSTRAINT chk_suggestion_status CHECK (status IN ('pending', 'approved', 'rejected', 'merged'))
);

CREATE INDEX IF NOT EXISTS idx_ingredient_suggestions_status
ON ingredient_suggestions(status, created_at)
WHERE status = 'pending';

-- ========================================
-- PART 4: STATE MACHINE MODERATION SYSTEM
-- ========================================

-- Rejection Reasons Lookup Table
CREATE TABLE IF NOT EXISTS rejection_reasons (
    reason_code VARCHAR(50) PRIMARY KEY,
    category VARCHAR(20) NOT NULL CHECK (category IN ('photo', 'text')),
    admin_label VARCHAR(100) NOT NULL,
    user_message_template TEXT NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_admin_label UNIQUE (admin_label)
);

CREATE INDEX IF NOT EXISTS idx_rejection_reasons_category
ON rejection_reasons(category, is_active) WHERE is_active = TRUE;

COMMENT ON TABLE rejection_reasons IS
'Centralized rejection reason codes for moderation.
Admin-facing labels and user-facing message templates for consistent rejection handling.';

-- NOTE: Seed data loaded from blueprints/rejection_reasons.json via rebuild_db.py

-- NOTE: moderation_logs table moved to 'system' schema (module 05)

-- ========================================
-- PART 5: STATE MACHINE MODERATION FUNCTIONS
-- ========================================

-- Functions for Official Photos/Edits/Ingredients (unchanged)
CREATE OR REPLACE FUNCTION approve_restaurant_edit(target_request_id INT) RETURNS void AS $$
DECLARE req RECORD;
BEGIN
    SELECT * INTO req FROM restaurant_edit_requests WHERE request_id = target_request_id AND status = 'pending';
    IF NOT FOUND THEN RAISE EXCEPTION 'Request % not found', target_request_id; END IF;
    UPDATE restaurants SET
        restaurant_name = COALESCE(req.new_name, restaurant_name),
        description = COALESCE(req.new_description, description),
        address = COALESCE(req.new_address, address),
        cuisine_type = COALESCE(req.new_cuisine_type, cuisine_type),
        phone = COALESCE(req.new_phone, phone),
        website = COALESCE(req.new_website, website),
        updated_at = NOW()
    WHERE restaurant_id = req.restaurant_id;
    UPDATE restaurant_edit_requests SET status = 'approved', resolved_at = NOW() WHERE request_id = target_request_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION reject_restaurant_edit(target_request_id INT, reason VARCHAR) RETURNS void AS $$
BEGIN
    UPDATE restaurant_edit_requests SET status = 'rejected', admin_note = reason, resolved_at = NOW()
    WHERE request_id = target_request_id AND status = 'pending';
END;
$$ LANGUAGE plpgsql;

-- Media Asset Moderation Functions (State Machine with Logging)
CREATE OR REPLACE FUNCTION approve_media_asset(
    p_asset_id BIGINT,
    p_admin_id INT DEFAULT NULL,
    p_admin_note TEXT DEFAULT NULL
) RETURNS void AS $$
BEGIN
    -- Approve the media asset
    UPDATE media_assets SET status = 'approved'
    WHERE asset_id = p_asset_id AND status = 'pending';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Media asset % not found or not pending', p_asset_id;
    END IF;

    -- Log the approval
    INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note, processed_by)
    VALUES ('photo', p_asset_id,
            CASE WHEN p_admin_id IS NOT NULL THEN 'admin' ELSE 'system' END,
            'approve', ARRAY[]::VARCHAR[], p_admin_note, p_admin_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION reject_media_asset(
    p_asset_id BIGINT,
    p_reason_codes VARCHAR[],
    p_admin_id INT DEFAULT NULL,
    p_admin_note TEXT DEFAULT NULL
) RETURNS void AS $$
DECLARE user_message TEXT;
BEGIN
    -- Build user-facing message from reason codes
    SELECT STRING_AGG(user_message_template, ' ') INTO user_message
    FROM rejection_reasons WHERE reason_code = ANY(p_reason_codes);

    -- Reject the media asset
    UPDATE media_assets
    SET status = 'rejected', rejection_reason = COALESCE(user_message, 'Odrzucone.')
    WHERE asset_id = p_asset_id AND status = 'pending';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Media asset % not found or not pending', p_asset_id;
    END IF;

    -- Log the rejection
    INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note, processed_by)
    VALUES ('photo', p_asset_id,
            CASE WHEN p_admin_id IS NOT NULL THEN 'admin' ELSE 'ai' END,
            'reject', p_reason_codes, p_admin_note, p_admin_id);
END;
$$ LANGUAGE plpgsql;

-- Review Content Moderation Functions
CREATE OR REPLACE FUNCTION approve_review_content(
    p_review_id INT,
    p_admin_id INT DEFAULT NULL,
    p_admin_note TEXT DEFAULT NULL
) RETURNS void AS $$
BEGIN
    -- Approve the content
    UPDATE reviews SET content_status = 'approved'
    WHERE review_id = p_review_id AND content_status = 'pending';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Review % content not pending', p_review_id;
    END IF;

    -- Log the approval
    INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note, processed_by)
    VALUES ('review', p_review_id,
            CASE WHEN p_admin_id IS NOT NULL THEN 'admin' ELSE 'system' END,
            'approve', ARRAY[]::VARCHAR[], p_admin_note, p_admin_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION reject_review_content(
    p_review_id INT,
    p_reason_codes VARCHAR[],
    p_admin_id INT DEFAULT NULL,
    p_admin_note TEXT DEFAULT NULL
) RETURNS void AS $$
DECLARE user_message TEXT;
BEGIN
    -- Build user-facing message from reason codes
    SELECT STRING_AGG(user_message_template, ' ') INTO user_message
    FROM rejection_reasons WHERE reason_code = ANY(p_reason_codes);

    -- Reject the content
    UPDATE reviews
    SET content_status = 'rejected',
        content_rejection_reason = COALESCE(user_message, 'Odrzucony.')
    WHERE review_id = p_review_id AND content_status = 'pending';

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Review % content not pending', p_review_id;
    END IF;

    -- Log the rejection
    INSERT INTO system.moderation_logs (entity_type, entity_id, actor, verdict, reason_codes, admin_note, processed_by)
    VALUES ('review', p_review_id,
            CASE WHEN p_admin_id IS NOT NULL THEN 'admin' ELSE 'ai' END,
            'reject', p_reason_codes, p_admin_note, p_admin_id);
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION approve_ingredient_suggestion(target_suggestion_id INT, admin_id INT DEFAULT NULL) RETURNS void AS $$
DECLARE sug RECORD;
BEGIN
    SELECT * INTO sug FROM ingredient_suggestions WHERE suggestion_id = target_suggestion_id AND status = 'pending';
    INSERT INTO ingredients (ingredient_name, is_allergen, is_vegetarian, is_vegan, is_gluten_free, is_lactose_free)
    VALUES (sug.suggested_name, sug.is_allergen, sug.is_vegetarian, sug.is_vegan, sug.is_gluten_free, sug.is_lactose_free);
    UPDATE ingredient_suggestions SET status = 'approved', reviewed_at = NOW(), reviewed_by_admin_id = admin_id WHERE suggestion_id = target_suggestion_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION reject_ingredient_suggestion(target_suggestion_id INT, reason VARCHAR, admin_id INT DEFAULT NULL) RETURNS void AS $$
BEGIN
    UPDATE ingredient_suggestions SET status = 'rejected', admin_note = reason, reviewed_at = NOW(), reviewed_by_admin_id = admin_id WHERE suggestion_id = target_suggestion_id AND status = 'pending';
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION merge_ingredient_suggestion(target_suggestion_id INT, existing_ingredient_id INT, admin_id INT DEFAULT NULL) RETURNS void AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ingredients WHERE ingredient_id = existing_ingredient_id) THEN RAISE EXCEPTION 'Target ingredient not found'; END IF;
    UPDATE ingredient_suggestions SET status = 'merged', merged_ingredient_id = existing_ingredient_id, reviewed_at = NOW(), reviewed_by_admin_id = admin_id WHERE suggestion_id = target_suggestion_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION activate_restaurant(target_restaurant_id INT) RETURNS void AS $$
BEGIN
    UPDATE restaurants SET status = 'active', updated_at = NOW() WHERE restaurant_id = target_restaurant_id AND status = 'pending_approval';
END;
$$ LANGUAGE plpgsql;

-- ========================================
-- SUMMARY
-- ========================================
-- Implemented unified moderation system with history tracking and AI reasoning:
--   1. Media moderation via media_assets.status (pending, approved, rejected)
--   2. Unified moderation queues with history tracking
--   3. Restaurant edit requests for B2B workflows
--   4. Ingredient suggestions for crowd-sourced data
-- ========================================
