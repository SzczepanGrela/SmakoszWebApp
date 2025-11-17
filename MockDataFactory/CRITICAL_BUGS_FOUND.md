# 🚨 KRYTYCZNE BŁĘDY ZNALEZIONE W MOCKDATAFACTORY

**Data analizy:** 2025-01-17
**Analiza:** Ultra-dokładna, 3-krotna weryfikacja
**Status:** WYMAGA NATYCHMIASTOWEJ NAPRAWY

---

## ❌ BŁĄD #1: Cross-Impact NIE DZIAŁA (rating_engine.py:48)

**Lokalizacja:** `algorithms/rating_engine.py:48`

**Problem:**
```python
# Linia 48 - TA LINIJKA NIE DZIAŁA!
apply_cross_impact(food_score, [service_score, cleanliness_score, ambiance_score], cross_impact_factor)
```

**Przyczyna:**
Funkcja `apply_cross_impact()` modyfikuje `other_scores` in-place (linia 228), ALE przekazujemy **listę wartości skopiowanych**, a nie referencje do zmiennych!

```python
# Linia 228 w apply_cross_impact():
other_scores[i] = min(10.0, other_scores[i] + boost)
# To modyfikuje lokalną listę, NIE oryginalne zmienne!
```

**Skutek:**
- Cross-impact (efekt halo) **w ogóle nie wpływa** na oceny!
- Parametr `secret_cross_impact_factor = 0.02` jest IGNOROWANY!
- Linia 51-56 używa **niezmienione** wartości `service_score`, `cleanliness_score`, `ambiance_score`!

**Naprawa:**
```python
# ZAMIAST:
apply_cross_impact(food_score, [service_score, cleanliness_score, ambiance_score], cross_impact_factor)

# POWINNO BYĆ:
service_score, cleanliness_score, ambiance_score = apply_cross_impact(
    food_score, service_score, cleanliness_score, ambiance_score, cross_impact_factor
)

# I zmienić funkcję apply_cross_impact() aby ZWRACAŁA nowe wartości:
def apply_cross_impact(food_score, service_score, cleanliness_score, ambiance_score, cross_impact_factor):
    if food_score > 7:
        boost = (food_score - 7) * cross_impact_factor * 0.5
        service_score = min(10.0, service_score + boost)
        cleanliness_score = min(10.0, cleanliness_score + boost)
        ambiance_score = min(10.0, ambiance_score + boost)
    return service_score, cleanliness_score, ambiance_score
```

---

## ❌ BŁĄD #2: Niepoprawny JSON w bazie (phase4_users.py:125-131)

**Lokalizacja:** `generators/phase4_users.py:125-131`

**Problem:**
```python
"secret_enjoyed_archetypes": str(enjoyed_archetypes),  # BŁĄD!
"secret_ingredient_preferences": str(ingredient_preferences),
"secret_price_preference_range": str(price_preference_range),
"secret_cleanliness_preference": str(cleanliness_expectations),
```

**Przyczyna:**
Używamy `str(dict)` zamiast `json.dumps(dict)`!

```python
str({"Pizza": 0.9})  # → "{'Pizza': 0.9}" (apostrof - NIE jest poprawnym JSON!)
json.dumps({"Pizza": 0.9})  # → '{"Pizza": 0.9}' (cudzysłów - poprawny JSON)
```

**Skutek:**
- Parsowanie w `phase5_reviews.py:89-95` używa `.replace("'", "\"")` - to **ZAWIEDZIE** gdy wartości zawierają apostrofy!
- Przykład: `{"ingredient": "tom's special"}` → po replace: `{"ingredient": "tom"s special"}` → **BŁĄD JSON!**

**Naprawa:**
```python
import json

# ZAMIAST:
"secret_enjoyed_archetypes": str(enjoyed_archetypes),

# POWINNO BYĆ:
"secret_enjoyed_archetypes": json.dumps(enjoyed_archetypes),
```

---

## ❌ BŁĄD #3: Składniki NIE SĄ ŁADOWANE (phase5_reviews.py:173)

**Lokalizacja:** `generators/phase5_reviews.py:173`

**Problem:**
```python
dish_dicts = [
    {
        ...
        'ingredients': []  # Simplified - BŁĄD!
    }
    for d in dishes
]
```

**Skutek:**
- Lista składników jest **ZAWSZE PUSTA**!
- Cała sekcja oceniania składników w `rating_engine.py:115-124` **NIE DZIAŁA**:
  ```python
  for ingredient in dish_ingredients:  # Pętla NIGDY się nie wykona!
      pref = ingredient_prefs.get(ingredient, 0.5)
      if pref > 0.7:
          score += 0.3
  ```
- Mechanizm dopasowania preferencji składnikowych jest **WYŁĄCZONY**!

**Naprawa:**
```python
# TRZEBA POBRAĆ składniki z bazy:
dish_ingredients = db.fetch_all(f"""
    SELECT i.ingredient_name
    FROM Dish_Ingredients_Link dil
    JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id
    WHERE dil.dish_id = {dish_id}
""")

dish_dicts = [
    {
        ...
        'ingredients': [ing[0] for ing in db.fetch_all(f"SELECT i.ingredient_name FROM Dish_Ingredients_Link dil JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id WHERE dil.dish_id = {d[0]}")],
    }
    for d in dishes
]
```

---

## ❌ BŁĄD #4: Błędne dish_id w linkach (phase3_dishes.py:73)

**Lokalizacja:** `generators/phase3_dishes.py:73`

**Problem:**
```python
dish_id = len(dish_data) + 1  # BŁĄD!

dish_ingredient_links.append({
    "dish_id": dish_id,  # To NIE będzie prawdziwe ID z bazy!
    "ingredient_id": ingredient_map[ingredient_name]
})
```

**Przyczyna:**
- Zakładamy że `dish_id = len(dish_data) + 1` (np. 1, 2, 3...)
- ALE `db.insert_bulk("Dishes", dish_data)` używa **IDENTITY** - baza przypisuje własne ID!
- Jeśli w bazie są już jakieś dania, prawdziwe ID będą RÓŻNE od założonych!

**Skutek:**
- Powiązania w `Dish_Ingredients_Link` będą wskazywać **ZŁE dish_id**!
- Powiązania w `Dish_Tags` będą błędne!
- Zdjęcia w `Photos` będą przypisane do złych dań!

**Naprawa:**
```python
# OPCJA 1: Robić insert pojedynczo i pobierać ID:
for variant in menu_dishes:
    ...
    dish_id = db.insert_single("Dishes", dish_data_single)

    # Teraz dish_id jest prawdziwy
    for ingredient_name in ingredients:
        db.insert_single("Dish_Ingredients_Link", {
            "dish_id": dish_id,
            "ingredient_id": ingredient_map[ingredient_name]
        })

# OPCJA 2: Najpierw insert wszystkich dań, potem pobrać ID i dopiero linki:
db.insert_bulk("Dishes", dish_data)
inserted_dishes = db.fetch_all("SELECT TOP ... dish_id FROM Dishes ORDER BY dish_id DESC")
```

---

## ❌ BŁĄD #5: Błędne review_id dla zdjęć (phase5_reviews.py:220)

**Lokalizacja:** `generators/phase5_reviews.py:220`

**Problem:**
```python
photo_batch.append({
    'review_id': total_reviews,  # Approximation - BŁĄD!
    'photo_url': photo_pools.get_user_photo_generic()
})
```

**Przyczyna:**
- `total_reviews` to licznik, ALE `review_id` jest **IDENTITY** w bazie!
- Prawdziwe `review_id` może być RÓŻNE od licznika!

**Skutek:**
- Zdjęcia będą przypisane do **ZŁYCH recenzji**!

**Naprawa:**
```python
# Robić insert recenzji pojedynczo i pobierać ID:
review_id = db.insert_single("Reviews", review_data)

if random.random() < 0.30:
    db.insert_single("User_Photos", {
        'review_id': review_id,  # Teraz prawdziwe!
        'uploaded_by_user_id': user_id,
        'photo_url': photo_pools.get_user_photo_generic()
    })
```

---

## ❌ BŁĄD #6: NIEZGODNOŚĆ SCHEMATU BAZY - Photos (phase2_restaurants.py, phase3_dishes.py)

**Problem:**
**Schema** (schema_updated.sql:196-205):
```sql
CREATE TABLE Photos (
    photo_id INT PRIMARY KEY IDENTITY(1,1),
    entity_type NVARCHAR(20) NOT NULL,  -- 'dish' or 'restaurant'
    entity_id INT NOT NULL,
    photo_url NVARCHAR(500) NOT NULL,
    is_primary BIT DEFAULT 0,
    created_at DATETIME DEFAULT GETDATE()
);
```

**KOD używa** (phase2_restaurants.py:79-82, phase3_dishes.py:107-110):
```python
photo_data.append({
    "restaurant_id": restaurant_id,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "photo_url": url,
    "upload_date": ...  # BŁĄD! Kolumna NIE ISTNIEJE (jest created_at)!
})
```

**Naprawa:**
```python
photo_data.append({
    "entity_type": "restaurant",  # DODAĆ!
    "entity_id": restaurant_id,   # ZMIENIĆ z restaurant_id
    "photo_url": url,
    "is_primary": False,
    # created_at jest DEFAULT - nie trzeba podawać
})
```

---

## ❌ BŁĄD #7: Zdjęcia użytkowników do ZŁEJtabeli (phase5_reviews.py:219-222)

**Problem:**
**Schema** ma osobną tabelę `User_Photos` (linia 298-313):
```sql
CREATE TABLE User_Photos (
    user_photo_id INT PRIMARY KEY IDENTITY(1,1),
    review_id INT NOT NULL,
    uploaded_by_user_id INT NOT NULL,
    photo_url NVARCHAR(500) NOT NULL,
    upload_date DATETIME DEFAULT GETDATE(),
    is_approved BIT DEFAULT 0,
    FOREIGN KEY (review_id) REFERENCES Reviews(review_id)
);
```

**KOD używa** tabeli `Photos` (phase5_reviews.py:228):
```python
db.insert_bulk("Photos", photo_batch)  # BŁĄD! Zła tabela!
```

**Naprawa:**
```python
db.insert_bulk("User_Photos", photo_batch)  # POPRAWNA tabela!

# I zmienić strukturę:
photo_batch.append({
    'review_id': review_id,
    'uploaded_by_user_id': user_id,  # DODAĆ!
    'photo_url': photo_pools.get_user_photo_generic(),
    'is_approved': True  # Lub False jeśli wymaga moderacji
})
```

---

## ❌ BŁĄD #8: NIEZGODNOŚĆ SCHEMATU - Users (phase4_users.py:119-136)

**Schema ma kolumny** (schema_updated.sql:214-246):
- `home_city_id` (zamiast `city_id`)
- `email`
- `password_hash` ← WYMAGANE!
- `account_created_at` (zamiast `join_date`)
- `secret_travel_propensity` (zamiast `travel_propensity`)
- `secret_spice_preference` ← NIE ISTNIEJE W SCHEMACIE!
- `secret_richness_preference` ← NIE ISTNIEJE!
- `secret_texture_preference` ← NIE ISTNIEJE!
- Brak: `secret_chance_dine_random`, `secret_chance_pick_random_dish`, `secret_chance_to_update_rating`
- Brak: `secret_enjoyed_restaurant_themes`, `secret_enjoyed_variants`

**KOD używa** (phase4_users.py:119-136):
```python
user_data.append({
    "username": username,
    "email": email,
    "city_id": city_id,  # BŁĄD! Powinno być home_city_id
    "join_date": ...,  # BŁĄD! Powinno być account_created_at
    "secret_total_review_count": ...,
    "secret_enjoyed_archetypes": ...,
    "secret_ingredient_preferences": ...,
    "secret_price_preference_range": ...,
    "secret_spice_preference": ...,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "secret_richness_preference": ...,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "secret_texture_preference": ...,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "secret_cleanliness_preference": ...,
    "secret_preferred_ambiance": ...,
    "secret_mood_propensity": ...,
    "secret_cross_impact_factor": ...,
    "travel_propensity": ...  # BŁĄD! Powinno być secret_travel_propensity
})

# BRAKUJE WYMAGANYCH KOLUMN:
# - password_hash (WYMAGANE!)
# - secret_chance_dine_random
# - secret_chance_pick_random_dish
# - secret_chance_to_update_rating
```

---

## ❌ BŁĄD #9: NIEZGODNOŚĆ SCHEMATU - Restaurants (phase2_restaurants.py:25-46)

**Schema ma** (schema_updated.sql:44-75):
- `public_cuisine_theme` (zamiast `theme`)
- `created_at` (zamiast `created_date`)
- Brak kolumny `menu_blueprint`!
- Wymagane: `address`, `public_price_range`, etc.

**KOD używa** (phase2_restaurants.py:25-46):
```python
restaurant_data.append({
    "city_id": city_id,
    "restaurant_name": name,
    "theme": theme,  # BŁĄD! Powinno być public_cuisine_theme
    "created_date": ...,  # BŁĄD! Powinno być created_at
    "secret_price_multiplier": ...,
    "secret_overall_food_quality": ...,
    "secret_service_quality": ...,
    "secret_cleanliness_score": ...,
    "secret_ambiance_type": ...,
    "secret_ambiance_quality": ...,
    "menu_blueprint": menu_blueprint  # BŁĄD! Kolumna NIE ISTNIEJE!
})
```

---

## ❌ BŁĄD #10: NIEZGODNOŚĆ SCHEMATU - Dishes (phase3_dishes.py:75-86)

**Schema ma** (schema_updated.sql:110-128):
- `public_price`
- `secret_base_price`
- `secret_price_to_default_ratio`
- `secret_quality`
- `secret_spiciness`
- Brak: `archetype`, `secret_richness`, `secret_texture_score`, `popularity_factor`!

**KOD używa** (phase3_dishes.py:75-86):
```python
dish_data.append({
    "restaurant_id": restaurant_id,
    "dish_name": dish_name,
    "archetype": archetype,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "public_price": public_price,
    "secret_base_price": ...,
    "secret_quality": ...,
    "secret_spiciness": ...,
    "secret_richness": ...,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "secret_texture_score": ...,  # BŁĄD! Kolumna NIE ISTNIEJE!
    "popularity_factor": ...  # BŁĄD! Kolumna NIE ISTNIEJE!
})
```

---

## 📊 PODSUMOWANIE BŁĘDÓW

| # | Błąd | Priorytet | Skutek |
|---|------|-----------|--------|
| 1 | Cross-impact nie działa | **KRYTYCZNY** | Algorytm oceniania niepoprawny |
| 2 | Niepoprawny JSON w bazie | **KRYTYCZNY** | Parsowanie ZAWIEDZIE dla niektórych danych |
| 3 | Składniki nie są ładowane | **KRYTYCZNY** | Dopasowanie preferencji NIE DZIAŁA |
| 4 | Błędne dish_id w linkach | **KRYTYCZNY** | Powiązania składniki-dania BŁĘDNE |
| 5 | Błędne review_id dla zdjęć | **KRYTYCZNY** | Zdjęcia przypisane do złych recenzji |
| 6 | Niezgodność schematu Photos | **KRYTYCZNY** | INSERT FAIL - kolumny nie istnieją |
| 7 | Zdjęcia użytkowników do złej tabeli | **KRYTYCZNY** | Struktura danych niepoprawna |
| 8 | Niezgodność schematu Users | **KRYTYCZNY** | INSERT FAIL - brak kolumn |
| 9 | Niezgodność schematu Restaurants | **KRYTYCZNY** | INSERT FAIL - brak kolumn |
| 10 | Niezgodność schematu Dishes | **KRYTYCZNY** | INSERT FAIL - brak kolumn |

---

## 🔥 CAŁKOWITY WYNIK ANALIZY

**Status:** ❌ **KOD NIE ZADZIAŁA** - Insert do bazy ZAWIEDZIE z powodu nieistniejących kolumn!

**Kluczowe problemy:**
1. **Algorytm oceniania** ma błędy logiczne (cross-impact, składniki)
2. **Schema bazy danych** NIE PASUJE do kodu (~15 kolumn niezgodnych!)
3. **JSON nie jest poprawnie serializowany**
4. **ID są przypisywane przed insertem** zamiast po

**Co zrobić:**
1. **Naprawić schema** - dodać brakujące kolumny
2. **Naprawić kod** - dostosować nazwy kolumn do schematu
3. **Naprawić algorytm** - cross-impact, składniki, ID
4. **Dodać testy** - sprawdzić czy insert działa

---

## ⚠️ UWAGA

System **NIE ZADZIAŁA** bez naprawy tych błędów!
Próba uruchomienia `python main.py` zakończy się **błędem SQL**:
```
Column name 'theme' is invalid
Column name 'archetype' is invalid
Column name 'secret_spice_preference' is invalid
... (i wiele innych)
```

**WYMAGANA NATYCHMIASTOWA NAPRAWA!**
