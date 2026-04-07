#!/bin/bash
set -euo pipefail
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

CACHE_FILE="/var/lib/cloudflare-ips.cache"
TEMP_FILE=$(mktemp)
URL_V4="https://www.cloudflare.com/ips-v4"
URL_V6="https://www.cloudflare.com/ips-v6"
TARGET_PORT="443"

NPM_DATA_DIR="/opt/npm-proxy/data"
NPM_ALLOW_FILE="$NPM_DATA_DIR/cloudflare_allow.conf"
NPM_REALIP_FILE="$NPM_DATA_DIR/cloudflare_realip.conf"
NPM_CONTAINER_NAME="nginx-proxy-manager"
HOME_IP="31.128.13.231"

cleanup() { rm -f "$TEMP_FILE"; }
trap cleanup EXIT

for cmd in curl ufw cmp docker; do
    if ! command -v "$cmd" &> /dev/null; then
        echo "Missing required command: $cmd" >&2
        exit 1
    fi
done

echo "Fetching Cloudflare IP ranges..."
curl -fs "$URL_V4" >> "$TEMP_FILE" || { echo "Failed to fetch IPv4 list" >&2; exit 1; }
echo "" >> "$TEMP_FILE"
curl -fs "$URL_V6" >> "$TEMP_FILE" || { echo "Failed to fetch IPv6 list" >&2; exit 1; }

SORTED_TEMP=$(mktemp)
grep -v '^\s*$' "$TEMP_FILE" | sort | uniq > "$SORTED_TEMP"
mv "$SORTED_TEMP" "$TEMP_FILE"

if [ ! -s "$TEMP_FILE" ]; then
    echo "Fetched IP list is empty" >&2
    exit 1
fi

if [ -f "$CACHE_FILE" ] && cmp -s "$TEMP_FILE" "$CACHE_FILE"; then
    exit 0
fi

echo "Change detected - updating..."

# --- UFW ---

if [ -f "$CACHE_FILE" ]; then
    while IFS= read -r ip; do
        [[ -z "$ip" ]] && continue
        ufw delete allow proto tcp from "$ip" to any port "$TARGET_PORT" > /dev/null 2>&1 || true
    done < "$CACHE_FILE"
fi

while IFS= read -r ip; do
    [[ -z "$ip" ]] && continue
    ufw allow proto tcp from "$ip" to any port "$TARGET_PORT" comment 'Cloudflare Only' > /dev/null
done < "$TEMP_FILE"

# --- NPM ---

if [ -d "$NPM_DATA_DIR" ]; then

    {
        echo "# Auto-generated $(date)"
        echo ""
        while IFS= read -r ip; do
            [[ -z "$ip" ]] && continue
            echo "allow $ip;"
        done < "$TEMP_FILE"
        echo ""
        echo "allow $HOME_IP;"
        echo ""
        echo "deny all;"
    } > "$NPM_ALLOW_FILE"

    {
        echo "# Auto-generated $(date)"
        echo ""
        while IFS= read -r ip; do
            [[ -z "$ip" ]] && continue
            echo "set_real_ip_from $ip;"
        done < "$TEMP_FILE"
        echo ""
        echo "real_ip_header CF-Connecting-IP;"
    } > "$NPM_REALIP_FILE"

    if docker ps --format '{{.Names}}' | grep -q "^${NPM_CONTAINER_NAME}$"; then
        docker exec "$NPM_CONTAINER_NAME" nginx -s reload
    else
        echo "Container $NPM_CONTAINER_NAME is not running - skipping reload" >&2
    fi

else
    echo "Directory $NPM_DATA_DIR does not exist - skipping NPM" >&2
fi

# --- Finalize ---

mv "$TEMP_FILE" "$CACHE_FILE"
chmod 644 "$CACHE_FILE"
ufw reload > /dev/null

echo "OK: UFW + NPM updated"
