import json
import logging
import random
import time
from datetime import datetime, timedelta, timezone

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.db_connection import DatabaseConnection

logger = logging.getLogger(__name__)

CONTACT_SUBJECTS = [
    "Problem z logowaniem",
    "Błąd na stronie restauracji",
    "Pytanie o usunięcie konta",
    "Zgłoszenie bledu w wyszukiwarce",
    "Prośba o wspolprace",
    "Uwaga dotycząca ocen",
]

CONTACT_MESSAGES = [
    "Dzien dobry, od kilku dni nie moge sie zalogowac na swoje konto. Prosze o pomoc.",
    "Na stronie restauracji wyswietla sie blad 404 po kliknieciu w zdjecia.",
    "Chcialbym usunac swoje konto wraz ze wszystkimi danymi. Jak to zrobic?",
    "Wyszukiwarka nie zwraca wynikow dla mojej okolicy mimo ze restauracje sa w bazie.",
    "Jestem wlascicielem restauracji i chcialbym omowic mozliwosc wylacznej wspolpracy.",
    "Kilka ocen dla naszej restauracji wydaje sie falszywych. Prosze o weryfikacje.",
]

RESTAURANT_REQUEST_NAMES = [
    "Trattoria Bella Vista",
    "Sushi Sakura",
    "Burger Factory",
    "Vegan Corner",
    "Taco Loco",
    "Piekarnia Jankowska",
    "Restauracja Pod Lipami",
    "Kuchnia Azjatycka Panda",
]

POLISH_STREETS = [
    "Marszalkowska", "Nowy Świat", "Krakowskie Przedmieście", "Długa", "Lipowa",
    "Kosciuszki", "Mickiewicza", "Sienkiewicza", "Piłsudskiego", "Słowackiego",
]


def _make_ticket(ticket_type, reference_id, priority, created_at, description=None, requester_id=None):
    now = datetime.now(timezone.utc)
    scatter_days = random.randint(0, 90)
    ticket_created = created_at + timedelta(days=scatter_days)
    if ticket_created > now:
        ticket_created = now
    return {
        "ticket_type": ticket_type,
        "reference_id": reference_id,
        "status": "open",
        "priority": priority,
        "description": description,
        "requester_id": requester_id,
        "assigned_admin_id": None,
        "created_at": ticket_created,
        "updated_at": ticket_created,
        "version": 1,
    }


def _generate_system_tickets(db: DatabaseConnection):
    logger.info("Generating system tickets...")

    admin_users = db.fetch_all("SELECT user_id FROM users WHERE role = 'admin'")
    admin_ids = [u[0] for u in admin_users] if admin_users else []

    now = datetime.now(timezone.utc)
    tickets = []

    pending_reviews = db.fetch_all("""
        SELECT r.review_id, r.created_at, r.content
        FROM reviews r
        WHERE r.content_status = 'pending'
    """)
    for row in pending_reviews:
        review_id, created_at, review_text = row
        description = (review_text or "")[:300] or None
        tickets.append(_make_ticket("review_content", review_id, 2, created_at or now, description=description))

    logger.info(f"  ReviewContent tickets: {len(pending_reviews)}")

    pending_photos = db.fetch_all("""
        SELECT ma.asset_id, ma.created_at
        FROM media_assets ma
        WHERE ma.status = 'pending'
    """)
    for row in pending_photos:
        asset_id, created_at = row
        tickets.append(_make_ticket("photo", asset_id, 2, created_at or now))

    logger.info(f"  Photo tickets: {len(pending_photos)}")

    pending_reports = db.fetch_all("""
        SELECT rp.report_id, rp.created_at
        FROM reports rp
        WHERE rp.status = 'pending'
    """)
    for row in pending_reports:
        report_id, created_at = row
        tickets.append(_make_ticket("report", report_id, 2, created_at or now))

    logger.info(f"  Report tickets: {len(pending_reports)}")

    pending_edits = db.fetch_all("""
        SELECT re.request_id, re.created_at
        FROM restaurant_edit_requests re
        WHERE re.status = 'pending'
    """)
    for row in pending_edits:
        request_id, created_at = row
        tickets.append(_make_ticket("edit_request", request_id, 3, created_at or now))

    logger.info(f"  EditRequest tickets: {len(pending_edits)}")

    pending_corrections = db.fetch_all("""
        SELECT dcr.request_id, dcr.created_at
        FROM data_correction_requests dcr
        WHERE dcr.status = 'pending'
    """)
    for row in pending_corrections:
        request_id, created_at = row
        tickets.append(_make_ticket("data_correction", request_id, 3, created_at or now))

    logger.info(f"  DataCorrection tickets: {len(pending_corrections)}")

    suggestions = db.fetch_all(
        "SELECT suggestion_id, created_at FROM ingredient_suggestions LIMIT 200"
    )
    for row in suggestions:
        suggestion_id, created_at = row
        tickets.append(_make_ticket("ingredient_suggestion", suggestion_id, 1, created_at or now))

    logger.info(f"  IngredientSuggestion tickets: {len(suggestions)}")

    for i in range(15):
        subject = random.choice(CONTACT_SUBJECTS)
        body = random.choice(CONTACT_MESSAGES)
        description = f"Subject: {subject}\n\n{body}"
        base_date = now - timedelta(days=random.randint(1, 180))
        t = _make_ticket("contact", i + 1, random.choice([1, 2, 3]), base_date, description=description[:5000])
        t["status"] = random.choices(["open", "in_progress", "resolved"], weights=[50, 20, 30])[0]
        tickets.append(t)

    logger.info("  Contact tickets: 15")

    restaurants = db.fetch_all(
        "SELECT restaurant_id, restaurant_name FROM restaurants WHERE status = 'active' LIMIT 200"
    )
    regular_users = db.fetch_all(
        "SELECT user_id FROM users WHERE role = 'user' AND is_banned = false LIMIT 500"
    )
    if restaurants and regular_users:
        claim_sample = random.sample(restaurants, min(10, len(restaurants)))
        for rest_row in claim_sample:
            restaurant_id, restaurant_name = rest_row
            requester_id = random.choice(regular_users)[0]
            description = f"Chcialbym przejac zarzadzanie restauracja {restaurant_name}. Jestem wlascicielem."
            base_date = now - timedelta(days=random.randint(1, 120))
            t = _make_ticket(
                "restaurant_claim", restaurant_id, 3, base_date,
                description=description[:5000], requester_id=requester_id,
            )
            t["status"] = random.choices(["open", "in_progress", "resolved"], weights=[60, 15, 25])[0]
            tickets.append(t)

    logger.info(f"  RestaurantClaim tickets: {min(10, len(restaurants)) if restaurants else 0}")

    cities = db.fetch_all("SELECT city_id FROM cities LIMIT 50")
    cuisine_types = db.fetch_all("SELECT cuisine_type_id FROM cuisine_types LIMIT 50")
    if cities and cuisine_types:
        for i in range(8):
            city_id = random.choice(cities)[0]
            cuisine_type_id = random.choice(cuisine_types)[0]
            street = random.choice(POLISH_STREETS)
            payload = {
                "Name": random.choice(RESTAURANT_REQUEST_NAMES),
                "Address": f"ul. {street} {random.randint(1, 99)}",
                "CityId": city_id,
                "CuisineTypeId": cuisine_type_id,
                "Phone": f"+48 {random.randint(500, 799)} {random.randint(100, 999)} {random.randint(100, 999)}",
                "Email": f"kontakt{i + 1}@example.com",
                "Description": "Prosze o dodanie naszej restauracji do bazy Smakosz.",
            }
            base_date = now - timedelta(days=random.randint(1, 90))
            t = _make_ticket(
                "restaurant_request", i + 1, 2, base_date,
                description=json.dumps(payload, ensure_ascii=False)[:5000],
            )
            t["status"] = random.choices(["open", "in_progress", "resolved"], weights=[50, 20, 30])[0]
            tickets.append(t)

    logger.info("  RestaurantRequest tickets: 8")

    if admin_ids:
        num_to_resolve = int(len(tickets) * 0.3)
        resolve_indices = random.sample(range(len(tickets)), min(num_to_resolve, len(tickets)))
        for idx in resolve_indices:
            t = tickets[idx]
            t["status"] = "resolved"
            t["assigned_admin_id"] = random.choice(admin_ids)
            t["updated_at"] = t["created_at"] + timedelta(hours=random.randint(1, 72))

    if tickets:
        db.insert_bulk("system.tickets", tickets)
        db.commit()

    total = len(tickets)
    resolved = sum(1 for t in tickets if t["status"] == "resolved")
    logger.info(f"Generated {total} system tickets ({resolved} resolved, {total - resolved} open)")


class TicketsPhase(BasePhase):

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase7_tickets",
            display_name="System Tickets Generation",
            dependencies=[
                "phase5_reviews",
                "phase6_social",
            ],
            required_tables=["system.tickets"],
            cleanup_tables=["system.tickets"],
            estimated_duration=30,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 7: System Tickets Generation")
        logger.info("=" * 60)

        try:
            _generate_system_tickets(context.db)

            tickets_count = context.db.fetch_val("SELECT COUNT(*) FROM system.tickets") or 0

            duration = time.time() - start_time

            logger.info(f"Phase 7 completed in {duration:.2f}s")
            logger.info(f"Generated: {tickets_count:,} system tickets")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={
                    "system_tickets": tickets_count,
                },
                error=None,
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 7 FAILED after {duration:.2f}s: {e}", exc_info=True)

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
