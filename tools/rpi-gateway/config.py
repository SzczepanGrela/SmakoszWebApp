import os
import sys

from dotenv import load_dotenv

load_dotenv()

API_TOKEN: str = os.environ.get("RPI_GATEWAY_API_TOKEN", "")
PORT: int = int(os.environ.get("RPI_GATEWAY_PORT", "5000"))
GPU_WORKER_MAC: str = os.environ.get("RPI_GATEWAY_GPU_WORKER_MAC", "")
HOMELAB_API_URL: str = os.environ.get("RPI_GATEWAY_HOMELAB_API_URL", "")
HOMELAB_API_TOKEN: str = os.environ.get("RPI_GATEWAY_HOMELAB_API_TOKEN", "")

def validate():
    errors = []
    if not API_TOKEN:
        errors.append("RPI_GATEWAY_API_TOKEN is required")
    if not GPU_WORKER_MAC:
        errors.append("RPI_GATEWAY_GPU_WORKER_MAC is required")
    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)
