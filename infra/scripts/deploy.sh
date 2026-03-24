#!/bin/bash
set -e
cd /home/smakosz
curl -sSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/master/docker-compose.yml -o docker-compose.yml
docker compose pull
docker compose up -d
docker image prune -f

check_health() {
  local name=$1 url=$2
  for i in $(seq 1 15); do
    status=$(curl -sf -o /dev/null -w '%{http_code}' "$url" 2>/dev/null) || true
    [ "$status" = "200" ] && echo "$name is healthy" && return 0
    echo "Attempt $i: waiting for $name..."
    sleep 5
  done
  echo "ERROR: $name health check failed (HTTP $status)"
  return 1
}

check_health "API" "http://localhost:5000/health"
check_health "Orchestrator" "http://localhost:8081/health"
check_health "Client" "http://localhost:5003/"
