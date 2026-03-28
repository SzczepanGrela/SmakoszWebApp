import random
from datetime import datetime, timedelta
from typing import Any

import numpy as np

MIN_DATE = datetime(2020, 1, 1)
MAX_DATE = datetime(2024, 12, 31)

def ensure_naive(dt: Any) -> datetime:
    if dt is None:
        return None  # type: ignore[return-value]
    if not isinstance(dt, datetime):
        if hasattr(dt, "year") and hasattr(dt, "month") and hasattr(dt, "day") and not hasattr(dt, "hour"):
            return datetime.combine(dt, datetime.min.time())
        return dt
    if dt.tzinfo is not None:
        return dt.replace(tzinfo=None)
    return dt

def generate_random_date(start: datetime | None = None, end: datetime | None = None) -> datetime:
    start = ensure_naive(start) if start is not None else MIN_DATE
    end = ensure_naive(end) if end is not None else MAX_DATE

    delta = end - start
    random_days = random.randint(0, delta.days)

    return start + timedelta(days=random_days)

def generate_business_hours_datetime(date: datetime | None = None) -> datetime:
    date = ensure_naive(date) if date is not None else generate_random_date()

    hour = random.randint(10, 22)
    minute = random.randint(0, 59)

    return date.replace(hour=hour, minute=minute, second=0, microsecond=0)

def generate_restaurant_created_date() -> datetime:
    beta_value = np.random.beta(2, 5)

    delta = MAX_DATE - MIN_DATE
    days_offset = int(beta_value * delta.days)

    created_date = MIN_DATE + timedelta(days=days_offset)

    return created_date.replace(hour=10, minute=0, second=0, microsecond=0)

def generate_review_date(restaurant_created: datetime, user_first_review: datetime | None = None) -> datetime:
    restaurant_created = ensure_naive(restaurant_created)
    user_first_review = ensure_naive(user_first_review)

    earliest_date = restaurant_created + timedelta(days=1)

    if user_first_review and user_first_review > earliest_date:
        earliest_date = user_first_review

    latest_date = MAX_DATE

    if earliest_date >= latest_date:
        return earliest_date + timedelta(days=random.randint(1, 7))

    return generate_random_date(earliest_date, latest_date)

def generate_dates_with_spacing(
    count: int, start_date: datetime, min_days: int = 1, max_days: int = 30
) -> list[datetime]:
    if count <= 0:
        return []

    start_date = ensure_naive(start_date)
    dates = []

    incubation_days = int(random.expovariate(1 / 14))
    incubation_days = min(incubation_days, 180)

    current_date = start_date + timedelta(days=incubation_days)
    now_naive = datetime.now().replace(tzinfo=None)

    if current_date > now_naive:
        current_date = now_naive - timedelta(days=count)

    for _ in range(count):
        dates.append(current_date)
        gap = random.randint(min_days, max_days)
        current_date += timedelta(gap)

        if current_date > now_naive:
            break

    return dates

def generate_dates_skewed_to_end(count: int, start_date: datetime, end_date: datetime) -> list[datetime]:
    if count <= 0:
        return []

    start_date = ensure_naive(start_date)
    end_date = ensure_naive(end_date)

    total_seconds = (end_date - start_date).total_seconds()
    if total_seconds <= 0:
        return [start_date] * count

    dates: set[datetime] = set()
    attempts = 0
    max_attempts = count * 3

    while len(dates) < count and attempts < max_attempts:
        ratio = random.betavariate(5, 1)

        offset_seconds = int(total_seconds * ratio)
        gen_date = start_date + timedelta(seconds=offset_seconds)
        gen_date = gen_date.replace(second=0, microsecond=0)

        dates.add(gen_date)
        attempts += 1

    result = sorted(dates)
    while len(result) < count:
        result.append(end_date)

    return result

def generate_user_join_date() -> datetime:
    beta_value = np.random.beta(2, 4)

    delta = MAX_DATE - MIN_DATE
    days_offset = int(beta_value * delta.days)

    join_date = MIN_DATE + timedelta(days=days_offset)

    return join_date.replace(hour=12, minute=0, second=0, microsecond=0)

def to_sql_datetime(dt: datetime) -> str:
    return dt.strftime("%Y-%m-%d %H:%M:%S")

def to_sql_date(dt: datetime) -> str:
    return dt.strftime("%Y-%m-%d")
