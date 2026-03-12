import json
import logging
import random
import time
import uuid
from datetime import datetime, timedelta

from argon2 import PasswordHasher as Argon2Hasher
from scipy.stats import beta as beta_dist
from tqdm import tqdm

from config import GENERATION_CONFIG
from data_access import UserDAO
from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.blueprint_loader import BlueprintLoader
from utils.date_generator import generate_user_join_date, to_sql_datetime
from utils.db_connection import DatabaseConnection
from utils.distributions import sample_normal
from utils.faker_instance import fake
from utils.logging_config import LoggingConfig
from utils.photo_pools import PhotoPools
from utils.text_generator import slugify
from utils.user_helpers import (
    generate_full_name,
    generate_phone,
    generate_user_characteristics_vector,
)

logger = logging.getLogger(__name__)

_argon2_hasher = Argon2Hasher(time_cost=2, memory_cost=19456, parallelism=1, hash_len=32, salt_len=16)

ARGON2_HASH_PASSWORD123 = _argon2_hasher.hash("Password123!")

def generate_argon2_hash(password: str) -> str:
    if password == "Password123!":
        return ARGON2_HASH_PASSWORD123
    return _argon2_hasher.hash(password)

def allocate_users_to_cities(cities, num_users, blueprints_dir="blueprints"):
    blueprint_loader = BlueprintLoader(blueprints_dir)
    city_config = blueprint_loader.load_blueprint("cities.json")["CITY_CONFIG"]

    city_data = {}
    for city_id, city_name in cities:
        if city_name in city_config:
            config = city_config[city_name]
            weight = config.get("weight", 0.0)
            city_data[city_name] = (city_id, weight)
        else:
            city_data[city_name] = (city_id, 0.0)

    city_allocations = {}
    allocated_total = 0

    sorted_cities = sorted(city_data.items(), key=lambda x: x[1][1], reverse=True)

    for city_name, (city_id, weight) in sorted_cities:
        count = int(num_users * weight)
        city_allocations[city_name] = {"city_id": city_id, "count": count, "weight": weight}
        allocated_total += count

    remainder = num_users - allocated_total
    if remainder > 0:
        top_city = sorted_cities[0][0]
        city_allocations[top_city]["count"] += remainder

    user_city_assignments = []
    for city_name, data in city_allocations.items():
        for _ in range(data["count"]):
            user_city_assignments.append((data["city_id"], city_name))

    random.shuffle(user_city_assignments)
    return user_city_assignments

def _generate_user_attributes(
    role: str, all_archetypes: list[str], ingredient_names: list[str]
) -> dict:
    if role != "user":
        return {
            "is_power_user": False,
            "mobility_factor": 0.0,
            "secret_total_review_count": 0,
            "is_influencer": random.random() < 0.50,
            "secret_mood_propensity": 0.0,
            "secret_cross_impact_factor": 0.0,
            "ingredient_preferences": {},
            "enjoyed_archetypes": {},
            "cleanliness_expectations": {},
            "secret_preferred_ambiance": "Casual",
        }

    is_power_user = random.random() < 0.15
    mobility_factor = float(round(beta_dist.rvs(2, 5), 3))

    if is_power_user:
        secret_total_review_count = max(50, int(random.gauss(100, 15)))
        mobility_factor = min(1.0, mobility_factor + 0.1)
        is_influencer = random.random() < 0.20
    elif random.random() < 0.05:
        secret_total_review_count = random.randint(1, 3)
        is_influencer = False
    else:
        secret_total_review_count = max(10, int(random.gauss(40, 20)))
        is_influencer = random.random() < 0.005

    secret_mood_propensity = sample_normal(0.3, 0.05, 0.20, 0.40)
    secret_cross_impact_factor = sample_normal(0.02, 0.01, 0.01, 0.04)

    ingredient_preferences = {}
    sampled_ingredients = random.sample(ingredient_names, min(30, len(ingredient_names)))
    for ingredient in sampled_ingredients:
        ingredient_preferences[ingredient] = round(random.uniform(0.0, 1.0), 2)

    num_favorites = random.randint(3, 7)
    favorites = random.sample(all_archetypes, min(num_favorites, len(all_archetypes)))
    remaining = [a for a in all_archetypes if a not in favorites]
    num_dislikes = random.randint(1, 3)
    dislikes = random.sample(remaining, min(num_dislikes, len(remaining)))

    enjoyed_archetypes = {}
    for arch in favorites:
        enjoyed_archetypes[arch] = round(random.uniform(0.7, 1.0), 2)
    for arch in dislikes:
        enjoyed_archetypes[arch] = round(random.uniform(0.1, 0.3), 2)

    cleanliness_expectations = {
        "Fine dining": round(random.uniform(8.0, 9.5), 1),
        "Casual": round(random.uniform(6.0, 8.0), 1),
        "Fast casual": round(random.uniform(5.0, 7.0), 1),
    }
    ambiance_types = ["Spokojny", "Energiczny", "Romantyczny", "Rodzinny", "Biznesowy"]

    return {
        "is_power_user": is_power_user,
        "mobility_factor": mobility_factor,
        "secret_total_review_count": secret_total_review_count,
        "is_influencer": is_influencer,
        "secret_mood_propensity": secret_mood_propensity,
        "secret_cross_impact_factor": secret_cross_impact_factor,
        "ingredient_preferences": ingredient_preferences,
        "enjoyed_archetypes": enjoyed_archetypes,
        "cleanliness_expectations": cleanliness_expectations,
        "secret_preferred_ambiance": random.choice(ambiance_types),
    }

def generate_users(db: DatabaseConnection, num_users: int = 50000, cleanup: bool = True):
    start_time = time.time()
    logger.info("Generating users...")

    if cleanup:
        logger.info("Cleaning up old Phase 4 data...")
        try:
            db.execute_query("TRUNCATE TABLE users RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE saved_dishes RESTART IDENTITY CASCADE")
            db.commit()
            logger.info("Cleanup complete.")
        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            raise e

    cities = db.fetch_all("SELECT city_id, city_name FROM cities")
    if not cities:
        raise ValueError("Cannot generate users without cities")

    restaurants = db.fetch_all("SELECT restaurant_id, restaurant_name, city_id FROM restaurants")
    logger.info(f"Found {len(restaurants)} restaurants. Deciding ownership (70% target)...")

    all_ingredients = db.fetch_all("SELECT ingredient_name FROM ingredients")
    ingredient_names = [ing_name for (ing_name,) in all_ingredients]

    loader = BlueprintLoader("blueprints")
    dish_blueprints = loader.load_blueprint("dishes.json")
    all_archetypes = list(dish_blueprints.keys())

    photo_pools = PhotoPools()

    common_hash = generate_argon2_hash("Password123!")
    logger.info(f"Generated common Argon2id hash for 'Password123!': {common_hash[:40]}...")

    total_admins = 1
    total_moderators = 3

    user_city_assignments = allocate_users_to_cities(cities, num_users, blueprints_dir="blueprints")
    num_standard_users = len(user_city_assignments)

    user_data = []

    claimed_restaurants = [r for r in restaurants if random.random() < 0.70]

    for r_id, r_name, r_city_id in tqdm(claimed_restaurants, desc="Generating restaurant accounts", unit=" user"):
        sanitized_name = "".join(c for c in r_name if c.isalnum()).lower()[:15]
        username = f"rest_{sanitized_name}_{r_id}"[:30]
        if len(username) < 3:
            username = f"rest{r_id}"
        email = f"contact_{r_id}@{sanitized_name}.com"

        phone = generate_phone()
        join_date = generate_user_join_date()

        days_since_join = (datetime.now() - join_date).days
        if days_since_join > 0:
            last_login = join_date + timedelta(days=random.randint(0, days_since_join), hours=random.randint(0, 23))
        else:
            last_login = join_date

        user_data.append(
            {
                "public_id": str(uuid.uuid4()),
                "username": username,
                "slug": slugify(username),
                "email": email,
                "email_verified": True,
                "is2fa_enabled": random.random() < 0.2,
                "review_count": 0,
                "photo_count": 0,
                "followers_count": 0,
                "following_count": 0,
                "password_hash": common_hash,
                "security_stamp": str(uuid.uuid4()),
                "role": "restaurant",
                "secret_home_city_id": r_city_id,
                "restaurant_id": r_id,
                "created_at": to_sql_datetime(join_date),
                "last_login_at": to_sql_datetime(last_login),
                "is_active": True,
                "is_banned": False,
                "is_deleted": False,
                "deleted_at": None,
                "full_name": r_name,
                "first_name": None,
                "last_name": None,
                "phone": phone,
                "avatar_url": None,
                "avatar_blurhash": None,
                "secret_total_review_count": 0,
                "secret_travel_propensity": 0,
                "secret_enjoyed_archetypes": json.dumps({}),
                "secret_chance_dine_random": 0,
                "secret_chance_pick_random_dish": 0,
                "secret_cross_impact_factor": 0,
                "secret_mood_propensity": 0,
                "secret_is_influencer": False,
                "secret_rating_baseline": 6.0,
                "secret_characteristics_vector": json.dumps({}),
                "secret_ingredient_preferences": json.dumps({}),
                "secret_cleanliness_preference": json.dumps({}),
                "secret_preferred_ambiance": "Casual",
            }
        )

    for i in tqdm(
        range(num_standard_users),
        desc="Generating standard users",
        unit=" user",
        mininterval=1.0,
        disable=LoggingConfig.is_quiet(),
    ):
        if i < total_admins:
            role = "admin"
            username = f"admin_{i + 1}"
            email = f"admin_{i + 1}@smakosz.xyz"
        elif i < total_admins + total_moderators:
            role = "moderator"
            mod_num = i - total_admins + 1
            username = f"moderator_{mod_num}"
            email = f"moderator_{mod_num}@smakosz.xyz"
        else:
            role = "user"
            base_username = fake.user_name()
            username = f"{base_username}{i}"[:30]
            if len(username) < 3:
                username = f"user{i}"
            email = f"{base_username}{i}@example.com"

        city_id, city_name = user_city_assignments[i]
        join_date = generate_user_join_date()

        attrs = _generate_user_attributes(role, all_archetypes, ingredient_names)
        secret_total_review_count = attrs["secret_total_review_count"]
        is_influencer = attrs["is_influencer"]
        mobility_factor = attrs["mobility_factor"]
        secret_mood_propensity = attrs["secret_mood_propensity"]
        secret_cross_impact_factor = attrs["secret_cross_impact_factor"]
        ingredient_preferences = attrs["ingredient_preferences"]
        enjoyed_archetypes = attrs["enjoyed_archetypes"]
        cleanliness_expectations = attrs["cleanliness_expectations"]
        secret_preferred_ambiance = attrs["secret_preferred_ambiance"]

        full_name = generate_full_name()

        phone = generate_phone() if random.random() < 0.30 else None  # type: ignore[assignment]

        custom_avatar_chance = float(GENERATION_CONFIG["custom_avatar_percentage"])  # type: ignore[arg-type]
        if random.random() < custom_avatar_chance:
            avatar_metadata = photo_pools.get_user_avatar()
            avatar_url = avatar_metadata["url"]
            avatar_blurhash = avatar_metadata.get("blurhash")
        else:
            avatar_url = None
            avatar_blurhash = None

        days_since_join = (datetime.now() - join_date).days
        if days_since_join > 0:
            last_login = join_date + timedelta(days=random.randint(0, days_since_join), hours=random.randint(0, 23))
        else:
            last_login = join_date

        is_verified = True
        is_banned = random.random() < 0.002
        is_active = True
        is_deleted = False
        deleted_at = None

        if random.random() < 0.05:
            is_active = False
            is_deleted = True
            days_active = random.randint(1, 300)
            deletion_date = join_date + timedelta(days=days_active)
            if deletion_date > datetime.now():
                deletion_date = datetime.now()
            deleted_at = to_sql_datetime(deletion_date)

        personality_roll = random.random()
        if personality_roll < 0.15:       # 15% harsh critics
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(3.5, 1.0)))
        elif personality_roll < 0.75:     # 60% balanced
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(6.0, 1.2)))
        else:                              # 25% generous
            secret_rating_baseline = max(1.0, min(10.0, random.gauss(8.5, 1.0)))
        secret_rating_baseline = round(secret_rating_baseline, 2)

        user_data.append(
            {
                "public_id": str(uuid.uuid4()),
                "username": username,
                "slug": slugify(username),
                "email": email,
                "email_verified": is_verified,
                "is2fa_enabled": random.random() < 0.05,
                "review_count": 0,
                "photo_count": 0,
                "followers_count": 0,
                "following_count": 0,
                "password_hash": common_hash,
                "security_stamp": str(uuid.uuid4()),
                "role": role,
                "secret_home_city_id": city_id,
                "restaurant_id": None,
                "created_at": to_sql_datetime(join_date),
                "last_login_at": to_sql_datetime(last_login),
                "is_active": is_active,
                "is_banned": is_banned,
                "is_deleted": is_deleted,
                "deleted_at": deleted_at,
                "full_name": full_name,
                "first_name": full_name.split()[0] if full_name else None,
                "last_name": " ".join(full_name.split()[1:]) if full_name and len(full_name.split()) > 1 else None,
                "phone": phone,
                "avatar_url": avatar_url,
                "avatar_blurhash": avatar_blurhash,
                "secret_total_review_count": secret_total_review_count,
                "secret_travel_propensity": round(mobility_factor, 3),
                "secret_enjoyed_archetypes": json.dumps(enjoyed_archetypes),
                "secret_chance_dine_random": 0.1,
                "secret_chance_pick_random_dish": 0.05,
                "secret_cross_impact_factor": round(secret_cross_impact_factor, 3),
                "secret_mood_propensity": round(secret_mood_propensity, 3),
                "secret_is_influencer": is_influencer,
                "secret_rating_baseline": secret_rating_baseline,
                "secret_characteristics_vector": (
                    json.dumps(generate_user_characteristics_vector()) if role == "user" else json.dumps({})
                ),
                "secret_ingredient_preferences": json.dumps(ingredient_preferences),
                "secret_cleanliness_preference": json.dumps(cleanliness_expectations),
                "secret_preferred_ambiance": secret_preferred_ambiance,
            }
        )

    db.insert_bulk("users", user_data)

    logger.info("Syncing restaurant owners...")
    db.execute_query("""
        UPDATE restaurants r
        SET owner_id = u.user_id
        FROM users u
        WHERE u.restaurant_id = r.restaurant_id
          AND u.role = 'restaurant'
    """)
    db.commit()

    _insert_user_avatars_to_media_assets(db, photo_pools)
    _assign_saved_dishes(db)
    _generate_user_notification_settings(db)

    duration = time.time() - start_time
    logger.info(f"Generated {len(user_data)} users in {duration:.2f}s")

def _insert_user_avatars_to_media_assets(db: DatabaseConnection, photo_pools: PhotoPools):
    logger.info("Inserting user avatars into media_assets...")

    users_with_avatars = db.fetch_all("SELECT user_id, avatar_url FROM users WHERE avatar_url IS NOT NULL")

    if not users_with_avatars:
        logger.info("No users with custom avatars found")
        return

    avatar_data = []

    for user_id, avatar_url in tqdm(
        users_with_avatars, desc="Processing avatars", unit=" avatar", mininterval=1.0, disable=LoggingConfig.is_quiet()
    ):
        avatar_data.append(
            {
                "public_id": str(uuid.uuid4()),
                "entity_type": "user",
                "entity_id": user_id,
                "url": avatar_url,
                "blurhash": None,
                "width": None,
                "height": None,
                "is_primary": True,
                "status": "approved",
            }
        )

    if avatar_data:
        db.insert_bulk("media_assets", avatar_data)
        logger.info(f"Inserted {len(avatar_data)} user avatars into media_assets")

def _assign_saved_dishes(db: DatabaseConnection):
    logger.info("Assigning saved dishes...")

    users = UserDAO.get_all_users_basic(db)
    all_dishes = db.fetch_all("SELECT dish_id FROM dishes")

    if not all_dishes:
        logger.warning("No dishes found - skipping saved_dishes")
        return

    saved_data = []
    dish_list = [d[0] for d in all_dishes]

    for user_id, review_count in tqdm(
        users, desc="Assigning saved dishes", unit=" user", mininterval=1.0, disable=LoggingConfig.is_quiet()
    ):
        is_power_user = review_count is not None and review_count > 80

        if random.random() < 0.15:
            continue

        if is_power_user:
            num_saved = random.randint(15, 30)
        else:
            num_saved = random.randint(3, 10)

        num_saved = min(num_saved, len(dish_list))

        sampled_dishes = random.sample(dish_list, num_saved)

        for dish_id in sampled_dishes:
            saved_data.append({"user_id": user_id, "dish_id": dish_id})

    if saved_data:
        db.insert_bulk("saved_dishes", saved_data)
        logger.info(f"Assigned {len(saved_data):,} saved dishes")

def _generate_user_notification_settings(db: DatabaseConnection):
    logger.info("Generating user notification settings...")

    user_ids = db.fetch_all("SELECT user_id FROM users")

    if not user_ids:
        logger.warning("No users found - skipping notification settings")
        return

    settings_data = []
    for (user_id,) in tqdm(
        user_ids, desc="Generating settings", unit=" user", mininterval=1.0, disable=LoggingConfig.is_quiet()
    ):
        settings_data.append(
            {
                "user_id": user_id,
                "push_like": random.random() < 0.80,
                "push_follow": random.random() < 0.90,
                "push_system": True,
            }
        )

    if settings_data:
        db.insert_bulk("user_notification_settings", settings_data)
        logger.info(f"Generated {len(settings_data):,} notification settings")

class UsersPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase4_users",
            display_name="Users Generation",
            dependencies=["phase1_cities", "phase3_dishes"],
            required_tables=["users", "user_notification_settings"],
            cleanup_tables=["users", "user_notification_settings", "saved_dishes"],
            estimated_duration=40,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("Phase 4: Generating users...")

        try:
            num_users = context.config.get("num_users", GENERATION_CONFIG["num_users"])

            generate_users(context.db, num_users=num_users, cleanup=False)

            users_count = context.db.fetch_val("SELECT COUNT(*) FROM users")
            settings_count = context.db.fetch_val("SELECT COUNT(*) FROM user_notification_settings")

            duration = time.time() - start_time
            logger.info(
                f"[OK] Generated {users_count} users with {settings_count} notification settings in {duration:.2f}s"
            )

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated={"users": users_count, "notification_settings": settings_count},
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"[FAIL] Users generation failed: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
