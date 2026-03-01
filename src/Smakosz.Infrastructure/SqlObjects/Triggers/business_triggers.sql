-- business_triggers.sql
-- Opening hours validation + phone number normalization

-- ============================================================
-- validate_opening_hours: no overlap, open != close
-- ============================================================
CREATE OR REPLACE FUNCTION validate_opening_hours()
RETURNS TRIGGER AS $$
DECLARE
    overlap_count INT;
BEGIN
    IF NEW.is_closed = TRUE THEN
        RETURN NEW;
    END IF;

    IF NEW.open_time = NEW.close_time THEN
        RAISE EXCEPTION 'Nieprawidłowy czas otwarcia: open_time nie może być równe close_time.';
    END IF;

    SELECT COUNT(*) INTO overlap_count
    FROM restaurant_opening_hours
    WHERE restaurant_id = NEW.restaurant_id
      AND day_of_week = NEW.day_of_week
      AND is_closed = FALSE
      AND hours_id != COALESCE(NEW.hours_id, -1)
      AND (NEW.open_time < close_time AND NEW.close_time > open_time);

    IF overlap_count > 0 THEN
        RAISE EXCEPTION 'Konflikt czasu otwarcia: Zakresy nachodzą na siebie.';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validate_opening_hours ON restaurant_opening_hours;
CREATE TRIGGER trg_validate_opening_hours
BEFORE INSERT OR UPDATE ON restaurant_opening_hours
FOR EACH ROW
EXECUTE FUNCTION validate_opening_hours();

-- ============================================================
-- normalize_phone_number: E.164 normalization
-- ============================================================
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
            RAISE EXCEPTION 'Nieprawidłowy format numeru telefonu. Wymagany E.164.';
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
