# MockDataFactory - Raport Dogłębnej Analizy Błędów

**Data:** 2025-11-18
**Analiza:** Ultra-szczegółowa weryfikacja z maksymalną dociekliwością
**Status:** ❌ **ZNALEZIONO KRYTYCZNE BŁĘDY - WYMAGA NAPRAWY!**

---

## 🔴 BŁĘDY KRYTYCZNE (Zablokują działanie)

### 1. **N+1 Query Problem - Phase 5 Reviews** 🔥🔥🔥

**Plik:** `generators/phase5_reviews.py`, linie 185-191
**Severity:** **KRYTYCZNA** - spowoduje działanie przez GODZINY zamiast 15 minut

**Problem:**
```python
for d in dishes:
    dish_id = d[0]
    # ❌ BARDZO WOLNE - wykonuje osobne zapytanie dla KAŻDEGO dania!
    dish_ingredients = db.fetch_all("""
        SELECT i.ingredient_name
        FROM Dish_Ingredients_Link dil
        JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id
        WHERE dil.dish_id = ?
    """, (dish_id,))
```

**Dlaczego to problem:**
- Kod jest wewnątrz pętli generującej 875,000 recenzji
- Każda recenzja wybiera restaurację z ~17 daniami średnio
- Dla KAŻDEGO dania wykonuje osobne zapytanie SQL
- **Szacunek:** 875,000 reviews × 17 dishes = **~14,875,000 zapytań SQL!**
- **Czas:** Przy 10ms per query = **41 GODZIN** zamiast 15 minut!

**Rozwiązanie:**
```python
# Pobierz WSZYSTKIE składniki dla WSZYSTKICH dań restauracji za jednym razem
dish_ids = [d[0] for d in dishes]
placeholders = ','.join(['?'] * len(dish_ids))
all_ingredients = db.fetch_all(f"""
    SELECT dil.dish_id, i.ingredient_name
    FROM Dish_Ingredients_Link dil
    JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id
    WHERE dil.dish_id IN ({placeholders})
""", tuple(dish_ids))

# Grupuj per dish_id
ingredients_by_dish = {}
for dish_id, ingredient_name in all_ingredients:
    if dish_id not in ingredients_by_dish:
        ingredients_by_dish[dish_id] = []
    ingredients_by_dish[dish_id].append(ingredient_name)
```

**Impact:** Redukcja z 14.8M zapytań do ~875K (jedno per review) = **17x szybciej!**

---

### 2. **Niespójność Nazewnictwa Restrykcji** 🔴

**Plik:** `generators/phase1_core.py`, linie 171-201 vs `schema_updated.sql`, linia 104
**Severity:** **KRYTYCZNA** - dane nie będą pasować do schematu

**Problem:**

**Schema definiuje (angielskie):**
```sql
-- restriction_type values: 'vegetarian', 'vegan', 'gluten-free',
-- 'lactose-free', 'nut-allergy', 'halal', 'kosher', 'shellfish-allergy'
```

**Kod wstawia (polskie):**
```python
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "Wegetariańskie"  # ❌ Powinno być 'vegetarian'
})
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "Wegańskie"  # ❌ Powinno być 'vegan'
})
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "Bez laktozy"  # ❌ Powinno być 'lactose-free'
})
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "Bezglutenowe"  # ❌ Powinno być 'gluten-free'
})
```

**Konsekwencje:**
- Zapytania filtrujące po `restriction_type = 'vegetarian'` nie znajdą nic
- Frontend/API szukające 'vegan' nie znajdzie nic
- Funkcjonalność filtrów dietetycznych całkowicie zepsuta

**Rozwiązanie:**
```python
# Użyj angielskich nazw zgodnie ze schematem
if any(meat in ingredient_lower for meat in ["mięso", "wołowina", ...]):
    restrictions.append({
        "ingredient_id": ingredient_id,
        "restriction_type": "vegetarian"  # ✅ Angielskie
    })
    restrictions.append({
        "ingredient_id": ingredient_id,
        "restriction_type": "vegan"  # ✅ Angielskie
    })
```

---

### 3. **Division by Zero Risk** 🔴

**Plik:** `generators/phase5_reviews.py`, linia 223
**Severity:** **WYSOKA** - może wywołać crash

**Problem:**
```python
price_ratio=selected_dish['public_price'] / user_data['secret_price_preference_range']
```

**Co jeśli:**
- `user_data['secret_price_preference_range']` jest 0?
- `user_data['secret_price_preference_range']` jest None?

**Domyślna wartość w linii 109:**
```python
'secret_price_preference_range': float(user[6]) if user[6] else 35.0
```

Ale `float(user[6])` może być 0.0 jeśli w bazie jest '0' lub 0.

**Rozwiązanie:**
```python
price_pref = user_data.get('secret_price_preference_range', 35.0)
if price_pref == 0:
    price_pref = 35.0  # Fallback

price_ratio = selected_dish['public_price'] / price_pref
```

---

## 🟡 BŁĘDY WYSOKIEGO PRIORYTETU (Powinny być naprawione)

### 4. **Brakujące Pola w Restaurants** 🟡

**Plik:** `generators/phase2_restaurants.py`, linie 74-87
**Severity:** **WYSOKA** - wiele pól pozostanie NULL

**Problem:**

Schema definiuje te pola:
```sql
public_price_range NVARCHAR(5),       -- ❌ NIE MA w kodzie
address NVARCHAR(200),                -- ❌ NIE MA w kodzie
latitude DECIMAL(10,7),               -- ❌ NIE MA w kodzie
longitude DECIMAL(10,7),              -- ❌ NIE MA w kodzie
phone NVARCHAR(20),                   -- ❌ NIE MA w kodzie
website NVARCHAR(200),                -- ❌ NIE MA w kodzie
description NVARCHAR(1000),           -- ❌ NIE MA w kodzie
image_url NVARCHAR(500),              -- ❌ NIE MA w kodzie
```

**Kod wstawia tylko:**
```python
restaurant_data.append({
    "city_id": city_id,
    "restaurant_name": name,
    "public_cuisine_theme": theme,
    "theme": theme,
    "created_at": created_date,
    "secret_price_multiplier": ...,
    "secret_overall_food_quality": ...,
    # ... secret attributes ...
    "menu_blueprint": menu_blueprint
    # ❌ Brak: address, phone, website, description, image_url, public_price_range!
})
```

**Konsekwencje:**
- Restauracje nie będą miały adresów (Google Maps nie zadziała)
- Brak numerów telefonu (nie można zadzwonić)
- Brak opisów (UI będzie puste)
- Brak zdjęć restauracji (tylko placeholder)
- `public_price_range` NULL - filtry po cenie nie zadziałają

**Rozwiązanie:**
Dodać generowanie tych pól:
```python
from faker import Faker
fake = Faker('pl_PL')

restaurant_data.append({
    # ... existing fields ...
    "address": f"{fake.street_address()}, {city_name}",
    "latitude": round(random.uniform(49.0, 54.5), 7),
    "longitude": round(random.uniform(14.0, 24.0), 7),
    "phone": fake.phone_number(),
    "website": f"https://{name.lower().replace(' ', '')}.pl",
    "description": f"Restauracja {theme} w sercu {city_name}...",
    "image_url": photo_pools.get_restaurant_photo(theme),
    "public_price_range": _calculate_price_range(secret_price_multiplier),  # $$-$$$
})
```

---

### 5. **Float Conversion Without Try/Catch** 🟡

**Plik:** `generators/phase5_reviews.py`, linia 109
**Severity:** **ŚREDNIA** - może wywołać ValueError

**Problem:**
```python
'secret_price_preference_range': float(user[6]) if user[6] else 35.0
```

**Co jeśli `user[6]` jest:**
- String który nie jest liczbą? → `ValueError: could not convert string to float`
- `'abc'`? → Crash
- `''` (pusty string)? → Już obsłużone przez `if user[6]`

**Rozwiązanie:**
```python
def safe_float(value, default=35.0):
    if value is None or value == '':
        return default
    try:
        return float(value)
    except (ValueError, TypeError):
        return default

'secret_price_preference_range': safe_float(user[6], 35.0)
```

---

## 🟢 PROBLEMY NISKIEGO PRIORYTETU (Code Quality)

### 6. **SQL Injection Risk (Teoretyczne)** 🟢

**Plik:** `utils/db_connection.py`, linie 67, 88
**Severity:** **NISKA** - table name jest kontrolowany przez kod

**Problem:**
```python
sql = f"INSERT INTO {table} ({columns}) VALUES ({placeholders})"
```

Używa f-stringa do wstawienia nazwy tabeli. W teorii, jeśli `table` pochodziłoby z user input, to byłaby SQL injection.

**Ale:**
- `table` jest zawsze hardcoded w kodzie (`"Cities"`, `"Restaurants"`, etc.)
- Nie pochodzi z external input
- **Ryzyko praktyczne: BARDZO NISKIE**

**Rozwiązanie (opcjonalne):**
```python
# Whitelist dozwolonych tabel
ALLOWED_TABLES = {
    'Cities', 'Restaurants', 'Dishes', 'Users', 'Reviews',
    'Ingredients', 'Tags', 'Photos', 'User_Photos', # etc.
}

def insert_bulk(self, table: str, data_list: List[Dict[str, Any]]):
    if table not in ALLOWED_TABLES:
        raise ValueError(f"Invalid table name: {table}")
    # ... reszta kodu
```

---

### 7. **Missing Default in .get()** 🟢

**Plik:** `generators/phase3_dishes.py`, linia 143
**Severity:** **NISKA** - zwróci None jeśli klucz nie istnieje

**Problem:**
```python
matching_dishes = [v for v in variants if v.get("archetype") in archetypes]
```

Jeśli `v` nie ma klucza `"archetype"`, `.get("archetype")` zwróci `None`, a `None in archetypes` zwróci `False`. To jest OK, ale lepiej być explicit:

**Rozwiązanie:**
```python
matching_dishes = [v for v in variants if v.get("archetype", "") in archetypes]
```

---

## 📋 Podsumowanie Statystyk Błędów

| Kategoria | Liczba | Severity |
|-----------|--------|----------|
| **Krytyczne** | 3 | 🔴🔴🔴 |
| **Wysokie** | 2 | 🟡🟡 |
| **Niskie** | 2 | 🟢🟢 |
| **RAZEM** | **7** | |

### Błędy Krytyczne:
1. ✗ N+1 Query Problem (41h → 15min po naprawie)
2. ✗ Niespójność nazw restrykcji (filtry nie zadziałają)
3. ✗ Division by zero risk (crash)

### Błędy Wysokie:
4. ⚠ Brakujące pola Restaurants (NULL addresses, phones, descriptions)
5. ⚠ Float conversion bez try/catch

### Błędy Niskie:
6. ⚠ Teoretyczne SQL injection (praktycznie bezpieczne)
7. ⚠ Missing default w .get()

---

## 🎯 Priorytet Napraw

### NAJPIERW (przed pierwszym uruchomieniem):
1. **Napraw N+1 Problem** - bez tego generator będzie działał 41 godzin
2. **Napraw nazwy restrykcji** - użyj angielskich nazw zgodnie ze schematem
3. **Dodaj zabezpieczenie division by zero**

### POTEM (dla kompletności danych):
4. **Dodaj brakujące pola Restaurants** (address, phone, website, description, image_url)
5. **Dodaj safe_float() helper**

### OPCJONALNIE (code quality):
6. Dodaj whitelist tabel w db_connection.py
7. Dodaj defaulty w .get()

---

## 🔧 Gotowe Rozwiązania

### Fix #1: N+1 Problem

**Zamień:**
```python
# ❌ STARY KOD (wolny)
for d in dishes:
    dish_id = d[0]
    dish_ingredients = db.fetch_all("""
        SELECT i.ingredient_name
        FROM Dish_Ingredients_Link dil
        JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id
        WHERE dil.dish_id = ?
    """, (dish_id,))
    ingredient_names = [ing[0] for ing in dish_ingredients]
    dish_dicts.append({
        # ...
        'ingredients': ingredient_names
    })
```

**Na:**
```python
# ✅ NOWY KOD (szybki)
# Pobierz WSZYSTKIE składniki dla WSZYSTKICH dań naraz
dish_ids = [d[0] for d in dishes]
if dish_ids:
    placeholders = ','.join(['?'] * len(dish_ids))
    all_ingredients = db.fetch_all(f"""
        SELECT dil.dish_id, i.ingredient_name
        FROM Dish_Ingredients_Link dil
        JOIN Ingredients i ON dil.ingredient_id = i.ingredient_id
        WHERE dil.dish_id IN ({placeholders})
    """, tuple(dish_ids))

    # Grupuj per dish_id
    ingredients_by_dish = {}
    for dish_id, ing_name in all_ingredients:
        if dish_id not in ingredients_by_dish:
            ingredients_by_dish[dish_id] = []
        ingredients_by_dish[dish_id].append(ing_name)

    # Teraz buduj dish_dicts
    for d in dishes:
        dish_id = d[0]
        dish_dicts.append({
            'dish_id': dish_id,
            'dish_name': d[1],
            # ...
            'ingredients': ingredients_by_dish.get(dish_id, [])
        })
```

---

### Fix #2: Nazwy Restrykcji

**Zamień w phase1_core.py:**
```python
# ❌ STARY KOD (polskie nazwy)
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "Wegetariańskie"
})
```

**Na:**
```python
# ✅ NOWY KOD (angielskie nazwy zgodnie ze schematem)
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "vegetarian"
})
restrictions.append({
    "ingredient_id": ingredient_id,
    "restriction_type": "vegan"
})
# ... podobnie dla 'gluten-free', 'lactose-free'
```

---

### Fix #3: Division by Zero

**Dodaj helper function na początku phase5_reviews.py:**
```python
def safe_divide(numerator, denominator, default=1.0):
    """Bezpieczne dzielenie z zabezpieczeniem przed zerem"""
    if denominator == 0 or denominator is None:
        return default
    return numerator / denominator
```

**Użyj:**
```python
price_ratio = safe_divide(
    selected_dish['public_price'],
    user_data['secret_price_preference_range'],
    default=1.0
)
```

---

## ⏱️ Szacowany Czas Napraw

| Fix | Czas | Trudność |
|-----|------|----------|
| #1 N+1 Problem | 15 minut | Średnia |
| #2 Nazwy restrykcji | 5 minut | Łatwa |
| #3 Division by zero | 5 minut | Łatwa |
| #4 Pola Restaurants | 30 minut | Średnia |
| #5 safe_float() | 10 minut | Łatwa |
| **RAZEM** | **~65 minut** | |

---

## ✅ Co Działa Poprawnie

Mimo znalezionych błędów, wiele rzeczy jest zaimplementowanych dobrze:

- ✅ Kompilacja wszystkich plików Python
- ✅ Struktura modułów i importy
- ✅ Rating engine algorytm (30+ czynników)
- ✅ Restaurant/dish selectors (Zipf, anchor items)
- ✅ Date generator z proper spacing
- ✅ Context manager w DatabaseConnection
- ✅ Bulk inserting (oprócz N+1)
- ✅ Error logging
- ✅ Safe JSON parsing (safe_json_loads)
- ✅ Schema 17 tabel - kompletny

---

## 🚀 Następne Kroki

### Przed uruchomieniem:
1. ❌ **NAPRAW 3 błędy krytyczne** (65 minut pracy)
2. ✅ Zainstaluj dependencies: `pip install -r requirements.txt`
3. ✅ Utwórz bazę SQL Server i wykonaj schema
4. ✅ Skonfiguruj config.py

### Po naprawieniu błędów:
5. ✅ Uruchom: `python3 main.py`
6. ✅ Czekaj ~15-20 minut (po naprawie N+1)
7. ✅ Wykonaj: `EXEC UpdateAverageRatings`

---

**Data raportu:** 2025-11-18
**Wersja:** 1.0.0
**Analiza przez:** Claude (ultra-deep audit)
**Rekomendacja:** Napraw 3 błędy krytyczne przed pierwszym uruchomieniem!
