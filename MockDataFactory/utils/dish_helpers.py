"""
Dish Helper Functions

This module contains utility functions for generating dish-specific content
like descriptions and calorie calculations. These helpers are used by the
Phase 3 dish generator to create realistic dish attributes.
"""

import random

def generate_dish_description(
    dish_name: str, archetype: str, ingredients: list, quality: float, spiciness: float
) -> str:
    """
    Generates a Polish-language description for a dish based on its attributes.

    Args:
        dish_name: Name of the dish
        archetype: Dish archetype (Pizza, Burger, Sushi, etc.)
        ingredients: List of ingredient names
        quality: Quality score (0.0-1.0)
        spiciness: Spiciness level (0-10)

    Returns:
        A description string (max 500 characters)
    """
    if quality >= 0.8:
        quality_adj = random.choice(["wyśmienite", "wyjątkowe", "premium", "perfekcyjne"])
    elif quality >= 0.6:
        quality_adj = random.choice(["pyszne", "smaczne", "aromatyczne", "smakowite"])
    else:
        quality_adj = random.choice(["dobre", "klasyczne", "tradycyjne", "domowe"])

    archetype_intros = {
        "Pizza": f"{quality_adj.capitalize()} pizza {dish_name}",
        "Burger": f"{quality_adj.capitalize()} burger {dish_name}",
        "Sushi": f"{quality_adj.capitalize()} sushi {dish_name}",
        "Pasta": f"{quality_adj.capitalize()} makaron {dish_name}",
        "Deser": f"{quality_adj.capitalize()} deser {dish_name}",
        "Zupa": f"{quality_adj.capitalize()} zupa {dish_name}",
        "Sałatka": f"{quality_adj.capitalize()} sałatka {dish_name}",
    }
    base = archetype_intros.get(archetype, f"{quality_adj.capitalize()} danie {dish_name}")

    spice = ""
    if spiciness >= 7:
        spice = ", bardzo ostre"
    elif spiciness >= 5:
        spice = ", ostre"
    elif spiciness >= 3:
        spice = ", średnio ostre"

    key_ingredients = ", ".join(ingredients[:4]) if ingredients else "świeże składniki"

    description = f"{base}. {quality_adj.capitalize()} danie{spice}. Składniki: {key_ingredients}."
    return description[:500]

def generate_dish_calories(archetype: str, price: float, richness: float) -> int:
    """
    Calculates estimated calories for a dish based on archetype and richness.
    Uses base calories per 100g and estimated portion size.
    Price is ignored (Mod 12).
    """
    
    # Base calories per 100g
    calories_per_100g = {
        "Pizza": 270,
        "Burger": 295,
        "Sushi": 140,
        "Pasta": 160,
        "Deser": 350,
        "Zupa": 45,
        "Sałatka": 80,
        "Steak": 250,
        "Ryba": 120,
        "Kurczak": 165,
        "Kebab": 220,
        "Ramen": 70,
        "Tacos": 210,
        "Seafood": 100,
        "Vegan": 120,
        "Curry": 140,
        "Dim Sum": 190
    }
    
    # Estimated portion sizes in grams
    portion_sizes = {
        "Pizza": 450,
        "Burger": 350,
        "Sushi": 250,
        "Pasta": 400,
        "Deser": 150,
        "Zupa": 350,
        "Sałatka": 300,
        "Steak": 300,
        "Ryba": 250,
        "Kurczak": 300,
        "Kebab": 450,
        "Ramen": 600,
        "Tacos": 300,
        "Seafood": 300,
        "Vegan": 350,
        "Curry": 400,
        "Dim Sum": 250
    }

    base_cal_100g = calories_per_100g.get(archetype, 150)
    portion = portion_sizes.get(archetype, 300)

    # Add variance to 100g value
    variance = random.uniform(0.9, 1.1)
    
    # Richness impacts calorie density slightly
    density_modifier = 0.9 + (richness * 0.2) # 0.9 to 1.1

    final_cal_100g = base_cal_100g * variance * density_modifier
    
    total_calories = (final_cal_100g * portion) / 100.0

    return int(round(total_calories / 10) * 10)
