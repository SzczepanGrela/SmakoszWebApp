import os
import sys

from faker import Faker

sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from config import LOCALE

fake = Faker(LOCALE)
