-- ========================================
-- SCHEMA: TRIGGERS
-- ========================================

-- Lifecycle Triggers (Timestamps)
CREATE TRIGGER trg_update_timestamp_users BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_timestamp();
CREATE TRIGGER trg_update_timestamp_restaurants BEFORE UPDATE ON restaurants FOR EACH ROW EXECUTE FUNCTION update_timestamp();
CREATE TRIGGER trg_update_timestamp_dishes BEFORE UPDATE ON dishes FOR EACH ROW EXECUTE FUNCTION update_timestamp();
CREATE TRIGGER trg_update_timestamp_reviews BEFORE UPDATE ON reviews FOR EACH ROW EXECUTE FUNCTION update_timestamp();
CREATE TRIGGER trg_update_timestamp_notifications BEFORE UPDATE ON notifications FOR EACH ROW EXECUTE FUNCTION update_timestamp();

-- Lifecycle Triggers (Soft Delete)
CREATE TRIGGER trg_soft_delete_user
AFTER UPDATE OF is_deleted ON users
FOR EACH ROW
EXECUTE FUNCTION propagate_soft_delete_user();

-- Security Triggers
CREATE TRIGGER trg_update_last_login
AFTER INSERT ON system.security_logs
FOR EACH ROW
EXECUTE FUNCTION update_last_login_from_log();

-- Business Logic: Dish Ingredients Sync
CREATE TRIGGER sync_dish_insert
AFTER INSERT ON dish_ingredients
REFERENCING NEW TABLE AS new_table
FOR EACH STATEMENT
EXECUTE FUNCTION trg_refresh_dish_metadata_on_insert();

CREATE TRIGGER sync_dish_delete
AFTER DELETE ON dish_ingredients
REFERENCING OLD TABLE AS old_table
FOR EACH STATEMENT
EXECUTE FUNCTION trg_refresh_dish_metadata_on_delete();

CREATE TRIGGER sync_dish_update
AFTER UPDATE ON dish_ingredients
REFERENCING NEW TABLE AS new_table OLD TABLE AS old_table
FOR EACH STATEMENT
EXECUTE FUNCTION trg_refresh_dish_metadata_on_update();

CREATE TRIGGER sync_dish_on_ingredient_change
AFTER UPDATE ON ingredients
FOR EACH ROW
WHEN (OLD.is_allergen IS DISTINCT FROM NEW.is_allergen OR
      OLD.is_vegan IS DISTINCT FROM NEW.is_vegan OR
      OLD.is_vegetarian IS DISTINCT FROM NEW.is_vegetarian OR
      OLD.is_gluten_free IS DISTINCT FROM NEW.is_gluten_free OR
      OLD.is_lactose_free IS DISTINCT FROM NEW.is_lactose_free OR
      OLD.ingredient_name IS DISTINCT FROM NEW.ingredient_name)
EXECUTE FUNCTION trg_refresh_dish_metadata_on_ingredient_change();

-- Notifications: Correction Request (v2.0 - ROW-level for routing)
DROP TRIGGER IF EXISTS trg_notify_correction ON data_correction_requests;

CREATE TRIGGER trg_notify_correction
AFTER INSERT ON data_correction_requests
FOR EACH ROW
EXECUTE FUNCTION notify_owner_on_correction_request();

COMMENT ON TRIGGER trg_notify_correction ON data_correction_requests IS
'v2.0: Routes to owner (claimed) or admin (unclaimed) with daily aggregation.';

-- Notifications: Like
CREATE TRIGGER trg_notify_like
AFTER INSERT ON review_likes
REFERENCING NEW TABLE AS new_table
FOR EACH STATEMENT
EXECUTE FUNCTION trg_create_like_notifications_bulk();

-- Notifications: Follow
CREATE TRIGGER trg_notify_follow
AFTER INSERT ON user_follows
REFERENCING NEW TABLE AS new_table
FOR EACH STATEMENT
EXECUTE FUNCTION trg_create_follow_notifications_bulk();

-- Sync: Review Helpful Count
CREATE TRIGGER trg_sync_review_likes_insert
AFTER INSERT ON review_likes
REFERENCING NEW TABLE AS new_table
FOR EACH STATEMENT
EXECUTE FUNCTION sync_review_helpful_count_insert();

CREATE TRIGGER trg_sync_review_likes_delete
AFTER DELETE ON review_likes
REFERENCING OLD TABLE AS old_table
FOR EACH STATEMENT
EXECUTE FUNCTION sync_review_helpful_count_delete();

-- Sync: Social Counters (Followers/Following)
DROP TRIGGER IF EXISTS trg_update_social_counts ON user_follows;
CREATE TRIGGER trg_update_follow_counts
    AFTER INSERT OR DELETE ON user_follows
    FOR EACH ROW
    EXECUTE FUNCTION update_follow_counts();

COMMENT ON TRIGGER trg_update_follow_counts ON user_follows IS
'Maintains denormalized follower/following counts in users table.';

-- Sync: Review Counts (User & Dish)
CREATE TRIGGER trg_update_review_counts
    AFTER INSERT OR DELETE ON reviews
    FOR EACH ROW
    EXECUTE FUNCTION update_review_counts();

-- Sync: Photo Counts (User)
CREATE TRIGGER trg_update_photo_counts
    AFTER INSERT OR DELETE ON media_assets
    FOR EACH ROW
    EXECUTE FUNCTION update_photo_counts();

-- Slug: Users
CREATE TRIGGER trg_users_slug
    BEFORE INSERT OR UPDATE OF username ON users
    FOR EACH ROW
    EXECUTE FUNCTION trg_generate_user_slug();