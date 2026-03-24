import os

from dotenv import load_dotenv

load_dotenv()

API_TOKEN: str = os.environ.get("HOMELAB_API_TOKEN", "")
PORT: int = int(os.environ.get("HOMELAB_PORT", "5001"))
LOCKFILE: str = os.environ.get("HOMELAB_LOCKFILE", "/tmp/shutdown.lock")
DOCKER_CS2: str = os.environ.get("HOMELAB_DOCKER_CS2", "cs2-server")
PROC_AI: str = os.environ.get("HOMELAB_PROC_AI", "python.*main.py")
