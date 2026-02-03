-- =============================================================================
-- SCHEMA: MODERATION & TICKETS SYNCHRONIZATION (v5.1 - Fix Split-Brain)
-- =============================================================================
-- This file implements ONE-WAY synchronization from Source Tables -> system.tickets.
--
-- ARCHITECTURE CHANGE (v5.1):
-- 1. Removed "Backward Sync" (Ticket -> Source) trigger to prevent loops and race conditions.
-- 2. Added `system.resolve_ticket()` procedural facade for safe updates with locking.
-- 3. system.tickets is now a Read Model reflecting the Source of Truth.

-- =============================================================================
-- 1. UTILITY: Forward Sync Helper
-- =============================================================================
CREATE OR REPLACE FUNCTION system.sync_to_ticket(
    p_type VARCHAR, p_ref_id BIGINT, p_status VARCHAR, p_priority INT
) RETURNS VOID AS $$
DECLARE
    v_ticket_status VARCHAR;
BEGIN
    -- Map Source Status -> Ticket Status
    v_ticket_status := CASE 
        WHEN p_status = 'pending' THEN 'open'
        WHEN p_status = 'approved' THEN 'resolved'
        WHEN p_status = 'resolved' THEN 'resolved'
        WHEN p_status = 'rejected' THEN 'rejected'
        WHEN p_status = 'dismissed' THEN 'rejected'
        WHEN p_status = 'processing' THEN 'in_progress'
        ELSE 'open'
    END;

    -- UPSERT Ticket
    INSERT INTO system.tickets (ticket_type, reference_id, status, priority)
    VALUES (p_type, p_ref_id, v_ticket_status, p_priority)
    ON CONFLICT (ticket_type, reference_id) 
    DO UPDATE SET 
        status = EXCLUDED.status,
        updated_at = NOW(),
        version = system.tickets.version + 1;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- 2. FORWARD SYNC: Source -> Ticket (Triggers)
-- =============================================================================

-- TRIGGER: Reviews (Content)
CREATE OR REPLACE FUNCTION public.trg_review_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT' AND NEW.content_status = 'pending') 
       OR (TG_OP = 'UPDATE' AND NEW.content_status <> OLD.content_status) THEN
        PERFORM system.sync_to_ticket('review_content', NEW.review_id, NEW.content_status, 3);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- TRIGGER: Media Assets
CREATE OR REPLACE FUNCTION public.trg_media_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT' AND NEW.status = 'pending') 
       OR (TG_OP = 'UPDATE' AND NEW.status <> OLD.status) THEN
        PERFORM system.sync_to_ticket('media_asset', NEW.asset_id, NEW.status, 3);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- TRIGGER: Reports
CREATE OR REPLACE FUNCTION public.trg_report_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') OR (TG_OP = 'UPDATE' AND NEW.status <> OLD.status) THEN
        PERFORM system.sync_to_ticket('report', NEW.report_id, NEW.status, 2);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- TRIGGER: Data Corrections (Unclaimed)
CREATE OR REPLACE FUNCTION public.trg_correction_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') OR (TG_OP = 'UPDATE' AND NEW.status <> OLD.status) THEN
        PERFORM system.sync_to_ticket('data_correction', NEW.request_id, NEW.status, 4);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- TRIGGER: Restaurant Edits (B2B)
CREATE OR REPLACE FUNCTION public.trg_edit_request_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') OR (TG_OP = 'UPDATE' AND NEW.status <> OLD.status) THEN
        PERFORM system.sync_to_ticket('restaurant_edit', NEW.request_id, NEW.status, 3);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- TRIGGER: Ingredient Suggestions
CREATE OR REPLACE FUNCTION public.trg_ingredient_to_ticket() RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'INSERT') OR (TG_OP = 'UPDATE' AND NEW.status <> OLD.status) THEN
        PERFORM system.sync_to_ticket('ingredient_proposal', NEW.suggestion_id, NEW.status, 5);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Binding Triggers
DROP TRIGGER IF EXISTS trg_sync_ticket ON public.reviews;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF content_status ON public.reviews FOR EACH ROW EXECUTE FUNCTION public.trg_review_to_ticket();

DROP TRIGGER IF EXISTS trg_sync_ticket ON public.media_assets;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF status ON public.media_assets FOR EACH ROW EXECUTE FUNCTION public.trg_media_to_ticket();

DROP TRIGGER IF EXISTS trg_sync_ticket ON public.reports;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF status ON public.reports FOR EACH ROW EXECUTE FUNCTION public.trg_report_to_ticket();

DROP TRIGGER IF EXISTS trg_sync_ticket ON public.data_correction_requests;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF status ON public.data_correction_requests FOR EACH ROW EXECUTE FUNCTION public.trg_correction_to_ticket();

DROP TRIGGER IF EXISTS trg_sync_ticket ON public.restaurant_edit_requests;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF status ON public.restaurant_edit_requests FOR EACH ROW EXECUTE FUNCTION public.trg_edit_request_to_ticket();

DROP TRIGGER IF EXISTS trg_sync_ticket ON public.ingredient_suggestions;
CREATE TRIGGER trg_sync_ticket AFTER INSERT OR UPDATE OF status ON public.ingredient_suggestions FOR EACH ROW EXECUTE FUNCTION public.trg_ingredient_to_ticket();

-- =============================================================================
-- 3. TRANSACTIONAL API: Safe Ticket Resolution
-- =============================================================================
-- Replaces direct updates to system.tickets.
-- Uses Pessimistic Locking (FOR UPDATE) to prevent race conditions.

CREATE OR REPLACE FUNCTION system.resolve_ticket(
    p_ticket_id INT,
    p_verdict VARCHAR, -- 'approved', 'rejected'
    p_admin_id INT DEFAULT NULL,
    p_note TEXT DEFAULT NULL
) RETURNS VOID AS $$
DECLARE
    v_type VARCHAR;
    v_ref_id BIGINT;
BEGIN
    -- 1. Get Ticket Info
    SELECT ticket_type, reference_id
    INTO v_type, v_ref_id
    FROM system.tickets
    WHERE ticket_id = p_ticket_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'Ticket % not found', p_ticket_id;
    END IF;

    -- 2. Validate Verdict
    IF p_verdict NOT IN ('approved', 'rejected') THEN
        RAISE EXCEPTION 'Invalid verdict: %. Must be approved or rejected.', p_verdict;
    END IF;

    -- 3. Routing & Locking (Update SOURCE TABLE, which triggers Sync back to Ticket)
    CASE v_type
        WHEN 'review_content' THEN
            PERFORM 1 FROM reviews WHERE review_id = v_ref_id FOR UPDATE;
            UPDATE reviews
            SET content_status = p_verdict,
                content_rejection_reason = CASE WHEN p_verdict = 'rejected' THEN p_note ELSE NULL END
            WHERE review_id = v_ref_id;

        WHEN 'media_asset' THEN
            PERFORM 1 FROM media_assets WHERE asset_id = v_ref_id FOR UPDATE;
            UPDATE media_assets
            SET status = p_verdict,
                rejection_reason = CASE WHEN p_verdict = 'rejected' THEN p_note ELSE NULL END
            WHERE asset_id = v_ref_id;

        WHEN 'report' THEN
            PERFORM 1 FROM reports WHERE report_id = v_ref_id FOR UPDATE;
            UPDATE reports
            SET status = CASE WHEN p_verdict = 'approved' THEN 'resolved' ELSE 'dismissed' END,
                resolved_at = NOW(),
                resolved_by_admin_id = p_admin_id
            WHERE report_id = v_ref_id;

        WHEN 'data_correction' THEN
            PERFORM 1 FROM data_correction_requests WHERE request_id = v_ref_id FOR UPDATE;
            UPDATE data_correction_requests
            SET status = p_verdict
            WHERE request_id = v_ref_id;

        WHEN 'restaurant_edit' THEN
            PERFORM 1 FROM restaurant_edit_requests WHERE request_id = v_ref_id FOR UPDATE;
            UPDATE restaurant_edit_requests
            SET status = p_verdict,
                admin_note = p_note,
                resolved_at = NOW(),
                resolved_by_admin_id = p_admin_id
            WHERE request_id = v_ref_id;

        WHEN 'ingredient_proposal' THEN
            PERFORM 1 FROM ingredient_suggestions WHERE suggestion_id = v_ref_id FOR UPDATE;
            UPDATE ingredient_suggestions
            SET status = p_verdict,
                admin_note = p_note,
                reviewed_at = NOW(),
                reviewed_by_admin_id = p_admin_id
            WHERE suggestion_id = v_ref_id;

        ELSE
            RAISE EXCEPTION 'Unknown ticket type: %', v_type;
    END CASE;
    
    -- Note: We rely on the FORWARD SYNC triggers defined in Section 2 to update the ticket status.
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION system.resolve_ticket IS 
'Safe transactional method to resolve admin tickets. 
Updates the SOURCE table with pessimistic locking, letting triggers sync the status back to the ticket.';

-- =============================================================================
-- 4. AUTO-APPLY: Apply approved changes to target tables
-- =============================================================================

-- Function for Restaurant Edits
CREATE OR REPLACE FUNCTION public.trg_apply_restaurant_edit()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.status = 'approved' AND OLD.status = 'pending' THEN
        UPDATE public.restaurants SET
            restaurant_name = COALESCE(NEW.new_name, restaurant_name),
            description = COALESCE(NEW.new_description, description),
            address = COALESCE(NEW.new_address, address),
            cuisine_type = COALESCE(NEW.new_cuisine_type, cuisine_type),
            phone = COALESCE(NEW.new_phone, phone),
            website = COALESCE(NEW.new_website, website),
            image_url = COALESCE(NEW.new_image_url, image_url),
            image_blurhash = COALESCE(NEW.new_image_blurhash, image_blurhash),
            updated_at = NOW(),
            version = version + 1
        WHERE restaurant_id = NEW.restaurant_id;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Function for Ingredient Suggestions
CREATE OR REPLACE FUNCTION public.trg_apply_ingredient_suggestion()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.status = 'approved' AND OLD.status = 'pending' THEN
        INSERT INTO public.ingredients (
            ingredient_name, icon_url, icon_blurhash, 
            is_allergen, is_vegetarian, is_vegan, is_gluten_free, is_lactose_free
        ) VALUES (
            NEW.suggested_name, NEW.icon_url, NEW.icon_blurhash,
            NEW.is_allergen, NEW.is_vegetarian, NEW.is_vegan, NEW.is_gluten_free, NEW.is_lactose_free
        )
        ON CONFLICT (ingredient_name) DO UPDATE SET
            icon_url = EXCLUDED.icon_url,
            is_allergen = EXCLUDED.is_allergen;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Binding Auto-Apply Triggers
DROP TRIGGER IF EXISTS trg_apply_edit ON public.restaurant_edit_requests;
CREATE TRIGGER trg_apply_edit AFTER UPDATE OF status ON public.restaurant_edit_requests FOR EACH ROW EXECUTE FUNCTION public.trg_apply_restaurant_edit();

DROP TRIGGER IF EXISTS trg_apply_suggestion ON public.ingredient_suggestions;
CREATE TRIGGER trg_apply_suggestion AFTER UPDATE OF status ON public.ingredient_suggestions FOR EACH ROW EXECUTE FUNCTION public.trg_apply_ingredient_suggestion();

-- =============================================================================
-- 5. REVIEW VISIBILITY LOGIC (The "State Machine")
-- =============================================================================
-- Calculates reviews.is_visible based on content_status and media_assets.status.

CREATE OR REPLACE FUNCTION evaluate_review_visibility(p_review_id INT)
RETURNS VOID AS $$
DECLARE
    v_content_status VARCHAR;
    v_has_pending_photos BOOLEAN;
    v_has_rejected_photos BOOLEAN;
    v_new_visibility BOOLEAN;
BEGIN
    -- 1. Get current content status
    SELECT content_status INTO v_content_status
    FROM reviews WHERE review_id = p_review_id;

    IF NOT FOUND THEN RETURN; END IF;

    -- 2. Check photos status
    SELECT 
        COALESCE(BOOL_OR(status = 'pending'), FALSE),
        COALESCE(BOOL_OR(status = 'rejected'), FALSE)
    INTO v_has_pending_photos, v_has_rejected_photos
    FROM media_assets 
    WHERE entity_type = 'review' AND entity_id = p_review_id;

    -- 3. Calculate Visibility Logic
    -- Rule: Visible ONLY if content is Approved AND No pending photos AND No rejected photos
    v_new_visibility := (v_content_status IN ('approved', 'none'))
                        AND (NOT v_has_pending_photos)
                        AND (NOT v_has_rejected_photos);

    -- 4. Update if changed
    UPDATE reviews
    SET is_visible = v_new_visibility
    WHERE review_id = p_review_id
      AND is_visible IS DISTINCT FROM v_new_visibility;
END;
$$ LANGUAGE plpgsql;

-- Trigger for Review changes (Content Status)
CREATE OR REPLACE FUNCTION trg_on_review_status_change()
RETURNS TRIGGER AS $$
BEGIN
    -- If content status changed, re-evaluate visibility
    PERFORM evaluate_review_visibility(NEW.review_id);
    RETURN NULL; -- AFTER trigger
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_review_visibility ON reviews;
CREATE TRIGGER trg_review_visibility
    AFTER UPDATE OF content_status ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_on_review_status_change();

-- Trigger for Photo changes (Assets)
CREATE OR REPLACE FUNCTION trg_on_photo_change()
RETURNS TRIGGER AS $$
DECLARE
    v_review_id INT;
BEGIN
    IF NEW.entity_type = 'review' THEN
        v_review_id := NEW.entity_id;
        PERFORM evaluate_review_visibility(v_review_id);
    ELSIF OLD.entity_type = 'review' THEN -- Handle deletes
        v_review_id := OLD.entity_id;
        PERFORM evaluate_review_visibility(v_review_id);
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_photo_visibility ON media_assets;
CREATE TRIGGER trg_photo_visibility
    AFTER INSERT OR UPDATE OF status OR DELETE ON media_assets
    FOR EACH ROW EXECUTE FUNCTION trg_on_photo_change();