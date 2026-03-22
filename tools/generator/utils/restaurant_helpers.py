"""
Restaurant Helper Functions

This module contains utilities for generating restaurant-specific content,
particularly restaurant name generation with uniqueness tracking.
"""

import random

class RestaurantNameGenerator:
    """
    Generates unique restaurant names based on theme and city.

    Maintains internal state to ensure name uniqueness across multiple
    generation calls. Each instance tracks used names and counters.
    """

    def __init__(self):
        """Initialize the name generator with empty state."""
        self._used_restaurant_names = set()
        self._name_counter = {}

    def generate_name(self, theme: str, city: str) -> str:
        """
        Generate a unique restaurant name for the given theme and city.

        Args:
            theme: Restaurant theme (Pizzeria, Sushi Bar, etc.)
            city: City name

        Returns:
            Unique restaurant name combining base name and city
        """
        base_name = self._generate_base_name(theme)

        candidate = f"{base_name} {city}"

        if candidate not in self._used_restaurant_names:
            self._used_restaurant_names.add(candidate)
            return candidate

        counter_key = f"{base_name}_{city}"
        if counter_key not in self._name_counter:
            self._name_counter[counter_key] = 1

        while True:
            self._name_counter[counter_key] += 1
            candidate = f"{base_name} {city} {self._name_counter[counter_key]}"
            if candidate not in self._used_restaurant_names:
                self._used_restaurant_names.add(candidate)
                return candidate

    def _generate_base_name(self, theme: str) -> str:
        """
        Generate a theme-appropriate base restaurant name.

        Args:
            theme: Restaurant theme

        Returns:
            Base name without city suffix
        """
        prefixes = ["Restauracja", "Bistro", "Gospoda", "Smaki", "Bar"]
        suffixes = [
            "Pod Aniołem",
            "Starówka",
            "Centrum",
            "Parkowa",
            "Królewska",
            "Na Rogu",
            "U Babci",
            "Smaczna",
            "Domowa",
            "Zielona",
        ]

        if theme == "Pizzeria":
            return f"Pizzeria {random.choice(['Bella', 'Roma', 'Napoli', 'Milano', 'Toscana', 'Palermo', 'Venezia', 'Firenze'])}"
        elif theme == "Sushi Bar":
            return f"Sushi {random.choice(['Tokyo', 'Osaka', 'Sakura', 'Zen', 'Kyoto', 'Fuji', 'Samurai', 'Ninja'])}"
        elif theme == "Burger Bar":
            return f"{random.choice(['The', 'Big', 'Best', 'Prime', 'Classic'])} Burger {random.choice(['House', 'Bar', 'Kitchen', 'Joint', 'Spot'])}"
        elif theme == "Asian Fusion":
            return f"{random.choice(['Asian', 'Oriental', 'Golden', 'Dragon'])} {random.choice(['Fusion', 'Kitchen', 'Garden', 'Palace'])}"
        elif theme == "Steakhouse":
            return f"{random.choice(['Prime', 'Black', 'Gold', 'Royal'])} {random.choice(['Steakhouse', 'Grill', 'Meat', 'Beef'])}"
        elif theme == "Vegan Cafe":
            return f"{random.choice(['Green', 'Fresh', 'Pure', 'Organic'])} {random.choice(['Cafe', 'Kitchen', 'Garden', 'Bistro'])}"
        elif theme == "Mexican":
            return f"{random.choice(['El', 'La', 'Casa'])} {random.choice(['Taco', 'Burrito', 'Fiesta', 'Mexico', 'Cantina'])}"
        elif theme == "French Bistro":
            return f"{random.choice(['Le', 'La', 'Petit'])} {random.choice(['Bistro', 'Cafe', 'Paris', 'Provence'])}"
        elif theme == "Seafood":
            return f"{random.choice(['Ocean', 'Sea', 'Blue', 'Harbor'])} {random.choice(['Catch', 'Fish', 'Grill', 'Kitchen'])}"
        else:
            return f"{random.choice(prefixes)} {random.choice(suffixes)}"
