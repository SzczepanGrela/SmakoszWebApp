import logging
import sys
import threading
import time
from functools import wraps

import requests as http_requests
from flask import Flask, jsonify, request
from wakeonlan import send_magic_packet

import config

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    stream=sys.stdout,
)
logger = logging.getLogger("rbpi-gateway")

class _SkipHealthAccessLog(logging.Filter):
    def filter(self, record):
        return "GET /health" not in record.getMessage()

logging.getLogger("werkzeug").addFilter(_SkipHealthAccessLog())

app = Flask(__name__)

def require_token(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        token = request.headers.get("X-API-Token")
        if not token or token != config.API_TOKEN:
            return jsonify({"error": "Unauthorized"}), 401
        return f(*args, **kwargs)
    return decorated

@app.route("/health")
def health():
    return jsonify({"status": "ok", "service": "rbpi-gateway"})

def _start_gpu_worker_after_boot():
    if not config.HOMELAB_API_URL:
        logger.warning("HOMELAB_API_URL not configured, skipping container start")
        return

    headers: dict[str, str | bytes] = {}
    if config.HOMELAB_API_TOKEN:
        headers["X-API-Token"] = config.HOMELAB_API_TOKEN

    for i in range(18):  # 18 × 10s = 3 minutes max
        time.sleep(10)
        try:
            resp = http_requests.post(
                f"{config.HOMELAB_API_URL}/api/gpu-worker/start",
                headers=headers,
                timeout=5,
            )
            if resp.ok:
                logger.info("gpu-worker container started via homelab-api")
                return
        except Exception:
            logger.debug("homelab-api not reachable yet (attempt %d/18)", i + 1)

    logger.warning("Failed to start gpu-worker container after 3 minutes")

@app.route("/wake", methods=["POST"])
@require_token
def wake_gpu_worker():
    if not config.GPU_WORKER_MAC:
        return jsonify({"error": "GPU_WORKER_MAC not configured"}), 500
    try:
        logger.info("Sending WoL magic packet to %s", config.GPU_WORKER_MAC)
        send_magic_packet(config.GPU_WORKER_MAC)

        threading.Thread(target=_start_gpu_worker_after_boot, daemon=True).start()

        return jsonify({"status": "sent", "mac": config.GPU_WORKER_MAC})
    except Exception as e:
        logger.error("Failed to send WoL packet: %s", e)
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    config.validate()
    logger.info("Starting rbpi-gateway (port=%d)", config.PORT)
    app.run(host="0.0.0.0", port=config.PORT)
