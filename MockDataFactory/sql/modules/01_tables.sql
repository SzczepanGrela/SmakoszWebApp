-- ========================================
-- SCHEMA: TABLES
-- ========================================

-- 1. CITIES
CREATE TABLE cities (
    city_id SERIAL PRIMARY KEY,
    city_name VARCHAR(100) NOT NULL UNIQUE,
    postal_code_prefix VARCHAR(2),
    created_at TIMESTAMPTZ
);

-- 1B. CUISINE TYPES (Dictionary)
CREATE TABLE cuisine_types (
    cuisine_type_id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,      -- "italian", "polish", "asian"
    display_name VARCHAR(100) NOT NULL,    -- "Włoska", "Polska", "Azjatycka"
    icon VARCHAR(10)                       -- "🍕", "🥟"
);

-- 2. RESTAURANTS
CREATE TABLE restaurants (
    restaurant_id SERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    city_id INT REFERENCES cities(city_id),
    restaurant_name VARCHAR(255) NOT NULL UNIQUE,
    cuisine_type VARCHAR(100),
    price_level INT,
    address VARCHAR(200),
    postal_code VARCHAR(10),
    latitude NUMERIC(10,7),
    longitude NUMERIC(10,7),
    phone VARCHAR(20),
    email VARCHAR(255),
    website VARCHAR(200),
    description VARCHAR(1000),
    slug VARCHAR(255),
    image_url VARCHAR(500),
    image_blurhash VARCHAR(50) DEFAULT NULL,
    status VARCHAR(50) DEFAULT 'active',
    is_verified BOOLEAN DEFAULT FALSE,
    owner_id INT,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    avg_service DOUBLE PRECISION NULL,
    avg_cleanliness DOUBLE PRECISION NULL,
    avg_ambiance DOUBLE PRECISION NULL,
    avg_food_score DOUBLE PRECISION NULL,
    trending_score DECIMAL(10,4),
    secret_price_multiplier DOUBLE PRECISION,
    secret_overall_food_quality DOUBLE PRECISION,
    secret_service_quality DOUBLE PRECISION,
    secret_cleanliness_score DOUBLE PRECISION,
    secret_ambiance_type VARCHAR(100),
    secret_ambiance_quality DOUBLE PRECISION,
    secret_archetype_modifiers JSONB DEFAULT '{}',
    secret_menu_blueprint VARCHAR(100),
    version INT DEFAULT 1, -- Optimistic Locking
    verified_at TIMESTAMPTZ,
    verified_by INT,
    CONSTRAINT chk_price_level CHECK (price_level BETWEEN 1 AND 3),
    CONSTRAINT chk_restaurant_status CHECK (status IN ('pending_verification', 'active', 'renovation', 'closed_permanently', 'suspended')),
    CONSTRAINT restaurants_slug_unique UNIQUE (slug)
);
CREATE INDEX idx_restaurants_slug ON restaurants(slug);
CREATE INDEX idx_restaurants_public_id ON restaurants(public_id);
CREATE INDEX idx_restaurants_city ON restaurants(city_id);
-- High-Performance Listing Index: City + Status + Rating (Active only)
CREATE INDEX idx_restaurants_listing_active 
ON restaurants(city_id, status, avg_food_score DESC NULLS LAST) 
WHERE status NOT IN ('closed_permanently', 'suspended');

CREATE INDEX idx_restaurants_cuisine ON restaurants(cuisine_type);
CREATE INDEX idx_restaurants_status ON restaurants(status);
CREATE INDEX idx_restaurants_verification ON restaurants(is_verified, owner_id);

CREATE TABLE restaurant_opening_hours (
    hours_id SERIAL PRIMARY KEY,
    restaurant_id INT NOT NULL,
    day_of_week INT NOT NULL,
    open_time TIME NOT NULL,
    close_time TIME NOT NULL,
    is_closed BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    CONSTRAINT chk_day_of_week CHECK (day_of_week BETWEEN 1 AND 7) -- ISO 8601: 1=Mon, 7=Sun
);
CREATE INDEX idx_opening_hours_restaurant ON restaurant_opening_hours(restaurant_id);
-- Performance Index: "Open Now" filter
CREATE INDEX idx_opening_hours_filter ON restaurant_opening_hours(day_of_week, open_time, close_time) WHERE is_closed = FALSE;

CREATE TABLE menu_sections (
    section_id SERIAL PRIMARY KEY,
    restaurant_id INT NOT NULL,
    section_name VARCHAR(100) NOT NULL,
    display_order INT DEFAULT 0,
    created_at TIMESTAMPTZ,
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    CONSTRAINT uq_restaurant_section UNIQUE (restaurant_id, section_name)
);
CREATE INDEX idx_menu_sections_restaurant ON menu_sections(restaurant_id, display_order);

-- 3. INGREDIENTS
CREATE TABLE ingredients (
    ingredient_id SERIAL PRIMARY KEY,
    ingredient_name VARCHAR(100) NOT NULL UNIQUE,
    icon_url VARCHAR(500) DEFAULT NULL,
    icon_blurhash VARCHAR(50) DEFAULT NULL,
    is_allergen BOOLEAN DEFAULT FALSE,
    is_vegetarian BOOLEAN DEFAULT TRUE,
    is_vegan BOOLEAN DEFAULT TRUE,
    is_gluten_free BOOLEAN DEFAULT TRUE,
    is_lactose_free BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ
);

CREATE TABLE dish_archetypes (
    archetype_id SERIAL PRIMARY KEY,
    archetype_name VARCHAR(50) NOT NULL UNIQUE, -- e.g. 'Pizza', 'Burger'
    created_at TIMESTAMPTZ
);

CREATE TABLE dish_variants (
    variant_id SERIAL PRIMARY KEY,
    variant_name VARCHAR(100) NOT NULL,
    archetype_id INT NOT NULL REFERENCES dish_archetypes(archetype_id) ON DELETE CASCADE,
    UNIQUE(variant_name, archetype_id)
);

-- 4. DISHES

CREATE TABLE dishes (
    dish_id SERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    restaurant_id INT REFERENCES restaurants(restaurant_id),
    variant_id INT REFERENCES dish_variants(variant_id),
    dish_name VARCHAR(255) NOT NULL,
    price NUMERIC(10, 2),
    description VARCHAR(500),
    slug VARCHAR(255),
    trending_score DECIMAL(10,4),
    is_vegetarian BOOLEAN DEFAULT TRUE,
    is_vegan BOOLEAN DEFAULT FALSE,
    is_gluten_free BOOLEAN DEFAULT TRUE,
    is_lactose_free BOOLEAN DEFAULT TRUE,
    is_spicy BOOLEAN DEFAULT FALSE,
    ingredients_json JSONB DEFAULT '[]',
    is_available BOOLEAN DEFAULT TRUE,
    calories INT NULL,
    image_url VARCHAR(500),
    image_blurhash VARCHAR(50) DEFAULT NULL,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    secret_base_price NUMERIC(10, 2),
    secret_characteristics_vector JSONB NOT NULL DEFAULT '{}',
    secret_penalty_vector JSONB DEFAULT NULL,
    secret_quality DOUBLE PRECISION,
    secret_popularity_factor DOUBLE PRECISION,
    avg_rating DOUBLE PRECISION NULL,
    review_count INT DEFAULT 0,
    CONSTRAINT dishes_slug_unique UNIQUE (slug)
);
CREATE INDEX idx_dishes_slug ON dishes(slug);
CREATE INDEX idx_dishes_trending ON dishes(trending_score DESC NULLS LAST) WHERE trending_score IS NOT NULL;
CREATE INDEX idx_dishes_public_id ON dishes(public_id);
CREATE INDEX idx_dishes_restaurant ON dishes(restaurant_id);
CREATE INDEX idx_dishes_price ON dishes(price); -- Sorting/Filtering by price
CREATE INDEX idx_dishes_available ON dishes(is_available);
CREATE INDEX idx_dishes_avg_rating ON dishes(avg_rating DESC NULLS LAST);
CREATE INDEX idx_dishes_variant ON dishes(variant_id);

CREATE TABLE dish_section_assignments (
    dish_id INT NOT NULL REFERENCES dishes(dish_id) ON DELETE CASCADE,
    section_id INT NOT NULL REFERENCES menu_sections(section_id) ON DELETE CASCADE,
    display_order INT NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (dish_id, section_id)
);

CREATE INDEX idx_dish_sections_section ON dish_section_assignments(section_id, display_order);
CREATE INDEX idx_dish_sections_dish ON dish_section_assignments(dish_id);

COMMENT ON TABLE dish_section_assignments IS 'Relacja M:N między daniami a sekcjami menu. Danie może należeć do wielu sekcji (np. Pizza + Promocje dnia).';

CREATE TABLE dish_ingredients (
    dish_id INT,
    ingredient_id INT,
    PRIMARY KEY (dish_id, ingredient_id),
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (ingredient_id) REFERENCES ingredients(ingredient_id)
);
CREATE INDEX idx_dish_ingredients_ingredient ON dish_ingredients(ingredient_id);

CREATE TABLE tags (
    tag_id SERIAL PRIMARY KEY,
    tag_name VARCHAR(50) NOT NULL UNIQUE,
    category VARCHAR(30) NOT NULL,
    target_entity VARCHAR(20) DEFAULT 'both' CHECK (target_entity IN ('restaurant', 'dish', 'both')),
    display_color VARCHAR(20),
    created_at TIMESTAMPTZ
);
CREATE INDEX idx_tags_category ON tags(category);

CREATE TABLE dish_tags (
    dish_id INT,
    tag_id INT,
    PRIMARY KEY (dish_id, tag_id),
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(tag_id) ON DELETE CASCADE
);

CREATE TABLE restaurant_tags (
    restaurant_id INT,
    tag_id INT,
    PRIMARY KEY (restaurant_id, tag_id),
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES tags(tag_id) ON DELETE CASCADE
);

-- Unified Media Assets Table (replaces photos and user_photos)
CREATE TABLE media_assets (
    asset_id BIGSERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    entity_type VARCHAR(50) NOT NULL,
    entity_id INT NOT NULL,
    url VARCHAR(500) NOT NULL,
    blurhash VARCHAR(50),
    width INT,
    height INT,
    is_primary BOOLEAN DEFAULT FALSE,
    status VARCHAR(20) DEFAULT 'approved',
    uploaded_by INT,
    created_at TIMESTAMPTZ,
    rejection_reason VARCHAR(200),
    ai_nsfw_score DECIMAL(5,4),
    ai_on_topic_score DECIMAL(5,4),
    ai_verdict VARCHAR(20),
    ai_model_version VARCHAR(50),
    ai_processed_at TIMESTAMPTZ,
    credit_text VARCHAR(100), -- Attribution for Unsplash images (e.g. "John Doe / Unsplash")
    version INT DEFAULT 1, -- Optimistic Locking
    CONSTRAINT chk_media_entity_type CHECK (entity_type IN ('restaurant', 'dish', 'user', 'review', 'hero')),
    CONSTRAINT chk_media_status CHECK (status IN ('pending', 'approved', 'rejected'))
);

CREATE INDEX idx_media_assets_public_id ON media_assets(public_id);
-- Optimized Partial Indexes (simulate separate table performance)
CREATE INDEX idx_media_restaurant ON media_assets(entity_id) WHERE entity_type = 'restaurant';
CREATE INDEX idx_media_dish ON media_assets(entity_id) WHERE entity_type = 'dish';
CREATE INDEX idx_media_review ON media_assets(entity_id) WHERE entity_type = 'review';
CREATE INDEX idx_media_primary ON media_assets(entity_type, entity_id) WHERE is_primary = TRUE;
CREATE INDEX idx_media_moderation ON media_assets(status, created_at) WHERE status = 'pending';
-- Hero images: fast random selection via TABLESAMPLE or ORDER BY random() LIMIT 1
CREATE INDEX idx_media_hero ON media_assets(asset_id) WHERE entity_type = 'hero' AND status = 'approved';

-- 5. USERS
CREATE TABLE users (
    user_id SERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    username VARCHAR(100) NOT NULL UNIQUE,
    home_city_id INT REFERENCES cities(city_id),
    restaurant_id INT UNIQUE REFERENCES restaurants(restaurant_id) ON DELETE SET NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    email_verified BOOLEAN DEFAULT FALSE,
    newsletter_consent BOOLEAN DEFAULT FALSE,
    password_hash VARCHAR(255) NOT NULL,
    security_stamp VARCHAR(50), -- ASP.NET Core Identity security stamp (session invalidation)
    first_name VARCHAR(50),
    last_name VARCHAR(50),
    full_name VARCHAR(100),
    phone VARCHAR(20),
    avatar_url VARCHAR(500),
    avatar_blurhash VARCHAR(50) DEFAULT NULL,
    date_of_birth DATE,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    last_login_at TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE,
    is_banned BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,
    deleted_at TIMESTAMP,
    role VARCHAR(20) NOT NULL DEFAULT 'user',
    secret_total_review_count INT,
    secret_travel_propensity DOUBLE PRECISION,
    secret_enjoyed_archetypes JSONB,
    secret_chance_dine_random DOUBLE PRECISION,
    secret_chance_pick_random_dish DOUBLE PRECISION,
    secret_cross_impact_factor DOUBLE PRECISION,
    secret_mood_propensity DOUBLE PRECISION,
    secret_is_influencer BOOLEAN DEFAULT FALSE,
    secret_rating_baseline DOUBLE PRECISION DEFAULT 6.0,
    secret_characteristics_vector JSONB NOT NULL DEFAULT '{}',
    secret_ingredient_preferences JSONB,
    secret_cleanliness_preference JSONB,
    secret_preferred_ambiance VARCHAR(100),
    followers_count INT DEFAULT 0,
    following_count INT DEFAULT 0,
    slug VARCHAR(100) UNIQUE,
    is_2fa_enabled BOOLEAN DEFAULT FALSE,
    review_count INT DEFAULT 0,
    photo_count INT DEFAULT 0,
    CONSTRAINT chk_user_role CHECK (role IN ('user', 'admin', 'moderator', 'restaurant')),
    CONSTRAINT chk_username_length CHECK (length(username) BETWEEN 3 AND 30)
);
CREATE INDEX idx_users_public_id ON users(public_id);
CREATE INDEX idx_users_city ON users(home_city_id);
-- Optimized Login Index: Only indexes users who can actually log in (active and not deleted)
CREATE INDEX idx_users_active_login ON users(email) WHERE is_active = TRUE AND is_deleted = FALSE;
CREATE INDEX idx_users_role ON users(role);
CREATE INDEX idx_users_influencer ON users(secret_is_influencer) WHERE secret_is_influencer = TRUE;
CREATE INDEX idx_users_characteristics_gin ON users USING GIN (secret_characteristics_vector);

-- Add FK for media_assets.uploaded_by (deferred because media_assets defined before users)
ALTER TABLE media_assets ADD CONSTRAINT fk_media_uploaded_by
    FOREIGN KEY (uploaded_by) REFERENCES users(user_id) ON DELETE SET NULL;

-- 6. AUTHENTICATION & SECURITY (Refactored v5.0)
-- Replaces monolithic 'auth_tokens' with specialized tables for clean separation.

-- A. Verification Codes (Short-lived, One-time use)
-- Used for: Registration, Password Reset, 2FA
CREATE TABLE verification_codes (
    verification_code_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    code_hash VARCHAR(255) NOT NULL, -- Hashed code (e.g. 6-digit OTP)
    type VARCHAR(20) NOT NULL CHECK (type IN ('register', 'reset_password', '2fa', 'email_change')),
    payload VARCHAR(255), -- Context data (e.g. new_email for email_change)
    expires_at TIMESTAMP NOT NULL,
    attempts_count INT DEFAULT 0 CHECK (attempts_count >= 0),
    created_at TIMESTAMPTZ
);
CREATE INDEX idx_verification_codes_hash ON verification_codes(code_hash);
CREATE INDEX idx_verification_codes_user ON verification_codes(user_id);

COMMENT ON TABLE verification_codes IS 'Short-lived OTP codes (email/SMS). Deleted after use or expiry.';

-- D. User Notification Settings (Preferences)
CREATE TABLE user_notification_settings (
    user_id INT PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    
    -- Push Preferences (email is transactional only - no user preferences)
    push_like BOOLEAN DEFAULT TRUE,
    push_follow BOOLEAN DEFAULT TRUE,
    push_system BOOLEAN DEFAULT TRUE,
    
    updated_at TIMESTAMPTZ
);

-- B. User Sessions (Long-lived, Refresh Tokens)
-- Used for: Maintaining login state (Mobile/Web), Device Management
CREATE TABLE user_sessions (
    user_session_id BIGSERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    refresh_token_hash VARCHAR(255) NOT NULL UNIQUE, -- Hashed Refresh Token
    device_name VARCHAR(200), -- e.g. "iPhone 13", "Chrome on Windows"
    ip_address VARCHAR(45),
    expires_at TIMESTAMP NOT NULL,
    last_active_at TIMESTAMPTZ,
    is_revoked BOOLEAN DEFAULT FALSE, -- Remote logout capability
    created_at TIMESTAMPTZ
);
CREATE INDEX idx_user_sessions_user ON user_sessions(user_id);
CREATE INDEX idx_user_sessions_token ON user_sessions(refresh_token_hash);

COMMENT ON TABLE user_sessions IS 'Long-lived refresh tokens. Used to list/revoke active devices.';

COMMENT ON COLUMN restaurants.slug IS 'SEO-friendly URL slug, np. pizzeria-roma-rzeszow. Generowany automatycznie z nazwy i miasta.';
COMMENT ON COLUMN dishes.slug IS 'SEO-friendly URL slug, np. pizza-margherita-pizzeria-roma. Generowany automatycznie z nazwy dania i restauracji.';
COMMENT ON COLUMN media_assets.ai_nsfw_score IS 'Prawdopodobieństwo NSFW (0.0-1.0) z modelu klasyfikacji obrazów';
COMMENT ON COLUMN media_assets.ai_on_topic_score IS 'Score CLIP: czy zdjęcie przedstawia jedzenie/restaurację (0.0-1.0)';
COMMENT ON COLUMN media_assets.ai_verdict IS 'Finalna decyzja AI: approved, rejected, needs_review';
COMMENT ON COLUMN media_assets.credit_text IS 'Attribution text for Unsplash images (e.g. "John Doe / Unsplash"). Required for hero images per API guidelines. NULL for Pixabay (CC0).';
COMMENT ON COLUMN media_assets.entity_type IS 'Type: restaurant, dish, user, review, hero. Hero images are homepage backgrounds with required attribution.';

-- NOTE: security_logs and email_logs have been moved to 'system' schema (module 05)

CREATE TABLE search_history (
    search_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE CASCADE,
    search_query VARCHAR(255) NOT NULL,
    created_at TIMESTAMPTZ
);
CREATE INDEX idx_search_history_user ON search_history(user_id, created_at DESC);

CREATE TABLE data_correction_requests (
    request_id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(user_id) ON DELETE SET NULL,
    restaurant_id INT REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    issue_type VARCHAR(50) NOT NULL,
    description VARCHAR(500),
    status VARCHAR(20) DEFAULT 'pending',
    created_at TIMESTAMPTZ,
    version INT DEFAULT 1 -- Optimistic Locking
);
CREATE INDEX idx_correction_requests_pending ON data_correction_requests(restaurant_id) WHERE status = 'pending';

CREATE TABLE user_follows (
    follower_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    followed_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (follower_id, followed_id),
    CONSTRAINT chk_no_self_follow CHECK (follower_id <> followed_id)
);
-- Optimized for "My Followers" (sorted by newest)
CREATE INDEX idx_user_follows_followers_time ON user_follows(followed_id, created_at DESC);
-- Optimized for "I'm Following" (sorted by newest)
CREATE INDEX idx_user_follows_following_time ON user_follows(follower_id, created_at DESC);

COMMENT ON TABLE user_follows IS 'Relacja obserwowania między użytkownikami. Triggery aktualizują liczniki w tabeli users.';

CREATE TABLE notifications (
    notification_id SERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    user_id INT NOT NULL,
    actor_id INT REFERENCES users(user_id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL, -- 'like', 'follow', 'system', 'security'
    title VARCHAR(100) NOT NULL,
    message VARCHAR(255) NOT NULL,
    metadata JSONB DEFAULT '{}', -- Flexible context (target_id, links, snapshots)
    priority INT DEFAULT 1, -- 1=Low, 2=Medium, 3=High (Critical)
    group_key VARCHAR(200), -- Aggregation key (e.g. 'like:review:123')
    counter INT DEFAULT 1,
    is_read BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    send_email BOOLEAN DEFAULT FALSE,
    email_status VARCHAR(20) DEFAULT 'none' CHECK (email_status IN ('none', 'pending', 'sent', 'failed')),
    send_push BOOLEAN DEFAULT FALSE,
    push_status VARCHAR(20) DEFAULT 'none' CHECK (push_status IN ('none', 'pending', 'sent', 'failed')),
    severity VARCHAR(20) DEFAULT 'info' CHECK (severity IN ('info', 'success', 'warning', 'danger')),
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);
CREATE INDEX idx_notifications_public_id ON notifications(public_id);
CREATE INDEX idx_notifications_user ON notifications(user_id, created_at DESC);

-- Partial Unique Index dla mechanizmu UPSERT (agregacja)
-- TYLKO nieprzeczytane i nie-usunięte notyfikacje mogą być mergowane
CREATE UNIQUE INDEX idx_notifications_group_key_unique
ON notifications (user_id, group_key)
WHERE is_read = FALSE AND is_deleted = FALSE AND group_key IS NOT NULL;

-- Optimized Badge: Active unread count
CREATE INDEX idx_notifications_badge ON notifications(user_id) WHERE is_read = FALSE AND is_deleted = FALSE;
CREATE INDEX idx_notifications_metadata ON notifications USING GIN (metadata);

-- 8. REVIEWS & SOCIAL PROOF

COMMENT ON COLUMN notifications.group_key IS
'Grouping key for notification aggregation (e.g., "like:review:123").
NULL = no grouping (individual notification).';

COMMENT ON COLUMN notifications.counter IS
'Number of aggregated events. Incremented via UPSERT when group_key matches.
Example: "5 people liked your review" -> counter = 5';

COMMENT ON COLUMN notifications.severity IS
'UI severity level: info (default), success (green), warning (yellow), danger (red).
Used for visual prioritization in frontend.';

COMMENT ON COLUMN notifications.send_email IS
'Flag: should this notification trigger email sending?
Email worker queries WHERE send_email = TRUE AND email_status = "pending".';

COMMENT ON COLUMN notifications.email_status IS
'Email delivery status: none (no email), pending (queued), sent (delivered), failed (bounce).
Managed by background email worker.';

COMMENT ON COLUMN notifications.is_deleted IS
'Soft delete flag. User can "delete" notification without losing audit trail.
Deleted notifications excluded from queries but retained for analytics.';

COMMENT ON COLUMN notifications.updated_at IS
'Timestamp of last modification. Updated automatically via trigger when counter is incremented.
Used to track when aggregated notifications were last updated (e.g., "5 people liked your review - 2 minutes ago").';

-- 6. CONTENT
CREATE TABLE reviews (
    review_id SERIAL PRIMARY KEY,
    public_id UUID NOT NULL UNIQUE,
    user_id INT NOT NULL,
    restaurant_id INT NOT NULL,
    dish_id INT NOT NULL,
    visit_date DATE NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMPTZ,
    dish_rating INT NOT NULL CHECK (dish_rating BETWEEN 1 AND 10),
    service_rating INT NOT NULL CHECK (service_rating BETWEEN 1 AND 10),
    cleanliness_rating INT NOT NULL CHECK (cleanliness_rating BETWEEN 1 AND 10),
    ambiance_rating INT NOT NULL CHECK (ambiance_rating BETWEEN 1 AND 10),
    content TEXT, -- optional review text
    -- State Machine Moderation (NEW)
    is_visible BOOLEAN DEFAULT FALSE,
    content_status VARCHAR(20) DEFAULT 'none',
    content_rejection_reason VARCHAR(200) NULL,
    helpful_count INT DEFAULT 0,
    is_approved BOOLEAN DEFAULT TRUE,
    ai_toxicity_score DECIMAL(5,4),
    ai_spam_score DECIMAL(5,4),
    ai_verdict VARCHAR(20),
    ai_model_version VARCHAR(50),
    ai_processed_at TIMESTAMPTZ,
    is_deleted BOOLEAN DEFAULT FALSE,
    deleted_at TIMESTAMP NULL,
    version INT DEFAULT 1, -- Optimistic Locking
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (restaurant_id) REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE,
    CONSTRAINT chk_dish_rating_range CHECK (dish_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_visit_date_before_created CHECK (visit_date <= created_at::DATE),
    CONSTRAINT chk_content_status CHECK (content_status IN ('none', 'pending', 'approved', 'rejected')),
    CONSTRAINT chk_content_length CHECK (content IS NULL OR length(content) >= 10) -- content is optional, but if provided must be >= 10 chars
);
CREATE INDEX idx_reviews_public_id ON reviews(public_id);
CREATE INDEX idx_reviews_user ON reviews(user_id, created_at DESC);
CREATE INDEX idx_reviews_dish ON reviews(dish_id, created_at DESC);
CREATE INDEX idx_reviews_restaurant ON reviews(restaurant_id, created_at DESC); -- Optimized for profile view
CREATE INDEX idx_reviews_content_status ON reviews(content_status, created_at) WHERE content_status = 'pending';
-- NCF Training Data Index (user-dish-rating for ML export)
CREATE INDEX idx_reviews_ncf ON reviews(user_id, dish_id, dish_rating) WHERE is_deleted = FALSE;

COMMENT ON COLUMN reviews.ai_toxicity_score IS 'Prawdopodobieństwo toksyczności/wulgaryzmów (0.0-1.0) z HerBERT';
COMMENT ON COLUMN reviews.ai_spam_score IS 'Prawdopodobieństwo spamu (0.0-1.0)';
COMMENT ON COLUMN reviews.ai_verdict IS 'Finalna decyzja AI: approved, rejected, needs_review';

CREATE TABLE review_likes (
    user_id INT NOT NULL,
    review_id INT NOT NULL,
    created_at TIMESTAMPTZ,
    PRIMARY KEY (user_id, review_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (review_id) REFERENCES reviews(review_id) ON DELETE CASCADE
);
CREATE INDEX idx_review_likes_review ON review_likes(review_id); -- Reverse lookup & Cascade delete support
CREATE INDEX idx_review_likes_user_time ON review_likes(user_id, created_at DESC); -- "My Likes" sorted by time

CREATE TABLE saved_dishes (
    user_id INT,
    dish_id INT,
    created_at TIMESTAMPTZ,
    PRIMARY KEY (user_id, dish_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (dish_id) REFERENCES dishes(dish_id) ON DELETE CASCADE
);
CREATE INDEX idx_saved_dishes_user ON saved_dishes(user_id, created_at DESC);
CREATE INDEX idx_saved_dishes_dish ON saved_dishes(dish_id);

CREATE TABLE favorite_restaurants (
    user_id INT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    restaurant_id INT NOT NULL REFERENCES restaurants(restaurant_id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ,
    PRIMARY KEY (user_id, restaurant_id)
);
CREATE INDEX idx_favorite_restaurants_user ON favorite_restaurants(user_id, created_at DESC);
CREATE INDEX idx_favorite_restaurants_restaurant ON favorite_restaurants(restaurant_id);

-- 8. REPORTING & MODERATION
-- Advanced reporting system with many-to-many reasons.

CREATE TABLE report_reason_definitions (
    reason_code VARCHAR(50) PRIMARY KEY, -- e.g. 'spam', 'hate_speech', 'fake_info'
    label_pl VARCHAR(100) NOT NULL,
    description TEXT,
    severity_score INT DEFAULT 1, -- Higher = more urgent
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ
);

CREATE TABLE reports (
    report_id SERIAL PRIMARY KEY,
    reporter_id INT NOT NULL REFERENCES users(user_id),
    entity_type VARCHAR(20) NOT NULL, -- 'review', 'photo', 'user'
    entity_id INT NOT NULL,
    description VARCHAR(500), -- User's personal explanation
    status VARCHAR(20) DEFAULT 'pending',
    created_at TIMESTAMPTZ,
    resolved_at TIMESTAMP NULL,
    resolved_by_admin_id INT NULL REFERENCES users(user_id),
    version INT DEFAULT 1, -- Optimistic Locking
    CONSTRAINT chk_reports_entity_type CHECK (entity_type IN ('review', 'photo', 'user')),
    CONSTRAINT chk_reports_status CHECK (status IN ('pending', 'processing', 'resolved', 'dismissed'))
);
CREATE INDEX idx_reports_status ON reports(status, created_at DESC);
CREATE INDEX idx_reports_polymorphic ON reports(entity_type, entity_id);

CREATE TABLE report_reason_assignments (
    report_id INT NOT NULL REFERENCES reports(report_id) ON DELETE CASCADE,
    reason_code VARCHAR(50) NOT NULL REFERENCES report_reason_definitions(reason_code) ON DELETE CASCADE,
    PRIMARY KEY (report_id, reason_code)
);
CREATE INDEX idx_report_assignments_reason ON report_reason_assignments(reason_code);

-- ========================================
-- CIRCULAR DEPENDENCIES RESOLUTION
-- ========================================
-- Add FK from restaurants.owner_id -> users.user_id
-- Constraint: ON DELETE SET NULL ensures restaurant remains if owner is deleted.
ALTER TABLE restaurants
    ADD CONSTRAINT fk_restaurants_owner
    FOREIGN KEY (owner_id) REFERENCES users(user_id)
    ON DELETE SET NULL;

CREATE INDEX idx_restaurants_owner ON restaurants(owner_id);
CREATE INDEX idx_media_assets_uploaded_by ON media_assets(uploaded_by);
