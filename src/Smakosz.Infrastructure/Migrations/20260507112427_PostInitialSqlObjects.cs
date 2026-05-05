using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Smakosz.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostInitialSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION f_unaccent(text)
                  RETURNS text AS
                $func$
                SELECT public.unaccent('public.unaccent', $1)
                $func$  LANGUAGE sql IMMUTABLE;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION validate_opening_hours()
                RETURNS TRIGGER AS $$
                DECLARE
                    overlap_count INT;
                BEGIN
                    IF NEW.is_closed = TRUE THEN
                        RETURN NEW;
                    END IF;

                    IF NEW.open_time = NEW.close_time THEN
                        RAISE EXCEPTION 'Nieprawidlowy czas otwarcia: open_time nie moze byc rowne close_time.';
                    END IF;

                    SELECT COUNT(*) INTO overlap_count
                    FROM restaurant_opening_hours
                    WHERE restaurant_id = NEW.restaurant_id
                      AND day_of_week = NEW.day_of_week
                      AND is_closed = FALSE
                      AND hours_id != COALESCE(NEW.hours_id, -1)
                      AND (NEW.open_time < close_time AND NEW.close_time > open_time);

                    IF overlap_count > 0 THEN
                        RAISE EXCEPTION 'Konflikt czasu otwarcia: Zakresy nachodza na siebie.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_validate_opening_hours ON restaurant_opening_hours;
                CREATE TRIGGER trg_validate_opening_hours
                BEFORE INSERT OR UPDATE ON restaurant_opening_hours
                FOR EACH ROW
                EXECUTE FUNCTION validate_opening_hours();

                CREATE OR REPLACE FUNCTION normalize_phone_number()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.phone IS NOT NULL THEN
                        NEW.phone := REGEXP_REPLACE(NEW.phone, '[ \-\(\)]', '', 'g');

                        IF NEW.phone ~ '^[0-9]{9}$' THEN
                            NEW.phone := '+48' || NEW.phone;
                        END IF;

                        IF NEW.phone ~ '^00' THEN
                            NEW.phone := '+' || SUBSTRING(NEW.phone, 3);
                        END IF;

                        IF NEW.phone !~ '^\+[0-9]{7,15}$' THEN
                            RAISE EXCEPTION 'Nieprawidlowy format numeru telefonu. Wymagany E.164.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                DROP TRIGGER IF EXISTS trg_normalize_user_phone ON users;
                CREATE TRIGGER trg_normalize_user_phone
                BEFORE INSERT OR UPDATE OF phone ON users
                FOR EACH ROW
                WHEN (NEW.phone IS NOT NULL)
                EXECUTE FUNCTION normalize_phone_number();

                DROP TRIGGER IF EXISTS trg_normalize_restaurant_phone ON restaurants;
                CREATE TRIGGER trg_normalize_restaurant_phone
                BEFORE INSERT OR UPDATE OF phone ON restaurants
                FOR EACH ROW
                WHEN (NEW.phone IS NOT NULL)
                EXECUTE FUNCTION normalize_phone_number();
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION queue_file_deletion()
                RETURNS TRIGGER AS $$
                BEGIN
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
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION enforce_primary_photo()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF NEW.is_primary = TRUE THEN
                        INSERT INTO system.files_to_delete (r2key, bucket, reason, source_entity, source_id, queued_at)
                        SELECT
                            substring(url FROM 'https?://[^/]+/(.+)'),
                            'smakosz-photos',
                            'primary_photo_replaced',
                            NEW.entity_type || ':' || NEW.entity_id,
                            asset_id::int,
                            NOW()
                        FROM media_assets
                        WHERE entity_type = NEW.entity_type
                          AND entity_id = NEW.entity_id
                          AND asset_id != NEW.asset_id
                          AND is_primary = TRUE
                          AND url IS NOT NULL
                          AND url NOT LIKE '%/seed/%';

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
            ");

            migrationBuilder.Sql(@"
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
                            RAISE EXCEPTION 'Limit 5 zdjec na recenzje zostal osiagniety.';
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
            ");

            migrationBuilder.Sql(@"
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
                        RAISE EXCEPTION 'Uzytkownicy z rola % nie moga wykonywac akcji spolecznosciowych', user_role;
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
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_name
                    ON restaurants
                    USING GIN (f_unaccent(lower(restaurant_name)) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS trgm_idx_dishes_name
                    ON dishes
                    USING GIN (f_unaccent(lower(dish_name)) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS trgm_idx_users_username
                    ON users
                    USING GIN (f_unaccent(lower(username)) gin_trgm_ops);

                CREATE INDEX IF NOT EXISTS idx_users_email_lower ON users (lower(email));
                CREATE INDEX IF NOT EXISTS idx_users_username_lower ON users (lower(username));
            ");

            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS search_autocomplete;

                CREATE VIEW search_autocomplete AS
                    SELECT
                        'restaurant'::text AS type,
                        r.restaurant_id AS id,
                        r.restaurant_name AS name,
                        r.slug,
                        ct.display_name AS subtitle,
                        r.image_url AS icon,
                        r.image_blurhash,
                        f_unaccent(lower(r.restaurant_name || ' ' || COALESCE(ct.display_name, ''))) AS name_normalized,
                        2 AS priority
                    FROM restaurants r
                    LEFT JOIN cuisine_types ct ON ct.cuisine_type_id = r.cuisine_type_id
                    WHERE r.status = 'active'

                    UNION ALL

                    SELECT
                        'dish'::text AS type,
                        d.dish_id AS id,
                        d.dish_name AS name,
                        d.slug,
                        r.restaurant_name AS subtitle,
                        d.image_url AS icon,
                        d.image_blurhash,
                        f_unaccent(lower(d.dish_name)) AS name_normalized,
                        3 AS priority
                    FROM dishes d
                    JOIN restaurants r ON d.restaurant_id = r.restaurant_id
                    WHERE d.is_available = TRUE AND r.status = 'active';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS search_autocomplete;
                DROP INDEX IF EXISTS trgm_idx_restaurants_name;
                DROP INDEX IF EXISTS trgm_idx_dishes_name;
                DROP INDEX IF EXISTS trgm_idx_users_username;
                DROP INDEX IF EXISTS idx_users_email_lower;
                DROP INDEX IF EXISTS idx_users_username_lower;
                DROP INDEX IF EXISTS idx_users_id_role;

                DROP TRIGGER IF EXISTS trg_prevent_illegal_reviews ON reviews;
                DROP TRIGGER IF EXISTS trg_prevent_illegal_likes ON review_likes;
                DROP TRIGGER IF EXISTS trg_prevent_illegal_follows ON user_follows;
                DROP FUNCTION IF EXISTS prevent_non_user_social_actions;

                DROP TRIGGER IF EXISTS trg_check_review_photo_limit ON media_assets;
                DROP FUNCTION IF EXISTS check_review_photo_limit;

                DROP TRIGGER IF EXISTS trg_enforce_primary_photo ON media_assets;
                DROP FUNCTION IF EXISTS enforce_primary_photo;

                DROP TRIGGER IF EXISTS trg_queue_media_deletion ON media_assets;
                DROP FUNCTION IF EXISTS queue_file_deletion;

                DROP TRIGGER IF EXISTS trg_normalize_user_phone ON users;
                DROP TRIGGER IF EXISTS trg_normalize_restaurant_phone ON restaurants;
                DROP FUNCTION IF EXISTS normalize_phone_number;

                DROP TRIGGER IF EXISTS trg_validate_opening_hours ON restaurant_opening_hours;
                DROP FUNCTION IF EXISTS validate_opening_hours;

                DROP FUNCTION IF EXISTS f_unaccent(text);
            ");
        }
    }
}
