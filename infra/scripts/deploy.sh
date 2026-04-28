#!/bin/bash
set -e

DEPLOY_LOCKFILE=/tmp/smakosz-deploy.lock
trap 'rm -f "$DEPLOY_LOCKFILE"' EXIT
echo "$$" > "$DEPLOY_LOCKFILE"

cd /home/smakosz
curl -sSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/docker-compose.yml -o docker-compose.yml
docker compose pull
docker compose up -d
docker image prune -f

BACKUP_SCRIPTS_DIR=/opt/smakosz-backup
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/scripts/backup-db.sh -o "$BACKUP_SCRIPTS_DIR/backup-db.sh"
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/scripts/restore-db.sh -o "$BACKUP_SCRIPTS_DIR/restore-db.sh"
chmod 755 "$BACKUP_SCRIPTS_DIR"/*.sh

check_health() {
  local name=$1 container=$2
  for i in $(seq 1 15); do
    status=$(docker inspect --format='{{.State.Health.Status}}' "$container" 2>/dev/null || echo "missing")
    [ "$status" = "healthy" ] && echo "$name is healthy" && return 0
    echo "Attempt $i: waiting for $name (status: $status)..."
    sleep 5
  done
  echo "ERROR: $name health check failed (last status: $status)"
  return 1
}

check_health "API" "smakosz-api-1"
check_health "Orchestrator" "smakosz-orchestrator-1"
check_health "Client" "smakosz-client-1"
