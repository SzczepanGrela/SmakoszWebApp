from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    api_url: str = "http://localhost:5000"
    api_key: str = ""
    worker_id: str = "gpu-homelab"

    poll_interval_idle: int = 5
    poll_interval_busy: int = 1
    heartbeat_interval: int = 60
    config_cache_ttl: int = 300

    r2_endpoint: str = ""
    r2_access_key: str = ""
    r2_secret_key: str = ""
    r2_bucket: str = ""

    herbert_model_version: str = "v1"
    nsfw_model_version: str = "v1"
    clip_model_version: str = "v1"

    herbert_max_length: int = 256
    device: str = "auto"  # "auto", "cuda", "cpu"

    health_port: int = 8000

    batch_mode: bool = True  # True = process all jobs then shutdown; False = continuous polling

    auto_shutdown: bool = True
    shutdown_api_url: str = ""
    shutdown_api_token: str = ""

    idle_shutdown_cycles: int = 3

    model_config = {"env_prefix": "GPU_WORKER_", "env_file": ".env"}
