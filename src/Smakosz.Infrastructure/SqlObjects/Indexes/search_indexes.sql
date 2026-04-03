-- GIN trigram indexes for full-text search
CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_name
ON restaurants
USING GIN (f_unaccent(lower(restaurant_name)) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS trgm_idx_restaurants_full_search
ON restaurants
USING GIN (f_unaccent(lower(restaurant_name || ' ' || COALESCE(cuisine_type, ''))) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS trgm_idx_dishes_name
ON dishes
USING GIN (f_unaccent(lower(dish_name)) gin_trgm_ops);

CREATE INDEX IF NOT EXISTS trgm_idx_users_username
ON users
USING GIN (f_unaccent(lower(username)) gin_trgm_ops);

-- Expression/partial BTREE indexes
CREATE INDEX IF NOT EXISTS idx_restaurants_cuisine_btree
ON restaurants(cuisine_type) WHERE status = 'active';

CREATE INDEX IF NOT EXISTS idx_users_email_lower ON users (lower(email));
CREATE INDEX IF NOT EXISTS idx_users_username_lower ON users (lower(username));
