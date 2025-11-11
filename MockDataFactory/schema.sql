-- This script should be run after 'setup.sql' to build the tables.
USE MockDataDB;
GO

-- 1. CITIES (Public)
CREATE TABLE Cities (
    city_id INT PRIMARY KEY IDENTITY(1,1),
    city_name NVARCHAR(100) NOT NULL
);
GO

-- 2. RESTAURANTS (Public + Secret)
CREATE TABLE Restaurants (
    restaurant_id INT PRIMARY KEY IDENTITY(1,1),
    city_id INT FOREIGN KEY REFERENCES Cities(city_id),
    restaurant_name NVARCHAR(255) NOT NULL UNIQUE,
    public_cuisine_theme NVARCHAR(100),
    public_price_range NVARCHAR(5),

    -- Calculated Averages (for the app)
    avg_service FLOAT,
    avg_cleanliness FLOAT,
    avg_ambiance FLOAT,
    avg_food_score FLOAT,

    -- Secret Simulation Attributes
    secret_price_multiplier FLOAT,
    secret_overall_food_quality FLOAT,
    secret_service_quality FLOAT,
    secret_cleanliness_score FLOAT,
    secret_ambiance_type NVARCHAR(100),
    secret_ambiance_quality FLOAT
);
GO

-- 3. INGREDIENTS (Public)
CREATE TABLE Ingredients (
    ingredient_id INT PRIMARY KEY IDENTITY(1,1),
    name NVARCHAR(100) NOT NULL UNIQUE
);
GO

-- 4. DISHES (Public + Secret)
CREATE TABLE Dishes (
    dish_id INT PRIMARY KEY IDENTITY(1,1),
    restaurant_id INT FOREIGN KEY REFERENCES Restaurants(restaurant_id),
    dish_name NVARCHAR(255),
    public_price DECIMAL(10, 2),

    -- Secret Simulation Attributes
    secret_base_price DECIMAL(10, 2),
    secret_price_to_default_ratio FLOAT,
    secret_quality FLOAT,
    secret_spiciness INT
);
GO

-- 5. DISH_INGREDIENTS_LINK (Public)
CREATE TABLE Dish_Ingredients_Link (
    dish_id INT FOREIGN KEY REFERENCES Dishes(dish_id),
    ingredient_id INT FOREIGN KEY REFERENCES Ingredients(ingredient_id),
    PRIMARY KEY (dish_id, ingredient_id)
);
GO

-- 6. USERS (Public + Secret)
CREATE TABLE Users (
    user_id INT PRIMARY KEY IDENTITY(1,1),
    username NVARCHAR(100) NOT NULL,
    home_city_id INT FOREIGN KEY REFERENCES Cities(city_id),

    -- Secret Simulation Attributes
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
    secret_preferred_ambiance NVARCHAR(100)
);
GO

-- 7. REVIEWS (The Unified Rating Table)
CREATE TABLE Reviews (
    review_id INT PRIMARY KEY IDENTITY(1,1),
    user_id INT FOREIGN KEY REFERENCES Users(user_id),
    restaurant_id INT FOREIGN KEY REFERENCES Restaurants(restaurant_id),
    dish_id INT FOREIGN KEY REFERENCES Dishes(dish_id),
    review_date DATETIME,

    -- Ratings (1-10 scale)
    dish_rating INT NOT NULL,
    service_rating INT NULL, -- NULLable, as discussed
    cleanliness_rating INT NULL,
    ambiance_rating INT NULL
);
GO