#!/bin/bash
set -e

DEPLOY_LOCKFILE=/tmp/smakosz-rbpi-deploy.lock
trap 'rm -f "$DEPLOY_LOCKFILE"' EXIT
echo "$$" > "$DEPLOY_LOCKFILE"

cd /home/smakosz
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/tools/rbpi-gateway/docker-compose.yml -o docker-compose.yml

docker compose pull
docker compose up -d --remove-orphans
docker image prune -f

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

check_health "RBPI Gateway" "rbpi-gateway"
