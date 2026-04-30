#!/bin/bash
set -e

DEPLOY_LOCKFILE=/tmp/smakosz-deploy.lock
trap 'rm -f "$DEPLOY_LOCKFILE"' EXIT
echo "$$" > "$DEPLOY_LOCKFILE"

bash /opt/smakosz-backup/silence-alerts.sh 5m "deploy" || true

cd /home/smakosz
curl -sSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/docker-compose.yml -o docker-compose.yml

mkdir -p infra/prometheus infra/grafana/provisioning/datasources infra/grafana/provisioning/dashboards infra/grafana/provisioning/alerting infra/grafana/dashboards infra/grafana/email-templates
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/prometheus/prometheus.yml -o infra/prometheus/prometheus.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/provisioning/datasources/prometheus.yml -o infra/grafana/provisioning/datasources/prometheus.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/provisioning/dashboards/dashboards.yml -o infra/grafana/provisioning/dashboards/dashboards.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/dashboards/aspnetcore.json -o infra/grafana/dashboards/aspnetcore.json
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/dashboards/business.json -o infra/grafana/dashboards/business.json
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/dashboards/node.json -o infra/grafana/dashboards/node.json
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/provisioning/alerting/contact-points.yml -o infra/grafana/provisioning/alerting/contact-points.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/provisioning/alerting/policies.yml -o infra/grafana/provisioning/alerting/policies.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/provisioning/alerting/rules.yml -o infra/grafana/provisioning/alerting/rules.yml
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/grafana/email-templates/ng_alert_notification.html -o infra/grafana/email-templates/ng_alert_notification.html

docker compose pull
docker compose up -d
docker restart nginx-proxy-manager || true
docker image prune -f

BACKUP_SCRIPTS_DIR=/opt/smakosz-backup
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/scripts/backup-db.sh -o "$BACKUP_SCRIPTS_DIR/backup-db.sh"
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/scripts/restore-db.sh -o "$BACKUP_SCRIPTS_DIR/restore-db.sh"
curl -fsSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/main/infra/scripts/silence-alerts.sh -o "$BACKUP_SCRIPTS_DIR/silence-alerts.sh"
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
