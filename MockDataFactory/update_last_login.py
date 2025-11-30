"""
Update Last Login - Utility script for setting realistic last_login_at timestamps
"""

import logging
import random
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)

def update_last_login_for_users(db):
    """
    Aktualizuje last_login_at dla użytkowników, aby symulować aktywność

    Strategia:
    - 80% użytkowników logowało się w ostatnim miesiącu
    - 15% logowało się 1-6 miesięcy temu
    - 5% nieaktywnych (last_login_at = NULL)

    Args:
        db: DatabaseConnection instance
    """
    logger.info("🔄 Aktualizacja last_login_at...")

    # Pobierz wszystkich użytkowników
    users = db.fetch_all("SELECT user_id FROM users")

    if not users:
        logger.warning("⚠️  Brak użytkowników do aktualizacji")
        return

    now = datetime.now()
    updates = []

    for (user_id,) in users:
        rand = random.random()

        if rand < 0.80:
            # 80% - Ostatni miesiąc (aktywni)
            days_ago = random.randint(0, 30)
            hours_ago = random.randint(0, 23)
            minutes_ago = random.randint(0, 59)
            last_login = now - timedelta(days=days_ago, hours=hours_ago, minutes=minutes_ago)
        elif rand < 0.95:
            # 15% - 1-6 miesięcy temu (mniej aktywni)
            days_ago = random.randint(31, 180)
            last_login = now - timedelta(days=days_ago)
        else:
            # 5% - Nieaktywni (NULL)
            last_login = None

        updates.append({
            'user_id': user_id,
            'last_login_at': last_login
        })

    # Bulk update
    for update in updates:
        if update['last_login_at'] is not None:
            db.execute_query(
                "UPDATE users SET last_login_at = %s WHERE user_id = %s",
                (update['last_login_at'], update['user_id'])
            )
        else:
            db.execute_query(
                "UPDATE users SET last_login_at = NULL WHERE user_id = %s",
                (update['user_id'],)
            )

    db.commit()

    logger.info(f"✅ Zaktualizowano last_login_at dla {len(updates)} użytkowników")
    logger.info(f"   • Aktywni (ostatni miesiąc): ~{int(len(updates) * 0.80)}")
    logger.info(f"   • Mniej aktywni (1-6 miesięcy): ~{int(len(updates) * 0.15)}")
    logger.info(f"   • Nieaktywni (brak logowania): ~{int(len(updates) * 0.05)}")
