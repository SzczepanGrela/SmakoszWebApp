#!/bin/bash
set -uo pipefail

CONFIG_FILE="${MONITORING_CONFIG:-/etc/smakosz-monitoring/smakosz-monitoring.env}"
if [ ! -f "$CONFIG_FILE" ]; then
    echo "silence-alerts: missing $CONFIG_FILE, skipping silence" >&2
    exit 0
fi

# shellcheck disable=SC1090
source "$CONFIG_FILE"

DURATION="${1:-5m}"
REASON="${2:-auto-silence}"
CALLER="${0##*/}"

NOW=$(date -u +%Y-%m-%dT%H:%M:%S.000Z)
case "$DURATION" in
    *m) MINUTES="${DURATION%m}" ;;
    *) echo "silence-alerts: invalid duration '$DURATION' (expected Nm)" >&2; exit 0 ;;
esac
END=$(date -u -d "+${MINUTES} minutes" +%Y-%m-%dT%H:%M:%S.000Z)

PAYLOAD=$(cat <<EOF
{
  "matchers": [
    { "name": "alertname", "value": "NodeDisk.*|NodeRAM.*|NodeCPU.*|NodeLoad.*|NodeSwap.*", "isRegex": true, "isEqual": true }
  ],
  "startsAt": "$NOW",
  "endsAt": "$END",
  "createdBy": "$CALLER",
  "comment": "$REASON"
}
EOF
)

RESPONSE=$(curl -sS -m 10 -w "\n%{http_code}" \
    -X POST \
    -H "Authorization: Bearer $GRAFANA_API_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$PAYLOAD" \
    "$GRAFANA_URL/api/alertmanager/grafana/api/v2/silences" 2>&1) || {
    echo "silence-alerts: curl failed, alerts may fire" >&2
    exit 0
}

HTTP_CODE=$(echo "$RESPONSE" | tail -1)
BODY=$(echo "$RESPONSE" | head -n -1)

if [ "$HTTP_CODE" != "200" ] && [ "$HTTP_CODE" != "202" ]; then
    echo "silence-alerts: HTTP $HTTP_CODE, alerts may fire. Body: $BODY" >&2
    exit 0
fi

echo "silence-alerts: silenced for $DURATION (caller: $CALLER, reason: $REASON)"
