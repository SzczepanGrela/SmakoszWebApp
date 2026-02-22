-- Counter triggers - maintain denormalized counts on INSERT/DELETE
-- These are lightweight per-row triggers for the API (single operations).
-- The Python generator bypasses these and uses bulk sync_counters instead.

-- ============================================================
-- Follow counts (followers_count, following_count on users)
-- ============================================================
CREATE OR REPLACE FUNCTION trg_update_follow_counts()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE users SET followers_count = followers_count + 1
            WHERE user_id = NEW.followed_id;
        UPDATE users SET following_count = following_count + 1
            WHERE user_id = NEW.follower_id;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE users SET followers_count = followers_count - 1
            WHERE user_id = OLD.followed_id;
        UPDATE users SET following_count = following_count - 1
            WHERE user_id = OLD.follower_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_follow_counts ON user_follows;
CREATE TRIGGER trg_follow_counts
    AFTER INSERT OR DELETE ON user_follows
    FOR EACH ROW EXECUTE FUNCTION trg_update_follow_counts();

-- ============================================================
-- Review helpful_count (from review_likes)
-- ============================================================
CREATE OR REPLACE FUNCTION trg_sync_helpful_count()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE reviews SET helpful_count = helpful_count + 1
            WHERE review_id = NEW.review_id;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE reviews SET helpful_count = helpful_count - 1
            WHERE review_id = OLD.review_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_helpful_count ON review_likes;
CREATE TRIGGER trg_helpful_count
    AFTER INSERT OR DELETE ON review_likes
    FOR EACH ROW EXECUTE FUNCTION trg_sync_helpful_count();

-- ============================================================
-- User review_count (from reviews)
-- ============================================================
CREATE OR REPLACE FUNCTION trg_update_user_review_count()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE users SET review_count = review_count + 1
            WHERE user_id = NEW.user_id;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE users SET review_count = review_count - 1
            WHERE user_id = OLD.user_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_user_review_count ON reviews;
CREATE TRIGGER trg_user_review_count
    AFTER INSERT OR DELETE ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_update_user_review_count();

-- ============================================================
-- Dish review_count (from reviews)
-- ============================================================
CREATE OR REPLACE FUNCTION trg_update_dish_review_count()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE dishes SET review_count = review_count + 1
            WHERE dish_id = NEW.dish_id;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE dishes SET review_count = review_count - 1
            WHERE dish_id = OLD.dish_id;
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_dish_review_count ON reviews;
CREATE TRIGGER trg_dish_review_count
    AFTER INSERT OR DELETE ON reviews
    FOR EACH ROW EXECUTE FUNCTION trg_update_dish_review_count();
