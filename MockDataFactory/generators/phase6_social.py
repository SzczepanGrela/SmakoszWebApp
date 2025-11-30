"""
Phase 6 - Social Graph & Interactions (Likes, Follows, Notifications)
"""

import logging
import random
from datetime import timedelta

from utils.db_connection import DatabaseConnection
from utils.statistical import zipf_distribution
from utils.date_generator import DateGenerator

logger = logging.getLogger(__name__)

def generate_social_interactions(db: DatabaseConnection):
    """
    Generuje warstwę społecznościową:
    1. Follows (Zipf distribution for influencers)
    2. Likes for reviews (Zipf distribution for viral reviews)
    3. Notifications (for likes and follows)
    """
    logger.info(" Generowanie interakcji społecznościowych (Social Graph)...")

    # 1. USER FOLLOWS
    # ---------------------------------------------------------
    logger.info("  Generowanie obserwacji (Follows)...")
    
    # Fetch user_id, role, home_city_id AND secret_is_influencer
    users = db.fetch_all("SELECT user_id, role, home_city_id, secret_is_influencer FROM users")
    user_ids = [u[0] for u in users]
    
    # Identify influencers (REAL ones from Phase 4)
    real_influencers = [u[0] for u in users if u[3] is True]
    
    # Group users by city for local graph generation
    users_by_city = {}
    for u_id, _, city_id, _ in users:
        if city_id not in users_by_city:
            users_by_city[city_id] = []
        users_by_city[city_id].append(u_id)
    
    # Sort users to simulate "early adopters" or "influencers" getting more followers
    # Zipf distribution: top users get disproportionately more followers
    
    num_users = len(user_ids)
    if num_users < 2:
        return

    # Pre-calculate global tiers for fallback/global follows
    # Priority: Real Influencers > Early Adopters (fallback)
    if real_influencers:
        top_1_percent = real_influencers
    else:
        top_1_percent = user_ids[:max(1, int(num_users * 0.01))]
        
    top_10_percent = user_ids[:max(1, int(num_users * 0.10))]
    
    follows_data = []
    notifications_data = []
    
    for follower_id, role, city_id, _ in users:
        # How many people this user follows? (Log-normal: most follow few, some follow many)
        # Mean 8, sigma 5
        num_following = int(random.gauss(8, 5))
        num_following = max(0, min(num_following, 50))
        
        if num_following == 0:
            continue
            
        targets = set()
        
        # Get local peers
        local_peers = users_by_city.get(city_id, [])
        
        for _ in range(num_following):
            rand = random.random()
            target = None
            
            # 70% chance to follow someone LOCAL (if available)
            if rand < 0.70 and len(local_peers) > 1:
                # Pick random local user (simulates friends/local community)
                # Retry up to 3 times to find someone who isn't self
                for _ in range(3):
                    candidate = random.choice(local_peers)
                    if candidate != follower_id:
                        target = candidate
                        break
            
            # 30% chance (or if local failed) to follow GLOBAL (Influencers)
            if target is None:
                rand_global = random.random()
                if rand_global < 0.5: # 50% of global follows go to Top 1%
                    target = random.choice(top_1_percent)
                elif rand_global < 0.8: # 30% to Top 10%
                    target = random.choice(top_10_percent)
                else: # 20% random global
                    target = random.choice(user_ids)
                
            if target and target != follower_id:
                targets.add(target)
        
        for followed_id in targets:
            follows_data.append({
                'follower_id': follower_id,
                'followed_user_id': followed_id
            })
            
            # Create notification for the followed user (10% chance to avoid spam in DB)
            if random.random() < 0.1:
                notifications_data.append({
                    'user_id': followed_id,
                    'type': 'follow',
                    'title': 'Nowy obserwujący',
                    'message': 'Użytkownik zaczął Cię obserwować.',
                    'priority': 2, # Normal priority
                    'reference_id': follower_id,
                    'reference_type': 'user', # NEW: Reference type
                    'is_read': random.choice([True, False])
                })

    # Bulk insert follows
    # Use ON CONFLICT DO NOTHING equivalent logic or ignore errors?
    # Our generator logic (set) prevents duplicates per single follower loop, 
    # but we need to ensure DB clean state.
    if follows_data:
        chunk_size = 10000
        for i in range(0, len(follows_data), chunk_size):
            chunk = follows_data[i:i+chunk_size]
            db.insert_bulk("user_follows", chunk)
        logger.info(f"  Wygenerowano {len(follows_data):,} obserwacji (Follows)")

    # 2. REVIEW LIKES
    # ---------------------------------------------------------
    logger.info("  Generowanie polubień recenzji (Likes)...")
    
    # Fetch reviews to like
    # Only recent reviews get likes usually
    reviews = db.fetch_all("SELECT review_id, user_id, review_date FROM reviews ORDER BY review_date DESC LIMIT 50000")
    
    likes_data = []
    
    for review_id, author_id, r_date in reviews:
        # Zipf-like popularity for reviews
        num_likes = 0
        r = random.random()
        if r < 0.7: num_likes = random.randint(0, 2)
        elif r < 0.9: num_likes = random.randint(3, 10)
        elif r < 0.99: num_likes = random.randint(11, 50)
        else: num_likes = random.randint(51, 200) # Viral
        
        if num_likes == 0:
            continue
            
        likers = random.sample(user_ids, min(len(user_ids), num_likes))
        
        for liker_id in likers:
            if liker_id == author_id:
                continue
                
            likes_data.append({
                'user_id': liker_id,
                'review_id': review_id
            })
            
            # Notification for author (aggregated or single)
            # Generate only for 5% of likes to save DB space
            if random.random() < 0.05:
                notifications_data.append({
                    'user_id': author_id,
                    'type': 'like',
                    'title': 'Nowe polubienie',
                    'message': 'Ktoś polubił Twoją recenzję.',
                    'priority': 1, # Low priority
                    'reference_id': review_id,
                    'reference_type': 'review', # NEW: Reference type
                    'is_read': random.choice([True, False])
                })

    if likes_data:
        chunk_size = 10000
        for i in range(0, len(likes_data), chunk_size):
            chunk = likes_data[i:i+chunk_size]
            db.insert_bulk("review_likes", chunk)
        logger.info(f"  Wygenerowano {len(likes_data):,} polubień (Likes)")

    # 3. NOTIFICATIONS
    # ---------------------------------------------------------
    logger.info("  Generowanie powiadomień...")
    
    # Add some system notifications
    for user_id in random.sample(user_ids, int(len(user_ids) * 0.2)): # 20% users
        notifications_data.append({
            'user_id': user_id,
            'type': 'system',
            'title': 'Witamy w Smakoszu!',
            'message': 'Cieszymy się, że jesteś z nami. Odkryj najlepsze restauracje w Twojej okolicy.',
            'priority': 3, # High priority
            'reference_id': None,
            'reference_type': None,
            'is_read': True
        })

    if notifications_data:
        # Sort by user_id for better DB index performance on insert
        notifications_data.sort(key=lambda x: x['user_id'])
        
        chunk_size = 5000
        for i in range(0, len(notifications_data), chunk_size):
            chunk = notifications_data[i:i+chunk_size]
            db.insert_bulk("notifications", chunk)
            
        logger.info(f"  Wygenerowano {len(notifications_data):,} powiadomień")

    # 4. SEARCH HISTORY
    # ---------------------------------------------------------
    logger.info("  Generowanie historii wyszukiwania...")
    
    # Pobierz miasta i archetypy do generowania zapytań
    cities = db.fetch_all("SELECT city_name FROM cities")
    city_names = [c[0] for c in cities]
    archetypes = ["Pizza", "Burger", "Sushi", "Pasta", "Kebab", "Ramen", "Steak", "Wegańskie", "Tanio", "Randka"]
    
    search_data = []
    
    # 40% users have search history
    active_searchers = random.sample(user_ids, int(len(user_ids) * 0.4))
    
    for user_id in active_searchers:
        num_searches = random.randint(1, 5)
        for _ in range(num_searches):
            r = random.random()
            if r < 0.4:
                query = random.choice(city_names)
            elif r < 0.7:
                query = random.choice(archetypes)
            else:
                query = f"{random.choice(archetypes)} {random.choice(city_names)}"
                
            search_data.append({
                'user_id': user_id,
                'search_query': query
            })
            
    if search_data:
        chunk_size = 10000
        for i in range(0, len(search_data), chunk_size):
            chunk = search_data[i:i+chunk_size]
            db.insert_bulk("search_history", chunk)
        logger.info(f"  Wygenerowano {len(search_data):,} wpisów historii wyszukiwania")

    # 5. DATA CORRECTION REQUESTS
    # ---------------------------------------------------------
    logger.info("  Generowanie zgłoszeń błędów w danych (Data Correction)...")
    
    # Pick random restaurants that have issues
    restaurants = db.fetch_all("SELECT restaurant_id FROM restaurants")
    restaurant_ids = [r[0] for r in restaurants]
    
    issue_types = ['wrong_hours', 'closed_permanently', 'wrong_address', 'bad_menu', 'wrong_phone']
    
    correction_data = []
    num_requests = 200  # Rare event
    
    for _ in range(num_requests):
        user_id = random.choice(user_ids)
        restaurant_id = random.choice(restaurant_ids)
        issue = random.choice(issue_types)
        
        correction_data.append({
            'user_id': user_id,
            'restaurant_id': restaurant_id,
            'issue_type': issue,
            'description': f'Zgłoszenie błędu: {issue}. Proszę o weryfikację.',
            'status': random.choice(['pending', 'pending', 'resolved']) # Mostly pending
        })
        
    if correction_data:
        db.insert_bulk("data_correction_requests", correction_data)
        logger.info(f"  Wygenerowano {len(correction_data)} zgłoszeń błędów danych")
