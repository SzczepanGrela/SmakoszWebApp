-- review_triggers.sql
-- Review photo limit + visibility evaluation

-- ============================================================
-- check_review_photo_limit: max 5 photos per review
-- ============================================================
CREATE OR REPLACE FUNCTION check_review_photo_limit()
RETURNS TRIGGER AS $$
DECLARE
    v_photo_count INT;
BEGIN
    IF NEW.entity_type = 'review' THEN
        SELECT COUNT(*) INTO v_photo_count
        FROM media_assets
        WHERE entity_type = 'review'
          AND entity_id = NEW.entity_id;

        IF v_photo_count >= 5 THEN
            RAISE EXCEPTION 'Limit 5 zdjęć na recenzję został osiągnięty.';
        END IF;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_check_review_photo_limit ON media_assets;
CREATE TRIGGER trg_check_review_photo_limit
BEFORE INSERT ON media_assets
FOR EACH ROW
WHEN (NEW.entity_type = 'review')
EXECUTE FUNCTION check_review_photo_limit();

-- ============================================================
-- evaluate_review_visibility: is_visible based on content_status + photo status
-- ============================================================
CREATE OR REPLACE FUNCTION evaluate_review_visibility(p_review_id INT)
RETURNS VOID AS $$
DECLARE
    v_content_status VARCHAR;
    v_has_pending_photos BOOLEAN;
    v_has_rejected_photos BOOLEAN;
    v_new_visibility BOOLEAN;
BEGIN
    SELECT content_status INTO v_content_status
    FROM reviews WHERE review_id = p_review_id;

    IF NOT FOUND THEN RETURN; END IF;

    SELECT
        COALESCE(BOOL_OR(status = 'pending'), FALSE),
        COALESCE(BOOL_OR(status = 'rejected'), FALSE)
    INTO v_has_pending_photos, v_has_rejected_photos
    FROM media_assets
    WHERE entity_type = 'review' AND entity_id = p_review_id;

    v_new_visibility := (v_content_status IN ('approved', 'none'))
                        AND (NOT v_has_pending_photos)
                        AND (NOT v_has_rejected_photos);

    UPDATE reviews
    SET is_visible = v_new_visibility
    WHERE review_id = p_review_id
      AND is_visible IS DISTINCT FROM v_new_visibility;
END;
$$ LANGUAGE plpgsql;

-- Trigger on review content_status change
CREATE OR REPLACE FUNCTION trg_on_review_status_change()
RETURNS TRIGGER AS $$
BEGIN
    PERFORM evaluate_review_visibility(NEW.review_id);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_review_visibility ON reviews;
CREATE TRIGGER trg_review_visibility
    AFTER UPDATE OF content_status ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_on_review_status_change();

-- Trigger on photo status change (for review photos)
CREATE OR REPLACE FUNCTION trg_on_photo_change()
RETURNS TRIGGER AS $$
DECLARE
    v_review_id INT;
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.entity_type = 'review' THEN
            PERFORM evaluate_review_visibility(OLD.entity_id);
        END IF;
    ELSE
        IF NEW.entity_type = 'review' THEN
            PERFORM evaluate_review_visibility(NEW.entity_id);
        END IF;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_photo_visibility ON media_assets;
CREATE TRIGGER trg_photo_visibility
    AFTER INSERT OR UPDATE OF status OR DELETE ON media_assets
    FOR EACH ROW EXECUTE FUNCTION trg_on_photo_change();
