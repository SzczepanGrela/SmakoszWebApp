"""
Date Generator - Generowanie dat z ograniczeniami i spójnością czasową
"""

import random
from datetime import datetime, timedelta
from typing import List
import numpy as np

class DateGenerator:
    """
    Generuje daty z różnymi strategiami i ograniczeniami
    """

    def __init__(self):
        # Zakres dat dla generacji
        self.min_date = datetime(2020, 1, 1)
        self.max_date = datetime(2024, 12, 31)

    def generate_random_date(self, start: datetime = None, end: datetime = None) -> datetime:
        """
        Generuje losową datę w podanym zakresie

        Args:
            start: Data początkowa (domyślnie min_date)
            end: Data końcowa (domyślnie max_date)

        Returns:
            Losowa data
        """
        if start is None:
            start = self.min_date
        if end is None:
            end = self.max_date

        delta = end - start
        random_days = random.randint(0, delta.days)

        return start + timedelta(days=random_days)

    def generate_business_hours_datetime(self, date: datetime = None) -> datetime:
        """
        Generuje datę z godziną w godzinach pracy (10:00-22:00)

        Args:
            date: Data bazowa (jeśli None, losowa)

        Returns:
            Datetime z godziną w przedziale 10-22
        """
        if date is None:
            date = self.generate_random_date()

        # Godziny 10-22
        hour = random.randint(10, 22)
        minute = random.randint(0, 59)

        return date.replace(hour=hour, minute=minute, second=0, microsecond=0)

    def generate_restaurant_created_date(self) -> datetime:
        """
        Generuje datę otwarcia restauracji (beta distribution - więcej nowych)

        Returns:
            Data otwarcia restauracji
        """
        # Beta distribution (alpha=2, beta=5) daje więcej nowych restauracji
        beta_value = np.random.beta(2, 5)

        # Mapuj [0,1] na zakres dat
        delta = self.max_date - self.min_date
        days_offset = int(beta_value * delta.days)

        created_date = self.min_date + timedelta(days=days_offset)

        return created_date.replace(hour=10, minute=0, second=0, microsecond=0)

    def generate_review_date(self, restaurant_created: datetime,
                            user_first_review: datetime = None) -> datetime:
        """
        Generuje datę recenzji spójną z datą otwarcia restauracji

        Args:
            restaurant_created: Data otwarcia restauracji
            user_first_review: Pierwsza recenzja użytkownika (opcjonalnie)

        Returns:
            Data recenzji (PO otwarciu restauracji)
        """
        # Recenzja musi być PO otwarciu restauracji
        earliest_date = restaurant_created + timedelta(days=1)

        if user_first_review and user_first_review > earliest_date:
            earliest_date = user_first_review

        # Recenzja do dzisiaj
        latest_date = self.max_date

        if earliest_date >= latest_date:
            # Restauracja bardzo nowa - recenzja kilka dni później
            return earliest_date + timedelta(days=random.randint(1, 7))

        return self.generate_random_date(earliest_date, latest_date)

    def generate_dates_with_spacing(self, count: int, start_date: datetime, min_days: int = 1, max_days: int = 30) -> List[datetime]:
        """
        Generuje posortowaną listę dat recenzji z zachowaniem odstępów.
        Uwzględnia "czas inkubacji" (lurking period) przed pierwszą recenzją.
        """
        if count <= 0:
            return []

        dates = []
        
        # Czas inkubacji: większość userów czeka chwilę zanim napisze pierwszą recenzję
        # Rozkład wykładniczy: dużo małych wartości, mało dużych
        incubation_days = int(random.expovariate(1/14)) # Średnio 14 dni
        incubation_days = min(incubation_days, 180) # Max pół roku
        
        current_date = start_date + timedelta(days=incubation_days)
        
        # Zabezpieczenie przed wyjściem w przyszłość na starcie
        if current_date > datetime.now():
            current_date = datetime.now() - timedelta(days=count) # Fallback: start 'count' days ago

        for _ in range(count):
            dates.append(current_date)
            # Losowy odstęp do następnej recenzji
            gap = random.randint(min_days, max_days)
            current_date += timedelta(days=gap)
            
            # Jeśli przekroczymy "dzisiaj", przerywamy (nie generujemy recenzji z przyszłości)
            # W realnym scenariuszu user po prostu napisałby mniej recenzji niż 'count'
            if current_date > datetime.now():
                break

        return dates

    def generate_user_join_date(self) -> datetime:
        """
        Generuje datę dołączenia użytkownika (rozkład beta - więcej nowych)

        Returns:
            Data rejestracji użytkownika
        """
        # Beta distribution (alpha=2, beta=4) - więcej nowych użytkowników
        beta_value = np.random.beta(2, 4)

        # Mapuj na zakres dat
        delta = self.max_date - self.min_date
        days_offset = int(beta_value * delta.days)

        join_date = self.min_date + timedelta(days=days_offset)

        return join_date.replace(hour=12, minute=0, second=0, microsecond=0)

    @staticmethod
    def to_sql_datetime(dt: datetime) -> str:
        """
        Konwertuje datetime na format SQL (PostgreSQL/SQL Server kompatybilny)

        Args:
            dt: Obiekt datetime

        Returns:
            String w formacie SQL datetime
        """
        return dt.strftime('%Y-%m-%d %H:%M:%S')
