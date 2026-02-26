import gc
import logging
import sys
import time

import torch

from api.client import WorkerApiClient, request_shutdown
from api.health_server import set_current_phase, set_models_loaded, start_health_server
from config import Settings
from inference.image_moderator import ImageModerator
from inference.text_moderator import TextModerator
from models.model_manager import ModelManager
from training.ncf_trainer import NcfTrainer
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
    """Process all jobs in phases (text -> image -> NCF), then shutdown."""

    JOB_PHASES: list[tuple[str, str, list[str], object]] = [
        # (job_type, loading_phase_label, model_names, factory)
        ("text_moderation", "loading_herbert", ["herbert"],
         lambda: TextModerator(model_manager, settings, device)),
        ("image_moderation", "loading_nsfw_clip", ["nsfw", "clip"],
         lambda: ImageModerator(model_manager, settings, device)),
        ("ncf_training", "loading_ncf", [],
         lambda: NcfTrainer(model_manager, settings, device)),
    ]

    idle_count = 0

    while True:
        any_processed = False

        for job_type, loading_phase, model_names, factory in JOB_PHASES:
            # Peek: is there a job of this type?
            try:
                job = api.get_next_job(job_type=job_type)
            except Exception:
                logger.exception("Error polling for %s jobs", job_type)
                continue

            if job is None:
                continue

            # Lazy load model for this phase
            set_current_phase(loading_phase)
            logger.info("Loading model for phase: %s", job_type)
            try:
                handler_instance = factory()
            except Exception:
                logger.exception("Failed to load model for %s", job_type)
                set_current_phase("idle")
                continue

            set_models_loaded(model_names)
            set_current_phase(f"processing_{job_type}")

            # Get the handler function
            handler_fn = getattr(handler_instance, "handle_job", None) or handler_instance.train

            # Drain all jobs of this type
            processed = drain_jobs(api, job_type, handler_fn, job, settings)
            logger.info("Phase %s: processed %d jobs", job_type, processed)
            any_processed = any_processed or (processed > 0)

            # Unload model and free VRAM
            del handler_instance
            _free_vram()
            set_models_loaded([])
            set_current_phase("idle")
            logger.info("Unloaded models for phase: %s", job_type)

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

    logger.info("Loading text moderation model (HerBERT)...")
    try:
        text_mod = TextModerator(model_manager, settings, device)
        loaded_models.append("herbert")
    except Exception:
        logger.exception("Failed to load HerBERT model")
        text_mod = None

    logger.info("Loading image moderation models (NSFW + CLIP)...")
    try:
        image_mod = ImageModerator(model_manager, settings, device)
        loaded_models.extend(["nsfw", "clip"])
    except Exception:
        logger.exception("Failed to load image moderation models")
        image_mod = None

    ncf_trainer = NcfTrainer(model_manager, settings, device)

    handlers: dict = {}
    if text_mod is not None:
        handlers["text_moderation"] = text_mod.handle_job
    if image_mod is not None:
        handlers["image_moderation"] = image_mod.handle_job
    handlers["ncf_training"] = ncf_trainer.train

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
