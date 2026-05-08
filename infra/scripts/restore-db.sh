#!/bin/bash
set -euo pipefail

if [ $# -lt 1 ]; then
    echo "Usage: restore-db.sh <dump-file> [target-database]"
    exit 1
fi

CONFIG_FILE="${CONFIG_FILE:-/etc/smakosz-backup/smakosz-backup.env}"
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
