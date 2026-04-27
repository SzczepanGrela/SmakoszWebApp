#!/bin/bash
set -euo pipefail

if [ $# -lt 1 ]; then
    cat <<'USAGE'
Usage: restore-db.sh <dump-file> [target-database]

Restores a pg_dump custom-format file into the running postgres container.

Arguments:
  dump-file        Path to a .dump file produced by pg_dump -Fc
  target-database  Optional. Defaults to POSTGRES_DB from the config.
                   Pass a different name to restore into a test database.

Examples:
  restore-db.sh /var/backups/smakosz/daily-2026-04-23.dump
  restore-db.sh ./weekly-2026-W16.dump smakosz_db_restore

Prerequisites:
  /etc/smakosz-backup.env must define POSTGRES_CONTAINER, POSTGRES_USER,
  POSTGRES_DB, POSTGRES_PASSWORD.

Procedure:
  1. Validates dump file exists
  2. Stops api, orchestrator and client containers to prevent writes
  3. Terminates existing connections to target database
  4. Runs pg_restore --clean --if-exists into target database
  5. Restarts api, orchestrator and client containers

Restore from R2:
  export AWS_ACCESS_KEY_ID=<R2_ACCESS_KEY>
  export AWS_SECRET_ACCESS_KEY=<R2_SECRET_KEY>
  aws s3 cp s3://smakosz-backups/weekly/weekly-2026-W16.dump /tmp/ \
      --endpoint-url https://<account-id>.r2.cloudflarestorage.com
  sudo ./restore-db.sh /tmp/weekly-2026-W16.dump
USAGE
    exit 1
fi

CONFIG_FILE="${CONFIG_FILE:-/etc/smakosz-backup.env}"
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Missing $CONFIG_FILE" >&2
    exit 2
fi

# shellcheck disable=SC1090
source "$CONFIG_FILE"

: "${POSTGRES_CONTAINER:?}"
: "${POSTGRES_USER:?}"
: "${POSTGRES_DB:?}"
: "${POSTGRES_PASSWORD:?}"

DUMP_FILE="$1"
TARGET_DB="${2:-$POSTGRES_DB}"

if [ ! -f "$DUMP_FILE" ]; then
    echo "File not found: $DUMP_FILE" >&2
    exit 2
fi

COMPOSE_DIR="${COMPOSE_DIR:-/home/smakosz}"

echo "Stopping application containers"
(
    cd "$COMPOSE_DIR"
    docker compose stop api orchestrator client
)

echo "Terminating existing connections to $TARGET_DB"
docker exec -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_CONTAINER" \
    psql -U "$POSTGRES_USER" -d postgres -c \
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$TARGET_DB' AND pid <> pg_backend_pid();"

echo "Running pg_restore into $TARGET_DB"
docker exec -i -e PGPASSWORD="$POSTGRES_PASSWORD" "$POSTGRES_CONTAINER" \
    pg_restore --clean --if-exists --no-owner --no-privileges \
    -U "$POSTGRES_USER" -d "$TARGET_DB" < "$DUMP_FILE"

echo "Restarting application containers"
(
    cd "$COMPOSE_DIR"
    docker compose start api orchestrator client
)

echo "Restore completed. Verify with: docker logs smakosz-api-1 --tail 50"
