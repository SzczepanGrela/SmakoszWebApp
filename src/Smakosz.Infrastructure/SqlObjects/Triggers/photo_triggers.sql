-- photo_triggers.sql
-- Enforce max 1 primary photo per entity + sync primary photo URL to entity tables

-- ============================================================
-- enforce_primary_photo: demote other primaries when setting new
-- ============================================================
CREATE OR REPLACE FUNCTION enforce_primary_photo()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.is_primary = TRUE THEN
        UPDATE media_assets
        SET is_primary = FALSE
        WHERE entity_type = NEW.entity_type
          AND entity_id = NEW.entity_id
          AND asset_id != NEW.asset_id
          AND is_primary = TRUE;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_enforce_primary_photo ON media_assets;
CREATE TRIGGER trg_enforce_primary_photo
BEFORE INSERT OR UPDATE OF is_primary ON media_assets
FOR EACH ROW
WHEN (NEW.is_primary = TRUE)
EXECUTE FUNCTION enforce_primary_photo();

-- ============================================================
-- sync_primary_photo_to_entity: copy URL/blurhash to parent table
-- ============================================================
CREATE OR REPLACE FUNCTION sync_primary_photo_to_entity()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.is_primary = TRUE AND NEW.status = 'approved' THEN
        CASE NEW.entity_type
            WHEN 'restaurant' THEN
                UPDATE restaurants
                SET image_url = NEW.url,
                    image_blurhash = NEW.blurhash
                WHERE restaurant_id = NEW.entity_id;

                UPDATE users
                SET avatar_url = NEW.url
                WHERE restaurant_id = NEW.entity_id;

            WHEN 'dish' THEN
                UPDATE dishes
                SET image_url = NEW.url,
                    image_blurhash = NEW.blurhash
                WHERE dish_id = NEW.entity_id;

            WHEN 'user' THEN
                UPDATE users
                SET avatar_url = NEW.url,
                    avatar_blurhash = NEW.blurhash
                WHERE user_id = NEW.entity_id;

            ELSE
                RAISE WARNING 'Unknown entity_type in sync_primary_photo_to_entity: %', NEW.entity_type;
        END CASE;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_sync_primary_photo ON media_assets;
CREATE TRIGGER trg_sync_primary_photo
AFTER INSERT OR UPDATE OF is_primary, status, url, blurhash ON media_assets
FOR EACH ROW
WHEN (NEW.is_primary = TRUE AND NEW.status = 'approved')
EXECUTE FUNCTION sync_primary_photo_to_entity();
