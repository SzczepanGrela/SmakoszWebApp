-- ========================================
-- SMAKOSZ WEB APP - COMPLETE DATABASE SCHEMA (PostgreSQL)
-- Migrated from SQL Server to PostgreSQL
-- Updated schema with all required tables and attributes
-- No orders functionality - review-focused platform
-- ========================================

-- ========================================
-- DROP EXISTING TABLES (for clean rebuild)
-- ========================================
DROP TABLE IF EXISTS pending_comments CASCADE;
DROP TABLE IF EXISTS pending_user_photos CASCADE;
DROP TABLE IF EXISTS reports CASCADE;
DROP TABLE IF EXISTS user_photos CASCADE;
DROP TABLE IF EXISTS saved_dishes CASCADE;
DROP TABLE IF EXISTS restaurant_tags CASCADE;
DROP TABLE IF EXISTS dish_tags CASCADE;
DROP TABLE IF EXISTS ingredient_restrictions CASCADE;
DROP TABLE IF EXISTS photos CASCADE;
DROP TABLE IF EXISTS tags CASCADE;
DROP TABLE IF EXISTS reviews CASCADE;
DROP TABLE IF EXISTS dish_ingredients_link CASCADE;
DROP TABLE IF EXISTS dishes CASCADE;
DROP TABLE IF EXISTS restaurants CASCADE;
DROP TABLE IF EXISTS users CASCADE;
DROP TABLE IF EXISTS ingredients CASCADE;
DROP TABLE IF EXISTS cities CASCADE;

-- Drop views if exist
DROP VIEW IF EXISTS vw_active_dishes;
DROP VIEW IF EXISTS vw_user_stats;

-- ========================================
-- 1. CITIES (Public)
-- ========================================
CREATE TABLE cities (
    city_id SERIAL PRIMARY KEY,
    city_name VARCHAR(100) NOT NULL UNIQUE
);

-- ========================================
-- 2. RESTAURANTS (Public + Secret)
-- ========================================
CREATE TABLE restaurants (
    restaurant_id SERIAL PRIMARY KEY,
    city_id INT REFERENCES cities(city_id),
    restaurant_name VARCHAR(255) NOT NULL UNIQUE,

    -- Public attributes
    public_cuisine_theme VARCHAR(100),
    public_price_range VARCHAR(5),
    address VARCHAR(200),
    latitude NUMERIC(10,7),
    longitude NUMERIC(10,7),
    phone VARCHAR(20),
    website VARCHAR(200),
    description VARCHAR(1000),
    image_url VARCHAR(500),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Calculated Averages (updated by scheduled function)
    avg_service DOUBLE PRECISION NULL,
    avg_cleanliness DOUBLE PRECISION NULL,
    avg_ambiance DOUBLE PRECISION NULL,
    avg_food_score DOUBLE PRECISION NULL,

    -- Secret Simulation Attributes (for CF model and generation)
    secret_price_multiplier DOUBLE PRECISION,
    secret_overall_food_quality DOUBLE PRECISION,
    secret_service_quality DOUBLE PRECISION,
    secret_cleanliness_score DOUBLE PRECISION,
    secret_ambiance_type VARCHAR(100),
    secret_ambiance_quality DOUBLE PRECISION,

    -- Additional attributes for menu generation
    menu_blueprint VARCHAR(100),
    theme VARCHAR(100)
);

CREATE INDEX idx_restaurants_city ON restaurants(city_id);
CREATE INDEX idx_restaurants_cuisine ON restaurants(public_cuisine_theme);
CREATE INDEX idx_restaurants_active ON restaurants(is_active);

-- ========================================
-- 3. INGREDIENTS (Public)
-- ========================================
CREATE TABLE ingredients (
    ingredient_id SERIAL PRIMARY KEY,
    ingredient_name VARCHAR(100) NOT NULL UNIQUE,
    is_allergen BOOLEAN DEFAULT FALSE
);

-- ========================================
-- 4. INGREDIENT RESTRICTIONS
-- Links ingredients to dietary restriction types
-- ========================================
CREATE TABLE ingredient_restrictions (
    ingredient_id INT NOT NULL,
    restriction_type VARCHAR(50) NOT NULL,
    -- restriction_type values: 'vegetarian', 'vegan', 'gluten-free', 'lactose-free', 'nut-allergy', 'halal', 'kosher', 'shellfish-allergy'

    PRIMARY KEY (ingredient_id, restriction_type),
    FOREIGN KEY (ingredient_id) REFERENCES ingredients(ingredient_id) ON DELETE CASCADE
);

-- ========================================
-- 5. DISHES (Public + Secret)
-- ========================================
CREATE TABLE dishes (
    dish_id SERIAL PRIMARY KEY,
    restaurant_id INT REFERENCES restaurants(restaurant_id),
    dish_name VARCHAR(255) NOT NULL,

    -- Public attributes
    public_price NUMERIC(10, 2),
    description VARCHAR(500),
    is_available BOOLEAN DEFAULT TRUE,
    calories INT NULL,
    image_url VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Secret Simulation Attributes
    secret_base_price NUMERIC(10, 2),
    secret_price_to_default_ratio DOUBLE PRECISION,
    secret_quality DOUBLE PRECISION,
    secret_spiciness DOUBLE PRECISION,

    -- Additional attributes for CF model
    archetype VARCHAR(100),
    secret_richness DOUBLE PRECISION,
    secret_texture_score DOUBLE PRECISION,
    popularity_factor DOUBLE PRECISION,

    -- Calculated average (updated by scheduled function)
    avg_rating DOUBLE PRECISION NULL
);

CREATE INDEX idx_dishes_restaurant ON dishes(restaurant_id);
CREATE INDEX idx_dishes_available ON dishes(is_available);
CREATE INDEX idx_dishes_avg_rating ON dishes(avg_rating DESC NULLS LAST);

-- ========================================
-- 6. DISH_INGREDIENTS_LINK (Public)
-- Many-to-many relationship
-- ========================================
CREATE TABLE dish_ingredients_link (
    dish_id INT,
    ingredient_id INT,

    PRIMARY KEY (dish_id, ingredient_id),
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (ingredient_id) REFERENCES ingredients(ingredient_id)
);

-- ========================================
-- 7. TAGS
-- Categorization system for dishes and restaurants
-- ========================================
CREATE TABLE tags (
    tag_id SERIAL PRIMARY KEY,
    tag_name VARCHAR(50) NOT NULL UNIQUE,
    tag_category VARCHAR(30) NOT NULL,
    -- Categories: 'dietary', 'spice', 'cuisine', 'mood', 'occasion', 'meal_type', 'feature'
    display_color VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_tags_category ON tags(tag_category);

-- ========================================
-- 8. DISH_TAGS (Many-to-many)
-- ========================================
CREATE TABLE dish_tags (
    dish_id INT,
    tag_id INT,

    PRIMARY KEY (dish_id, tag_id),
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(tag_id) ON DELETE CASCADE
);

-- ========================================
-- 9. RESTAURANT_TAGS (Many-to-many)
-- ========================================
CREATE TABLE restaurant_tags (
    restaurant_id INT,
    tag_id INT,

    PRIMARY KEY (restaurant_id, tag_id),
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(tag_id) ON DELETE CASCADE
);

-- ========================================
-- 10. PHOTOS (System/Display Photos)
-- For dish and restaurant display images (pre-curated)
-- ========================================
CREATE TABLE photos (
    photo_id SERIAL PRIMARY KEY,
    entity_type VARCHAR(20) NOT NULL,
    entity_id INT NOT NULL,
    photo_url VARCHAR(500) NOT NULL,
    is_primary BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_photos_entity_type CHECK (entity_type IN ('dish', 'restaurant'))
);

CREATE INDEX idx_photos_entity ON photos(entity_type, entity_id);

-- ========================================
-- 11. USERS (Public + Secret)
-- ========================================
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL UNIQUE,
    home_city_id INT REFERENCES cities(city_id),

    -- Public profile attributes
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100),
    phone VARCHAR(20),
    avatar_url VARCHAR(500),
    date_of_birth DATE,
    account_created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE,

    -- Secret Simulation Attributes (for CF model)
    secret_total_review_count INT,
    secret_travel_propensity DOUBLE PRECISION,
    secret_chance_dine_random DOUBLE PRECISION,
    secret_chance_pick_random_dish DOUBLE PRECISION,
    secret_chance_to_update_rating DOUBLE PRECISION,
    secret_cross_impact_factor DOUBLE PRECISION,
    secret_mood_propensity DOUBLE PRECISION,
    secret_price_preference_range VARCHAR(50),
    secret_price_tolerance_above DOUBLE PRECISION,
    secret_price_tolerance_below DOUBLE PRECISION,
    secret_enjoyed_restaurant_themes JSONB,
    secret_enjoyed_archetypes JSONB,
    secret_enjoyed_variants JSONB,
    secret_ingredient_preferences JSONB,
    secret_cleanliness_preference JSONB,
    secret_preferred_ambiance VARCHAR(100),

    -- Additional preferences for CF model
    secret_spice_preference DOUBLE PRECISION,
    secret_richness_preference DOUBLE PRECISION,
    secret_texture_preference DOUBLE PRECISION
);

CREATE INDEX idx_users_city ON users(home_city_id);
CREATE INDEX idx_users_email ON users(email);

-- GIN indexes for JSONB fields (for efficient querying)
CREATE INDEX idx_users_themes_gin ON users USING GIN (secret_enjoyed_restaurant_themes);
CREATE INDEX idx_users_archetypes_gin ON users USING GIN (secret_enjoyed_archetypes);

-- ========================================
-- 12. REVIEWS (Unified Rating Table)
-- ========================================
CREATE TABLE reviews (
    review_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    restaurant_id INT NOT NULL,
    dish_id INT NOT NULL,
    review_date TIMESTAMP NOT NULL,

    -- Ratings (1-10 scale)
    dish_rating INT NOT NULL,
    service_rating INT NULL,
    cleanliness_rating INT NULL,
    ambiance_rating INT NULL,

    -- Review content
    review_title VARCHAR(100),
    review_comment VARCHAR(2000),
    helpful_count INT DEFAULT 0,
    is_approved BOOLEAN DEFAULT TRUE,

    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id),
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id),

    -- Rating range constraints
    CONSTRAINT chk_dish_rating_range CHECK (dish_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_service_rating_range CHECK (service_rating IS NULL OR service_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_cleanliness_rating_range CHECK (cleanliness_rating IS NULL OR cleanliness_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_ambiance_rating_range CHECK (ambiance_rating IS NULL OR ambiance_rating BETWEEN 1 AND 10)
);

CREATE INDEX idx_reviews_user ON reviews(user_id, review_date DESC);
CREATE INDEX idx_reviews_dish ON reviews(dish_id, review_date DESC);
CREATE INDEX idx_reviews_restaurant ON reviews(restaurant_id, review_date DESC);

-- ========================================
-- 13. USER_PHOTOS (Review Photos)
-- User-uploaded photos attached to reviews
-- ========================================
CREATE TABLE user_photos (
    user_photo_id SERIAL PRIMARY KEY,
    review_id INT NOT NULL,
    uploaded_by_user_id INT NOT NULL,
    photo_url VARCHAR(500) NOT NULL,
    upload_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_approved BOOLEAN DEFAULT FALSE,

    FOREIGN KEY (review_id) REFERENCES reviews(review_id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by_user_id) REFERENCES users(user_id)
);

CREATE INDEX idx_user_photos_review ON user_photos(review_id);
CREATE INDEX idx_user_photos_pending ON user_photos(is_approved);

-- ========================================
-- 14. SAVED_DISHES (User Favorites/Bookmarks)
-- ========================================
CREATE TABLE saved_dishes (
    user_id INT,
    dish_id INT,
    saved_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (user_id, dish_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE
);

CREATE INDEX idx_saved_dishes_user ON saved_dishes(user_id, saved_at DESC);

-- ========================================
-- 15. PENDING_USER_PHOTOS (Moderation Queue)
-- For admin review of user-uploaded photos
-- ========================================
CREATE TABLE pending_user_photos (
    pending_photo_id SERIAL PRIMARY KEY,
    user_photo_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) DEFAULT 'pending',
    reviewed_at TIMESTAMP NULL,
    reviewed_by_admin_id INT NULL,
    rejection_reason VARCHAR(200) NULL,

    FOREIGN KEY (user_photo_id) REFERENCES user_photos(user_photo_id),
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id)
);

CREATE INDEX idx_pending_photos_status ON pending_user_photos(status, submitted_at);

-- ========================================
-- 16. PENDING_COMMENTS (Moderation Queue)
-- For admin review of potentially problematic comments
-- ========================================
CREATE TABLE pending_comments (
    pending_comment_id SERIAL PRIMARY KEY,
    review_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    comment_text VARCHAR(2000) NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) DEFAULT 'pending',
    reviewed_at TIMESTAMP NULL,
    reviewed_by_admin_id INT NULL,
    rejection_reason VARCHAR(200) NULL,
    flagged_for_keywords BOOLEAN DEFAULT FALSE,

    FOREIGN KEY (review_id) REFERENCES reviews(review_id),
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id)
);

CREATE INDEX idx_pending_comments_status ON pending_comments(status, submitted_at);

-- ========================================
-- 17. REPORTS (Abuse/Content Reports)
-- Users can report reviews, photos, or other users
-- ========================================
CREATE TABLE reports (
    report_id SERIAL PRIMARY KEY,
    reporter_user_id INT NOT NULL,
    entity_type VARCHAR(20) NOT NULL,
    entity_id INT NOT NULL,
    reason VARCHAR(100) NOT NULL,
    description VARCHAR(500),
    status VARCHAR(20) DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    resolved_at TIMESTAMP NULL,
    resolved_by_admin_id INT NULL,

    FOREIGN KEY (reporter_user_id) REFERENCES users(user_id),
    CONSTRAINT chk_reports_entity_type CHECK (entity_type IN ('review', 'user_photo', 'user'))
);

CREATE INDEX idx_reports_status ON reports(status, created_at DESC);

-- ========================================
-- VIEWS
-- ========================================

-- View: Active dishes with their restaurant info
CREATE VIEW vw_active_dishes AS
SELECT
    d.dish_id,
    d.dish_name,
    d.public_price,
    d.description,
    d.image_url,
    r.restaurant_id,
    r.restaurant_name,
    r.public_cuisine_theme,
    c.city_name
FROM dishes d
JOIN restaurants r ON d.restaurant_id = r.restaurant_id
JOIN cities c ON r.city_id = c.city_id
WHERE d.is_available = TRUE AND r.is_active = TRUE;

-- View: User review statistics
CREATE VIEW vw_user_stats AS
SELECT
    u.user_id,
    u.username,
    COUNT(DISTINCT r.review_id) AS total_reviews,
    COUNT(DISTINCT up.user_photo_id) AS total_photos,
    AVG(r.dish_rating::DOUBLE PRECISION) AS avg_rating_given
FROM users u
LEFT JOIN reviews r ON u.user_id = r.user_id
LEFT JOIN user_photos up ON u.user_id = up.uploaded_by_user_id
GROUP BY u.user_id, u.username;

-- ========================================
-- FUNCTION: Update Average Ratings
-- ========================================
-- This function updates avg_rating for Dishes and avg_* columns for Restaurants
-- Run this manually or via pg_cron (recommended: every 10 minutes)

CREATE OR REPLACE FUNCTION update_average_ratings()
RETURNS void AS $$
BEGIN
    -- 1. Update average ratings for DISHES
    UPDATE dishes d
    SET avg_rating = sub.avg_dish_rating
    FROM (
        SELECT
            dish_id,
            AVG(dish_rating::DOUBLE PRECISION) AS avg_dish_rating
        FROM reviews
        WHERE dish_rating IS NOT NULL
        GROUP BY dish_id
    ) sub
    WHERE d.dish_id = sub.dish_id;

    -- 2. Update average ratings for RESTAURANTS
    UPDATE restaurants r
    SET
        avg_service = sub.avg_service_rating,
        avg_cleanliness = sub.avg_cleanliness_rating,
        avg_ambiance = sub.avg_ambiance_rating,
        avg_food_score = sub.avg_dish_rating
    FROM (
        SELECT
            restaurant_id,
            AVG(service_rating::DOUBLE PRECISION) AS avg_service_rating,
            AVG(cleanliness_rating::DOUBLE PRECISION) AS avg_cleanliness_rating,
            AVG(ambiance_rating::DOUBLE PRECISION) AS avg_ambiance_rating,
            AVG(dish_rating::DOUBLE PRECISION) AS avg_dish_rating
        FROM reviews
        WHERE
            service_rating IS NOT NULL
            OR cleanliness_rating IS NOT NULL
            OR ambiance_rating IS NOT NULL
            OR dish_rating IS NOT NULL
        GROUP BY restaurant_id
    ) sub
    WHERE r.restaurant_id = sub.restaurant_id;

    RAISE NOTICE 'Average ratings updated successfully at %', NOW();
END;
$$ LANGUAGE plpgsql;

-- ========================================
-- OPTIONAL: pg_cron for scheduled updates
-- ========================================
-- If pg_cron extension is installed, you can schedule the function:
--
-- CREATE EXTENSION IF NOT EXISTS pg_cron;
--
-- SELECT cron.schedule('update-ratings', '*/10 * * * *', 'SELECT update_average_ratings()');

-- ========================================
-- Initial execution (run once after data generation)
-- ========================================
-- SELECT update_average_ratings();

-- ========================================
DO $$
BEGIN
    RAISE NOTICE 'Schema created successfully!';
    RAISE NOTICE 'Total tables: 17';
    RAISE NOTICE 'Main entities: cities, restaurants, dishes, ingredients, users, reviews';
    RAISE NOTICE 'Supporting: photos, user_photos, tags, saved_dishes, reports';
    RAISE NOTICE 'Moderation: pending_user_photos, pending_comments';
    RAISE NOTICE '';
    RAISE NOTICE 'IMPORTANT: After data generation, run: SELECT update_average_ratings();';
    RAISE NOTICE 'OPTIONAL: Use pg_cron extension for automatic updates every 10 minutes';
END $$;
