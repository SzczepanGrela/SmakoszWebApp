import os
import sys

from dotenv import load_dotenv

load_dotenv()

API_TOKEN: str = os.environ.get("HOMELAB_API_TOKEN", "")
PORT: int = int(os.environ.get("HOMELAB_PORT", "5001"))
LOCKFILE: str = os.environ.get("HOMELAB_LOCKFILE", "/tmp/shutdown.lock")
DOCKER_BLOCKERS: str = os.environ.get("HOMELAB_DOCKER_BLOCKERS", "gpu-worker")
PROCESS_BLOCKERS: str = os.environ.get("HOMELAB_PROCESS_BLOCKERS", "")
GPU_WORKER_COMPOSE_DIR: str = os.environ.get("HOMELAB_GPU_WORKER_DIR", "/home/smakosz-gpu-worker")

def validate():
    if not API_TOKEN:
        print("ERROR: HOMELAB_API_TOKEN is required", file=sys.stderr)
        sys.exit(1)
