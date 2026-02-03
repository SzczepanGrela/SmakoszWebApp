import json
import logging
import os
import random
from datetime import datetime
from multiprocessing import Pool, cpu_count

import numpy as np
from tqdm import tqdm

from config import GENERATION_CONFIG, get_connection_params
from data_access import CityDAO, RestaurantDAO, ReviewDAO, UserDAO
from utils.blueprint_loader import BlueprintLoader
from utils.db_connection import DatabaseConnection

from .workers.phase6_worker import process_follows_chunk, worker_init_phase6

logger = logging.getLogger(__name__)

def flush_notifications(db: DatabaseConnection, buffer: list, threshold: int = 5000, force: bool = False):
    """
    Insert notifications buffer to database when threshold is reached.

    Args:
        db: Database connection
        buffer: List of notification dicts
        threshold: Insert when buffer reaches this size (default: 5000)
        force: Force insert even if below threshold (for final flush)

    Returns:
        Number of items remaining in buffer (0 if flushed, len(buffer) otherwise)
    """
    if (len(buffer) >= threshold or (force and len(buffer) > 0)) and buffer:
        # Sort by user_id for better database performance
        buffer.sort(key=lambda x: x["user_id"])
        db.insert_bulk("notifications", buffer)
        logger.debug(f"Flushed {len(buffer):,} notifications to database")
        buffer.clear()
    return len(buffer)

def generate_social_graph(db: DatabaseConnection, cleanup: bool = True):
    logger.info("Generating social graph...")

    if cleanup:
        logger.info("Cleaning up old Phase 6 data...")
        try:
            db.execute_query("TRUNCATE TABLE user_follows RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE review_likes RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE notifications RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE search_history RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE data_correction_requests RESTART IDENTITY CASCADE")
            db.execute_query("TRUNCATE TABLE reports RESTART IDENTITY CASCADE")
            db.commit()
            logger.info("Cleanup complete.")
        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
            db.rollback()
            raise e

    loader = BlueprintLoader("blueprints")
    variant_blueprints = loader.load_blueprint("dishes.json")
    archetypes = list(variant_blueprints.keys())

    # User Follows (MULTIPROCESSING)
    logger.info("Generating user follows...")

    # Memory optimization: Only fetch necessary columns
    users = UserDAO.get_all_users_for_social(db)
    user_ids = [int(u[0]) for u in users]  # Cast to native int

    username_map = {int(u[0]): u[1] for u in users}  # Cast keys to native int

    real_influencers = [int(u[0]) for u in users if u[3] is True]  # Cast to native int

    users_by_city: dict[int, list[int]] = {}
    for u_id, _, city_id, _ in users:
        u_id_int = int(u_id)  # Cast to native int
        if city_id not in users_by_city:
            users_by_city[city_id] = []
        users_by_city[city_id].append(u_id_int)

    num_users = len(user_ids)
    if num_users < 2:
        return

    if real_influencers:
        top_1_percent = real_influencers
    else:
        top_1_percent = user_ids[: max(1, int(num_users * 0.01))]

    top_10_percent = user_ids[: max(1, int(num_users * 0.10))]

    # Prepare user chunks for multiprocessing
    user_tuples = [(int(u[0]), u[1], u[2]) for u in users]  # (user_id, username, city_id)

    # Calculate worker count
    total_cores = cpu_count()
    target_workers = int(total_cores * float(GENERATION_CONFIG.get("worker_cpu_usage_percent", 0.75)))  # type: ignore
    num_processes = max(1, min(target_workers, int(GENERATION_CONFIG.get("max_db_connections_limit", 16))))  # type: ignore

    chunk_size = max(100, num_users // (num_processes * 4))  # Dynamic chunk size
    user_chunks = [user_tuples[i : i + chunk_size] for i in range(0, len(user_tuples), chunk_size)]

    logger.info(f"Multiprocessing: {num_processes} processes, {len(user_chunks)} chunks, {chunk_size} users/chunk")

    db_params = get_connection_params()

    follows_data = []
    notifications_buffer = []
    total_follows = 0
    total_notifications = 0

    # Process chunks in parallel with chunked insertion
    logger.info("Processing follows with chunked insertion...")
    with Pool(
        processes=num_processes,
        initializer=worker_init_phase6,
        initargs=(db_params, user_ids, users_by_city, top_1_percent, top_10_percent, username_map),
    ) as pool:
        for result in tqdm(
            pool.imap_unordered(process_follows_chunk, user_chunks),
            total=len(user_chunks),
            desc="Generating follows",
            mininterval=1.0,
        ):
            follows_data.extend(result["follows"])

            # Insert follows in chunks to avoid memory buildup
            if len(follows_data) >= 10000:
                db.insert_bulk("user_follows", follows_data)
                total_follows += len(follows_data)
                follows_data.clear()

        # Final flush for remaining follows
        if follows_data:
            db.insert_bulk("user_follows", follows_data)
            total_follows += len(follows_data)
            follows_data.clear()

    logger.info(f"Generated {total_follows:,} follows")
    logger.info("Notifications handled by DB triggers.")

    # Review Likes (Optimized with Zipf Distribution)
    logger.info("Generating review likes...")

    # Fetch ALL review IDs and authors (lightweight query)
    logger.info("Fetching review IDs...")
    reviews = ReviewDAO.get_all_reviews_basic(db)

    if not reviews:
        logger.warning("No reviews found, skipping likes generation")
    else:
        review_ids = np.array([row[0] for row in reviews])
        review_authors = {int(row[0]): int(row[1]) for row in reviews} 
        num_reviews = len(review_ids)

        logger.info(f"Found {num_reviews:,} reviews")

        # Vectorized: Use Zipf distribution for realistic popularity
        # Average ~5 likes per review, with power-law distribution
        total_target_likes = int(num_reviews * 5)

        # Generate like counts using power-law (Zipf-like) distribution
        # Most reviews get 0-2 likes, some get many
        logger.info(f"Generating ~{total_target_likes:,} likes with Zipf distribution...")

        # Use numpy's zipf distribution (parameter a=2.0 gives realistic skew)
        zipf_samples = np.random.zipf(a=2.0, size=num_reviews)
        # Clip to reasonable range (0-200)
        like_counts = np.clip(zipf_samples - 1, 0, 200).astype(int)

        # Adjust to hit target total (scale if needed)
        current_total = like_counts.sum()
        if current_total > 0:
            scale_factor = total_target_likes / current_total
            like_counts = (like_counts * scale_factor).astype(int)

        total_likes_needed = int(like_counts.sum())
        logger.info(f"Total likes to generate: {total_likes_needed:,}")

        # VECTORIZED APPROACH: Generate all likes at once
        logger.info("Generating likes using fully vectorized operations...")

        # Step 1: Create array of review IDs weighted by like counts
        # np.repeat: If review_1 gets 5 likes, repeat it 5 times
        # Result: [r1, r1, r1, r1, r1, r2, r2, r2, ...]
        logger.info("Creating weighted review array...")
        liked_review_ids = np.repeat(review_ids, like_counts)

        # Step 2: Generate random liker IDs for all likes at once
        logger.info(f"Sampling {len(liked_review_ids):,} liker IDs...")
        liker_user_ids = np.random.choice(user_ids, size=len(liked_review_ids), replace=True)

        # Step 3: Filter out self-likes
        # Identify indices where liker_user_id == author of the review
        # We need to map liked_review_ids to their authors efficiently
        
        # Vectorized mapping: Create an array of authors corresponding to liked_review_ids
        # Since liked_review_ids contains review_ids, and we have a dict, we can map it.
        # But map is slow for arrays. Better: review_authors is a dict.
        # Let's use list comprehension (fast enough for <10M items usually) or pandas if available (but we stick to numpy).
        
        # Optimization: iterate and build a mask.
        # Or simply: Loop and filter? No, slow.
        # Vectorized way:
        # Create an array of authors matching the shape of liked_review_ids.
        # We know liked_review_ids comes from repeating review_ids.
        # So we can repeat the authors array in the same way!
        
        review_authors_array = np.array([review_authors[rid] for rid in review_ids])
        liked_review_authors = np.repeat(review_authors_array, like_counts)
        
        # Now compare
        self_like_mask = (liker_user_ids == liked_review_authors)
        valid_likes_mask = ~self_like_mask
        
        logger.info(f"Filtering self-likes: Removed {np.sum(self_like_mask):,} self-likes")
        
        liker_user_ids = liker_user_ids[valid_likes_mask]
        liked_review_ids = liked_review_ids[valid_likes_mask]
        
        # Step 5: Remove duplicates using numpy unique
        logger.info("Removing duplicate likes...")
        # Stack into (n, 2) array for unique operation
        likes_pairs = np.column_stack((liker_user_ids, liked_review_ids))

        # Get unique pairs
        unique_likes_pairs = np.unique(likes_pairs, axis=0)

        logger.info(f"Final unique likes: {len(unique_likes_pairs):,}")

        # Step 6: Insert likes in chunks (streaming approach)
        logger.info("Inserting likes with chunked insertion...")
        
        # PERFORMANCE OPTIMIZATION: Disable triggers during bulk load
        logger.info("Disabling triggers on review_likes for performance...")
        try:
            db.execute_query("ALTER TABLE review_likes DISABLE TRIGGER trg_sync_review_likes_insert")
            db.execute_query("ALTER TABLE review_likes DISABLE TRIGGER trg_sync_review_likes_delete")
            db.execute_query("ALTER TABLE review_likes DISABLE TRIGGER trg_notify_like")
            db.commit()
        except Exception as e:
            logger.warning(f"Could not disable triggers (might need superuser): {e}")

        chunk_size = 50000 
        total_likes_inserted = 0
        
        # Process likes in chunks with progress bar
        num_chunks = (len(unique_likes_pairs) + chunk_size - 1) // chunk_size
        for chunk_start in tqdm(
            range(0, len(unique_likes_pairs), chunk_size),
            total=num_chunks,
            desc="Inserting likes (chunks)",
            unit=" chunk",
            mininterval=0.5,
        ):
            chunk_end = min(chunk_start + chunk_size, len(unique_likes_pairs))
            chunk_pairs = unique_likes_pairs[chunk_start:chunk_end]

            # Convert chunk to dicts
            likes_chunk = [
                {
                    "user_id": int(pair[0]),  # Cast to native int
                    "review_id": int(pair[1]),  # Cast to native int
                }
                for pair in chunk_pairs
            ]
            
            # OPTIMIZATION: Sort by review_id to improve DB locality during index updates
            likes_chunk.sort(key=lambda x: x["review_id"])

            # Insert chunk immediately (Triggers disabled)
            db.insert_bulk("review_likes", likes_chunk)
            total_likes_inserted += len(likes_chunk)
            
            # Clear chunk to free memory
            likes_chunk.clear()

        # Re-enable Triggers
        logger.info("Re-enabling triggers on review_likes...")
        try:
            db.execute_query("ALTER TABLE review_likes ENABLE TRIGGER trg_sync_review_likes_insert")
            db.execute_query("ALTER TABLE review_likes ENABLE TRIGGER trg_sync_review_likes_delete")
            db.execute_query("ALTER TABLE review_likes ENABLE TRIGGER trg_notify_like")
            db.commit()
        except Exception as e:
            logger.error(f"Could not re-enable triggers: {e}")

        logger.info(f"Generated {total_likes_inserted:,} likes")
        
        # Post-process: Generate notifications (since trigger was disabled)
        logger.info("Generating notifications for likes (Bulk)...")
        # We assume all current likes need notifications (Phase 6 is fresh generation).
        # In a real migration, we'd filter by created_at, but here we truncated everything.
        # However, we must respect the 50 limit pruning eventually, but for now generate all 
        # (or rely on prune later). Generating 12M notifications is heavy.
        # Let's check if we really want 12M notifications. 
        # Maybe we only generate for the latest ones?
        # Or better: Just run the bulk INSERT SELECT matching the trigger logic.
        
        db.execute_query("""
            INSERT INTO notifications (user_id, actor_id, type, title, message, metadata, priority)
            SELECT 
                r.user_id,          -- Recipient
                rl.user_id,         -- Actor
                'like',
                'Nowe polubienie',
                'Użytkownik polubił Twoją recenzję.',
                json_build_object(
                    'review_id', rl.review_id,
                    'target_type', 'review',
                    'dish_name', d.dish_name,
                    'restaurant_name', rest.restaurant_name
                ),
                1
            FROM review_likes rl
            JOIN reviews r ON rl.review_id = r.review_id
            JOIN dishes d ON r.dish_id = d.dish_id
            JOIN restaurants rest ON r.restaurant_id = rest.restaurant_id
            WHERE r.user_id != rl.user_id
        """)
        db.commit()
        logger.info("Notifications generated.")

    # System Welcome Notifications
    logger.info("Generating system notifications...")

    # Select 20% of users for welcome notifications
    num_welcome = int(len(user_ids) * 0.2)
    welcome_users = random.sample(user_ids, num_welcome)

    notifications_buffer = []
    for user_id in welcome_users:
        notifications_buffer.append(
            {
                "user_id": int(user_id),  # Cast to native int
                "type": "system",
                "title": "Witamy w Smakoszu!",
                "message": "Cieszymy się, że jesteś z nami. Odkryj najlepsze restauracje w Twojej okolicy.",
                "priority": 3,
                "metadata": json.dumps({"action": "welcome"}),
                "is_read": True,
            }
        )

        # Flush periodically
        flush_notifications(db, notifications_buffer, threshold=5000)

    # Final flush
    flush_notifications(db, notifications_buffer, force=True)

    logger.info(f"Generated {num_welcome:,} system welcome notifications")

    # Search History (Optimized)
    logger.info("Generating search history...")

    cities = CityDAO.get_all_city_names(db)
    city_names = np.array([c[0] for c in cities])

    search_data = []

    # Vectorized: Select active searchers (40% of users)
    num_searchers = int(len(user_ids) * 0.4)
    active_searchers = np.random.choice(user_ids, size=num_searchers, replace=False)

    # Vectorized: Generate search counts for all searchers at once
    search_counts = np.random.randint(1, 6, size=num_searchers)
    total_searches = int(search_counts.sum())  # Cast to native int

    logger.info(f"Generating {total_searches:,} search queries for {num_searchers:,} users...")

    # Vectorized: Pre-generate all search types
    search_types = np.random.random(total_searches)

    search_idx = 0
    for user_idx, user_id in enumerate(active_searchers):
        num_searches = int(search_counts[user_idx])  # Cast to native int

        for _ in range(num_searches):
            r = search_types[search_idx]
            search_idx += 1

            if r < 0.4:
                query = str(np.random.choice(city_names))
            elif r < 0.7:
                query = str(np.random.choice(archetypes))
            else:
                query = f"{str(np.random.choice(archetypes))} {str(np.random.choice(city_names))}"

            search_data.append(
                {
                    "user_id": int(user_id),  # Cast to native int
                    "search_query": query,
                }
            )

    if search_data:
        chunk_size = 10000
        for i in range(0, len(search_data), chunk_size):
            chunk = search_data[i : i + chunk_size]
            db.insert_bulk("search_history", chunk)
        logger.info(f"Generated {len(search_data):,} search history entries")

    # Prune old notifications to save space (keep latest 50 per user)
    logger.info("Pruning old notifications...")
    db.execute_query("SELECT prune_notifications()")
    db.commit()

    # Data Correction Requests (Optimized)
    logger.info("Generating data correction requests...")

    restaurants = RestaurantDAO.get_all_restaurant_ids(db)
    restaurant_ids = np.array([row[0] for row in restaurants])

    issue_types = ["wrong_hours", "closed_permanently", "wrong_address", "bad_menu", "wrong_phone"]

    num_requests = 200

    # Vectorized: Generate all requests at once
    request_users = np.random.choice(user_ids, size=num_requests)
    request_restaurants = np.random.choice(restaurant_ids, size=num_requests)
    request_issues = np.random.choice(issue_types, size=num_requests)
    request_statuses = np.random.choice(["pending", "pending", "resolved"], size=num_requests)

    correction_data = [
        {
            "user_id": int(request_users[i]),  # Cast to native int
            "restaurant_id": int(request_restaurants[i]),  # Cast to native int
            "issue_type": str(request_issues[i]),  # Cast to native str
            "description": f"Zgłoszenie błędu: {str(request_issues[i])}. Proszę o weryfikację.",
            "status": str(request_statuses[i]),  # Cast to native str
        }
        for i in range(num_requests)
    ]

    if correction_data:
        db.insert_bulk("data_correction_requests", correction_data)
        logger.info(f"Generated {len(correction_data)} correction requests")

    # Favorite Restaurants (NEW)
    logger.info("Generating favorite restaurants...")
    
    # Logic: About 5-10% of users have favorite restaurants (Loyalty)
    # Each user picks 1-3 favorite places (usually visited before)
    
    favorite_data = []
    
    # Fetch active users and restaurants
    active_users_subset = np.random.choice(user_ids, size=int(len(user_ids) * 0.10), replace=False)
    
    for u_id in active_users_subset:
        # User picks 1-3 favorites from known restaurants (simplified: pick random from list)
        num_favs = random.randint(1, 3)
        picked_restaurants = np.random.choice(restaurant_ids, size=num_favs, replace=False)
        
        for r_id in picked_restaurants:
            favorite_data.append({
                "user_id": int(u_id),
                "restaurant_id": int(r_id),
            })
            
    if favorite_data:
        db.insert_bulk("favorite_restaurants", favorite_data)
        logger.info(f"Generated {len(favorite_data)} favorite restaurant entries")

    # Reports (Abuse/Content) - UPDATED v5.5 SCHEMA
    logger.info("Generating abuse reports (Many-to-Many Schema)...")

    # 1. Seed Reason Definitions
    logger.info("Seeding report reason definitions...")
    reason_definitions = [
        {"reason_code": "spam", "label_pl": "Spam lub reklama", "description": "Treści reklamowe, powtarzające się.", "severity_score": 1},
        {"reason_code": "offensive", "label_pl": "Treści obraźliwe", "description": "Wulgaryzmy, mowa nienawiści.", "severity_score": 3},
        {"reason_code": "fake", "label_pl": "Fałszywa informacja", "description": "Wprowadzanie w błąd, fake news.", "severity_score": 2},
        {"reason_code": "irrelevant", "label_pl": "Nie na temat", "description": "Treść nie związana z restauracją.", "severity_score": 1},
        {"reason_code": "harassment", "label_pl": "Nękanie", "description": "Ataki personalne na użytkowników/obsługę.", "severity_score": 4},
        {"reason_code": "sexual", "label_pl": "Treści seksualne", "description": "Nagość, pornografia.", "severity_score": 5},
    ]
    
    # Using execute_query since definitions are static/config
    for r in reason_definitions:
        db.execute_query("""
            INSERT INTO report_reason_definitions (reason_code, label_pl, description, severity_score)
            VALUES (%s, %s, %s, %s)
            ON CONFLICT (reason_code) DO NOTHING
        """, (r["reason_code"], r["label_pl"], r["description"], r["severity_score"]))
    db.commit()

    # Fetch moderators/admins for resolution
    mod_users = db.fetch_all("SELECT user_id FROM users WHERE role IN ('admin', 'moderator')")
    mod_ids = [u[0] for u in mod_users] if mod_users else []

    report_reasons_keys = [r["reason_code"] for r in reason_definitions]
    reports_buffer = []
    assignments_buffer = []

    # 1. Report Reviews
    if len(review_ids) > 0:
        sample_size = min(150, len(review_ids))
        target_reviews = np.random.choice(review_ids, size=sample_size, replace=False)
        reporter_users = np.random.choice(user_ids, size=sample_size, replace=True)

        for i, r_id in enumerate(target_reviews):
            status = str(np.random.choice(["pending", "resolved", "dismissed"], p=[0.7, 0.2, 0.1]))
            resolved_by = None
            resolved_at = None
            
            if status != "pending" and mod_ids:
                resolved_by = int(random.choice(mod_ids))
                resolved_at = datetime.now().isoformat()

            # Create Report
            report_data = {
                "reporter_id": int(reporter_users[i]),
                "entity_type": "review",
                "entity_id": int(r_id),
                "description": "Zgłoszenie naruszenia regulaminu (Auto-generated).",
                "status": status,
                "resolved_by_admin_id": resolved_by,
                "resolved_at": resolved_at,
                "version": 1
            }
            
            # Insert Single (need ID for assignment)
            report_id = db.insert_single("reports", report_data)
            
            # Create Assignments (1-2 random reasons)
            num_reasons = random.randint(1, 2)
            picked_reasons = random.sample(report_reasons_keys, num_reasons)
            
            for code in picked_reasons:
                assignments_buffer.append({
                    "report_id": report_id,
                    "reason_code": code
                })

    # 2. Report Photos
    photo_sample = db.fetch_all("SELECT asset_id FROM media_assets WHERE entity_type = 'review' LIMIT 1000")
    if photo_sample:
        photo_ids = [p[0] for p in photo_sample]
        target_photos = random.sample(photo_ids, min(50, len(photo_ids)))
        reporter_users_p = np.random.choice(user_ids, size=len(target_photos))

        for i, pid in enumerate(target_photos):
            status = "pending"
            
            report_data = {
                "reporter_id": int(reporter_users_p[i]),
                "entity_type": "photo",
                "entity_id": pid,
                "description": "Nieodpowiednie zdjęcie (Auto-generated).",
                "status": status,
                "resolved_by_admin_id": None,
                "resolved_at": None,
                "version": 1
            }
            
            report_id = db.insert_single("reports", report_data)
            
            assignments_buffer.append({
                "report_id": report_id,
                "reason_code": str(np.random.choice(["sexual", "offensive", "irrelevant"]))
            })

    # Bulk insert assignments
    if assignments_buffer:
        db.insert_bulk("report_reason_assignments", assignments_buffer)
        logger.info(f"Generated {len(assignments_buffer)} report reason assignments")
    
    logger.info("Generated abuse reports successfully.")

    # Final step: Update average ratings for all restaurants/dishes
    logger.info("Updating average ratings...")
    try:
        db.execute_query("SELECT update_average_ratings()")
        db.commit()
        logger.info("Average ratings updated successfully")
        
        logger.info("Updating review helpful counts...")
        db.execute_query("SELECT sync_helpful_counts()")
        db.commit()
        logger.info("Review helpful counts updated successfully")
    except Exception as e:
        logger.warning(f"Failed to update averages or counts: {e}")
        # Non-critical, continue anyway

if __name__ == "__main__":
    import os
    import sys

    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

    from config import get_connection_params

    logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(name)s - %(levelname)s - %(message)s")

    try:
        connection_params = get_connection_params()

        with DatabaseConnection(connection_params) as db:
            generate_social_graph(db)
            logger.info("Phase 6 completed.")

    except Exception as e:
        logger.error(f"Error: {e}", exc_info=True)
        sys.exit(1)
