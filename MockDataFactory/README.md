# 🍽️ MockDataFactory - Generator Danych dla Collaborative Filtering

Generator realistycznych danych symulacyjnych dla platformy recenzji kulinarnych Smakosz.

## 📋 Spis Treści

- [O Projekcie](#o-projekcie)
- [Architektura](#architektura)
- [Instalacja](#instalacja)
- [Użycie](#użycie)
- [Struktura Danych](#struktura-danych)
- [Algorytmy](#algorytmy)
- [Metryki](#metryki)

---

## 🎯 O Projekcie

MockDataFactory generuje **~875,000 recenzji** kulinarnych z zaawansowanym algorytmem oceniania (30+ czynników) specjalnie zaprojektowanym do trenowania modelu **Collaborative Filtering**.

### Kluczowe Liczby

- **18** polskich miast
- **~1,200** restauracji
- **~20,000** dań
- **~25,000** użytkowników (w tym 5% power users)
- **~875,000** recenzji (35 na użytkownika średnio)

### Zoptymalizowane Parametry CF

- **Sparsity:** 99.825% ✅
- **Coverage:** 95%+ dań z >10 recenzjami ✅
- **Mood Propensity:** 0.3 (zredukowano z 0.6)
- **Cross-Impact Factor:** 0.02 (zredukowano z 0.05)
- **Anchor Items:** 40% wizyt w TOP 20% restauracji

---

## 🏗️ Architektura

```
MockDataFactory/
│
├── utils/                      # 📦 Narzędzia pomocnicze
│   ├── db_connection.py       # Połączenie SQL Server (pyodbc)
│   ├── blueprint_loader.py    # Wczytywanie JSON
│   ├── statistical.py         # Rozkłady (Zipf, Beta, Normal)
│   ├── date_generator.py      # Generowanie dat z spójnością
│   ├── text_generator.py      # Polskie komentarze (21 szablonów)
│   └── photo_pools.py         # URL-e Unsplash
│
├── generators/                 # 🔧 Generatory danych (5 faz)
│   ├── phase1_core.py         # Miasta, składniki, tagi
│   ├── phase2_restaurants.py  # Restauracje + secret attributes
│   ├── phase3_dishes.py       # Dania + secret attributes
│   ├── phase4_users.py        # Użytkownicy + preferencje
│   └── phase5_reviews.py      # Recenzje (używa rating engine!)
│
├── algorithms/                 # 🧠 Inteligencja (KLUCZOWE!)
│   ├── rating_engine.py       # Algorytm 30+ czynników
│   ├── restaurant_selector.py # Wybór restauracji (anchor items)
│   └── dish_selector.py       # Wybór dania (preferencje)
│
├── blueprints/                 # ✅ Konfiguracje JSON (GOTOWE)
│   ├── 00_global_rules.json
│   ├── 01_city_rules.json
│   ├── 02_restaurant_rules.json
│   ├── 03_menu_blueprints_flat_backup.json
│   ├── 04_user_persona_rules.json
│   └── dish_variants.json
│
├── main.py                     # 🚀 Orkiestrator (punkt wejścia)
├── config.py                   # ⚙️ Konfiguracja
└── requirements.txt            # 📦 Zależności
```

---

## 💻 Instalacja

### 1. Wymagania

- Python >= 3.8
- SQL Server (z ODBC Driver 17)
- Baza danych `MockDataDB` (schemat z `schema_updated.sql`)

### 2. Instalacja zależności

```bash
cd MockDataFactory
pip install -r requirements.txt
```

### 3. Konfiguracja

Ustaw zmienne środowiskowe (opcjonalnie):

```bash
export DB_SERVER="localhost"
export DB_NAME="MockDataDB"
export DB_DRIVER="ODBC Driver 17 for SQL Server"
export DB_TRUSTED="yes"
```

Lub edytuj `config.py`:

```python
DATABASE_CONFIG = {
    'server': 'localhost',
    'database': 'MockDataDB',
    'driver': 'ODBC Driver 17 for SQL Server',
    'trusted_connection': 'yes'
}
```

---

## 🚀 Użycie

### Uruchomienie generacji

```bash
python main.py
```

### Przebieg (5 faz)

```
🚀 MOCKDATAFACTORY - START
====================================

📍 PHASE 1: Miasta, składniki, tagi
  ✅ 18 miast
  ✅ 180 składników
  ✅ 70 tagów

🏪 PHASE 2: Restauracje
  ✅ 1,200 restauracji
  ✅ 3,600 zdjęć restauracji

🍕 PHASE 3: Dania
  ✅ 20,000 dań
  ✅ 60,000 powiązań składników

👥 PHASE 4: Użytkownicy
  ✅ 25,000 użytkowników
  ✅ 1,250 power users (5%)

⭐ PHASE 5: Recenzje (10-15 minut)
  ✅ 875,000 recenzji
  ✅ 262,500 zdjęć użytkowników

📊 STATYSTYKI:
  Sparsity: 99.825%
  Coverage: 95%+
  Avg reviews/user: 35
  Avg reviews/dish: 43.75

✅ MOCKDATAFACTORY - ZAKOŃCZONE
Czas trwania: ~15-20 minut
```

---

## 📊 Struktura Danych

### Secret Attributes (dla CF)

#### Restauracje
- `secret_price_multiplier` (0.8-1.3)
- `secret_overall_food_quality` (0.4-0.95, beta)
- `secret_service_quality` (0.3-0.95)
- `secret_cleanliness_score` (3.0-9.5)
- `secret_ambiance_type` + `secret_ambiance_quality`

#### Dania
- `secret_base_price` + `public_price`
- `secret_quality` (0.3-0.95, beta)
- `secret_spiciness` (0-10)
- `secret_richness` (0.0-1.0)
- `secret_texture_score` (0.0-1.0)

#### Użytkownicy
- `secret_enjoyed_archetypes` ({"Pizza": 0.9, ...})
- `secret_ingredient_preferences`
- `secret_price_preference_range`
- `secret_mood_propensity` (0.3 ± 0.05) **ZOPTYMALIZOWANE**
- `secret_cross_impact_factor` (0.02 ± 0.01) **ZOPTYMALIZOWANE**
- `travel_propensity` (0.20 ± 0.05)

---

## 🧠 Algorytmy

### Rating Engine (30+ czynników)

Algorytm oceniania (`algorithms/rating_engine.py`) oblicza 6 ocen:

#### 1. FOOD SCORE (40% wpływu)
- Jakość (30%): dish quality + restaurant quality
- Ostrość (10%): dopasowanie do preferencji
- Bogactwo (10%): richness preference
- Tekstura (10%): texture preference
- Składniki (15%): ingredient preferences
- Typ kuchni (15%): archetype affinity
- Nastrój (10%): mood variance (ZOPTYMALIZOWANE: 0.3)

#### 2. SERVICE SCORE (15% wpływu)
- Restaurant service quality + losowa wariancja

#### 3. CLEANLINESS SCORE (15% wpływu)
- Restaurant cleanliness vs user expectations

#### 4. AMBIANCE SCORE (10% wpływu)
- Restaurant ambiance + type matching

#### 5. VALUE FOR MONEY (10% wpływu)
- Price vs user's price tolerance

#### 6. OVERALL RATING (ważona średnia)
```python
overall = (
    food_score * 0.40 +
    service_score * 0.15 +
    cleanliness_score * 0.15 +
    ambiance_score * 0.10 +
    value_score * 0.10 +
    cross_impact * 0.10  # Efekt halo (0.02)
)
```

### Restaurant Selector

**Anchor Items dla CF:**
- 40% wizyt w TOP 20% najpopularniejszych
- 60% losowo z reszty
- Power users: 80% wizyt w TOP 30%

### Dish Selector

- 95% bazowane na preferencjach
- 5% losowe (eksploracja)
- Unikanie nielubianych składników

---

## 📈 Metryki

### Oczekiwane Metryki CF

| Metryka | Wartość | Status |
|---------|---------|--------|
| **Sparsity** | 99.825% | ✅ |
| **Coverage** | 95%+ | ✅ |
| **Total Reviews** | ~875,000 | ✅ |
| **Avg Reviews/User** | 35 | ✅ |
| **Avg Reviews/Dish** | 43.75 | ✅ |
| **Expected RMSE** | 0.9-1.2 | ✅ |
| **User-User Similarity** | 0.6-0.7 | ✅ |

### Walidacja

Po generacji sprawdź metryki:

```sql
-- Sparsity
SELECT
    1 - (CAST(COUNT(*) AS FLOAT) /
    ((SELECT COUNT(*) FROM Users) * (SELECT COUNT(*) FROM Dishes))) * 100
    AS Sparsity
FROM Reviews;

-- Coverage
SELECT
    COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Dishes) AS Coverage
FROM (
    SELECT dish_id, COUNT(*) AS review_count
    FROM Reviews
    GROUP BY dish_id
    HAVING COUNT(*) > 10
) AS covered_dishes;
```

---

## 🔧 Konfiguracja

Wszystkie parametry w `config.py`:

```python
GENERATION_CONFIG = {
    'num_users': 25000,
    'avg_reviews_per_user': 35,
    'power_user_percentage': 0.05,

    # ZOPTYMALIZOWANE:
    'default_mood_propensity': 0.3,
    'default_cross_impact_factor': 0.02,
    'default_travel_propensity': 0.20,
    'anchor_visit_rate': 0.40,
}
```

---

## 📝 Logi

Logi zapisywane w `mockdata_generation.log`:

```
2024-01-15 10:00:00 - INFO - 🚀 MOCKDATAFACTORY - START
2024-01-15 10:00:01 - INFO - 📍 PHASE 1: Generowanie danych podstawowych
2024-01-15 10:00:05 - INFO - ✅ Wygenerowano 18 miast
...
2024-01-15 10:15:00 - INFO - ✅ MOCKDATAFACTORY - ZAKOŃCZONE POMYŚLNIE
```

---

## 🎯 Kluczowe Pliki

### Najważniejsze do zrozumienia algorytmu:

1. **`algorithms/rating_engine.py`** - Rdzeń systemu (30+ czynników)
2. **`generators/phase5_reviews.py`** - Jak recenzje są generowane
3. **`config.py`** - Wszystkie zoptymalizowane parametry

### Dokumentacja (gotowa):

- `GENERATION_STRATEGY.md` - Szczegółowa strategia (1200 linii)
- `SCHEMA_REFERENCE.md` - Referencja schematu (500 linii)

---

## 🚨 Troubleshooting

### Błąd połączenia z bazą

```
❌ Błąd połączenia: [ODBC Driver error]
```

**Rozwiązanie:**
1. Sprawdź czy SQL Server działa
2. Weryfikuj connection string w `config.py`
3. Upewnij się że ODBC Driver 17 jest zainstalowany

### Brak blueprintów

```
FileNotFoundError: Folder blueprints nie istnieje!
```

**Rozwiązanie:**
Upewnij się że folder `blueprints/` z plikami JSON jest w tym samym katalogu co `main.py`.

### Wolne generowanie

Phase 5 (recenzje) może zająć 10-15 minut. To normalne - generuje 875,000 rekordów z zaawansowanymi obliczeniami.

---

## 📄 Licencja

Projekt wewnętrzny dla Smakosz Web Application.

---

## 👨‍💻 Autorzy

- **Szczepan Greła** - Architektura i implementacja
- **Claude Code** - Implementacja kodu

---

## 🎉 Status: ✅ GOTOWE DO UŻYCIA

Wszystkie 20 zadań zaimplementowane (3000+ linii kodu):
- ✅ UTILS (7 plików)
- ✅ GENERATORS (5 plików)
- ✅ ALGORITHMS (3 pliki)
- ✅ ORCHESTRATOR (main.py)
- ✅ CONFIG (config.py)
- ✅ DEPENDENCIES (requirements.txt)

**Rozpocznij generację:** `python main.py`
