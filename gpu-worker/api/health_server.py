import platform
import sys
import threading
import time

import torch
from fastapi import FastAPI

from config import Settings

app = FastAPI(title="GPU Worker Health")

_lock = threading.Lock()
_start_time = time.monotonic()
_models_loaded: list[str] = []
_current_phase: str = "idle"

def set_models_loaded(models: list[str]) -> None:
    global _models_loaded
    with _lock:
        _models_loaded = models

def set_current_phase(phase: str) -> None:
    global _current_phase
    with _lock:
        _current_phase = phase

@app.get("/health")
def health():
    with _lock:
        models_loaded = list(_models_loaded)
        current_phase = _current_phase

    cuda_available = torch.cuda.is_available()

    gpu_name = None
    gpu_memory_total = None
    gpu_memory_used = None
    cuda_version = None

    if cuda_available:
        gpu_name = torch.cuda.get_device_name(0)
        mem = torch.cuda.mem_get_info(0)
        gpu_memory_total = mem[1] // (1024 * 1024)
        gpu_memory_used = (mem[1] - mem[0]) // (1024 * 1024)
        cuda_version = torch.version.cuda

    status = "online" if models_loaded else "degraded"

    return {
        "status": status,
        "current_phase": current_phase,
        "gpu_available": cuda_available,
        "gpu_name": gpu_name,
        "gpu_memory_total": gpu_memory_total,
        "gpu_memory_used": gpu_memory_used,
        "models_loaded": models_loaded,
        "uptime_seconds": round(time.monotonic() - _start_time, 1),
        "python_version": platform.python_version(),
        "pytorch_version": torch.__version__,
        "cuda_version": cuda_version,
        "platform": sys.platform,
    }

def start_health_server(settings: Settings, loaded_models: list[str]) -> None:
    import uvicorn

    set_models_loaded(loaded_models)

    thread = threading.Thread(
        target=uvicorn.run,
        kwargs={
            "app": app,
            "host": "0.0.0.0",
            "port": settings.health_port,
            "log_level": "warning",
        },
        daemon=True,
    )
    thread.start()
