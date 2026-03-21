-- Slug fallback triggers - generate slug ONLY when NULL
-- SaveChangesAsync (C#) sets slug first -> trigger won't overwrite
-- Python generator sets slug inline -> trigger won't fire
-- Manual SQL INSERT without slug -> trigger generates it
-- Depends on: generate_slug() from Functions/generate_slug.sql

-- Restaurant slug
CREATE OR REPLACE FUNCTION trg_generate_restaurant_slug()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.slug IS NULL OR NEW.slug = '' THEN
        NEW.slug := generate_slug(NEW.restaurant_name);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_slug_restaurant ON restaurants;
CREATE TRIGGER trg_slug_restaurant
    BEFORE INSERT ON restaurants
    FOR EACH ROW EXECUTE FUNCTION trg_generate_restaurant_slug();

-- Dish slug
CREATE OR REPLACE FUNCTION trg_generate_dish_slug()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.slug IS NULL OR NEW.slug = '' THEN
        NEW.slug := generate_slug(NEW.dish_name);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_slug_dish ON dishes;
CREATE TRIGGER trg_slug_dish
    BEFORE INSERT ON dishes
    FOR EACH ROW EXECUTE FUNCTION trg_generate_dish_slug();
