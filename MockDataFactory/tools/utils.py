"""
Common utilities for MockDataFactory tools.
"""

import re
import unicodedata

def slugify(name: str) -> str:
    """
    Convert a name to ASCII-safe file prefix (lowercase, underscores).
    Replaces Polish characters with ASCII equivalents.
    
    Example: "Brisket Wołowy" -> "brisket_wolowy"
             "łosoś" -> "losos"
             "żółtko" -> "zoltko"
    """
    # Polish character mappings
    POLISH_TO_ASCII = {
        "ą": "a", "ć": "c", "ę": "e", "ł": "l", "ń": "n",
        "ó": "o", "ś": "s", "ź": "z", "ż": "z",
        "Ą": "A", "Ć": "C", "Ę": "E", "Ł": "L", "Ń": "N",
        "Ó": "O", "Ś": "S", "Ź": "Z", "Ż": "Z",
    }
    for polish, ascii_char in POLISH_TO_ASCII.items():
        name = name.replace(polish, ascii_char)
    
    # Normalize unicode
    name = unicodedata.normalize('NFKD', name)
    name = name.encode('ascii', 'ignore').decode('ascii')
    
    # Replace spaces and hyphens with underscores
    name = re.sub(r'[\s\-]+', '_', name)
    # Lowercase
    name = name.lower()
    # Remove special characters except underscores and alphanumeric
    name = re.sub(r'[^a-z0-9_]', '', name)
    # Remove multiple underscores
    name = re.sub(r'_+', '_', name)
    return name.strip('_')

