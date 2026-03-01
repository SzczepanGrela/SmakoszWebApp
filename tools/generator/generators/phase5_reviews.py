import logging
import os
import random
import time
import uuid
from datetime import timedelta
from multiprocessing import Pool, cpu_count

from tqdm import tqdm

from algorithms.dish_selector import select_dish_from_menu
from algorithms.restaurant_selector import select_restaurants_for_user
from config import GENERATION_CONFIG, get_connection_params
from data_access import RestaurantDAO, UserDAO
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from services.review_service import ReviewGeneratorService
from utils.blueprint_loader import BlueprintLoader
from utils.date_generator import DateGenerator
from utils.db_connection import DatabaseConnection
from utils.helpers import safe_json_loads
from utils.logging_config import LoggingConfig

logger = logging.getLogger(__name__)

_WORKER_DB_PARAMS: dict[str, str] = {}
_WORKER_RESTAURANTS: list[dict] = []
_WORKER_CITIES: list[dict] = []
_WORKER_ADJACENCY: dict[int, list[int]] = {}
_WORKER_VECTORS_DATA: dict[str, dict] = {}
_WORKER_SIMULATION_TODAY: object = None  # datetime object

def worker_init(db_params, restaurants, cities, adjacency_map, vectors_data, simulation_today):
    global \
        _WORKER_DB_PARAMS, \
        _WORKER_RESTAURANTS, \
        _WORKER_CITIES, \
        _WORKER_ADJACENCY, \
        _WORKER_VECTORS_DATA, \
        _WORKER_SIMULATION_TODAY

    # VALIDATION: Ensure critical data is non-empty (fail-fast)
    if not db_params:
        raise ValueError("worker_init: db_params is empty!")
    if not restaurants:
        raise ValueError("worker_init: restaurants list is empty! Phase 2 may have failed.")
    if not cities:
        raise ValueError("worker_init: cities list is empty! Phase 1 may have failed.")
    if not vectors_data:
        raise ValueError("worker_init: vectors_data (dishes.json) is empty or not loaded!")

    _WORKER_DB_PARAMS = db_params
    _WORKER_RESTAURANTS = restaurants
    _WORKER_CITIES = cities
    _WORKER_ADJACENCY = adjacency_map
    _WORKER_VECTORS_DATA = vectors_data
    _WORKER_SIMULATION_TODAY = simulation_today

    random.seed(os.getpid() + time.time())

def get_dishes_for_restaurant(db: DatabaseConnection, restaurant_id: int):
    """
    Fetch and enrich dishes for a restaurant.

    Note: Database has its own query cache, so no need for application-level caching.
    Each worker process has its own copy of this function.
    """
    dishes = db.fetch_all(
        """
        SELECT d.dish_id, d.dish_name, da.archetype_name, d.price,
               d.secret_base_price, d.secret_quality, d.secret_popularity_factor,
               d.secret_characteristics_vector, d.secret_penalty_vector, dv.variant_name
        FROM dishes d
        JOIN dish_variants dv ON d.secret_variant_id = dv.variant_id
        JOIN dish_archetypes da ON dv.archetype_id = da.archetype_id
        WHERE d.restaurant_id = %s
    """,
        (restaurant_id,),
    )

    if not dishes:
        return []

    dish_ids = [d[0] for d in dishes]
    placeholders = ",".join(["%s"] * len(dish_ids))
    all_ingredients = db.fetch_all(
        f"""
        SELECT dil.dish_id, i.ingredient_name
        FROM dish_ingredients dil
        JOIN ingredients i ON dil.ingredient_id = i.ingredient_id
        WHERE dil.dish_id IN ({placeholders})
    """,
        tuple(dish_ids),
    )

    ingredients_by_dish: dict[int, list[str]] = {}
    if all_ingredients:
        for d_id, i_name in all_ingredients:
            if d_id not in ingredients_by_dish:
                ingredients_by_dish[d_id] = []
            ingredients_by_dish[d_id].append(i_name)

    enriched_dishes = []
    for d in dishes:
        d_id = d[0]
        char_vector = safe_json_loads(d[7])
        enriched_dishes.append(
            {
                "dish_id": d_id,
                "dish_name": d[1],
                "secret_archetype": d[2],
                "price": d[3],
                "secret_base_price": d[4],
                "secret_quality": d[5],
                "secret_popularity_factor": d[6],
                "secret_characteristics_vector": char_vector,
                "secret_penalty_vector": safe_json_loads(d[8]),
                "secret_variant_name": d[9],
                "ingredients": ingredients_by_dish.get(d_id, []),
            }
        )

    return enriched_dishes

def process_user_chunk(user_data_chunk: list[dict]) -> dict[str, int]:
    stats = {"reviews": 0, "skipped_temporal": 0, "home": 0, "nearby": 0, "random": 0}

    date_gen = DateGenerator()
    review_service = ReviewGeneratorService()

    simulation_today = _WORKER_SIMULATION_TODAY

    # Retry connection up to 3 times with exponential backoff
    db = None
    max_retries = 3
    for attempt in range(max_retries):
        try:
            db = DatabaseConnection(_WORKER_DB_PARAMS)
            db.connect()
            break
        except Exception as e:
            if attempt == max_retries - 1:
                logger.error(f"Failed to connect to database after {max_retries} attempts: {e}")
                raise
            time.sleep(2**attempt)  # Exponential backoff: 1s, 2s, 4s

    city_ids = [c["city_id"] for c in _WORKER_CITIES]

    try:
        for user in user_data_chunk:
            user_id = user["user_id"]
            home_city_id = user["city_id"]
            num_reviews = user["secret_total_review_count"]
            travel_prop = user["travel_propensity"]

            reviewed_dishes = set()
            user_review_dates = []

            pending_commits = 0
            BATCH_SIZE = 50

            # Use Beta distribution to skew reviews towards "now" (end of simulation)
            # This ensures we have recent content for moderation queues
            review_dates = date_gen.generate_dates_skewed_to_end(
                count=num_reviews,
                start_date=user["join_date"],
                end_date=simulation_today,  # type: ignore[arg-type]
            )

            for review_date in review_dates:
                # Ensure review_date is naive
                review_date = DateGenerator.ensure_naive(review_date)

                eff_random = 0.05 + (travel_prop * 0.15)
                eff_nearby = 0.10 + (travel_prop * 0.20)
                rand_loc = random.random()

                if rand_loc < eff_random:
                    candidates = [c for c in city_ids if c != home_city_id]
                    city_id = random.choice(candidates) if candidates else home_city_id
                    stats["random"] += 1
                elif rand_loc < (eff_random + eff_nearby):
                    neighbors = _WORKER_ADJACENCY.get(home_city_id, [])
                    if neighbors:
                        city_id = random.choice(neighbors)
                        stats["nearby"] += 1
                    else:
                        candidates = [c for c in city_ids if c != home_city_id]
                        city_id = random.choice(candidates) if candidates else home_city_id
                        stats["random"] += 1
                else:
                    city_id = home_city_id
                    stats["home"] += 1

                available_restaurants = [
                    r
                    for r in _WORKER_RESTAURANTS
                    if r["city_id"] == city_id and DateGenerator.ensure_naive(r["created_at"]) <= review_date
                ]

                if not available_restaurants:
                    available_restaurants = [
                        r for r in _WORKER_RESTAURANTS if DateGenerator.ensure_naive(r["created_at"]) <= review_date
                    ]
                    if not available_restaurants:
                        stats["skipped_temporal"] += 1
                        continue
                    random_res = random.choice(available_restaurants)
                    city_id = random_res["city_id"]
                    available_restaurants = [r for r in available_restaurants if r["city_id"] == city_id]

                selected_restaurant_ids = select_restaurants_for_user(user, available_restaurants, city_id, count=3)

                if not selected_restaurant_ids:
                    continue

                selected_dish = None
                restaurant = None

                for r_id in selected_restaurant_ids:
                    candidate_restaurant = next((r for r in available_restaurants if r["restaurant_id"] == r_id), None)
                    if not candidate_restaurant:
                        continue

                    dishes = get_dishes_for_restaurant(db, r_id)  # type: ignore[arg-type]
                    if not dishes:
                        continue

                    unreviewed = [d for d in dishes if d["dish_id"] not in reviewed_dishes]
                    if not unreviewed:
                        continue

                    restaurant = candidate_restaurant
                    selected_dish = select_dish_from_menu(user, unreviewed)
                    if selected_dish:
                        break

                if not selected_dish or not restaurant:
                    continue

                reviewed_dishes.add(selected_dish["dish_id"])

                user_variant_preference_vector = None

                days_before_review = random.randint(0, 14)
                visit_date = review_date - timedelta(days=days_before_review)

                # Check restaurant created_at to ensure visit isn't before opening
                if restaurant["created_at"]:
                    res_created = restaurant["created_at"]
                    if hasattr(res_created, "date"):
                        res_created = res_created.date()
                    if hasattr(visit_date, "date"):
                        visit_date_val = visit_date.date()
                    else:
                        visit_date_val = visit_date

                    if visit_date_val < res_created:
                        visit_date = review_date  # Fallback: visit same day as review if calc fails

                review_result = review_service.generate_single_review(
                    user=user,
                    restaurant=restaurant,
                    dish=selected_dish,
                    review_date=review_date,
                    vectors_data=_WORKER_VECTORS_DATA,
                    user_variant_preference_vector=user_variant_preference_vector,
                    simulation_today=simulation_today,  # type: ignore[arg-type]
                )

                review_result["review_data"]["visit_date"] = DateGenerator.to_sql_date(visit_date)

                review_id = db.insert_single("reviews", review_result["review_data"])  # type: ignore[union-attr]
                stats["reviews"] += 1
                user_review_dates.append(review_date)
                pending_commits += 1

                if review_result["user_photo"]:
                    db.insert_single(  # type: ignore[union-attr]
                        "media_assets",
                        {
                            "public_id": str(uuid.uuid4()),
                            **review_result["user_photo"],
                            "entity_type": "review",
                            "entity_id": review_id,
                            "is_primary": False,
                            "created_at": DateGenerator.to_sql_datetime(review_date),
                            "uploaded_by": user["user_id"],
                            "version": 1,  # Optimistic Locking
                        },
                    )

                # Batch commit every N reviews for atomicity + performance
                if pending_commits >= BATCH_SIZE:
                    db.commit()  # type: ignore[union-attr]
                    pending_commits = 0

            # Commit any remaining reviews not caught by batch
            if pending_commits > 0:
                db.commit()  # type: ignore[union-attr]
                pending_commits = 0

            if user_review_dates:
                latest = max(user_review_dates)
                offset = timedelta(days=random.randint(0, 30), hours=random.randint(0, 23))
                last_login = latest + offset
                db.execute_query(  # type: ignore[union-attr]
                    "UPDATE users SET last_login_at = %s WHERE user_id = %s",
                    (DateGenerator.to_sql_datetime(last_login), user_id),
                )
                db.commit()  # type: ignore[union-attr]

    except Exception as e:
        logger.error(f"Worker process FAILED for chunk: {e}", exc_info=True)
        if db:
            db.rollback()
            db.close()
        raise  # Propagate exception to parent process
    finally:
        if db and hasattr(db, "is_connected") and db.is_connected():
            db.close()

    return stats

def generate_reviews(db: DatabaseConnection, cleanup: bool = True):
    logger.info("Generating reviews (Multiprocessing)...")

    if cleanup:
        logger.info("Cleaning up Phase 5 data...")
        try:
            # Order matters: notifications reference reviews, so delete them first
            db.execute_query("DELETE FROM notifications WHERE type IN ('like', 'comment')")
            db.execute_query("TRUNCATE TABLE review_likes RESTART IDENTITY CASCADE")
            # Review photos in media_assets (must be deleted before reviews due to FK)
            db.execute_query("DELETE FROM media_assets WHERE entity_type = 'review'")
            # Now safe to truncate reviews
            db.execute_query("TRUNCATE TABLE reviews RESTART IDENTITY CASCADE")
            # Clear moderation logs before generating (to avoid stale IDs)
            db.execute_query("TRUNCATE TABLE system.moderation_logs RESTART IDENTITY CASCADE")
            db.commit()
            logger.info("Cleanup complete.")
        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            logger.warning("Attempting FORCE cleanup with CASCADE...")
            try:
                # Force cleanup if normal order fails
                db.execute_query("TRUNCATE TABLE reviews, review_likes, media_assets RESTART IDENTITY CASCADE")
                db.execute_query("TRUNCATE TABLE system.moderation_logs RESTART IDENTITY CASCADE")
                db.commit()
                logger.info("FORCE cleanup succeeded.")
            except Exception as e2:
                logger.error(f"FORCE cleanup also failed: {e2}")
                db.rollback()
                return

    logger.info("Loading data into memory...")

    restaurants_raw = RestaurantDAO.get_all_restaurants_for_reviews(db)

    restaurants_data = []
    for row in restaurants_raw:
        # Ensure created_at is naive datetime
        created_at = row[3]
        if created_at and hasattr(created_at, "replace"):
            created_at = created_at.replace(tzinfo=None)

        restaurants_data.append(
            {
                "restaurant_id": row[0],
                "city_id": row[1],
                "cuisine_type": row[2],
                "created_at": created_at,
                "secret_price_multiplier": row[4],
                "secret_overall_food_quality": row[5],
                "secret_service_quality": row[6],
                "secret_cleanliness_score": row[7],
                "secret_ambiance_type": row[8],
                "secret_ambiance_quality": row[9],
            }
        )

    # VALIDATION: Ensure we have data from Phase 2 (Restaurants)
    if not restaurants_raw:
        logger.error("CRITICAL: No restaurants found! Phase 2 may have failed. Cannot generate reviews.")
        return

    if len(restaurants_data) == 0:
        logger.error("CRITICAL: restaurants_data is empty after processing. Cannot generate reviews.")
        return

    logger.info(f"Loaded {len(restaurants_data):,} restaurants for review generation")

    cities_raw = db.fetch_all("SELECT city_id, city_name FROM cities")
    cities_data = [{"city_id": c[0], "city_name": c[1]} for c in cities_raw]
    city_name_to_id = {c["city_name"]: c["city_id"] for c in cities_data}

    loader = BlueprintLoader("blueprints")
    city_rules = loader.load_blueprint("cities.json")
    city_config_json = city_rules.get("CITY_CONFIG", {})
    adjacency_map = {}
    for city_name, config in city_config_json.items():
        if city_name in city_name_to_id:
            cid = city_name_to_id[city_name]
            neighbors = [city_name_to_id[n] for n in config.get("adjacency", []) if n in city_name_to_id]
            adjacency_map[cid] = neighbors

    users_raw = UserDAO.get_all_users_for_reviews(db)

    # VALIDATION: Enhanced user filtering check
    total_users_in_db = db.fetch_val("SELECT COUNT(*) FROM users")
    users_with_role_user = len(users_raw) if users_raw else 0
    logger.info(
        f"Phase 5 User Filter: {users_with_role_user:,} users with role='user' (out of {total_users_in_db:,} total)"
    )

    if not users_raw:
        logger.error("No users found to process!")
        return

    if users_with_role_user == 0:
        logger.error("CRITICAL: WHERE role='user' filter returned 0 users! Check user_dao.py:82")
        logger.error(
            f"Total users in DB: {total_users_in_db:,} - all may be non-user roles (restaurant/admin/moderator)"
        )
        return

    user_objects = []
    for u in users_raw:
        join_date = u[12]
        if join_date and hasattr(join_date, "replace"):
            join_date = join_date.replace(tzinfo=None)

        pref_vector = safe_json_loads(u[13], {})
        user_objects.append(
            {
                "user_id": u[0],
                "city_id": u[1],
                "secret_total_review_count": u[2],
                "travel_propensity": u[3],
                "secret_enjoyed_archetypes": safe_json_loads(u[4], {}),
                "secret_ingredient_preferences": safe_json_loads(u[5], {}),
                "secret_price_preference_range": 35.0,
                "secret_price_tolerance_above": 2.0,
                "secret_price_tolerance_below": 0.5,
                "secret_spice_preference": pref_vector.get("flavor_spiciness", 0.5),
                "secret_richness_preference": pref_vector.get("physics_richness", 0.5),
                "secret_texture_preference": pref_vector.get("texture_crispy", 0.5),
                "secret_cleanliness_preference": safe_json_loads(u[6], {}),
                "secret_preferred_ambiance": u[7],
                "secret_mood_propensity": u[8],
                "secret_cross_impact_factor": u[9],
                "secret_chance_dine_random": u[10] if u[10] is not None else 0.1,
                "secret_chance_pick_random_dish": u[11] if u[11] is not None else 0.05,
                "join_date": join_date,
                "secret_characteristics_vector": pref_vector,
                "secret_rating_baseline": u[14] if len(u) > 14 else 6.0,
            }
        )

    logger.info(f"Prepared {len(user_objects)} users.")

    logger.info("Loading dishes.json for rating calculations...")
    vectors_data = loader.load_blueprint("dishes.json")
    logger.info(f"Loaded {len(vectors_data)} archetypes from dishes.json")

    total_cores = cpu_count()
    target_workers = int(total_cores * float(GENERATION_CONFIG.get("worker_cpu_usage_percent", 0.75)))  # type: ignore
    num_processes = max(1, min(target_workers, int(GENERATION_CONFIG.get("max_db_connections_limit", 16))))  # type: ignore

    chunk_size = 100
    user_chunks = [user_objects[i : i + chunk_size] for i in range(0, len(user_objects), chunk_size)]

    logger.info(f"Multiprocessing: {num_processes} processes, {len(user_chunks)} chunks")

    db_params = get_connection_params()

    # Calculate simulation_today for time-based pending logic (last 7 days = pending)
    from datetime import datetime

    simulation_today = datetime.now().replace(tzinfo=None)
    logger.info(f"Simulation today: {simulation_today.date()} (reviews from last 7 days will be pending)")

    total_stats = {"reviews": 0, "skipped_temporal": 0, "home": 0, "nearby": 0, "random": 0}

    with Pool(
        processes=num_processes,
        initializer=worker_init,
        initargs=(db_params, restaurants_data, cities_data, adjacency_map, vectors_data, simulation_today),
    ) as pool:
        for stats in tqdm(
            pool.imap_unordered(process_user_chunk, user_chunks),
            total=len(user_chunks),
            desc="Generating reviews",
            mininterval=1.0,
            disable=LoggingConfig.is_quiet(),
        ):
            for k, v in stats.items():
                total_stats[k] += v

    logger.info(f"Generated {total_stats['reviews']:,} reviews")
    logger.info(
        f"Review Locality: Home={total_stats['home']}, Nearby={total_stats['nearby']}, Random={total_stats['random']}"
    )
    logger.info("Phase 5 completed.")

class ReviewsPhase(BasePhase):
    """
    Phase 5: Reviews Generation

    Generates user reviews for dishes at restaurants using multiprocessing.
    This phase has TRIPLE DEPENDENCIES - requires users, dishes, and restaurants.

    Key features:
    - Multiprocessing with worker pools for parallel review generation
    - Temporal constraints (reviews only for restaurants open at time of review)
    - Travel propensity modeling (home city, nearby cities, random exploration)
    - Moderation state machine (recent reviews go to pending queue)
    - Review photos via media_assets
    - Batch commits for performance

    Dependencies:
        - phase4_users: Need users to generate reviews
        - phase3_dishes: Need dishes to review
        - phase2_restaurants: Need restaurants to visit

    Generates:
        - reviews: Main review records with ratings and text
        - media_assets: User-uploaded review photos
        - Updates users.last_login_at based on review activity
    """

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase5_reviews",
            display_name="Reviews Generation",
            dependencies=[
                "phase4_users",
                "phase2_restaurants",
                "phase3_dishes",
            ],
            required_tables=["reviews", "media_assets"],
            cleanup_tables=["reviews", "review_likes", "media_assets", "system.moderation_logs"],
            estimated_duration=300,  # 5 minutes (multiprocessing-intensive)
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        """
        Execute Phase 5: Reviews Generation.

        Process:
        1. Validate dependencies (users, restaurants, dishes exist)
        2. Load all necessary data into memory for workers
        3. Spawn multiprocessing pool
        4. Generate reviews with temporal/travel logic
        5. Update user last_login timestamps

        Args:
            context: Execution context with database connection

        Returns:
            PhaseResult with review generation statistics
        """
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 5: Reviews Generation (Multiprocessing)")
        logger.info("=" * 60)

        try:
            # Note: cleanup parameter always False - orchestrator handles cleanup
            generate_reviews(context.db, cleanup=False)

            reviews_count = context.db.fetch_val("SELECT COUNT(*) FROM reviews") or 0
            media_count = context.db.fetch_val("SELECT COUNT(*) FROM media_assets WHERE entity_type = 'review'") or 0

            duration = time.time() - start_time

            logger.info(f"Phase 5 completed in {duration:.2f}s")
            logger.info(f"Generated: {reviews_count:,} reviews, {media_count:,} review photos")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={
                    "reviews": reviews_count,
                    "review_photos": media_count,
                },
                error=None,
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 5 FAILED after {duration:.2f}s: {e}", exc_info=True)

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
