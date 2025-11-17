"""
Text Generator - Generowanie polskich komentarzy do recenzji
"""

import random
from typing import Dict, Any

class ReviewTextGenerator:
    """
    Generuje realistyczne polskie komentarze do recenzji
    """

    # Szablony dla wysokich ocen (8-10)
    HIGH_RATING_TEMPLATES = [
        "Rewelacyjne {dish_name}! {taste_aspect} było perfekcyjne. Obsługa bardzo miła i pomocna. Zdecydowanie wrócę!",
        "Przepyszne! {dish_name} w {restaurant_name} to prawdziwa uczta dla podniebienia. {quality_comment}. Polecam gorąco!",
        "Byłem tu po raz pierwszy i jestem zachwycony. {dish_name} {texture_comment}, a {taste_aspect} idealnie wyważone. Świetne miejsce!",
        "Nie spodziewałem się takiej jakości! {dish_name} {quality_comment}. Atmosfera {ambiance_quality}, obsługa na medal.",
        "Cudowne miejsce! {dish_name} było tak dobre, że zamówiłem drugie. {taste_aspect} i {texture_comment} to mistrzostwo.",
        "Najlepsze {dish_name} jakie jadłem w {city}! Wszystko świeże, smaczne i pięknie podane. Brawa dla szefa kuchni!",
        "Absolutnie warte swojej ceny. {dish_name} wykonane perfekcyjnie, {taste_aspect} doskonałe. Czysto, przytulnie, super obsługa."
    ]

    # Szablony dla średnich ocen (5-7)
    MEDIUM_RATING_TEMPLATES = [
        "{dish_name} było w porządku, nic szczególnego. {taste_aspect} mogłoby być lepsze. Obsługa OK.",
        "Średnio. {dish_name} {texture_comment}, ale {taste_aspect} rozczarowało. {price_comment}.",
        "Nie jest źle, ale też nic wybitnego. {dish_name} standardowe, {quality_comment}. Można spróbować.",
        "Mam mieszane uczucia. {dish_name} {taste_aspect} OK, ale {texture_comment}. Obsługa {service_quality}.",
        "{dish_name} w normie. Nic co by mnie zachwyciło. {price_comment}. Być może dam drugą szansę.",
        "Przeciętne doświadczenie. {dish_name} jadalne, ale bez szału. Atmosfera {ambiance_quality}, obsługa {service_quality}.",
        "Spodziewałem się więcej. {dish_name} {quality_comment}, ale {taste_aspect} mogłoby być lepsze."
    ]

    # Szablony dla niskich ocen (1-4)
    LOW_RATING_TEMPLATES = [
        "Rozczarowanie. {dish_name} było {quality_comment}. {taste_aspect} kiepskie, obsługa obojętna. Nie polecam.",
        "Słabe. {dish_name} {texture_comment} i {taste_aspect} nijak. {price_comment}. Nie wrócę.",
        "Niestety bardzo słaba jakość. {dish_name} było {quality_comment}. Obsługa {service_quality}. Szkoda pieniędzy.",
        "Totalnie przepłacone. {dish_name} mdłe, bez smaku. {texture_comment}. Czystość {cleanliness_comment}.",
        "Nie polecam tego miejsca. {dish_name} było {quality_comment}, a obsługa nieprofesjonalna. Rozczarowanie.",
        "Fatalne. {dish_name} {taste_aspect} okropne, {texture_comment}. Nie wrócę, nawet gdyby było za darmo.",
        "Katastrofa. {dish_name} niejadalne, {quality_comment}. Obsługa {service_quality}. Omijać szerokim łukiem."
    ]

    # Zmienne kontekstowe
    TASTE_ASPECTS = {
        'positive': ['smak', 'aromat', 'doprawienie', 'kompozycja smaków', 'wyrazistość'],
        'neutral': ['smak', 'aromat', 'doprawienie'],
        'negative': ['smak', 'aromat', 'doprawienie', 'brak smaku', 'mdłość']
    }

    QUALITY_COMMENTS = {
        'high': ['Świeże składniki, widać dbałość o szczegóły', 'Wszystko na najwyższym poziomie',
                 'Jakość premium', 'Widać doświadczenie kucharza'],
        'medium': ['w normie', 'przeciętne', 'standardowe', 'nic szczególnego'],
        'low': ['niskiej jakości', 'nie pierwsze świeżości', 'tandetne', 'wątpliwej jakości']
    }

    TEXTURE_COMMENTS = {
        'positive': ['konsystencja idealna', 'świetna tekstura', 'chrupiące na zewnątrz, miękkie w środku'],
        'medium': ['tekstura OK', 'konsystencja przeciętna'],
        'negative': ['rozwodnione', 'gumowate', 'suche jak pieprz', 'mdłe']
    }

    SERVICE_QUALITY = {
        'high': ['bardzo pomocna', 'szybka i profesjonalna', 'na medal'],
        'medium': ['w porządku', 'standardowa', 'przeciętna'],
        'low': ['obojętna', 'nieprofesjonalna', 'fatalna', 'niegrzeczna']
    }

    AMBIANCE_QUALITY = {
        'high': ['rewelacyjna', 'świetna', 'bardzo przyjemna'],
        'medium': ['OK', 'w porządku', 'niczym szczególnym'],
        'low': ['kiepska', 'słaba', 'nieprzyjemna']
    }

    PRICE_COMMENTS = {
        'cheap': ['Cena śmiesznie niska!', 'Świetny stosunek jakości do ceny'],
        'fair': ['Cena adekwatna do jakości', 'Rozsądna cena'],
        'expensive': ['Trochę drogo', 'Przepłacone', 'Cena nieadekwatna do jakości']
    }

    CLEANLINESS_COMMENTS = {
        'high': ['na medal', 'wzorowa'],
        'medium': ['w porządku', 'OK'],
        'low': ['pozostawia wiele do życzenia', 'fatalna', 'straszna']
    }

    def __init__(self):
        random.seed()

    def generate_review_comment(self, rating: float, dish_name: str,
                                restaurant_name: str, city: str,
                                quality_score: float = 0.7,
                                price_ratio: float = 1.0,
                                service_score: float = 0.7,
                                cleanliness_score: float = 7.0,
                                ambiance_score: float = 7.0) -> str:
        """
        Generuje komentarz do recenzji na podstawie parametrów

        Args:
            rating: Ogólna ocena (1-10)
            dish_name: Nazwa dania
            restaurant_name: Nazwa restauracji
            city: Miasto
            quality_score: Ocena jakości (0-1)
            price_ratio: Stosunek ceny do oczekiwań (>1 = drogo)
            service_score: Ocena obsługi (0-1)
            cleanliness_score: Ocena czystości (1-10)
            ambiance_score: Ocena atmosfery (1-10)

        Returns:
            Wygenerowany komentarz
        """
        # Wybierz szablon na podstawie ratingu
        template = self._select_template(rating)

        # Wygeneruj zmienne
        variables = self._generate_variables(
            rating, dish_name, restaurant_name, city,
            quality_score, price_ratio, service_score,
            cleanliness_score, ambiance_score
        )

        # Wypełnij szablon
        comment = self._fill_template(template, variables)

        return comment

    def _select_template(self, rating: float) -> str:
        """Wybiera szablon na podstawie oceny"""
        if rating >= 8.0:
            return random.choice(self.HIGH_RATING_TEMPLATES)
        elif rating >= 5.0:
            return random.choice(self.MEDIUM_RATING_TEMPLATES)
        else:
            return random.choice(self.LOW_RATING_TEMPLATES)

    def _generate_variables(self, rating: float, dish_name: str,
                          restaurant_name: str, city: str,
                          quality_score: float, price_ratio: float,
                          service_score: float, cleanliness_score: float,
                          ambiance_score: float) -> Dict[str, str]:
        """Generuje zmienne do wypełnienia szablonu"""
        variables = {
            'dish_name': dish_name,
            'restaurant_name': restaurant_name,
            'city': city
        }

        # Taste aspect
        if rating >= 8.0:
            variables['taste_aspect'] = random.choice(self.TASTE_ASPECTS['positive'])
        elif rating >= 5.0:
            variables['taste_aspect'] = random.choice(self.TASTE_ASPECTS['neutral'])
        else:
            variables['taste_aspect'] = random.choice(self.TASTE_ASPECTS['negative'])

        # Quality comment
        if quality_score >= 0.7:
            variables['quality_comment'] = random.choice(self.QUALITY_COMMENTS['high'])
        elif quality_score >= 0.4:
            variables['quality_comment'] = random.choice(self.QUALITY_COMMENTS['medium'])
        else:
            variables['quality_comment'] = random.choice(self.QUALITY_COMMENTS['low'])

        # Texture comment
        if rating >= 8.0:
            variables['texture_comment'] = random.choice(self.TEXTURE_COMMENTS['positive'])
        elif rating >= 5.0:
            variables['texture_comment'] = random.choice(self.TEXTURE_COMMENTS['medium'])
        else:
            variables['texture_comment'] = random.choice(self.TEXTURE_COMMENTS['negative'])

        # Service quality
        if service_score >= 0.7:
            variables['service_quality'] = random.choice(self.SERVICE_QUALITY['high'])
        elif service_score >= 0.4:
            variables['service_quality'] = random.choice(self.SERVICE_QUALITY['medium'])
        else:
            variables['service_quality'] = random.choice(self.SERVICE_QUALITY['low'])

        # Ambiance quality
        if ambiance_score >= 7.0:
            variables['ambiance_quality'] = random.choice(self.AMBIANCE_QUALITY['high'])
        elif ambiance_score >= 5.0:
            variables['ambiance_quality'] = random.choice(self.AMBIANCE_QUALITY['medium'])
        else:
            variables['ambiance_quality'] = random.choice(self.AMBIANCE_QUALITY['low'])

        # Price comment
        if price_ratio < 0.8:
            variables['price_comment'] = random.choice(self.PRICE_COMMENTS['cheap'])
        elif price_ratio <= 1.2:
            variables['price_comment'] = random.choice(self.PRICE_COMMENTS['fair'])
        else:
            variables['price_comment'] = random.choice(self.PRICE_COMMENTS['expensive'])

        # Cleanliness comment
        if cleanliness_score >= 8.0:
            variables['cleanliness_comment'] = random.choice(self.CLEANLINESS_COMMENTS['high'])
        elif cleanliness_score >= 6.0:
            variables['cleanliness_comment'] = random.choice(self.CLEANLINESS_COMMENTS['medium'])
        else:
            variables['cleanliness_comment'] = random.choice(self.CLEANLINESS_COMMENTS['low'])

        return variables

    def _fill_template(self, template: str, variables: Dict[str, str]) -> str:
        """Wypełnia szablon zmiennymi"""
        try:
            return template.format(**variables)
        except KeyError:
            # Jeśli brakuje zmiennej, zwróć szablon bez wypełnienia
            return template
