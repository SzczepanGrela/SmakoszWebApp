-- generate_slug(text) - converts text to URL-friendly slug
-- Depends on: f_unaccent() from f_unaccent.sql
-- Used by: slug triggers (fallback when slug IS NULL)

CREATE OR REPLACE FUNCTION generate_slug(input_text TEXT)
RETURNS TEXT AS $$
BEGIN
    RETURN LOWER(
        REGEXP_REPLACE(
            REGEXP_REPLACE(
                f_unaccent(TRIM(input_text)),
                '[^a-zA-Z0-9\s-]', '', 'g'
            ),
            '\s+', '-', 'g'
        )
    );
END;
$$ LANGUAGE plpgsql IMMUTABLE;
