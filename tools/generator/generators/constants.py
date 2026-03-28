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
    "Piekarnia z Kawiarnią": "Bakery Cafe",
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
}

MENU_BLUEPRINTS: dict[str, dict] = {
    "Pizzeria": {"archetypes": ["Pizza", "Makaron", "Sałatka", "Deser"], "mean": 25, "sigma": 5},
    "Burger Bar": {"archetypes": ["Burger", "Frytki", "Napój Bezalkoholowy"], "mean": 15, "sigma": 3},
    "Sushi Bar": {"archetypes": ["Sushi", "Zupa", "Sałatka", "Przystawka", "Napój Bezalkoholowy"], "mean": 40, "sigma": 8},
    "Asian Fusion": {
        "archetypes": ["Ramen", "Danie Azjatyckie", "Pho", "Curry", "Sushi", "Zupa", "Deser", "Napój Bezalkoholowy"],
        "mean": 35,
        "sigma": 7,
    },
    "Steakhouse": {
        "archetypes": ["Stek", "Dania BBQ", "Burger", "Sałatka", "Frytki", "Zupa", "Deser", "Napój Bezalkoholowy"],
        "mean": 20,
        "sigma": 4,
    },
    "Vegan Cafe": {"archetypes": ["Sałatka", "Zupa", "Deser", "Drink", "Wrap"], "mean": 22, "sigma": 5},
    "Mexican Restaurant": {
        "archetypes": ["Taco", "Mexican", "Wrap", "Przystawka", "Sałatka", "Zupa", "Deser", "Napój Bezalkoholowy"],
        "mean": 28,
        "sigma": 6,
    },
    "Italian Restaurant": {
        "archetypes": ["Pizza", "Makaron", "Risotto", "Deser", "Sałatka", "Wino"],
        "mean": 30,
        "sigma": 6,
    },
    "French Bistro": {
        "archetypes": ["Stek", "Zupa", "Danie Francuskie", "Deser", "Sałatka", "Wino"],
        "mean": 20,
        "sigma": 4,
    },
    "Seafood Restaurant": {
        "archetypes": ["Ryby i Owoce Morza", "Sushi", "Zupa", "Sałatka", "Przystawka", "Deser", "Wino"],
        "mean": 25,
        "sigma": 5,
    },
    "General": {"archetypes": ["Pizza", "Burger", "Makaron", "Sałatka", "Kebab", "Zupa"], "mean": 20, "sigma": 5},
    "Kebab Place": {"archetypes": ["Kebab", "Sałatka", "Frytki", "Napój Bezalkoholowy"], "mean": 12, "sigma": 3},
    "Polish Restaurant": {"archetypes": ["Danie Polskie", "Zupa", "Pierogi", "Deser", "Piwo"], "mean": 25, "sigma": 5},
    "Indian Restaurant": {"archetypes": ["Curry", "Naan", "Dodatek", "Zupa", "Deser", "Napój Bezalkoholowy"], "mean": 30, "sigma": 6},
    "Greek Taverna": {
        "archetypes": ["Danie Greckie", "Sałatka", "Ryby i Owoce Morza", "Wino", "Zupa", "Deser"],
        "mean": 28,
        "sigma": 5,
    },
    "BBQ Smokehouse": {"archetypes": ["Dania BBQ", "Stek", "Burger", "Frytki", "Piwo", "Zupa", "Deser"], "mean": 25, "sigma": 5},
    "Korean Restaurant": {
        "archetypes": ["Danie Koreańskie", "Zupa", "Dodatek", "Danie Azjatyckie", "Deser", "Napój Bezalkoholowy"],
        "mean": 26,
        "sigma": 6,
    },
    "Tapas Bar": {"archetypes": ["Tapas", "Wino", "Ryby i Owoce Morza", "Przystawka", "Zupa", "Deser"], "mean": 18, "sigma": 4},
    "American Diner": {"archetypes": ["Burger", "Milkshake", "Naleśniki", "Frytki", "Kawa", "Zupa", "Śniadanie"], "mean": 20, "sigma": 5},
    "German Pub": {"archetypes": ["Danie Niemieckie", "Pieczywo", "Piwo", "Zupa", "Deser"], "mean": 22, "sigma": 4},
    "Middle Eastern": {
        "archetypes": ["Danie Bliskowschodnie", "Kebab", "Przystawka", "Sałatka", "Zupa", "Deser", "Napój Bezalkoholowy"],
        "mean": 24,
        "sigma": 5,
    },
    "Ice Cream Shop": {
        "archetypes": ["Lody", "Sorbet", "Deser", "Milkshake", "Kawa", "Gorąca Czekolada", "Naleśniki"],
        "mean": 15,
        "sigma": 4,
    },
    "Sandwich Shop": {
        "archetypes": ["Kanapka", "Panini", "Sałatka", "Kawa", "Napój Bezalkoholowy", "Zupa", "Deser"],
        "mean": 12,
        "sigma": 3,
    },
    "Bakery Cafe": {"archetypes": ["Pieczywo", "Deser", "Kanapka", "Kawa", "Herbata", "Śniadanie"], "mean": 15, "sigma": 4},
    "Cafe": {"archetypes": ["Kawa", "Herbata", "Deser", "Kanapka", "Milkshake", "Śniadanie"], "mean": 15, "sigma": 4},
    "Fine Dining": {
        "archetypes": ["Stek", "Ryby i Owoce Morza", "Wino", "Deser", "Danie Francuskie", "Przystawka", "Zupa"],
        "mean": 45,
        "sigma": 10,
    },
}

MEAT_KEYWORDS: list[str] = [
    "mięso", "kurczak", "wołowina", "wieprzowina", "boczek", "szynka", "kiełbasa",
    "ryba", "łosoś", "tuńczyk", "krewetki",
    "befsztyk", "polędwica", "antrykot", "rostbef", "stek",
    "kaczka", "indyk", "jagnięcina", "cielęcina",
    "dziczyzna", "królik", "wątroba", "wątróbka", "smalec", "słonina",
    "pepperoni", "salami", "mortadela", "parówka", "flaki",
    "żeberka", "skrzydełka", "kotlet", "schab", "karkówka", "łopatka", "bekon",
    "chorizo", "prosciutto", "chashu", "homar", "krab", "małże", "kalmary",
    "węgorz", "dorsz", "okoń", "pasztet", "klopsiki", "mielona",
]

DAIRY_KEYWORDS: list[str] = [
    "ser", "mleko", "śmietana", "śmietanka", "masło", "jogurt", "kefir",
    "mozzarella", "parmezan", "feta", "ricotta", "mascarpone",
]

EGG_KEYWORDS: list[str] = ["jajko", "jaja", "żółtko"]

GLUTEN_KEYWORDS: list[str] = [
    "mąka", "chleb", "makaron", "pszenica", "ciasto", "bułka", "tortilla", "pita",
    "naleśnik", "pierogi", "kluski", "bagietka", "bajgiel", "panini",
    "spaghetti", "penne", "fettuccine", "gnocchi", "ravioli",
    "focaccia", "precel", "grzanki", "naan",
]
