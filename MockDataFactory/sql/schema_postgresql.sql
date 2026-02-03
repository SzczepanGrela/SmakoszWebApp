-- ========================================
-- SMAKOSZ WEB APP - COMPLETE DATABASE SCHEMA (PostgreSQL)
-- Migrated from SQL Server to PostgreSQL
-- Updated schema with all required tables and attributes
-- No orders functionality - review-focused platform
-- ========================================

-- ========================================
-- DROP EXISTING TABLES (for clean rebuild)
-- ========================================
-- New tables added to DROP list
DROP TABLE IF EXISTS user_variant_preferences CASCADE;
DROP TABLE IF EXISTS auth_tokens CASCADE;
DROP TABLE IF EXISTS security_logs CASCADE;
DROP TABLE IF EXISTS email_logs CASCADE;
DROP TABLE IF EXISTS search_history CASCADE;
DROP TABLE IF EXISTS data_correction_requests CASCADE;
DROP TABLE IF EXISTS user_follows CASCADE;
DROP TABLE IF EXISTS notifications CASCADE;
DROP TABLE IF EXISTS review_likes CASCADE;
DROP TABLE IF EXISTS restaurant_opening_hours CASCADE;

-- Existing tables
DROP TABLE IF EXISTS admin_review_comments CASCADE;
DROP TABLE IF EXISTS admin_review_photos CASCADE;
DROP TABLE IF EXISTS ai_review_comments CASCADE;
DROP TABLE IF EXISTS ai_review_photos CASCADE;
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
DROP VIEW IF EXISTS moderation_queue_stats;
DROP VIEW IF EXISTS admin_review_pending;
DROP VIEW IF EXISTS ai_review_pending;
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
    cuisine_type VARCHAR(100), -- Renamed from public_cuisine_theme
    price_level INT, -- 1 to 4
    address VARCHAR(200),
    latitude NUMERIC(10,7),
    longitude NUMERIC(10,7),
    phone VARCHAR(20),
    website VARCHAR(200),
    description VARCHAR(1000),
    image_url VARCHAR(500),
    status VARCHAR(50) DEFAULT 'active', -- 'active', 'renovation', 'closed_permanently', 'suspended'
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
    secret_archetype_modifiers JSONB DEFAULT '{}',

    -- Additional attributes for menu generation (SECRET - only for generation)
    secret_menu_blueprint VARCHAR(100),

    CONSTRAINT chk_price_level CHECK (price_level BETWEEN 1 AND 4),
    CONSTRAINT chk_restaurant_status CHECK (status IN ('active', 'renovation', 'closed_permanently', 'suspended'))
);

CREATE INDEX idx_restaurants_city ON restaurants(city_id);
CREATE INDEX idx_restaurants_cuisine ON restaurants(cuisine_type);
CREATE INDEX idx_restaurants_status ON restaurants(status);
CREATE INDEX idx_restaurants_price ON restaurants(price_level); -- Optimized filtering

-- ========================================
-- 2a. RESTAURANT OPENING HOURS (New)
-- ========================================
CREATE TABLE restaurant_opening_hours (
    hours_id SERIAL PRIMARY KEY,
    restaurant_id INT NOT NULL,
    day_of_week INT NOT NULL, -- 0=Sunday, 1=Monday, ..., 6=Saturday
    open_time TIME NOT NULL,
    close_time TIME NOT NULL,
    is_closed BOOLEAN DEFAULT FALSE,

    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    CONSTRAINT chk_day_of_week CHECK (day_of_week BETWEEN 0 AND 6)
);

CREATE INDEX idx_opening_hours_restaurant ON restaurant_opening_hours(restaurant_id);

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
    price NUMERIC(10, 2), -- Renamed from public_price
    description VARCHAR(500),
    menu_section VARCHAR(50), -- NEW: e.g., 'Przystawki', 'Dania Główne'
    is_vegan BOOLEAN DEFAULT FALSE, -- Denormalized for performance
    is_spicy BOOLEAN DEFAULT FALSE, -- Denormalized for performance
    is_available BOOLEAN DEFAULT TRUE,
    calories INT NULL,
    image_url VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Secret Simulation Attributes
    secret_base_price NUMERIC(10, 2),
    secret_characteristics_vector JSONB NOT NULL DEFAULT '{}',
    secret_weights_vector JSONB DEFAULT NULL,
    secret_quality DOUBLE PRECISION,

    -- Additional attributes for CF model
    secret_archetype VARCHAR(100),
    secret_variant_name VARCHAR(100),  -- NEW: Abstract variant key (e.g., 'Margherita')
    secret_popularity_factor DOUBLE PRECISION,

    -- Calculated average (updated by scheduled function)
    avg_rating DOUBLE PRECISION NULL
);

CREATE INDEX idx_dishes_restaurant ON dishes(restaurant_id);
CREATE INDEX idx_dishes_available ON dishes(is_available);
CREATE INDEX idx_dishes_avg_rating ON dishes(avg_rating DESC NULLS LAST);
CREATE INDEX idx_dishes_vegan ON dishes(is_vegan) WHERE is_vegan = TRUE;

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
    email_verified BOOLEAN DEFAULT FALSE, -- NEW: Email verification status
    newsletter_consent BOOLEAN DEFAULT FALSE, -- NEW: Marketing consent
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(100),
    phone VARCHAR(20),
    avatar_url VARCHAR(500),
    date_of_birth DATE,
    account_created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE,
    is_banned BOOLEAN DEFAULT FALSE, -- Admin ban
    is_deleted BOOLEAN DEFAULT FALSE, -- User self-delete (Soft delete)
    deleted_at TIMESTAMP, -- Timestamp for soft delete cleanup

    -- RBAC: Role-Based Access Control
    role VARCHAR(20) NOT NULL DEFAULT 'user',

    -- Secret Simulation Attributes (for CF model)
    secret_total_review_count INT,
    secret_travel_propensity DOUBLE PRECISION,
    secret_enjoyed_archetypes JSONB, -- NEW: Stores affinity for archetypes e.g., {"Pizza": 0.9, "Sushi": 0.2}
    secret_chance_dine_random DOUBLE PRECISION,
    secret_chance_pick_random_dish DOUBLE PRECISION,
    secret_cross_impact_factor DOUBLE PRECISION,
    secret_mood_propensity DOUBLE PRECISION,
    secret_is_influencer BOOLEAN DEFAULT FALSE, -- NEW: Determines popularity in social graph
    secret_rating_baseline DOUBLE PRECISION DEFAULT 6.0, -- NEW: User's baseline rating tendency (Critic=4.0, Realist=6.0, Fan=7.5)
    secret_characteristics_vector JSONB NOT NULL DEFAULT '{}',
    secret_ingredient_preferences JSONB,
    secret_cleanliness_preference JSONB,
    secret_preferred_ambiance VARCHAR(100),

    -- RBAC: Role validation constraint
    CONSTRAINT chk_user_role CHECK (role IN ('user', 'admin', 'moderator'))
);

CREATE INDEX idx_users_city ON users(home_city_id);
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_role ON users(role);  -- RBAC: For role-based queries
CREATE INDEX idx_users_influencer ON users(secret_is_influencer) WHERE secret_is_influencer = TRUE; -- Optimize for Phase 6

-- GIN index for JSONB field (for efficient querying)
CREATE INDEX idx_users_characteristics_gin ON users USING GIN (secret_characteristics_vector);

-- ========================================
-- 10a. USER_VARIANT_PREFERENCES (New - Materialized Preferences)
-- ========================================
-- This table stores pre-calculated contextual preference vectors
-- for each user-variant combination. This optimization allows:
-- 1. Fast lookup during review generation (no recalculation needed)
-- 2. Consistent preference application across all dishes of a variant
-- 3. Potential for A/B testing different preference algorithms
CREATE TABLE user_variant_preferences (
    preference_id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    variant_name VARCHAR(100) NOT NULL,    -- Abstract variant key (e.g., 'Margherita')
    archetype_name VARCHAR(50) NOT NULL,   -- For easier querying/grouping (e.g., 'Pizza')
    preference_vector JSONB NOT NULL,      -- Calculated contextual target vector for this user/variant
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Prevent duplicate entries for same user-variant combination
    CONSTRAINT uq_user_variant UNIQUE (user_id, variant_name)
);

-- Composite index for fast lookups during review generation
CREATE INDEX idx_user_variant_preferences_lookup ON user_variant_preferences(user_id, variant_name);

-- Index for archetype-level analysis
CREATE INDEX idx_user_variant_preferences_archetype ON user_variant_preferences(archetype_name);

-- GIN index for JSONB preference vector (for advanced queries)
CREATE INDEX idx_user_variant_preferences_vector_gin ON user_variant_preferences USING GIN (preference_vector);

-- ========================================
-- 11a. AUTH TOKENS (New - Security)
-- ========================================
CREATE TABLE auth_tokens (
    token_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    token_type VARCHAR(20) NOT NULL, -- 'verification', 'password_reset'
    token_hash VARCHAR(255) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_used BOOLEAN DEFAULT FALSE,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE INDEX idx_auth_tokens_hash ON auth_tokens(token_hash);
CREATE INDEX idx_auth_tokens_user ON auth_tokens(user_id);

-- ========================================
-- 11b. SECURITY LOGS (New - Audit)
-- ========================================
CREATE TABLE security_logs (
    log_id SERIAL PRIMARY KEY,
    user_id INT,
    event_type VARCHAR(50) NOT NULL, -- 'login', 'password_change', 'failed_login'
    ip_address VARCHAR(45), -- IPv4 or IPv6
    user_agent VARCHAR(500),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE SET NULL
);

CREATE INDEX idx_security_logs_user ON security_logs(user_id, created_at DESC);

-- ========================================
-- 11c. EMAIL LOGS (New - Communication History)
-- ========================================
CREATE TABLE email_logs (
    email_log_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE SET NULL,
    recipient_email VARCHAR(100) NOT NULL,
    subject VARCHAR(200) NOT NULL,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(20) DEFAULT 'sent' -- 'sent', 'failed'
);

CREATE INDEX idx_email_logs_user ON email_logs(user_id, sent_at DESC);

-- ========================================
-- 11d. SEARCH HISTORY (New - User Activity)
-- ========================================
CREATE TABLE search_history (
    search_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE CASCADE,
    search_query VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_search_history_user ON search_history(user_id, created_at DESC);

-- ========================================
-- 11e. DATA CORRECTION REQUESTS (New - Crowdsourcing)
-- ========================================
CREATE TABLE data_correction_requests (
    request_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE SET NULL,
    restaurant_id INT REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    issue_type VARCHAR(50) NOT NULL, -- 'closed', 'hours', 'address', 'menu'
    description VARCHAR(500),
    status VARCHAR(20) DEFAULT 'pending', -- 'pending', 'resolved', 'rejected'
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_correction_requests_restaurant ON data_correction_requests(restaurant_id);

-- ========================================
-- 11f. USER FOLLOWS (New - Social)
-- ========================================
CREATE TABLE user_follows (
    follower_id INT NOT NULL,
    followed_user_id INT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (follower_id, followed_user_id),
    FOREIGN KEY (follower_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (followed_user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    CONSTRAINT chk_no_self_follow CHECK (follower_id <> followed_user_id)
);

CREATE INDEX idx_user_follows_follower ON user_follows(follower_id);
CREATE INDEX idx_user_follows_followed ON user_follows(followed_user_id);

-- ========================================
-- 11d. NOTIFICATIONS (New - System)
-- ========================================
CREATE TABLE notifications (
    notification_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL,
    type VARCHAR(50) NOT NULL, -- 'like', 'follow', 'system', 'promo'
    title VARCHAR(100),
    message VARCHAR(255),
    reference_id INT, -- ID of related entity (e.g. review_id, user_id)
    reference_type VARCHAR(50), -- 'review', 'user', 'restaurant'
    priority INT DEFAULT 1, -- 1=Low, 2=Normal, 3=High
    is_read BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE INDEX idx_notifications_user ON notifications(user_id, created_at DESC);
CREATE INDEX idx_notifications_unread ON notifications(user_id) WHERE is_read = FALSE;

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
    
    -- Soft Delete
    is_deleted BOOLEAN DEFAULT FALSE,
    deleted_at TIMESTAMP NULL,

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
-- 12a. REVIEW LIKES (New - Social)
-- ========================================
CREATE TABLE review_likes (
    user_id INT NOT NULL,
    review_id INT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (user_id, review_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (review_id) REFERENCES reviews(review_id) ON DELETE CASCADE
);

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
-- MODERATION REVIEW QUEUES
-- ========================================

-- AI Review: Photos
CREATE TABLE ai_review_photos (
    queue_id SERIAL PRIMARY KEY,
    user_photo_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(30) DEFAULT 'pending',
    processed_at TIMESTAMP NULL,

    FOREIGN KEY (user_photo_id) REFERENCES user_photos(user_photo_id) ON DELETE CASCADE,
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE INDEX idx_ai_review_photos_status ON ai_review_photos(status);
CREATE INDEX idx_ai_review_photos_submitted ON ai_review_photos(submitted_at);

-- AI Review: Comments
CREATE TABLE ai_review_comments (
    queue_id SERIAL PRIMARY KEY,
    pending_comment_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(30) DEFAULT 'pending',
    processed_at TIMESTAMP NULL,

    FOREIGN KEY (pending_comment_id) REFERENCES pending_comments(pending_comment_id) ON DELETE CASCADE,
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE INDEX idx_ai_review_comments_status ON ai_review_comments(status);
CREATE INDEX idx_ai_review_comments_submitted ON ai_review_comments(submitted_at);

-- Admin Review: Photos
CREATE TABLE admin_review_photos (
    queue_id SERIAL PRIMARY KEY,
    user_photo_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(30) DEFAULT 'pending',

    -- Admin Review Fields
    reviewed_by_admin_id INT NULL,
    reviewed_at TIMESTAMP NULL,
    admin_decision VARCHAR(20) NULL,
    admin_notes TEXT NULL,

    FOREIGN KEY (user_photo_id) REFERENCES user_photos(user_photo_id) ON DELETE CASCADE,
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (reviewed_by_admin_id) REFERENCES users(user_id) ON DELETE SET NULL,

    CONSTRAINT chk_admin_photo_decision CHECK (admin_decision IN ('approved', 'rejected'))
);

CREATE INDEX idx_admin_review_photos_status ON admin_review_photos(status);
CREATE INDEX idx_admin_review_photos_submitted ON admin_review_photos(submitted_at);
CREATE INDEX idx_admin_review_photos_pending ON admin_review_photos(status) WHERE status = 'pending';

-- Admin Review: Comments
CREATE TABLE admin_review_comments (
    queue_id SERIAL PRIMARY KEY,
    pending_comment_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(30) DEFAULT 'pending',

    -- Admin Review Fields
    reviewed_by_admin_id INT NULL,
    reviewed_at TIMESTAMP NULL,
    admin_decision VARCHAR(20) NULL,
    admin_notes TEXT NULL,

    FOREIGN KEY (pending_comment_id) REFERENCES pending_comments(pending_comment_id) ON DELETE CASCADE,
    FOREIGN KEY (submitted_by_user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (reviewed_by_admin_id) REFERENCES users(user_id) ON DELETE SET NULL,

    CONSTRAINT chk_admin_comment_decision CHECK (admin_decision IN ('approved', 'rejected'))
);

CREATE INDEX idx_admin_review_comments_status ON admin_review_comments(status);
CREATE INDEX idx_admin_review_comments_submitted ON admin_review_comments(submitted_at);
CREATE INDEX idx_admin_review_comments_pending ON admin_review_comments(status) WHERE status = 'pending';

-- ========================================
-- VIEWS
-- ========================================

-- View: Active dishes with their restaurant info
CREATE VIEW vw_active_dishes AS
SELECT
    d.dish_id,
    d.dish_name,
    d.price, -- Renamed from public_price
    d.description,
    d.image_url,
    d.menu_section, -- NEW: Added menu_section
    r.restaurant_id,
    r.restaurant_name,
    r.cuisine_type, -- Renamed from public_cuisine_theme
    c.city_name
FROM dishes d
JOIN restaurants r ON d.restaurant_id = r.restaurant_id
JOIN cities c ON r.city_id = c.city_id
WHERE d.is_available = TRUE AND r.status = 'active';

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

-- Moderation View: AI Review Pending
CREATE VIEW ai_review_pending AS
SELECT
    'photo' as content_type,
    queue_id,
    user_photo_id as content_id,
    submitted_by_user_id,
    submitted_at,
    status
FROM ai_review_photos
WHERE status = 'pending'

UNION ALL

SELECT
    'comment' as content_type,
    queue_id,
    pending_comment_id as content_id,
    submitted_by_user_id,
    submitted_at,
    status
FROM ai_review_comments
WHERE status = 'pending'

ORDER BY submitted_at ASC;

-- Moderation View: Admin Review Pending
CREATE VIEW admin_review_pending AS
SELECT
    'photo' as content_type,
    queue_id,
    user_photo_id as content_id,
    submitted_by_user_id,
    submitted_at,
    status,
    reviewed_by_admin_id,
    reviewed_at,
    admin_decision,
    admin_notes
FROM admin_review_photos
WHERE status = 'pending'

UNION ALL

SELECT
    'comment' as content_type,
    queue_id,
    pending_comment_id as content_id,
    submitted_by_user_id,
    submitted_at,
    status,
    reviewed_by_admin_id,
    reviewed_at,
    admin_decision,
    admin_notes
FROM admin_review_comments
WHERE status = 'pending'

ORDER BY submitted_at ASC;

-- Moderation View: Queue Statistics
CREATE VIEW moderation_queue_stats AS
SELECT
    'ai_photos' as queue_name,
    COUNT(*) as total_items,
    COUNT(*) FILTER (WHERE status = 'pending') as pending_count,
    COUNT(*) FILTER (WHERE status = 'processed') as processed_count
FROM ai_review_photos

UNION ALL

SELECT
    'ai_comments' as queue_name,
    COUNT(*) as total_items,
    COUNT(*) FILTER (WHERE status = 'pending') as pending_count,
    COUNT(*) FILTER (WHERE status = 'processed') as processed_count
FROM ai_review_comments

UNION ALL

SELECT
    'admin_photos' as queue_name,
    COUNT(*) as total_items,
    COUNT(*) FILTER (WHERE status = 'pending') as pending_count,
    COUNT(*) FILTER (WHERE admin_decision = 'approved') as approved_count
FROM admin_review_photos

UNION ALL

SELECT
    'admin_comments' as queue_name,
    COUNT(*) as total_items,
    COUNT(*) FILTER (WHERE status = 'pending') as pending_count,
    COUNT(*) FILTER (WHERE admin_decision = 'approved') as approved_count
FROM admin_review_comments;

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
    RAISE NOTICE 'Total tables: 25';
    RAISE NOTICE 'Main entities: cities, restaurants, dishes, ingredients, users, reviews';
    RAISE NOTICE 'New Features: restaurant_opening_hours, notifications, review_likes, user_follows';
    RAISE NOTICE 'Supporting: photos, user_photos, tags, saved_dishes, reports';
    RAISE NOTICE 'Moderation: pending_user_photos, pending_comments';
    RAISE NOTICE 'Review Queues: ai_review_photos, ai_review_comments, admin_review_photos, admin_review_comments';
    RAISE NOTICE '';
    RAISE NOTICE 'IMPORTANT: After data generation, run: SELECT update_average_ratings();';
    RAISE NOTICE 'OPTIONAL: Use pg_cron extension for automatic updates every 10 minutes';
END $$;