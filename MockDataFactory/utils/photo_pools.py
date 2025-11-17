"""
Photo Pools - URL-e zdjęć Unsplash dla dań, restauracji i użytkowników
"""

import random
from typing import List

class PhotoPools:
    """
    Zarządza URL-ami zdjęć z Unsplash dla różnych kategorii
    """

    # Queries dla dań (według kategorii/archetypu)
    DISH_QUERIES = {
        'Pizza': ['pizza', 'italian,pizza', 'margherita', 'pepperoni', 'pizza,restaurant'],
        'Burger': ['burger', 'hamburger', 'cheeseburger', 'burger,fries', 'gourmet,burger'],
        'Sushi': ['sushi', 'sushi,roll', 'japanese,food', 'nigiri', 'sashimi'],
        'Pasta': ['pasta', 'spaghetti', 'italian,pasta', 'carbonara', 'penne'],
        'Ramen': ['ramen', 'ramen,bowl', 'japanese,noodles', 'ramen,soup'],
        'Steak': ['steak', 'beef,steak', 'grilled,steak', 'ribeye', 'meat'],
        'Salad': ['salad', 'fresh,salad', 'green,salad', 'caesar,salad', 'vegetable'],
        'Soup': ['soup', 'soup,bowl', 'hot,soup', 'cream,soup', 'vegetable,soup'],
        'Dessert': ['dessert', 'cake', 'sweet', 'chocolate,dessert', 'pastry'],
        'Ice Cream': ['ice,cream', 'gelato', 'dessert,ice', 'frozen,dessert'],
        'Tacos': ['tacos', 'mexican,food', 'taco,plate', 'tortilla'],
        'Kebab': ['kebab', 'doner', 'shawarma', 'middle,eastern,food'],
        'Pierogi': ['pierogi', 'dumplings', 'polish,food', 'stuffed,dumplings'],
        'Seafood': ['seafood', 'fish', 'salmon', 'shrimp', 'lobster'],
        'BBQ': ['bbq', 'barbecue', 'ribs', 'grilled,meat', 'smoked,meat'],
        'Chicken': ['chicken', 'fried,chicken', 'grilled,chicken', 'chicken,dish'],
        'Vegan': ['vegan', 'plant,based', 'vegan,food', 'vegetables'],
        'Breakfast': ['breakfast', 'eggs', 'pancakes', 'morning,food', 'brunch'],
        'Sandwich': ['sandwich', 'sub', 'panini', 'deli,sandwich'],
        'Noodles': ['noodles', 'asian,noodles', 'stir,fry', 'pad,thai'],
        'Curry': ['curry', 'indian,food', 'curry,dish', 'thai,curry'],
        'Dim Sum': ['dim,sum', 'chinese,food', 'steamed,dumplings', 'dumpling'],
        'Pho': ['pho', 'vietnamese,soup', 'pho,bo', 'vietnamese,food'],
        'Falafel': ['falafel', 'middle,eastern', 'chickpea', 'mediterranean'],
        'Risotto': ['risotto', 'italian,rice', 'mushroom,risotto', 'creamy,rice'],
        'Gnocchi': ['gnocchi', 'italian,pasta', 'potato,gnocchi'],
        'Biryani': ['biryani', 'indian,rice', 'rice,dish', 'biryani,plate'],
        'Paella': ['paella', 'spanish,food', 'rice,seafood', 'paella,pan'],
        'Nachos': ['nachos', 'tortilla,chips', 'mexican,snack', 'cheese,nachos'],
        'Quesadilla': ['quesadilla', 'mexican,food', 'cheese,quesadilla', 'tortilla'],
        'Wrap': ['wrap', 'tortilla,wrap', 'burrito', 'sandwich,wrap'],
        'Spring Rolls': ['spring,rolls', 'vietnamese,rolls', 'rice,paper', 'fresh,rolls'],
        'Tempura': ['tempura', 'fried,japanese', 'shrimp,tempura', 'japanese,food'],
        'Donuts': ['donuts', 'doughnuts', 'glazed,donut', 'sweet,pastry'],
        'Croissant': ['croissant', 'french,pastry', 'butter,croissant', 'bakery'],
        'Waffle': ['waffle', 'belgian,waffle', 'breakfast,waffle', 'sweet,waffle'],
        'Smoothie Bowl': ['smoothie,bowl', 'acai,bowl', 'healthy,bowl', 'fruit,bowl'],
        'Poke Bowl': ['poke,bowl', 'hawaiian,food', 'fish,bowl', 'rice,bowl'],
        'Buddha Bowl': ['buddha,bowl', 'healthy,bowl', 'grain,bowl', 'vegan,bowl'],
        'Fondue': ['fondue', 'cheese,fondue', 'swiss,food', 'melted,cheese'],
        'Tapas': ['tapas', 'spanish,food', 'small,plates', 'spanish,appetizers'],
        'Antipasti': ['antipasti', 'italian,appetizers', 'charcuterie', 'italian,starters'],
        'Oysters': ['oysters', 'raw,oysters', 'seafood,platter', 'fresh,oysters'],
        'Ceviche': ['ceviche', 'seafood,ceviche', 'peruvian,food', 'fish,ceviche'],
        'Empanadas': ['empanadas', 'latin,american', 'pastry,pockets', 'savory,pastry'],
        'Schnitzel': ['schnitzel', 'breaded,meat', 'german,food', 'fried,cutlet'],
        'Goulash': ['goulash', 'hungarian,stew', 'beef,stew', 'paprika,stew'],
        'Moussaka': ['moussaka', 'greek,food', 'eggplant,dish', 'mediterranean'],
        'Baklava': ['baklava', 'middle,eastern,dessert', 'phyllo,pastry', 'sweet,pastry'],
        'Tiramisu': ['tiramisu', 'italian,dessert', 'coffee,dessert', 'mascarpone'],
    }

    # Queries dla restauracji (według motywu)
    RESTAURANT_QUERIES = {
        'Italian': ['italian,restaurant', 'pizzeria', 'trattoria', 'italian,interior'],
        'Asian': ['asian,restaurant', 'chinese,restaurant', 'japanese,restaurant', 'sushi,bar'],
        'Mexican': ['mexican,restaurant', 'taco,bar', 'cantina', 'mexican,interior'],
        'American': ['american,diner', 'burger,restaurant', 'cafe,interior', 'casual,dining'],
        'French': ['french,restaurant', 'bistro', 'parisian,cafe', 'french,interior'],
        'Mediterranean': ['mediterranean,restaurant', 'greek,taverna', 'middle,eastern,restaurant'],
        'Steakhouse': ['steakhouse', 'grill,restaurant', 'meat,restaurant', 'fine,dining'],
        'Seafood': ['seafood,restaurant', 'fish,restaurant', 'oyster,bar', 'coastal,restaurant'],
        'Vegan': ['vegan,restaurant', 'plant,based,cafe', 'healthy,restaurant', 'vegetarian,cafe'],
        'Cafe': ['cafe', 'coffee,shop', 'bakery,cafe', 'cozy,cafe'],
    }

    # Queries dla zdjęć użytkowników (generyczne)
    USER_PHOTO_QUERIES = [
        'portrait', 'person', 'face', 'people', 'man', 'woman', 'profile'
    ]

    def __init__(self):
        random.seed()

    def get_dish_photo(self, dish_category: str) -> str:
        """
        Zwraca URL Unsplash dla zdjęcia dania

        Args:
            dish_category: Kategoria dania (np. 'Pizza', 'Burger')

        Returns:
            URL Unsplash
        """
        # Pobierz queries dla kategorii
        queries = self.DISH_QUERIES.get(dish_category, ['food', 'dish', 'meal'])

        # Wybierz losowy query
        query = random.choice(queries)

        # Wygeneruj URL Unsplash
        return self._generate_unsplash_url(query)

    def get_restaurant_photo(self, restaurant_theme: str) -> str:
        """
        Zwraca URL Unsplash dla zdjęcia restauracji

        Args:
            restaurant_theme: Motyw restauracji (np. 'Italian', 'Asian')

        Returns:
            URL Unsplash
        """
        # Pobierz queries dla motywu
        queries = self.RESTAURANT_QUERIES.get(restaurant_theme, ['restaurant', 'dining', 'interior'])

        # Wybierz losowy query
        query = random.choice(queries)

        return self._generate_unsplash_url(query)

    def get_user_photo_generic(self) -> str:
        """
        Zwraca URL Unsplash dla generycznego zdjęcia użytkownika

        Returns:
            URL Unsplash
        """
        query = random.choice(self.USER_PHOTO_QUERIES)
        return self._generate_unsplash_url(query)

    def _generate_unsplash_url(self, query: str, width: int = 800, height: int = 600) -> str:
        """
        Generuje URL Unsplash z podanym query

        Args:
            query: Fraza wyszukiwania (np. 'pizza', 'italian,restaurant')
            width: Szerokość obrazu
            height: Wysokość obrazu

        Returns:
            URL Unsplash
        """
        # Dodaj losową liczbę dla różnorodności (seed)
        seed = random.randint(1, 10000)

        # Unsplash Source API
        # Format: https://source.unsplash.com/{width}x{height}/?{query}&sig={seed}
        query_clean = query.replace(' ', ',')
        url = f"https://source.unsplash.com/{width}x{height}/?{query_clean}&sig={seed}"

        return url

    def get_random_dish_photo(self) -> str:
        """Zwraca losowe zdjęcie jedzenia"""
        category = random.choice(list(self.DISH_QUERIES.keys()))
        return self.get_dish_photo(category)

    def get_random_restaurant_photo(self) -> str:
        """Zwraca losowe zdjęcie restauracji"""
        theme = random.choice(list(self.RESTAURANT_QUERIES.keys()))
        return self.get_restaurant_photo(theme)
