"""
Shared constants for the MockDataFactory generator pipeline.

These are the single source of truth for restaurant theme mappings and menu blueprints.
Both the generators (phase2, phase3) and tools (verify_blueprints) import from here.
"""

# Maps Polish restaurant theme names (from blueprints/restaurant_types.json)
# to English menu blueprint profile names used internally by the generator.
THEME_TO_MENU_BLUEPRINT: dict[str, str] = {
    "Pizzeria": "Pizzeria",
    "Burgerownia": "Burger Bar",
    "Sushi Bar": "Sushi Bar",
    "Kuchnia Azjatycka": "Asian Fusion",
    "Kuchnia Wietnamska": "Asian Fusion",
    "Kuchnia Chińska": "Asian Fusion",
    "Ramen Bar": "Asian Fusion",
    "Steakhouse": "Steakhouse",
    "Kawiarnia": "Cafe",
    "Piekarnia z Kawiarnią": "Cafe",
    "Bar Meksykański": "Mexican Restaurant",
    "Kuchnia Włoska": "Italian Restaurant",
    "Francuskie Bistro": "French Bistro",
    "Restauracja z Owocami Morza": "Seafood Restaurant",
    "Kebab": "Kebab Place",
    "Kuchnia Polska": "Polish Restaurant",
    "Kuchnia Indyjska": "Indian Restaurant",
    "Grecka Taverna": "Greek Taverna",
    "Wędzarnia BBQ": "BBQ Smokehouse",
    "Korean BBQ": "Korean Restaurant",
    "Bar Tapas": "Tapas Bar",
    "Amerykański Diner": "American Diner",
    "Niemiecki Pub": "German Pub",
    "Kuchnia Bliskowschodnia": "Middle Eastern",
    "Kuchnia Turecka": "Middle Eastern",
    "Lodziarnia": "Ice Cream Shop",
    "Kanapkownia": "Sandwich Shop",
    "Wykwintna Restauracja": "Fine Dining",
    # Fallback: any theme not listed -> "General"
}

# Maps English menu blueprint profile names to dish generation config.
# Each entry specifies which dish archetypes are served and the statistical
# parameters for how many dishes to generate (mean ± sigma).
MENU_BLUEPRINTS: dict[str, dict] = {
    "Pizzeria": {"archetypes": ["Pizza", "Pasta", "Salad", "Deser"], "mean": 25, "sigma": 5},
    "Burger Bar": {"archetypes": ["Burger", "Steak", "Salad"], "mean": 15, "sigma": 3},
    "Sushi Bar": {"archetypes": ["Sushi", "Soup", "Salad"], "mean": 40, "sigma": 8},
    "Asian Fusion": {
        "archetypes": ["Ramen", "Noodles", "Dim Sum", "Pho", "Curry", "Sushi", "Kanapka", "Danie Azjatyckie"],
        "mean": 35,
        "sigma": 7,
    },
    "Steakhouse": {"archetypes": ["Steak", "BBQ", "Burger", "Salad"], "mean": 20, "sigma": 4},
    "Vegan Cafe": {"archetypes": ["Vegan", "Salad", "Soup", "Smoothie Bowl"], "mean": 22, "sigma": 5},
    "Mexican Restaurant": {"archetypes": ["Tacos", "Quesadilla", "Nachos", "Burrito"], "mean": 28, "sigma": 6},
    "Italian Restaurant": {"archetypes": ["Pizza", "Pasta", "Risotto", "Gnocchi", "Deser"], "mean": 30, "sigma": 6},
    "French Bistro": {"archetypes": ["Steak", "Soup", "Fondue", "Deser"], "mean": 20, "sigma": 4},
    "Seafood Restaurant": {"archetypes": ["Seafood", "Sushi", "Oysters", "Fish"], "mean": 25, "sigma": 5},
    "General": {"archetypes": ["Pizza", "Burger", "Pasta", "Salad", "Kebab", "Zupa"], "mean": 20, "sigma": 5},
    "Kebab Place": {"archetypes": ["Kebab", "Salad", "Frytki", "Napój Bezalkoholowy"], "mean": 12, "sigma": 3},
    "Polish Restaurant": {"archetypes": ["Danie Polskie", "Zupa", "Pierogi", "Deser", "Piwo"], "mean": 25, "sigma": 5},
    "Indian Restaurant": {"archetypes": ["Curry", "Naan", "Ryż", "Zupa"], "mean": 30, "sigma": 6},
    "Greek Taverna": {"archetypes": ["Danie Greckie", "Sałatka", "Owoce Morza", "Wino"], "mean": 28, "sigma": 5},
    "BBQ Smokehouse": {"archetypes": ["Dania BBQ", "Stek", "Burger", "Frytki", "Piwo"], "mean": 25, "sigma": 5},
    "Korean Restaurant": {
        "archetypes": ["Danie Koreańskie", "Zupa", "Ryż", "Danie Azjatyckie"],
        "mean": 26,
        "sigma": 6,
    },
    "Tapas Bar": {"archetypes": ["Tapas", "Wino", "Owoce Morza", "Przystawka"], "mean": 18, "sigma": 4},
    "American Diner": {"archetypes": ["Burger", "Milkshake", "Naleśniki", "Frytki", "Kawa"], "mean": 20, "sigma": 5},
    "German Pub": {"archetypes": ["Danie Niemieckie", "Kiełbasa", "Piwo", "Precel"], "mean": 22, "sigma": 4},
    "Middle Eastern": {"archetypes": ["Danie Bliskowschodnie", "Kebab", "Hummus", "Falafel"], "mean": 24, "sigma": 5},
    "Ice Cream Shop": {
        "archetypes": ["Lody", "Sorbet", "Deser", "Milkshake", "Kawa", "Gorąca Czekolada"],
        "mean": 15,
        "sigma": 4,
    },
    "Sandwich Shop": {
        "archetypes": ["Kanapka", "Panini", "Sałatka", "Kawa", "Napój Bezalkoholowy"],
        "mean": 12,
        "sigma": 3,
    },
    "Cafe": {"archetypes": ["Kawa", "Herbata", "Deser", "Ciasto", "Kanapka"], "mean": 15, "sigma": 4},
    "Fine Dining": {
        "archetypes": ["Stek", "Owoce Morza", "Wino", "Deser", "Danie Francuskie"],
        "mean": 45,
        "sigma": 10,
    },
}
