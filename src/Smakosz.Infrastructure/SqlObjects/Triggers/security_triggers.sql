-- security_triggers.sql
-- Prevent non-user roles from social actions (DB-level enforcement)

CREATE OR REPLACE FUNCTION prevent_non_user_social_actions()
RETURNS TRIGGER AS $$
DECLARE
    user_role TEXT;
    v_user_id INT;
BEGIN
    CASE TG_TABLE_NAME
        WHEN 'reviews' THEN v_user_id := NEW.user_id;
        WHEN 'review_likes' THEN v_user_id := NEW.user_id;
        WHEN 'user_follows' THEN v_user_id := NEW.follower_id;
        ELSE RETURN NEW;
    END CASE;

    SELECT role INTO user_role FROM users WHERE user_id = v_user_id;

    IF user_role IN ('admin', 'moderator', 'restaurant') THEN
        RAISE EXCEPTION 'Użytkownicy z rolą % nie mogą wykonywać akcji społecznościowych', user_role;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_prevent_illegal_reviews ON reviews;
CREATE TRIGGER trg_prevent_illegal_reviews
    BEFORE INSERT ON reviews
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

DROP TRIGGER IF EXISTS trg_prevent_illegal_likes ON review_likes;
CREATE TRIGGER trg_prevent_illegal_likes
    BEFORE INSERT ON review_likes
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

DROP TRIGGER IF EXISTS trg_prevent_illegal_follows ON user_follows;
CREATE TRIGGER trg_prevent_illegal_follows
    BEFORE INSERT ON user_follows
    FOR EACH ROW EXECUTE FUNCTION prevent_non_user_social_actions();

CREATE INDEX IF NOT EXISTS idx_users_id_role ON users(user_id, role);
