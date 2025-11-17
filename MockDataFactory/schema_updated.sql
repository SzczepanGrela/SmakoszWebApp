-- ========================================
-- SMAKOSZ WEB APP - COMPLETE DATABASE SCHEMA
-- Updated schema with all required tables and attributes
-- No orders functionality - review-focused platform
-- ========================================

USE MockDataDB;
GO

-- ========================================
-- DROP EXISTING TABLES (for clean rebuild)
-- ========================================
IF OBJECT_ID('Pending_Comments', 'U') IS NOT NULL DROP TABLE Pending_Comments;
IF OBJECT_ID('Pending_User_Photos', 'U') IS NOT NULL DROP TABLE Pending_User_Photos;
IF OBJECT_ID('Reports', 'U') IS NOT NULL DROP TABLE Reports;
IF OBJECT_ID('User_Photos', 'U') IS NOT NULL DROP TABLE User_Photos;
IF OBJECT_ID('Saved_Dishes', 'U') IS NOT NULL DROP TABLE Saved_Dishes;
IF OBJECT_ID('Restaurant_Tags', 'U') IS NOT NULL DROP TABLE Restaurant_Tags;
IF OBJECT_ID('Dish_Tags', 'U') IS NOT NULL DROP TABLE Dish_Tags;
IF OBJECT_ID('Ingredient_Restrictions', 'U') IS NOT NULL DROP TABLE Ingredient_Restrictions;
IF OBJECT_ID('Photos', 'U') IS NOT NULL DROP TABLE Photos;
IF OBJECT_ID('Tags', 'U') IS NOT NULL DROP TABLE Tags;
IF OBJECT_ID('Reviews', 'U') IS NOT NULL DROP TABLE Reviews;
IF OBJECT_ID('Dish_Ingredients_Link', 'U') IS NOT NULL DROP TABLE Dish_Ingredients_Link;
IF OBJECT_ID('Dishes', 'U') IS NOT NULL DROP TABLE Dishes;
IF OBJECT_ID('Restaurants', 'U') IS NOT NULL DROP TABLE Restaurants;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('Ingredients', 'U') IS NOT NULL DROP TABLE Ingredients;
IF OBJECT_ID('Cities', 'U') IS NOT NULL DROP TABLE Cities;
GO

-- ========================================
-- 1. CITIES (Public)
-- ========================================
CREATE TABLE Cities (
    city_id INT PRIMARY KEY IDENTITY(1,1),
    city_name NVARCHAR(100) NOT NULL UNIQUE
);
GO

-- ========================================
-- 2. RESTAURANTS (Public + Secret)
-- ========================================
CREATE TABLE Restaurants (
    restaurant_id INT PRIMARY KEY IDENTITY(1,1),
    city_id INT FOREIGN KEY REFERENCES Cities(city_id),
    restaurant_name NVARCHAR(255) NOT NULL UNIQUE,

    -- Public attributes
    public_cuisine_theme NVARCHAR(100),
    public_price_range NVARCHAR(5),
    address NVARCHAR(200),
    latitude DECIMAL(10,7),
    longitude DECIMAL(10,7),
    phone NVARCHAR(20),
    website NVARCHAR(200),
    description NVARCHAR(1000),
    image_url NVARCHAR(500),
    is_active BIT DEFAULT 1,
    created_at DATETIME DEFAULT GETDATE(),

    -- Calculated Averages (for the app, updated from reviews)
    avg_service FLOAT,
    avg_cleanliness FLOAT,
    avg_ambiance FLOAT,
    avg_food_score FLOAT,

    -- Secret Simulation Attributes (for CF model and generation)
    secret_price_multiplier FLOAT,
    secret_overall_food_quality FLOAT,
    secret_service_quality FLOAT,
    secret_cleanliness_score FLOAT,
    secret_ambiance_type NVARCHAR(100),
    secret_ambiance_quality FLOAT,

    -- Additional attributes for menu generation (ADDED FOR MOCKDATAFACTORY)
    menu_blueprint NVARCHAR(100), -- Menu type (pizza_menu, burger_menu, etc.)
    theme NVARCHAR(100) -- Restaurant theme for backward compatibility
);
GO

CREATE INDEX idx_restaurants_city ON Restaurants(city_id);
CREATE INDEX idx_restaurants_cuisine ON Restaurants(public_cuisine_theme);
CREATE INDEX idx_restaurants_active ON Restaurants(is_active);
GO

-- ========================================
-- 3. INGREDIENTS (Public)
-- ========================================
CREATE TABLE Ingredients (
    ingredient_id INT PRIMARY KEY IDENTITY(1,1),
    ingredient_name NVARCHAR(100) NOT NULL UNIQUE,  -- FIXED: name → ingredient_name for consistency
    is_allergen BIT DEFAULT 0
);
GO

-- ========================================
-- 4. INGREDIENT RESTRICTIONS
-- Links ingredients to dietary restriction types
-- ========================================
CREATE TABLE Ingredient_Restrictions (
    ingredient_id INT NOT NULL,
    restriction_type NVARCHAR(50) NOT NULL,
    -- restriction_type values: 'vegetarian', 'vegan', 'gluten-free', 'lactose-free', 'nut-allergy', 'halal', 'kosher', 'shellfish-allergy'

    PRIMARY KEY (ingredient_id, restriction_type),
    FOREIGN KEY (ingredient_id) REFERENCES Ingredients(ingredient_id) ON DELETE CASCADE
);
GO

-- ========================================
-- 5. DISHES (Public + Secret)
-- ========================================
CREATE TABLE Dishes (
    dish_id INT PRIMARY KEY IDENTITY(1,1),
    restaurant_id INT FOREIGN KEY REFERENCES Restaurants(restaurant_id),
    dish_name NVARCHAR(255) NOT NULL,

    -- Public attributes
    public_price DECIMAL(10, 2),
    description NVARCHAR(500),
    is_available BIT DEFAULT 1,
    calories INT NULL,
    image_url NVARCHAR(500), -- Primary display image
    created_at DATETIME DEFAULT GETDATE(),

    -- Secret Simulation Attributes
    secret_base_price DECIMAL(10, 2),
    secret_price_to_default_ratio FLOAT,
    secret_quality FLOAT,
    secret_spiciness FLOAT,

    -- Additional attributes for CF model (ADDED FOR MOCKDATAFACTORY)
    archetype NVARCHAR(100), -- Dish archetype (Pizza, Burger, Sushi, etc.)
    secret_richness FLOAT,
    secret_texture_score FLOAT,
    popularity_factor FLOAT -- For Zipf distribution
);
GO

CREATE INDEX idx_dishes_restaurant ON Dishes(restaurant_id);
CREATE INDEX idx_dishes_available ON Dishes(is_available);
GO

-- ========================================
-- 6. DISH_INGREDIENTS_LINK (Public)
-- Many-to-many relationship
-- ========================================
CREATE TABLE Dish_Ingredients_Link (
    dish_id INT,
    ingredient_id INT,

    PRIMARY KEY (dish_id, ingredient_id),
    FOREIGN KEY (dish_id) REFERENCES Dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (ingredient_id) REFERENCES Ingredients(ingredient_id)
);
GO

-- ========================================
-- 7. TAGS
-- Categorization system for dishes and restaurants
-- ========================================
CREATE TABLE Tags (
    tag_id INT PRIMARY KEY IDENTITY(1,1),
    tag_name NVARCHAR(50) NOT NULL UNIQUE,
    tag_category NVARCHAR(30) NOT NULL,
    -- Categories: 'dietary', 'spice', 'cuisine', 'mood', 'occasion', 'meal_type', 'feature'
    display_color NVARCHAR(20),
    created_at DATETIME DEFAULT GETDATE()
);
GO

CREATE INDEX idx_tags_category ON Tags(tag_category);
GO

-- ========================================
-- 8. DISH_TAGS (Many-to-many)
-- ========================================
CREATE TABLE Dish_Tags (
    dish_id INT,
    tag_id INT,

    PRIMARY KEY (dish_id, tag_id),
    FOREIGN KEY (dish_id) REFERENCES Dishes(dish_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES Tags(tag_id) ON DELETE CASCADE
);
GO

-- ========================================
-- 9. RESTAURANT_TAGS (Many-to-many)
-- ========================================
CREATE TABLE Restaurant_Tags (
    restaurant_id INT,
    tag_id INT,

    PRIMARY KEY (restaurant_id, tag_id),
    FOREIGN KEY (restaurant_id) REFERENCES Restaurants(restaurant_id) ON DELETE CASCADE,
    FOREIGN KEY (tag_id) REFERENCES Tags(tag_id) ON DELETE CASCADE
);
GO

-- ========================================
-- 10. PHOTOS (System/Display Photos)
-- For dish and restaurant display images (pre-curated)
-- ========================================
CREATE TABLE Photos (
    photo_id INT PRIMARY KEY IDENTITY(1,1),
    entity_type NVARCHAR(20) NOT NULL, -- 'dish' or 'restaurant'
    entity_id INT NOT NULL,
    photo_url NVARCHAR(500) NOT NULL,
    is_primary BIT DEFAULT 0,
    created_at DATETIME DEFAULT GETDATE(),

    CONSTRAINT chk_photos_entity_type CHECK (entity_type IN ('dish', 'restaurant'))
);
GO

CREATE INDEX idx_photos_entity ON Photos(entity_type, entity_id);
GO

-- ========================================
-- 11. USERS (Public + Secret)
-- ========================================
CREATE TABLE Users (
    user_id INT PRIMARY KEY IDENTITY(1,1),
    username NVARCHAR(100) NOT NULL UNIQUE,
    home_city_id INT FOREIGN KEY REFERENCES Cities(city_id),

    -- Public profile attributes
    email NVARCHAR(100) UNIQUE NOT NULL,
    password_hash NVARCHAR(255) NOT NULL, -- Plain text for now, but named for future hashing
    full_name NVARCHAR(100),
    phone NVARCHAR(20),
    avatar_url NVARCHAR(500),
    date_of_birth DATE,
    account_created_at DATETIME DEFAULT GETDATE(),
    last_login_at DATETIME,
    is_active BIT DEFAULT 1,

    -- Secret Simulation Attributes (for CF model)
    secret_total_review_count INT,
    secret_travel_propensity FLOAT,
    secret_chance_dine_random FLOAT,
    secret_chance_pick_random_dish FLOAT,
    secret_chance_to_update_rating FLOAT,
    secret_cross_impact_factor FLOAT,
    secret_mood_propensity FLOAT,
    secret_price_preference_range NVARCHAR(50),
    secret_price_tolerance_above FLOAT,
    secret_price_tolerance_below FLOAT,
    secret_enjoyed_restaurant_themes NVARCHAR(MAX), -- JSON
    secret_enjoyed_archetypes NVARCHAR(MAX), -- JSON
    secret_enjoyed_variants NVARCHAR(MAX), -- JSON
    secret_ingredient_preferences NVARCHAR(MAX), -- JSON
    secret_cleanliness_preference NVARCHAR(MAX), -- JSON
    secret_preferred_ambiance NVARCHAR(100),

    -- Additional preferences for CF model (ADDED FOR MOCKDATAFACTORY)
    secret_spice_preference FLOAT,
    secret_richness_preference FLOAT,
    secret_texture_preference FLOAT
);
GO

CREATE INDEX idx_users_city ON Users(home_city_id);
CREATE INDEX idx_users_email ON Users(email);
GO

-- ========================================
-- 12. REVIEWS (Unified Rating Table)
-- ========================================
CREATE TABLE Reviews (
    review_id INT PRIMARY KEY IDENTITY(1,1),
    user_id INT NOT NULL,
    restaurant_id INT NOT NULL,
    dish_id INT NOT NULL,
    review_date DATETIME NOT NULL,

    -- Ratings (1-10 scale)
    dish_rating INT NOT NULL,
    service_rating INT NULL,
    cleanliness_rating INT NULL,
    ambiance_rating INT NULL,

    -- Review content
    review_title NVARCHAR(100),
    review_comment NVARCHAR(2000),
    helpful_count INT DEFAULT 0,
    is_approved BIT DEFAULT 1, -- For moderation

    FOREIGN KEY (user_id) REFERENCES Users(user_id),
    FOREIGN KEY (restaurant_id) REFERENCES Restaurants(restaurant_id),
    FOREIGN KEY (dish_id) REFERENCES Dishes(dish_id),

    -- Temporal constraint: review must be after user account creation
    CONSTRAINT chk_review_after_account CHECK (review_date >= (SELECT account_created_at FROM Users WHERE user_id = Reviews.user_id)),
    CONSTRAINT chk_dish_rating_range CHECK (dish_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_service_rating_range CHECK (service_rating IS NULL OR service_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_cleanliness_rating_range CHECK (cleanliness_rating IS NULL OR cleanliness_rating BETWEEN 1 AND 10),
    CONSTRAINT chk_ambiance_rating_range CHECK (ambiance_rating IS NULL OR ambiance_rating BETWEEN 1 AND 10)
);
GO

CREATE INDEX idx_reviews_user ON Reviews(user_id, review_date DESC);
CREATE INDEX idx_reviews_dish ON Reviews(dish_id, review_date DESC);
CREATE INDEX idx_reviews_restaurant ON Reviews(restaurant_id, review_date DESC);
GO

-- ========================================
-- 13. USER_PHOTOS (Review Photos)
-- User-uploaded photos attached to reviews
-- ========================================
CREATE TABLE User_Photos (
    user_photo_id INT PRIMARY KEY IDENTITY(1,1),
    review_id INT NOT NULL,
    uploaded_by_user_id INT NOT NULL,
    photo_url NVARCHAR(500) NOT NULL,
    upload_date DATETIME DEFAULT GETDATE(),
    is_approved BIT DEFAULT 0, -- Requires moderation

    FOREIGN KEY (review_id) REFERENCES Reviews(review_id) ON DELETE CASCADE,
    FOREIGN KEY (uploaded_by_user_id) REFERENCES Users(user_id)
);
GO

CREATE INDEX idx_user_photos_review ON User_Photos(review_id);
CREATE INDEX idx_user_photos_pending ON User_Photos(is_approved);
GO

-- ========================================
-- 14. SAVED_DISHES (User Favorites/Bookmarks)
-- ========================================
CREATE TABLE Saved_Dishes (
    user_id INT,
    dish_id INT,
    saved_at DATETIME DEFAULT GETDATE(),

    PRIMARY KEY (user_id, dish_id),
    FOREIGN KEY (user_id) REFERENCES Users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (dish_id) REFERENCES Dishes(dish_id) ON DELETE CASCADE
);
GO

CREATE INDEX idx_saved_dishes_user ON Saved_Dishes(user_id, saved_at DESC);
GO

-- ========================================
-- 15. PENDING_USER_PHOTOS (Moderation Queue)
-- For admin review of user-uploaded photos
-- ========================================
CREATE TABLE Pending_User_Photos (
    pending_photo_id INT PRIMARY KEY IDENTITY(1,1),
    user_photo_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    submitted_at DATETIME DEFAULT GETDATE(),
    status NVARCHAR(20) DEFAULT 'pending', -- 'pending', 'approved', 'rejected'
    reviewed_at DATETIME NULL,
    reviewed_by_admin_id INT NULL,
    rejection_reason NVARCHAR(200) NULL,

    FOREIGN KEY (user_photo_id) REFERENCES User_Photos(user_photo_id),
    FOREIGN KEY (submitted_by_user_id) REFERENCES Users(user_id)
);
GO

CREATE INDEX idx_pending_photos_status ON Pending_User_Photos(status, submitted_at);
GO

-- ========================================
-- 16. PENDING_COMMENTS (Moderation Queue)
-- For admin review of potentially problematic comments
-- ========================================
CREATE TABLE Pending_Comments (
    pending_comment_id INT PRIMARY KEY IDENTITY(1,1),
    review_id INT NOT NULL,
    submitted_by_user_id INT NOT NULL,
    comment_text NVARCHAR(2000) NOT NULL,
    submitted_at DATETIME DEFAULT GETDATE(),
    status NVARCHAR(20) DEFAULT 'pending', -- 'pending', 'approved', 'rejected'
    reviewed_at DATETIME NULL,
    reviewed_by_admin_id INT NULL,
    rejection_reason NVARCHAR(200) NULL,
    flagged_for_keywords BIT DEFAULT 0,

    FOREIGN KEY (review_id) REFERENCES Reviews(review_id),
    FOREIGN KEY (submitted_by_user_id) REFERENCES Users(user_id)
);
GO

CREATE INDEX idx_pending_comments_status ON Pending_Comments(status, submitted_at);
GO

-- ========================================
-- 17. REPORTS (Abuse/Content Reports)
-- Users can report reviews, photos, or other users
-- ========================================
CREATE TABLE Reports (
    report_id INT PRIMARY KEY IDENTITY(1,1),
    reporter_user_id INT NOT NULL,
    entity_type NVARCHAR(20) NOT NULL, -- 'review', 'user_photo', 'user'
    entity_id INT NOT NULL,
    reason NVARCHAR(100) NOT NULL,
    description NVARCHAR(500),
    status NVARCHAR(20) DEFAULT 'pending', -- 'pending', 'resolved', 'dismissed'
    created_at DATETIME DEFAULT GETDATE(),
    resolved_at DATETIME NULL,
    resolved_by_admin_id INT NULL,

    FOREIGN KEY (reporter_user_id) REFERENCES Users(user_id),
    CONSTRAINT chk_reports_entity_type CHECK (entity_type IN ('review', 'user_photo', 'user'))
);
GO

CREATE INDEX idx_reports_status ON Reports(status, created_at DESC);
GO

-- ========================================
-- COMPUTED COLUMNS / VIEWS (Optional)
-- ========================================

-- View: Active dishes with their restaurant info
CREATE VIEW vw_Active_Dishes AS
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
FROM Dishes d
JOIN Restaurants r ON d.restaurant_id = r.restaurant_id
JOIN Cities c ON r.city_id = c.city_id
WHERE d.is_available = 1 AND r.is_active = 1;
GO

-- View: User review statistics
CREATE VIEW vw_User_Stats AS
SELECT
    u.user_id,
    u.username,
    COUNT(DISTINCT r.review_id) AS total_reviews,
    COUNT(DISTINCT up.user_photo_id) AS total_photos,
    AVG(CAST(r.dish_rating AS FLOAT)) AS avg_rating_given
FROM Users u
LEFT JOIN Reviews r ON u.user_id = r.user_id
LEFT JOIN User_Photos up ON u.user_id = up.uploaded_by_user_id
GROUP BY u.user_id, u.username;
GO

PRINT 'Schema created successfully!';
PRINT 'Total tables: 17';
PRINT 'Main entities: Cities, Restaurants, Dishes, Ingredients, Users, Reviews';
PRINT 'Supporting: Photos, User_Photos, Tags, Saved_Dishes, Reports';
PRINT 'Moderation: Pending_User_Photos, Pending_Comments';
GO
