import random
import re
import unicodedata

def slugify(text: str) -> str:
    polish_chars = {
        "ą": "a",
        "Ą": "A",
        "ć": "c",
        "Ć": "C",
        "ę": "e",
        "Ę": "E",
        "ł": "l",
        "Ł": "L",
        "ń": "n",
        "Ń": "N",
        "ó": "o",
        "Ó": "O",
        "ś": "s",
        "Ś": "S",
        "ź": "z",
        "Ź": "Z",
        "ż": "z",
        "Ż": "Z",
    }
    for polish, ascii_char in polish_chars.items():
        text = text.replace(polish, ascii_char)

    text = unicodedata.normalize("NFKD", text)
    text = text.encode("ascii", "ignore").decode("ascii")
    text = text.lower().replace(" ", "-")
    text = re.sub(r"[^a-z0-9-]", "", text)
    text = re.sub(r"-+", "-", text)
    return text.strip("-")

class ReviewTextGenerator:
    HIGH_RATING_TEMPLATES = [
        "Rewelacyjne {dish_name}! {taste_aspect} było perfekcyjne. Obsługa bardzo miła i pomocna. Zdecydowanie wrócę!",
        "Przepyszne! {dish_name} w {restaurant_name} to prawdziwa uczta dla podniebienia. {quality_comment}. Polecam gorąco!",
        "Byłem tu po raz pierwszy i jestem zachwycony. {dish_name} {texture_comment}, a {taste_aspect} idealnie wyważone. Świetne miejsce!",
        "Nie spodziewałem się takiej jakości! {dish_name} {quality_comment}. Atmosfera {ambiance_quality}, obsługa na medal.",
        "Cudowne miejsce! {dish_name} było tak dobre, że zamówiłem drugie. {taste_aspect} i {texture_comment} to mistrzostwo.",
        "Najlepsze {dish_name} jakie jadłem w {city}! Wszystko świeże, smaczne i pięknie podane. Brawa dla szefa kuchni!",
        "Absolutnie warte swojej ceny. {dish_name} wykonane perfekcyjnie, {taste_aspect} doskonałe. Czysto, przytulnie, super obsługa.",
    ]

    MEDIUM_RATING_TEMPLATES = [
        "{dish_name} było w porządku, nic szczególnego. {taste_aspect} mogłoby być lepsze. Obsługa OK.",
        "Średnio. {dish_name} {texture_comment}, ale {taste_aspect} rozczarowało. {price_comment}.",
        "Nie jest źle, ale też nic wybitnego. {dish_name} standardowe, {quality_comment}. Można spróbować.",
        "Mam mieszane uczucia. {dish_name} {taste_aspect} OK, ale {texture_comment}. Obsługa {service_quality}.",
        "{dish_name} w normie. Nic co by mnie zachwyciło. {price_comment}. Być może dam drugą szansę.",
        "Przeciętne doświadczenie. {dish_name} jadalne, ale bez szału. Atmosfera {ambiance_quality}, obsługa {service_quality}.",
        "Spodziewałem się więcej. {dish_name} {quality_comment}, ale {taste_aspect} mogłoby być lepsze.",
    ]

    LOW_RATING_TEMPLATES = [
        "Rozczarowanie. {dish_name} było {quality_comment}. {taste_aspect} kiepskie, obsługa obojętna. Nie polecam.",
        "Słabe. {dish_name} {texture_comment} i {taste_aspect} nijak. {price_comment}. Nie wrócę.",
        "Niestety bardzo słaba jakość. {dish_name} było {quality_comment}. Obsługa {service_quality}. Szkoda pieniędzy.",
        "Totalnie przepłacone. {dish_name} mdłe, bez smaku. {texture_comment}. Czystość {cleanliness_comment}.",
        "Nie polecam tego miejsca. {dish_name} było {quality_comment}, a obsługa nieprofesjonalna. Rozczarowanie.",
        "Fatalne. {dish_name} {taste_aspect} okropne, {texture_comment}. Nie wrócę, nawet gdyby było za darmo.",
        "Katastrofa. {dish_name} niejadalne, {quality_comment}. Obsługa {service_quality}. Omijać szerokim łukiem.",
    ]

    TASTE_ASPECTS = {
        "positive": ["smak", "aromat", "doprawienie", "kompozycja smaków", "wyrazistość"],
        "neutral": ["smak", "aromat", "doprawienie"],
        "negative": ["smak", "aromat", "doprawienie", "brak smaku", "mdłość"],
    }

    QUALITY_COMMENTS = {
        "high": [
            "Świeże składniki, widać dbałość o szczegóły",
            "Wszystko na najwyższym poziomie",
            "Jakość premium",
            "Widać doświadczenie kucharza",
        ],
        "medium": ["w normie", "przeciętne", "standardowe", "nic szczególnego"],
        "low": ["niskiej jakości", "nie pierwszej świeżości", "tandetne", "wątpliwej jakości"],
    }

    TEXTURE_COMMENTS = {
        "positive": ["konsystencja idealna", "świetna tekstura", "chrupiące na zewnątrz, miękkie w środku"],
        "medium": ["tekstura OK", "konsystencja przeciętna"],
        "negative": ["rozwodnione", "gumowate", "suche jak pieprz", "mdłe"],
    }

    SERVICE_QUALITY = {
        "high": ["bardzo pomocna", "szybka i profesjonalna", "na medal"],
        "medium": ["w porządku", "standardowa", "przeciętna"],
        "low": ["obojętna", "nieprofesjonalna", "fatalna", "niegrzeczna"],
    }

    AMBIANCE_QUALITY = {
        "high": ["rewelacyjna", "świetna", "bardzo przyjemna"],
        "medium": ["OK", "w porządku", "niczym szczególnym"],
        "low": ["kiepska", "słaba", "nieprzyjemna"],
    }

    PRICE_COMMENTS = {
        "cheap": ["Cena śmiesznie niska!", "Świetny stosunek jakości do ceny"],
        "fair": ["Cena adekwatna do jakości", "Rozsądna cena"],
        "expensive": ["Trochę drogo", "Przepłacone", "Cena nieadekwatna do jakości"],
    }

    CLEANLINESS_COMMENTS = {
        "high": ["na medal", "wzorowa"],
        "medium": ["w porządku", "OK"],
        "low": ["pozostawia wiele do życzenia", "fatalna", "straszna"],
    }

    def __init__(self):
        random.seed()

    def generate_review_comment(
        self,
        rating: float,
        dish_name: str,
        restaurant_name: str,
        city: str,
        quality_score: float = 0.7,
        price_ratio: float = 1.0,
        service_score: float = 0.7,
        cleanliness_score: float = 7.0,
        ambiance_score: float = 7.0,
    ) -> str:
        template = self._select_template(rating)

        variables = self._generate_variables(
            rating,
            dish_name,
            restaurant_name,
            city,
            quality_score,
            price_ratio,
            service_score,
            cleanliness_score,
            ambiance_score,
        )

        comment = self._fill_template(template, variables)

        return comment

    def _select_template(self, rating: float) -> str:
        if rating >= 7.0:
            return random.choice(self.HIGH_RATING_TEMPLATES)
        elif rating >= 5.0:
            return random.choice(self.MEDIUM_RATING_TEMPLATES)
        else:
            return random.choice(self.LOW_RATING_TEMPLATES)

    def _get_sentiment_tier(self, rating: float) -> str:
        if rating >= 7.0:
            return "high"
        elif rating >= 5.0:
            return "medium"
        else:
            return "low"

    def _generate_variables(
        self,
        rating: float,
        dish_name: str,
        restaurant_name: str,
        city: str,
        quality_score: float,
        price_ratio: float,
        service_score: float,
        cleanliness_score: float,
        ambiance_score: float,
    ) -> dict[str, str]:
        variables = {"dish_name": dish_name, "restaurant_name": restaurant_name, "city": city}

        tier = self._get_sentiment_tier(rating)

        taste_map = {"high": "positive", "medium": "neutral", "low": "negative"}
        texture_map = {"high": "positive", "medium": "medium", "low": "negative"}

        variables["taste_aspect"] = random.choice(self.TASTE_ASPECTS[taste_map[tier]])
        variables["quality_comment"] = random.choice(self.QUALITY_COMMENTS[tier])
        variables["texture_comment"] = random.choice(self.TEXTURE_COMMENTS[texture_map[tier]])
        variables["service_quality"] = random.choice(self.SERVICE_QUALITY[tier])
        variables["ambiance_quality"] = random.choice(self.AMBIANCE_QUALITY[tier])
        variables["cleanliness_comment"] = random.choice(self.CLEANLINESS_COMMENTS[tier])

        if price_ratio < 0.8:
            variables["price_comment"] = random.choice(self.PRICE_COMMENTS["cheap"])
        elif price_ratio <= 1.2:
            variables["price_comment"] = random.choice(self.PRICE_COMMENTS["fair"])
        else:
            variables["price_comment"] = random.choice(self.PRICE_COMMENTS["expensive"])

        return variables

    def _fill_template(self, template: str, variables: dict[str, str]) -> str:
        try:
            return template.format(**variables)
        except KeyError:
            return template
