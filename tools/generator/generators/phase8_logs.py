import base64
import hashlib
import json
import logging
import random
import time
import uuid
from datetime import datetime, timedelta

from psycopg2.extras import Json

from orchestration.context import ExecutionContext
from orchestration.phase import BasePhase, PhaseMetadata, PhaseResult, PhaseStatus
from utils.date_generator import ensure_naive
from utils.db_connection import DatabaseConnection
from utils.faker_instance import fake

logger = logging.getLogger(__name__)

ADMIN_NOTE_TEMPLATES = [
    "Zatwierdzono po manualnej weryfikacji.",
    "Odrzucono - wulgaryzmy.",
    "Wymaga dodatkowej kontroli.",
    "Tresc niezgodna z regulaminem.",
    "Zatwierdzono - falszywy alarm AI.",
    "Odrzucono - spam reklamowy.",
]

VERDICT_LOG_MAP = {
    "approved": "approve",
    "rejected": "reject",
    "needs_review": "needs_review",
}

EMAIL_TYPE_VERIFICATION = "Verification"
EMAIL_TYPE_TWO_FACTOR = "TwoFactorAuth"
EMAIL_TYPE_PASSWORD_RESET = "PasswordReset"

EMAIL_SUBJECTS = {
    EMAIL_TYPE_VERIFICATION: "Weryfikacja email",
    EMAIL_TYPE_TWO_FACTOR: "Kod 2FA",
    EMAIL_TYPE_PASSWORD_RESET: "Reset hasla",
}


def _generate_ai_logs(db: DatabaseConnection) -> int:
    logger.info("Generating AI moderation logs...")

    rows = db.fetch_all(
        "SELECT entity_type, entity_id, scores, ai_verdict, processed_at "
        "FROM system.moderation_results"
    )

    if not rows:
        logger.warning("No moderation_results found, skipping ai_logs")
        return 0

    buffer = []
    for entity_type, entity_id, scores, verdict, processed_at in rows:
        if entity_type == "review":
            model_type = "text_moderation"
            model_name = "HerBERT-base-cased-pl"
            model_version = "1.0.0"
        elif entity_type == "photo":
            model_type = "image_moderation"
            model_name = "gemini-2.0-flash"
            model_version = "2.0"
        else:
            continue

        buffer.append({
            "model_type": model_type,
            "model_name": model_name,
            "model_version": model_version,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "scores": Json(scores) if scores is not None else None,
            "verdict": verdict,
            "processing_time_ms": random.randint(50, 500),
            "fallback": False,
            "created_at": ensure_naive(processed_at),
        })

        if len(buffer) >= 5000:
            db.insert_bulk("system.ai_logs", buffer)
            buffer.clear()

    if buffer:
        db.insert_bulk("system.ai_logs", buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.ai_logs") or 0
    logger.info(f"Generated {count:,} ai_logs entries")
    return count


def _generate_moderation_logs(db: DatabaseConnection) -> int:
    logger.info("Generating moderation logs (AI + admin override)...")

    rows = db.fetch_all(
        "SELECT entity_type, entity_id, ai_verdict, scores, processed_at "
        "FROM system.moderation_results "
        "WHERE entity_type IN ('review', 'photo')"
    )

    if not rows:
        logger.warning("No moderation_results found, skipping moderation_logs")
        return 0

    admin_ids = [r[0] for r in db.fetch_all(
        "SELECT user_id FROM users WHERE role = 'admin' AND is_deleted = false"
    )]
    if not admin_ids:
        admin_ids = [r[0] for r in db.fetch_all(
            "SELECT user_id FROM users WHERE is_deleted = false LIMIT 1"
        )]

    ai_buffer = []
    admin_buffer = []
    for entity_type, entity_id, verdict, scores, processed_at in rows:
        log_verdict = VERDICT_LOG_MAP.get(verdict, "needs_review")
        created_at = ensure_naive(processed_at)

        ai_buffer.append({
            "entity_type": entity_type,
            "entity_id": entity_id,
            "actor": "ai",
            "verdict": log_verdict,
            "reason_codes": [],
            "admin_note": None,
            "processed_by": None,
            "ai_scores": Json(scores) if scores is not None else None,
            "created_at": created_at,
        })

        if verdict == "needs_review" and random.random() < 0.25 and admin_ids:
            admin_verdict = "approve" if random.random() < 0.6 else "reject"
            admin_note = random.choice(ADMIN_NOTE_TEMPLATES) if random.random() < 0.4 else None
            admin_buffer.append({
                "entity_type": entity_type,
                "entity_id": entity_id,
                "actor": "admin",
                "verdict": admin_verdict,
                "reason_codes": [],
                "admin_note": admin_note,
                "processed_by": random.choice(admin_ids),
                "ai_scores": None,
                "created_at": created_at + timedelta(minutes=random.randint(1, 30)),
            })

        if len(ai_buffer) >= 5000:
            db.insert_bulk("system.moderation_logs", ai_buffer)
            ai_buffer.clear()
        if len(admin_buffer) >= 5000:
            db.insert_bulk("system.moderation_logs", admin_buffer)
            admin_buffer.clear()

    if ai_buffer:
        db.insert_bulk("system.moderation_logs", ai_buffer)
    if admin_buffer:
        db.insert_bulk("system.moderation_logs", admin_buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.moderation_logs") or 0
    logger.info(f"Generated {count:,} moderation_logs entries")
    return count


def _generate_email_logs(db: DatabaseConnection) -> int:
    logger.info("Generating email logs...")

    users = db.fetch_all(
        "SELECT user_id, email, is2fa_enabled, created_at "
        "FROM users WHERE is_deleted = false"
    )
    if not users:
        logger.warning("No users found, skipping email_logs")
        return 0

    now_naive = datetime.utcnow().replace(microsecond=0)
    buffer = []

    for _user_id, email, is_2fa_enabled, user_created_at in users:
        recipient = email.lower()
        verification_at = ensure_naive(user_created_at) + timedelta(minutes=random.randint(1, 5))
        buffer.append({
            "type": EMAIL_TYPE_VERIFICATION,
            "recipient": recipient,
            "subject": EMAIL_SUBJECTS[EMAIL_TYPE_VERIFICATION],
            "status": "sent",
            "provider": None,
            "provider_message_id": None,
            "error_message": None,
            "created_at": verification_at,
            "sent_at": verification_at + timedelta(seconds=10),
        })

        if is_2fa_enabled:
            for _ in range(random.randint(2, 4)):
                ts = now_naive - timedelta(days=random.randint(0, 30), minutes=random.randint(0, 1440))
                buffer.append({
                    "type": EMAIL_TYPE_TWO_FACTOR,
                    "recipient": recipient,
                    "subject": EMAIL_SUBJECTS[EMAIL_TYPE_TWO_FACTOR],
                    "status": "sent",
                    "provider": None,
                    "provider_message_id": None,
                    "error_message": None,
                    "created_at": ts,
                    "sent_at": ts + timedelta(seconds=10),
                })

        if random.random() < 0.05:
            ts = now_naive - timedelta(days=random.randint(0, 90), minutes=random.randint(0, 1440))
            buffer.append({
                "type": EMAIL_TYPE_PASSWORD_RESET,
                "recipient": recipient,
                "subject": EMAIL_SUBJECTS[EMAIL_TYPE_PASSWORD_RESET],
                "status": "sent",
                "provider": None,
                "provider_message_id": None,
                "error_message": None,
                "created_at": ts,
                "sent_at": ts + timedelta(seconds=10),
            })

        if len(buffer) >= 5000:
            db.insert_bulk("system.email_logs", buffer)
            buffer.clear()

    if buffer:
        db.insert_bulk("system.email_logs", buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.email_logs") or 0
    logger.info(f"Generated {count:,} email_logs entries")
    return count


def _generate_security_logs(db: DatabaseConnection) -> int:
    logger.info("Generating security logs...")

    users = db.fetch_all(
        "SELECT user_id, email, is2fa_enabled FROM users WHERE is_deleted = false"
    )
    if not users:
        logger.warning("No users found, skipping security_logs")
        return 0

    banned_ips = [r[0] for r in db.fetch_all(
        "SELECT value FROM system.banned_identifiers WHERE type = 'ip'"
    )]

    now_naive = datetime.utcnow().replace(microsecond=0)
    buffer = []

    for user_id, email, is_2fa_enabled in users:
        recipient = email.lower()

        if random.random() < 0.10:
            for _ in range(random.randint(1, 2)):
                ts = now_naive - timedelta(days=random.randint(0, 90), minutes=random.randint(0, 1440))
                reason = random.choice(["wrong_password", "account_locked"])
                buffer.append({
                    "event_type": "failed_login",
                    "ip_address": fake.ipv4_public(),
                    "user_agent": fake.user_agent(),
                    "email": recipient,
                    "user_id": None,
                    "details": json.dumps({"reason": reason}),
                    "country_code": None,
                    "city": None,
                    "created_at": ts,
                })

        if is_2fa_enabled:
            ts = now_naive - timedelta(days=random.randint(0, 60), minutes=random.randint(0, 1440))
            buffer.append({
                "event_type": "two_factor_enabled",
                "ip_address": fake.ipv4_public(),
                "user_agent": fake.user_agent(),
                "email": recipient,
                "user_id": user_id,
                "details": None,
                "country_code": None,
                "city": None,
                "created_at": ts,
            })

        if random.random() < 0.10:
            ts = now_naive - timedelta(days=random.randint(0, 90), minutes=random.randint(0, 1440))
            buffer.append({
                "event_type": "password_changed",
                "ip_address": fake.ipv4_public(),
                "user_agent": fake.user_agent(),
                "email": recipient,
                "user_id": user_id,
                "details": None,
                "country_code": None,
                "city": None,
                "created_at": ts,
            })

        if len(buffer) >= 5000:
            db.insert_bulk("system.security_logs", buffer)
            buffer.clear()

    for _ in range(random.randint(5, 10)):
        ts = now_naive - timedelta(days=random.randint(0, 90), minutes=random.randint(0, 1440))
        buffer.append({
            "event_type": "banned_registration",
            "ip_address": fake.ipv4_public(),
            "user_agent": fake.user_agent(),
            "email": fake.email().lower(),
            "user_id": None,
            "details": json.dumps({"reason": "banned_identifier"}),
            "country_code": None,
            "city": None,
            "created_at": ts,
        })

    if banned_ips:
        for _ in range(random.randint(3, 5)):
            ts = now_naive - timedelta(days=random.randint(0, 90), minutes=random.randint(0, 1440))
            buffer.append({
                "event_type": "blocked_ip",
                "ip_address": random.choice(banned_ips),
                "user_agent": fake.user_agent(),
                "email": fake.email().lower(),
                "user_id": None,
                "details": json.dumps({"reason": "banned_identifier"}),
                "country_code": None,
                "city": None,
                "created_at": ts,
            })

    if buffer:
        db.insert_bulk("system.security_logs", buffer)

    count = db.fetch_val("SELECT COUNT(*) FROM system.security_logs") or 0
    logger.info(f"Generated {count:,} security_logs entries")
    return count


def _hash_refresh_token() -> str:
    raw = uuid.uuid4().hex
    return base64.b64encode(hashlib.sha256(raw.encode()).digest()).decode()


def _generate_user_sessions(db: DatabaseConnection) -> int:
    logger.info("Generating user sessions...")

    users = db.fetch_all(
        "SELECT user_id, last_login_at, created_at FROM users "
        "WHERE is_deleted = false AND (last_login_at IS NOT NULL OR created_at IS NOT NULL)"
    )
    if not users:
        logger.warning("No users with login activity, skipping user_sessions")
        return 0

    now_naive = datetime.utcnow().replace(microsecond=0)
    buffer = []

    for user_id, last_login_at, created_at in users:
        anchor = ensure_naive(last_login_at) if last_login_at else ensure_naive(created_at)
        if anchor is None:
            continue

        active_session_at = anchor - timedelta(days=random.randint(0, 7))
        is_remember_me = random.random() < 0.20
        ttl_days = 30 if is_remember_me else 7
        buffer.append({
            "user_id": user_id,
            "refresh_token_hash": _hash_refresh_token(),
            "device_name": None,
            "ip_address": None,
            "last_active_at": None,
            "expires_at": active_session_at + timedelta(days=ttl_days),
            "is_revoked": False,
            "is_remember_me": is_remember_me,
            "created_at": active_session_at,
        })

        history_count = random.choices([0, 1, 2], weights=[0.4, 0.4, 0.2])[0]
        for _ in range(history_count):
            old_at = anchor - timedelta(days=random.randint(8, 120))
            old_remember = random.random() < 0.20
            old_ttl = 30 if old_remember else 7
            old_expires = old_at + timedelta(days=old_ttl)
            if old_expires > now_naive:
                old_expires = now_naive - timedelta(days=1)
            buffer.append({
                "user_id": user_id,
                "refresh_token_hash": _hash_refresh_token(),
                "device_name": None,
                "ip_address": None,
                "last_active_at": None,
                "expires_at": old_expires,
                "is_revoked": True,
                "is_remember_me": old_remember,
                "created_at": old_at,
            })

        if len(buffer) >= 5000:
            db.insert_bulk("user_sessions", buffer)
            buffer.clear()

    if buffer:
        db.insert_bulk("user_sessions", buffer)

    total = db.fetch_val("SELECT COUNT(*) FROM user_sessions") or 0
    distinct_hashes = db.fetch_val("SELECT COUNT(DISTINCT refresh_token_hash) FROM user_sessions") or 0
    if total != distinct_hashes:
        raise RuntimeError(
            f"Refresh token hash collision detected: total={total} distinct={distinct_hashes}"
        )
    logger.info(f"Generated {total:,} user_sessions entries")
    return total


def _generate_system_nodes(db: DatabaseConnection) -> int:
    logger.info("Generating system nodes...")

    now_naive = datetime.utcnow().replace(microsecond=0)
    nodes = [
        {
            "node_id": "rpi-gateway",
            "ip_address": "100.64.0.10",
            "mac_address": None,
            "wol_gateway_id": None,
            "role": "gateway",
            "status": "online",
            "node_type": "orchestrator",
            "hostname": "raspberry-pi",
            "gpu_name": None,
            "gpu_memory_total": None,
            "gpu_memory_used": None,
            "current_job_id": None,
            "metadata": json.dumps({"role_detail": "wol-gateway and uptime-kuma host"}),
            "last_heartbeat": now_naive - timedelta(minutes=1),
        },
        {
            "node_id": "gpu-worker",
            "ip_address": "100.64.0.20",
            "mac_address": "AA:BB:CC:DD:EE:FF",
            "wol_gateway_id": "rpi-gateway",
            "role": "worker",
            "status": "offline",
            "node_type": "gpu",
            "hostname": "gpu-worker",
            "gpu_name": "NVIDIA GeForce RTX 3060 Ti",
            "gpu_memory_total": 8192,
            "gpu_memory_used": 0,
            "current_job_id": None,
            "metadata": json.dumps({"cuda_version": "12.4", "driver": "550.x"}),
            "last_heartbeat": now_naive - timedelta(hours=12),
        },
        {
            "node_id": "vps-hetzner-prod",
            "ip_address": "100.64.0.5",
            "mac_address": None,
            "wol_gateway_id": None,
            "role": None,
            "status": "online",
            "node_type": "api",
            "hostname": "hetznerVPS",
            "gpu_name": None,
            "gpu_memory_total": None,
            "gpu_memory_used": None,
            "current_job_id": None,
            "metadata": json.dumps({"location": "Hetzner Falkenstein", "tier": "production"}),
            "last_heartbeat": now_naive - timedelta(minutes=1),
        },
    ]

    for node in nodes:
        cols = ", ".join(node.keys())
        placeholders = ", ".join(["%s"] * len(node))
        sql = f"INSERT INTO system.nodes ({cols}) VALUES ({placeholders})"
        db.execute_query(sql, tuple(node.values()))
    db.commit()

    count = db.fetch_val("SELECT COUNT(*) FROM system.nodes") or 0
    logger.info(f"Generated {count:,} system nodes")
    return count


def _generate_audit_logs(db: DatabaseConnection) -> int:
    logger.info("Generating audit log starter entries...")

    admin_row = db.fetch_one("SELECT user_id FROM users WHERE role = 'admin' AND is_deleted = false LIMIT 1")
    admin_id = admin_row[0] if admin_row else 1
    admin_str = str(admin_id)

    base = datetime(2024, 6, 1, 9, 0, 0)
    entries = [
        ("rejection_reasons", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (12 reasons)"})),
        ("dish_categories", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (18 categories)"})),
        ("forbidden_words", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (forbidden_words.json)"})),
        ("system_configs", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (blueprints/system_config.json)"})),
        ("cuisine_types", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (31 polish cuisines)"})),
        ("report_reason_definitions", 1, "INSERT", "system",
         None, json.dumps({"action": "initial seed (phase6 report reasons)"})),
        ("users", admin_id, "UPDATE", admin_str,
         json.dumps({"role": "user"}), json.dumps({"role": "admin"})),
        ("forbidden_words", 5, "UPDATE", admin_str,
         json.dumps({"isActive": False}), json.dumps({"isActive": True})),
        ("banned_identifiers", 1, "INSERT", admin_str,
         None, json.dumps({"value": "spam@example.com", "reason": "manual ban"})),
        ("system_configs", 2, "UPDATE", admin_str,
         json.dumps({"value": "5"}), json.dumps({"value": "10"})),
    ]

    rows = []
    for i, (table_name, record_id, operation, changed_by, old_values, new_values) in enumerate(entries):
        rows.append({
            "table_name": table_name,
            "record_id": record_id,
            "operation": operation,
            "changed_by": changed_by,
            "changed_at": base + timedelta(days=i * 3, hours=random.randint(0, 8)),
            "old_values": old_values,
            "new_values": new_values,
        })

    db.insert_bulk("audit_logs", rows)

    count = db.fetch_val("SELECT COUNT(*) FROM audit_logs") or 0
    logger.info(f"Generated {count:,} audit_logs entries")
    return count


class SystemLogsPhase(BasePhase):

    def __init__(self, blueprints_dir: str = "blueprints"):
        self.blueprints_dir = blueprints_dir

    @property
    def metadata(self) -> PhaseMetadata:
        return PhaseMetadata(
            phase_id="phase8_logs",
            display_name="System Logs and Sessions",
            dependencies=["phase4_users", "phase5_reviews", "phase6_social"],
            required_tables=[
                "system.ai_logs",
                "system.moderation_logs",
                "system.email_logs",
                "system.security_logs",
                "system.nodes",
                "audit_logs",
                "user_sessions",
            ],
            cleanup_tables=[
                "system.ai_logs",
                "system.moderation_logs",
                "system.email_logs",
                "system.security_logs",
                "system.nodes",
                "audit_logs",
                "user_sessions",
            ],
            estimated_duration=180,
        )

    def execute(self, context: ExecutionContext) -> PhaseResult:
        start_time = time.time()
        logger.info("=" * 60)
        logger.info("PHASE 8: System Logs and Sessions")
        logger.info("=" * 60)

        try:
            counts: dict[str, int] = {}
            counts["ai_logs"] = _generate_ai_logs(context.db)
            counts["moderation_logs"] = _generate_moderation_logs(context.db)
            counts["email_logs"] = _generate_email_logs(context.db)
            counts["security_logs"] = _generate_security_logs(context.db)
            counts["user_sessions"] = _generate_user_sessions(context.db)
            counts["system_nodes"] = _generate_system_nodes(context.db)
            counts["audit_logs"] = _generate_audit_logs(context.db)

            duration = time.time() - start_time
            logger.info(f"Phase 8 completed in {duration:.2f}s")
            logger.info(f"Generated: {counts}")

            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.COMPLETED,
                duration_seconds=duration,
                entities_generated=counts,
                error=None,
            )

        except Exception as e:
            duration = time.time() - start_time
            logger.error(f"Phase 8 FAILED after {duration:.2f}s: {e}", exc_info=True)
            return PhaseResult(
                phase_id=self.metadata.phase_id,
                status=PhaseStatus.FAILED,
                duration_seconds=duration,
                entities_generated={},
                error=e,
            )
