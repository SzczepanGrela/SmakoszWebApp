import logging
import threading
import time

import torch

from api.client import WorkerApiClient
from config import Settings

logger = logging.getLogger(__name__)

def get_gpu_info(settings: Settings) -> dict:
    cuda_available = torch.cuda.is_available()
    info: dict = {
        "nodeId": settings.worker_id,
    }
    if cuda_available:
        info["gpuName"] = torch.cuda.get_device_name(0)
        mem = torch.cuda.mem_get_info(0)
        info["gpuMemoryTotal"] = mem[1] // (1024 * 1024)
        info["gpuMemoryUsed"] = (mem[1] - mem[0]) // (1024 * 1024)
    return info

class HeartbeatThread(threading.Thread):
    daemon = True

    def __init__(self, api_client: WorkerApiClient, settings: Settings):
        super().__init__(name="heartbeat")
        self.api_client = api_client
        self.settings = settings

    def run(self) -> None:
        logger.info("Heartbeat thread started (interval=%ds)", self.settings.heartbeat_interval)
        while True:
            try:
                gpu_info = get_gpu_info(self.settings)
                self.api_client.send_heartbeat(gpu_info)
            except Exception:
                logger.exception("Heartbeat error")
            time.sleep(self.settings.heartbeat_interval)
