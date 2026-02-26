import os

from dotenv import load_dotenv

load_dotenv()

API_TOKEN: str = os.environ.get("RPI_GATEWAY_API_TOKEN", "")
PORT: int = int(os.environ.get("RPI_GATEWAY_PORT", "5000"))
GPU_WORKER_MAC: str = os.environ.get("RPI_GATEWAY_GPU_WORKER_MAC", "")
