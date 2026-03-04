-- ============================================================
-- Fix: Przeliczenie is_spicy, tagów ostrości i bezglutenowego
-- Jednorazowy skrypt naprawczy do uruchomienia na istniejącej bazie.
-- ============================================================

BEGIN;

-- 1. Przeliczenie is_spicy (nowy próg > 6.0 zamiast > 5.0)
UPDATE dishes
SET is_spicy = ((secret_characteristics_vector->>'flavor_spiciness')::float * 10) > 6.0
WHERE secret_characteristics_vector IS NOT NULL;

-- 2. Przeliczenie tagów ostrości (nowe progi: 4-6 średnio, 6-8 ostre, >8 bardzo)

-- 2a. Usuń stare tagi spice
DELETE FROM dish_tags
WHERE tag_id IN (SELECT tag_id FROM tags WHERE category = 'spice');

-- 2b. Wstaw nowe tagi na podstawie nowych progów
INSERT INTO dish_tags (dish_id, tag_id)
SELECT d.dish_id, t.tag_id
FROM dishes d
CROSS JOIN LATERAL (
  SELECT CASE
    WHEN (d.secret_characteristics_vector->>'flavor_spiciness')::float * 10 BETWEEN 4 AND 6 THEN 'Średnio ostre'
    WHEN (d.secret_characteristics_vector->>'flavor_spiciness')::float * 10 BETWEEN 6 AND 8 THEN 'Ostre'
    WHEN (d.secret_characteristics_vector->>'flavor_spiciness')::float * 10 > 8 THEN 'Bardzo ostre'
  END AS tag_name
) calc
JOIN tags t ON t.tag_name = calc.tag_name AND t.category = 'spice'
WHERE d.secret_characteristics_vector IS NOT NULL
  AND calc.tag_name IS NOT NULL;

-- 3. Przeliczenie bezglutenowego (rozszerzone keywords)

-- 3a. Usuń bezglutenowe tagi z dań zawierających gluten
DELETE FROM dish_tags
WHERE tag_id = (SELECT tag_id FROM tags WHERE tag_name = 'Bezglutenowe')
AND dish_id IN (
  SELECT dish_id FROM dishes
  WHERE ingredients_json::text ~* '(ciasto|bułka|tortilla|pita|naleśnik|pierogi|kluski|focaccia|ravioli|wonton|noodle|mąka|chleb|makaron|pszenica)'
);

-- 3b. Ustaw is_gluten_free = false dla dań z glutenem
UPDATE dishes SET is_gluten_free = false
WHERE ingredients_json::text ~* '(ciasto|bułka|tortilla|pita|naleśnik|pierogi|kluski|focaccia|ravioli|wonton|noodle|mąka|chleb|makaron|pszenica)';

COMMIT;

-- Weryfikacja
SELECT
  COUNT(*) AS total_dishes,
  COUNT(*) FILTER (WHERE is_spicy) AS spicy_count,
  ROUND(100.0 * COUNT(*) FILTER (WHERE is_spicy) / COUNT(*), 1) AS spicy_pct,
  COUNT(*) FILTER (WHERE is_gluten_free) AS gluten_free_count,
  ROUND(100.0 * COUNT(*) FILTER (WHERE is_gluten_free) / COUNT(*), 1) AS gluten_free_pct
FROM dishes;
