import re
import unicodedata

def slugify(name: str) -> str:
    POLISH_TO_ASCII = {
        "ą": "a",
        "ć": "c",
        "ę": "e",
        "ł": "l",
        "ń": "n",
        "ó": "o",
        "ś": "s",
        "ź": "z",
        "ż": "z",
        "Ą": "A",
        "Ć": "C",
        "Ę": "E",
        "Ł": "L",
        "Ń": "N",
        "Ó": "O",
        "Ś": "S",
        "Ź": "Z",
        "Ż": "Z",
    }
    for polish, ascii_char in POLISH_TO_ASCII.items():
        name = name.replace(polish, ascii_char)

    name = unicodedata.normalize("NFKD", name)
    name = name.encode("ascii", "ignore").decode("ascii")

    name = re.sub(r"[\s\-]+", "_", name)
    name = name.lower()
    name = re.sub(r"[^a-z0-9_]", "", name)
    name = re.sub(r"_+", "_", name)
    return name.strip("_")
