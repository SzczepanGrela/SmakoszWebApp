import logging
import sys
from functools import wraps

from flask import Flask, jsonify, request
from wakeonlan import send_magic_packet

import config

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    stream=sys.stdout,
)
logger = logging.getLogger("rpi-gateway")

app = Flask(__name__)

def require_token(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        token = request.headers.get("X-API-Token") or request.args.get("token")
        if not token or token != config.API_TOKEN:
            return jsonify({"error": "Unauthorized"}), 401
        return f(*args, **kwargs)
    return decorated

@app.route("/health")
def health():
    return jsonify({"status": "ok", "service": "rpi-gateway"})

@app.route("/wake", methods=["POST"])
@require_token
def wake_gpu_worker():
    if not config.GPU_WORKER_MAC:
        return jsonify({"error": "GPU_WORKER_MAC not configured"}), 500
    try:
        logger.info("Sending WoL magic packet to %s", config.GPU_WORKER_MAC)
        send_magic_packet(config.GPU_WORKER_MAC)
        return jsonify({"status": "sent", "mac": config.GPU_WORKER_MAC})
    except Exception as e:
        logger.error("Failed to send WoL packet: %s", e)
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    logger.info("Starting rpi-gateway (port=%d)", config.PORT)
    app.run(host="0.0.0.0", port=config.PORT)
