PRAGMA foreign_keys = ON;

CREATE TABLE archetypes (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    base_price_mean REAL NOT NULL,
    base_price_stdev REAL NOT NULL,
    pixabay_term TEXT,
    cuisine_tag TEXT
);

CREATE TABLE variants (
    id INTEGER PRIMARY KEY,
    archetype_id INTEGER NOT NULL REFERENCES archetypes(id),
    name TEXT NOT NULL,
    price_multiplier_mean REAL NOT NULL,
    price_multiplier_stdev REAL NOT NULL,
    pixabay_term TEXT,
    characteristics TEXT NOT NULL,
    weights TEXT
);

CREATE TABLE ingredients (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    pixabay_term TEXT,
    is_meat INTEGER NOT NULL DEFAULT 0,
    is_dairy INTEGER NOT NULL DEFAULT 0,
    is_egg INTEGER NOT NULL DEFAULT 0,
    is_gluten INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE variant_ingredients (
    variant_id INTEGER NOT NULL REFERENCES variants(id),
    ingredient_id INTEGER NOT NULL REFERENCES ingredients(id),
    PRIMARY KEY (variant_id, ingredient_id)
);

CREATE TABLE sections (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL
);

CREATE TABLE themes (
    id INTEGER PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    distribution_chance REAL NOT NULL,
    pixabay_term TEXT,
    dish_count_mean INTEGER NOT NULL,
    dish_count_sigma INTEGER NOT NULL,
    budget_prob REAL NOT NULL DEFAULT 0.2,
    casual_prob REAL NOT NULL DEFAULT 0.7,
    fine_dining_prob REAL NOT NULL DEFAULT 0.1,
    display_name TEXT,
    icon TEXT
);

CREATE TABLE theme_name_parts (
    id INTEGER PRIMARY KEY,
    theme_id INTEGER NOT NULL REFERENCES themes(id),
    part INTEGER NOT NULL CHECK (part IN (1, 2)),
    name TEXT NOT NULL,
    chance INTEGER NOT NULL
);

CREATE TABLE theme_sections (
    theme_id INTEGER NOT NULL REFERENCES themes(id),
    section_id INTEGER NOT NULL REFERENCES sections(id),
    chance REAL NOT NULL,
    limit_min INTEGER NOT NULL,
    limit_max INTEGER NOT NULL,
    PRIMARY KEY (theme_id, section_id)
);

CREATE TABLE theme_archetype_section (
    theme_id INTEGER NOT NULL REFERENCES themes(id),
    archetype_id INTEGER NOT NULL REFERENCES archetypes(id),
    section_id INTEGER NOT NULL REFERENCES sections(id),
    PRIMARY KEY (theme_id, archetype_id, section_id),
    FOREIGN KEY (theme_id, section_id) REFERENCES theme_sections(theme_id, section_id)
);

CREATE TABLE dietary_keywords (
    id INTEGER PRIMARY KEY,
    category TEXT NOT NULL CHECK (category IN ('meat', 'dairy', 'eggs', 'gluten')),
    keyword TEXT NOT NULL,
    UNIQUE (category, keyword)
);
