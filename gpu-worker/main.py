import gc
import logging
import sys
import time

import torch

from api.client import WorkerApiClient, request_shutdown
from api.health_server import set_current_phase, set_models_loaded, start_health_server
from config import Settings
from handlers.registry import HANDLER_CLASSES
from models.model_manager import ModelManager
from worker.heartbeat import HeartbeatThread, get_gpu_info
from worker.job_loop import drain_jobs, run_loop

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    stream=sys.stdout,
)
logger = logging.getLogger("gpu-worker")

def resolve_device(device_setting: str) -> torch.device:
    if device_setting == "auto":
        return torch.device("cuda" if torch.cuda.is_available() else "cpu")
    return torch.device(device_setting)

def _free_vram() -> None:
    """Release GPU memory after unloading a model."""
    if torch.cuda.is_available():
        torch.cuda.empty_cache()
    gc.collect()

def batch_run(
    api: WorkerApiClient,
    model_manager: ModelManager,
    settings: Settings,
    device: torch.device,
) -> None:
    """Process all jobs in phases (one per handler class), then shutdown."""

    idle_count = 0

    while True:
        any_processed = False

        for handler_cls in HANDLER_CLASSES:
            job_mappings = handler_cls.JOB_MAPPINGS
            model_names = [m.name for m in handler_cls.MODELS]

            # Peek: is there any job across this phase's types?
            first_jobs: dict[str, dict] = {}
            for mapping in job_mappings:
                try:
                    job = api.get_next_job(job_type=mapping.job_type)
                    if job is not None:
                        first_jobs[mapping.job_type] = job
                except Exception:
                    logger.exception("Error polling for %s jobs", mapping.job_type)

            if not first_jobs:
                continue

            # Lazy load model for this phase
            set_current_phase(handler_cls.PHASE_NAME)
            phase_label = job_mappings[0].job_type.rsplit("_batch", 1)[0]
            logger.info("Loading model for phase: %s", phase_label)
            try:
                handler_instance = handler_cls(model_manager, settings, device)
            except Exception:
                logger.exception("Failed to load model for %s", phase_label)
                set_current_phase("idle")
                continue

            set_models_loaded(model_names)

            # Drain all job types in this phase
            for mapping in job_mappings:
                handler_fn = getattr(handler_instance, mapping.method, None)
                if handler_fn is None:
                    continue

                set_current_phase(f"processing_{mapping.job_type}")

                first = first_jobs.get(mapping.job_type)
                if first is None:
                    try:
                        first = api.get_next_job(job_type=mapping.job_type)
                    except Exception:
                        logger.exception("Error polling for %s jobs", mapping.job_type)
                        continue
                if first is None:
                    continue

                processed = drain_jobs(api, mapping.job_type, handler_fn, first, settings)
                logger.info("Phase %s/%s: processed %d jobs", phase_label, mapping.job_type, processed)
                any_processed = any_processed or (processed > 0)

            # Unload model and free VRAM
            del handler_instance
            _free_vram()
            set_models_loaded([])
            set_current_phase("idle")
            logger.info("Unloaded models for phase: %s", phase_label)

        if any_processed:
            idle_count = 0
        else:
            idle_count += 1
            logger.info(
                "No jobs found (idle cycle %d/%d)",
                idle_count,
                settings.idle_shutdown_cycles,
            )
            if idle_count >= settings.idle_shutdown_cycles:
                logger.info(
                    "No jobs for %d cycles, initiating shutdown sequence",
                    idle_count,
                )
                if settings.auto_shutdown:
                    set_current_phase("shutting_down")
                    api.send_heartbeat({**get_gpu_info(settings), "status": "shutting_down"})
                    request_shutdown(settings)
                else:
                    logger.info("auto_shutdown disabled, would shutdown now - exiting")
                return
            time.sleep(settings.poll_interval_idle)

def legacy_continuous_run(
    api: WorkerApiClient,
    model_manager: ModelManager,
    settings: Settings,
    device: torch.device,
) -> None:
    """Original behavior: load all models upfront, poll indefinitely."""
    loaded_models: list[str] = []
    handlers: dict = {}

    for handler_cls in HANDLER_CLASSES:
        logger.info("Loading handler: %s (phase: %s)", handler_cls.__name__, handler_cls.PHASE_NAME)
        try:
            instance = handler_cls(model_manager, settings, device)
            for m in handler_cls.MODELS:
                loaded_models.append(m.name)
            for mapping in handler_cls.JOB_MAPPINGS:
                fn = getattr(instance, mapping.method, None)
                if fn is not None:
                    handlers[mapping.job_type] = fn
        except Exception:
            logger.exception("Failed to load %s", handler_cls.__name__)

    set_models_loaded(loaded_models)
    set_current_phase("processing")

    logger.info("Entering continuous polling loop (handlers: %s)", list(handlers.keys()))
    run_loop(api, handlers, settings)

def main() -> None:
    settings = Settings()

    device = resolve_device(settings.device)
    logger.info("Using device: %s", device)

    if torch.cuda.is_available():
        logger.info("GPU: %s", torch.cuda.get_device_name(0))
        mem = torch.cuda.mem_get_info(0)
        logger.info("GPU memory: %d MB total, %d MB free", mem[1] // (1024 * 1024), mem[0] // (1024 * 1024))
    else:
        logger.info("No CUDA GPU available, running on CPU")

    model_manager = ModelManager(settings)

    # Register HF mappings from all handlers
    for handler_cls in HANDLER_CLASSES:
        model_manager.register_models(handler_cls.MODELS)

    api = WorkerApiClient(settings)

    start_health_server(settings, [])
    logger.info("Health server started on port %d", settings.health_port)

    HeartbeatThread(api, settings).start()
    logger.info("Heartbeat thread started")

    logger.info(
        "GPU Worker started (batch_mode=%s, auto_shutdown=%s)",
        settings.batch_mode,
        settings.auto_shutdown,
    )

    try:
        if settings.batch_mode:
            batch_run(api, model_manager, settings, device)
        else:
            legacy_continuous_run(api, model_manager, settings, device)
    except KeyboardInterrupt:
        logger.info("Shutting down...")
    finally:
        api.close()

if __name__ == "__main__":
    main()
