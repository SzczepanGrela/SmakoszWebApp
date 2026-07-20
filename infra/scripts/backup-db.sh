#!/bin/bash
set -euo pipefail

CONFIG_FILE="${CONFIG_FILE:-/etc/smakosz-backup/smakosz-backup.env}"
if [ ! -f "$CONFIG_FILE" ]; then
    echo "Missing $CONFIG_FILE" >&2
    exit 2
fi

# shellcheck disable=SC1090
source "$CONFIG_FILE"

DEPLOY_LOCKFILE=/tmp/smakosz-deploy.lock
if [ -f "$DEPLOY_LOCKFILE" ]; then
    DEPLOY_PID=$(cat "$DEPLOY_LOCKFILE" 2>/dev/null || echo "")
    if [ -n "$DEPLOY_PID" ] && kill -0 "$DEPLOY_PID" 2>/dev/null; then
        echo "Deploy in progress (PID $DEPLOY_PID), skipping backup" >&2
        exit 0
    fi
    echo "Stale deploy lockfile found, removing" >&2
    rm -f "$DEPLOY_LOCKFILE"
fi

BACKUP_LOCKFILE=/var/run/smakosz-backup.lock
if [ -f "$BACKUP_LOCKFILE" ]; then
    BACKUP_PID=$(cat "$BACKUP_LOCKFILE" 2>/dev/null || echo "")
    if [ -n "$BACKUP_PID" ] && kill -0 "$BACKUP_PID" 2>/dev/null; then
        echo "Another backup already running (PID $BACKUP_PID)" >&2
        exit 1
    fi
    echo "Stale backup lockfile found, removing" >&2
    rm -f "$BACKUP_LOCKFILE"
fi

echo "$$" > "$BACKUP_LOCKFILE"
trap 'rm -f "$BACKUP_LOCKFILE"' EXIT

bash "$(dirname "$0")/silence-alerts.sh" 10m "backup" || true

: "${POSTGRES_CONTAINER:?}"
: "${POSTGRES_USER:?}"
: "${POSTGRES_DB:?}"
: "${POSTGRES_PASSWORD:?}"
: "${BACKUP_DIR:?}"
: "${R2_ENDPOINT:?}"
: "${R2_ACCESS_KEY:?}"
: "${R2_SECRET_KEY:?}"
: "${R2_BUCKET:?}"
: "${HEALTHCHECKS_URL:?}"

TODAY="$(date +%Y-%m-%d)"
DOW="$(date +%u)"
DOM="$(date +%d)"
WEEK_TAG="$(date +%Y-W%V)"
MONTH_TAG="$(date +%Y-%m)"

log() {
    printf '[%s] %s\n' "$(date -Iseconds)" "$*"
}

mkdir -p "$BACKUP_DIR"

DAILY_FILE="$BACKUP_DIR/daily-$TODAY.dump"

log "Starting pg_dump to $DAILY_FILE"
docker exec -e PGPASSWORD="$POSTGRES_PASSWORD" \
    "$POSTGRES_CONTAINER" \
    pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --compress=zstd:9 \
    > "$DAILY_FILE"

DUMP_SIZE=$(stat -c%s "$DAILY_FILE")
SIZE_MB=$(( DUMP_SIZE / 1024 / 1024 ))
log "Dump completed: ${SIZE_MB} MB"

find "$BACKUP_DIR" -maxdepth 1 -name 'daily-*.dump' -mtime +3 -delete

export AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY"
export AWS_SECRET_ACCESS_KEY="$R2_SECRET_KEY"

if [ "$DOW" = "7" ]; then
    WEEKLY_FILE="$BACKUP_DIR/weekly-$WEEK_TAG.dump"
    cp "$DAILY_FILE" "$WEEKLY_FILE"

    log "Uploading weekly to R2: $(basename "$WEEKLY_FILE")"
    aws s3 cp "$WEEKLY_FILE" "s3://$R2_BUCKET/weekly/$(basename "$WEEKLY_FILE")" \
        --endpoint-url "$R2_ENDPOINT"

    find "$BACKUP_DIR" -maxdepth 1 -name 'weekly-*.dump' -mtime +14 -delete

    if [ "$DOM" -le 07 ]; then
        MONTHLY_FILE="$BACKUP_DIR/monthly-$MONTH_TAG.dump"
        cp "$WEEKLY_FILE" "$MONTHLY_FILE"

        log "Uploading monthly to R2: $(basename "$MONTHLY_FILE")"
        aws s3 cp "$MONTHLY_FILE" "s3://$R2_BUCKET/monthly/$(basename "$MONTHLY_FILE")" \
            --endpoint-url "$R2_ENDPOINT"

        rm -f "$MONTHLY_FILE"
    fi
fi

log "Pinging healthchecks"
curl -fsS --retry 3 --max-time 10 -o /dev/null "$HEALTHCHECKS_URL"

log "Backup completed successfully"
