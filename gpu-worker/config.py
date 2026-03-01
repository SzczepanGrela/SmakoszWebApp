from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    # API connection
    api_url: str = "http://localhost:5000"
    api_key: str = ""
    worker_id: str = "gpu-worker"

    # Polling
    poll_interval_idle: int = 5
    poll_interval_busy: int = 1
    heartbeat_interval: int = 60
    config_cache_ttl: int = 300

    # Models - R2
    r2_endpoint: str = ""
    r2_access_key: str = ""
    r2_secret_key: str = ""
    r2_bucket: str = "smakosz-models"

    # Models - versions (fetched from API /config, these are fallbacks)
    herbert_model_version: str = "v1"
    nsfw_model_version: str = "v1"
    clip_model_version: str = "v1"

    # Inference
    herbert_max_length: int = 256
    device: str = "auto"  # "auto", "cuda", "cpu"

    # Health server
    health_port: int = 8000

    # Batch mode
    batch_mode: bool = True  # True = process all jobs then shutdown; False = continuous polling

    # Auto-shutdown (safe-shutdown API on Ubuntu homelab)
    auto_shutdown: bool = True
    shutdown_api_url: str = ""
    shutdown_api_token: str = ""

    # Idle detection - how many idle poll cycles before considering "done"
    idle_shutdown_cycles: int = 3

    model_config = {"env_prefix": "GPU_WORKER_"}
