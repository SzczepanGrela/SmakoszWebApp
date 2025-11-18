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

    def generate_dates_with_spacing(self, count: int,
                                    start_date: datetime = None,
                                    min_days: int = 3,
                                    max_days: int = 14) -> List[datetime]:
        """
        Generuje sekwencję dat z minimalnym odstępem (dla recenzji użytkownika)

        Args:
            count: Liczba dat do wygenerowania
            start_date: Data początkowa (domyślnie min_date)
            min_days: Minimalny odstęp między datami
            max_days: Maksymalny odstęp między datami

        Returns:
            Lista dat posortowana chronologicznie
        """
        if start_date is None:
            start_date = self.min_date

        dates = []
        current_date = start_date

        for _ in range(count):
            # Losowy odstęp
            spacing = random.randint(min_days, max_days)
            current_date = current_date + timedelta(days=spacing)

            # Sprawdź czy nie przekroczyliśmy max_date
            if current_date > self.max_date:
                break

            # Dodaj godzinę biznesową
            dated = self.generate_business_hours_datetime(current_date)
            dates.append(dated)

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
        Konwertuje datetime na format SQL Server

        Args:
            dt: Obiekt datetime

        Returns:
            String w formacie SQL datetime
        """
        return dt.strftime('%Y-%m-%d %H:%M:%S')
