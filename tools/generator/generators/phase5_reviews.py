import dataclasses
import logging
import os
import random
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timedelta
from multiprocessing import Pool, cpu_count

from tqdm import tqdm
from uuid6 import uuid7

from algorithms.dish_selector import select_dish_from_menu
from algorithms.restaurant_selector import select_restaurants_for_user
from algorithms.review_builder import generate_single_review
from config import GENERATION_CONFIG, get_connection_params
from data_access import RestaurantDAO, UserDAO
from data_access.city_dao import CityDAO
from models.domain import DishForReview
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_loader import BlueprintLoader
from utils.date_generator import (
    ensure_naive,
    generate_dates_skewed_to_end,
    to_sql_date,
    to_sql_datetime,
)
from utils.db_connection import DatabaseConnection
from utils.helpers import safe_json_loads
from utils.logging_config import LoggingConfig
from utils.photo_pools import PhotoPools
from utils.text_generator import ReviewTextGenerator

logger = logging.getLogger(__name__)

@dataclass
class Phase5WorkerContext:
    db_params: dict[str, str]
    restaurants: list[dict]
    cities: list[dict]
    adjacency_map: dict[int, list[int]]
    vectors_data: dict[str, dict]
    simulation_today: datetime

_worker_ctx: Phase5WorkerContext | None = None

def worker_init(db_params, restaurants, cities, adjacency_map, vectors_data, simulation_today):
    global _worker_ctx

    if not db_params:
        raise ValueError("worker_init: db_params is empty!")
    if not restaurants:
        raise ValueError("worker_init: restaurants list is empty! Phase 2 may have failed.")
    if not cities:
        raise ValueError("worker_init: cities list is empty! Phase 1 may have failed.")
    if not vectors_data:
        raise ValueError("worker_init: vectors_data (dishes.json) is empty or not loaded!")

    _worker_ctx = Phase5WorkerContext(
        db_params=db_params,
        restaurants=restaurants,
        cities=cities,
        adjacency_map=adjacency_map,
        vectors_data=vectors_data,
        simulation_today=simulation_today,
    )

    random.seed(os.getpid() + time.time())

def get_dishes_for_restaurant(db: DatabaseConnection, restaurant_id: int) -> list[DishForReview]:
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

    return [
        DishForReview(
            dish_id=d[0],
            dish_name=d[1],
            secret_archetype=d[2],
            price=d[3],
            secret_base_price=d[4],
            secret_quality=d[5],
            secret_popularity_factor=d[6],
            secret_characteristics_vector=safe_json_loads(d[7]),
            secret_penalty_vector=safe_json_loads(d[8]),
            secret_variant_name=d[9],
            ingredients=ingredients_by_dish.get(d[0], []),
        )
        for d in dishes
    ]

def _select_city(home_city_id: int, travel_prop: float, city_ids: list[int], adjacency_map: dict) -> tuple[int, str]:
    eff_random = 0.05 + (travel_prop * 0.15)
    eff_nearby = 0.10 + (travel_prop * 0.20)
    rand_loc = random.random()

    if rand_loc < eff_random:
        candidates = [c for c in city_ids if c != home_city_id]
        city_id = random.choice(candidates) if candidates else home_city_id
        return city_id, "random"

    if rand_loc < (eff_random + eff_nearby):
        neighbors = adjacency_map.get(home_city_id, [])
        if neighbors:
            return random.choice(neighbors), "nearby"
        candidates = [c for c in city_ids if c != home_city_id]
        city_id = random.choice(candidates) if candidates else home_city_id
        return city_id, "random"

    return home_city_id, "home"

def _select_restaurant_and_dish(
    user: dict, city_id: int, review_date, reviewed_dishes: set, db: DatabaseConnection, ctx: Phase5WorkerContext
) -> tuple[dict | None, dict | None, int]:
    available_restaurants = [
        r
        for r in ctx.restaurants
        if r["city_id"] == city_id and ensure_naive(r["created_at"]) <= review_date
    ]

    if not available_restaurants:
        available_restaurants = [
            r for r in ctx.restaurants if ensure_naive(r["created_at"]) <= review_date
        ]
        if not available_restaurants:
            return None, None, city_id
        random_res = random.choice(available_restaurants)
        city_id = random_res["city_id"]
        available_restaurants = [r for r in available_restaurants if r["city_id"] == city_id]

    selected_restaurant_ids = select_restaurants_for_user(user, available_restaurants, city_id, count=3)
    if not selected_restaurant_ids:
        return None, None, city_id

    for r_id in selected_restaurant_ids:
        candidate_restaurant = next((r for r in available_restaurants if r["restaurant_id"] == r_id), None)
        if not candidate_restaurant:
            continue

        dishes_raw = get_dishes_for_restaurant(db, r_id)
        if not dishes_raw:
            continue

        dishes = [dataclasses.asdict(d) for d in dishes_raw]
        unreviewed = [d for d in dishes if d["dish_id"] not in reviewed_dishes]
        if not unreviewed:
            continue

        selected_dish = select_dish_from_menu(user, unreviewed)
        if selected_dish:
            return candidate_restaurant, selected_dish, city_id

    return None, None, city_id

def _write_review(
    user: dict, restaurant: dict, dish: dict, review_date, text_gen, photo_pools, db, ctx: Phase5WorkerContext
) -> int | None:
    days_before_review = random.randint(0, 14)
    visit_date = review_date - timedelta(days=days_before_review)

    if restaurant["created_at"]:
        res_created = restaurant["created_at"]
        if hasattr(res_created, "date"):
            res_created = res_created.date()
        if hasattr(visit_date, "date"):
            visit_date_val = visit_date.date()
        else:
            visit_date_val = visit_date
        if visit_date_val < res_created:
            visit_date = review_date

    review_result = generate_single_review(
        user=user,
        restaurant=restaurant,
        dish=dish,
        review_date=review_date,
        vectors_data=ctx.vectors_data,
        text_gen=text_gen,
        photo_pools=photo_pools,
        user_variant_preference_vector=None,
        simulation_today=ctx.simulation_today,
    )

    review_result["review_data"]["visit_date"] = to_sql_date(visit_date)
    review_id = db.insert_single("reviews", review_result["review_data"])

    if review_result["user_photo"]:
        db.insert_single(
            "media_assets",
            {
                "public_id": str(uuid7()),
                **review_result["user_photo"],
                "entity_type": "review",
                "entity_id": review_id,
                "is_primary": False,
                "created_at": to_sql_datetime(review_date),
                "uploaded_by": user["user_id"],
                "version": 1,
            },
        )

    return review_id

def _generate_reviews_for_user(user: dict, db: DatabaseConnection, ctx: Phase5WorkerContext) -> dict[str, int]:
    stats = {"reviews": 0, "skipped_temporal": 0, "home": 0, "nearby": 0, "random": 0}
    text_gen = ReviewTextGenerator()
    photo_pools = PhotoPools()
    BATCH_SIZE = int(GENERATION_CONFIG.get("review_commit_batch_size", 50))

    city_ids = [c["city_id"] for c in ctx.cities]
    reviewed_dishes: set = set()
    user_review_dates = []
    pending_commits = 0

    review_dates = generate_dates_skewed_to_end(
        count=user["secret_total_review_count"],
        start_date=user["join_date"],
        end_date=ctx.simulation_today,
    )

    for review_date in review_dates:
        review_date = ensure_naive(review_date)

        city_id, loc_type = _select_city(user["city_id"], user["travel_propensity"], city_ids, ctx.adjacency_map)
        stats[loc_type] += 1

        restaurant, selected_dish, city_id = _select_restaurant_and_dish(
            user, city_id, review_date, reviewed_dishes, db, ctx
        )

        if not selected_dish or not restaurant:
            if loc_type == "random":
                pass  # already counted
            stats["skipped_temporal"] += 1
            continue

        reviewed_dishes.add(selected_dish["dish_id"])

        _write_review(user, restaurant, selected_dish, review_date, text_gen, photo_pools, db, ctx)
        stats["reviews"] += 1
        user_review_dates.append(review_date)
        pending_commits += 1

        if pending_commits >= BATCH_SIZE:
            db.commit()
            pending_commits = 0

    if pending_commits > 0:
        db.commit()
        pending_commits = 0

    if user_review_dates:
        latest = max(user_review_dates)
        offset = timedelta(days=random.randint(0, 30), hours=random.randint(0, 23))
        last_login = latest + offset
        db.execute_query(
            "UPDATE users SET last_login_at = %s WHERE user_id = %s",
            (to_sql_datetime(last_login), user["user_id"]),
        )
        db.commit()

    return stats

def process_user_chunk(user_data_chunk: list[dict]) -> dict[str, int]:
    ctx = _worker_ctx
    assert ctx is not None, "worker_init() must be called before process_user_chunk()"

    total_stats = {"reviews": 0, "skipped_temporal": 0, "home": 0, "nearby": 0, "random": 0}

    db = None
    max_retries = 3
    for attempt in range(max_retries):
        try:
            db = DatabaseConnection(ctx.db_params)
            db.connect()
            break
        except Exception as e:
            if attempt == max_retries - 1:
                logger.error(f"Failed to connect to database after {max_retries} attempts: {e}")
                raise
            time.sleep(2**attempt)

    try:
        for user in user_data_chunk:
            user_stats = _generate_reviews_for_user(user, db, ctx)
            for k, v in user_stats.items():
                total_stats[k] += v

    except Exception as e:
        logger.error(f"Worker process FAILED for chunk: {e}", exc_info=True)
        if db:
            db.rollback()
            db.close()
        raise
    finally:
        if db and hasattr(db, "is_connected") and db.is_connected():
            db.close()

    return total_stats

def generate_reviews(db: DatabaseConnection, cleanup: bool = True):
    logger.info("Generating reviews (Multiprocessing)...")

    if cleanup:
        logger.info("Cleaning up Phase 5 data...")
        try:
            db.execute_query("DELETE FROM notifications WHERE type IN ('like', 'comment')")
            db.execute_query("TRUNCATE TABLE review_likes RESTART IDENTITY CASCADE")
            db.execute_query("DELETE FROM media_assets WHERE entity_type = 'review'")
            db.execute_query("TRUNCATE TABLE reviews RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE system.moderation_logs RESTART IDENTITY CASCADE")
            db.commit()
            logger.info("Cleanup complete.")
        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            logger.warning("Attempting FORCE cleanup with CASCADE...")
            try:
                db.execute_query("TRUNCATE TABLE reviews, review_likes, media_assets RESTART IDENTITY CASCADE")
                db.execute_query("TRUNCATE TABLE system.moderation_logs RESTART IDENTITY CASCADE")
                db.commit()
                logger.info("FORCE cleanup succeeded.")
            except Exception as e2:
                logger.error(f"FORCE cleanup also failed: {e2}")
                db.rollback()
                return

    logger.info("Loading data into memory...")

    restaurants_objs = RestaurantDAO.get_all_restaurants_for_reviews(db)

    if not restaurants_objs:
        logger.error("CRITICAL: No restaurants found! Phase 2 may have failed. Cannot generate reviews.")
        return

    restaurants_data = [dataclasses.asdict(r) for r in restaurants_objs]
    logger.info(f"Loaded {len(restaurants_data):,} restaurants for review generation")

    cities_objs = CityDAO.get_all_cities(db)
    cities_data = [dataclasses.asdict(c) for c in cities_objs]
    city_name_to_id = {c.city_name: c.city_id for c in cities_objs}

    loader = BlueprintLoader("blueprints")
    city_rules = loader.load_blueprint("cities.json")
    city_config_json = city_rules.get("CITY_CONFIG", {})
    adjacency_map = {}
    for city_name, config in city_config_json.items():
        if city_name in city_name_to_id:
            cid = city_name_to_id[city_name]
            neighbors = [city_name_to_id[n] for n in config.get("adjacency", []) if n in city_name_to_id]
            adjacency_map[cid] = neighbors

    users_objs = UserDAO.get_all_users_for_reviews(db)

    total_users_in_db = db.fetch_val("SELECT COUNT(*) FROM users")
    users_with_role_user = len(users_objs) if users_objs else 0
    logger.info(
        f"Phase 5 User Filter: {users_with_role_user:,} users with role='user' (out of {total_users_in_db:,} total)"
    )

    if not users_objs:
        logger.error("No users found to process!")
        return

    if users_with_role_user == 0:
        logger.error("CRITICAL: WHERE role='user' filter returned 0 users!")
        logger.error(
            f"Total users in DB: {total_users_in_db:,} - all may be non-user roles (restaurant/admin/moderator)"
        )
        return

    user_objects = [dataclasses.asdict(u) for u in users_objs]
    logger.info(f"Prepared {len(user_objects)} users.")

    logger.info("Loading dishes.json for rating calculations...")
    vectors_data = loader.load_blueprint("dishes.json")
    logger.info(f"Loaded {len(vectors_data)} archetypes from dishes.json")

    total_cores = cpu_count()
    target_workers = int(total_cores * float(GENERATION_CONFIG.get("worker_cpu_usage_percent", 0.75)))  # type: ignore
    num_processes = max(1, min(target_workers, int(GENERATION_CONFIG.get("max_db_connections_limit", 16))))  # type: ignore

    chunk_size = int(GENERATION_CONFIG.get("review_user_chunk_size", 100))
    user_chunks = [user_objects[i : i + chunk_size] for i in range(0, len(user_objects), chunk_size)]

    logger.info(f"Multiprocessing: {num_processes} processes, {len(user_chunks)} chunks")

    db_params = get_connection_params()

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

    _generate_moderation_results(db)

    logger.info("Phase 5 completed.")

def _generate_moderation_results(db: DatabaseConnection):
    logger.info("Generating moderation results for reviews...")

    db.execute_query("DELETE FROM system.moderation_results WHERE entity_type IN ('review', 'photo')")
    db.commit()

    db.execute_query("""
        INSERT INTO system.moderation_results
            (entity_type, entity_id, status, ai_verdict, ai_model_name, ai_model_version,
             scores, auto_approved, auto_approve_reason, processed_at, created_at)
        SELECT
            'review',
            r.review_id,
            r.content_status,
            CASE
                WHEN r.content_status = 'approved' THEN 'approved'
                WHEN r.content_status = 'pending' THEN 'needs_review'
                ELSE 'approved'
            END,
            'text-moderation-v1',
            'mockHerbert-v1',
            json_build_object(
                'ToxicityScore', CASE WHEN r.content_status = 'pending' THEN round((random() * 0.4 + 0.3)::numeric, 4) ELSE round((random() * 0.1)::numeric, 4) END,
                'NsfwScore', 0.0,
                'RelevanceScore', 1.0,
                'Confidence', CASE WHEN r.content_status = 'approved' THEN 0.95 ELSE 0.5 END
            )::jsonb,
            CASE WHEN r.content_status = 'approved' THEN true ELSE false END,
            CASE WHEN r.content_status = 'approved' THEN 'AI auto-approved (toxicity below threshold)' ELSE NULL END,
            r.created_at,
            r.created_at
        FROM reviews r
        WHERE r.content_status != 'none'
    """)
    db.commit()

    review_mod_count = db.fetch_val(
        "SELECT COUNT(*) FROM system.moderation_results WHERE entity_type = 'review'"
    ) or 0
    logger.info(f"Generated {review_mod_count:,} review moderation results")

    logger.info("Generating moderation results for photos...")
    db.execute_query("""
        INSERT INTO system.moderation_results
            (entity_type, entity_id, status, ai_verdict, ai_model_name, ai_model_version,
             scores, auto_approved, auto_approve_reason, processed_at, created_at)
        SELECT
            'photo',
            ma.asset_id,
            ma.status,
            CASE
                WHEN ma.status = 'approved' THEN 'approved'
                WHEN ma.status = 'pending' THEN 'needs_review'
                ELSE 'approved'
            END,
            'image-moderation-v1',
            'mockNSFW-v1/mockCLIP-v1',
            json_build_object(
                'ToxicityScore', 0.0,
                'NsfwScore', CASE WHEN ma.status = 'pending' THEN round((random() * 0.3 + 0.3)::numeric, 4) ELSE round((random() * 0.05)::numeric, 4) END,
                'RelevanceScore', CASE WHEN ma.status = 'pending' THEN round((random() * 0.2 + 0.3)::numeric, 4) ELSE round((random() * 0.19 + 0.8)::numeric, 4) END,
                'Confidence', CASE WHEN ma.status = 'approved' THEN 0.95 ELSE 0.5 END
            )::jsonb,
            CASE WHEN ma.status = 'approved' THEN true ELSE false END,
            CASE WHEN ma.status = 'approved' THEN 'AI auto-approved (NSFW below threshold)' ELSE NULL END,
            ma.created_at,
            ma.created_at
        FROM media_assets ma
        WHERE ma.entity_type = 'review'
    """)
    db.commit()

    photo_mod_count = db.fetch_val(
        "SELECT COUNT(*) FROM system.moderation_results WHERE entity_type = 'photo'"
    ) or 0
    logger.info(f"Generated {photo_mod_count:,} photo moderation results")

class ReviewsPhase(BasePhase):

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
            required_tables=["reviews", "media_assets", "system.moderation_results"],
            cleanup_tables=["reviews", "review_likes", "media_assets", "system.moderation_logs", "system.moderation_results"],
            estimated_duration=300,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 5: Reviews Generation (Multiprocessing)")
        logger.info("=" * 60)

        try:
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
