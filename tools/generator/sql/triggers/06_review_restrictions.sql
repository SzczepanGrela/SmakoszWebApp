-- ========================================
-- TRIGGER: Role Restrictions (v5.0)
-- ========================================
-- Business Rule: Only users with role='user' can perform social actions.
-- Admins, Moderators, and Restaurant Owners are prohibited from:
--   1. Submitting reviews
--   2. Liking reviews
--   3. Following other users
--
-- Security: This is enforced at the database level to prevent API bypasses.

-- Function: Check user role before allowing social actions
CREATE OR REPLACE FUNCTION prevent_non_user_social_actions()
RETURNS TRIGGER AS $$
DECLARE
    user_role TEXT;
    v_user_id INT;
BEGIN
    -- Determine which ID to check based on the table
    CASE TG_TABLE_NAME
        WHEN 'reviews' THEN v_user_id := NEW.user_id;
        WHEN 'review_likes' THEN v_user_id := NEW.user_id;
        WHEN 'user_follows' THEN v_user_id := NEW.follower_id;
        ELSE RETURN NEW;
    END CASE;

    -- Lookup the user's role
    SELECT role INTO user_role FROM users WHERE user_id = v_user_id;

    -- Enforce business rule: only 'user' role can perform these actions
    IF user_role IN ('admin', 'moderator', 'restaurant') THEN
        RAISE EXCEPTION 'Użytkownicy z rolą % nie mogą wykonywać akcji społecznościowych (recenzje, lajki, followy)', user_role
            USING HINT = 'Tylko zwykli użytkownicy (role=user) mają dostęp do funkcji społecznościowych.';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 1. Apply to Reviews
DROP TRIGGER IF EXISTS trg_prevent_illegal_reviews ON reviews;
CREATE TRIGGER trg_prevent_illegal_reviews
    BEFORE INSERT ON reviews
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

-- 2. Apply to Likes
DROP TRIGGER IF EXISTS trg_prevent_illegal_likes ON review_likes;
CREATE TRIGGER trg_prevent_illegal_likes
    BEFORE INSERT ON review_likes
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

-- 3. Apply to Follows
DROP TRIGGER IF EXISTS trg_prevent_illegal_follows ON user_follows;
CREATE TRIGGER trg_prevent_illegal_follows
    BEFORE INSERT ON user_follows
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

-- Index optimization: Speed up role lookups during actions
CREATE INDEX IF NOT EXISTS idx_users_id_role ON users(user_id, role);

COMMENT ON FUNCTION prevent_non_user_social_actions() IS
    'Security function: Prevents non-user roles (admin/moderator/restaurant) from submitting reviews, likes, or follows';