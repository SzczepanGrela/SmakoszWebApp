-- cleanup_triggers.sql
-- Queue file deletion to R2 reaper when media assets are deleted

CREATE OR REPLACE FUNCTION queue_file_deletion()
RETURNS TRIGGER AS $$
BEGIN
    -- Seed assets are shared; do not enqueue them for deletion even if a media_asset row referencing one is removed.
    IF OLD.url IS NOT NULL AND OLD.url NOT LIKE '%/seed/%' THEN
        INSERT INTO system.files_to_delete (r2key, source_entity)
        VALUES (OLD.url, TG_TABLE_NAME);
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_queue_media_deletion ON media_assets;
CREATE TRIGGER trg_queue_media_deletion
AFTER DELETE ON media_assets
FOR EACH ROW
EXECUTE FUNCTION queue_file_deletion();
