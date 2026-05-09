import argparse
import logging
import os
import sys
from pathlib import Path

EVALUATION_ROOT = Path(__file__).parent.resolve()
TOOLS_ROOT = EVALUATION_ROOT.parent
PROJECT_ROOT = TOOLS_ROOT.parent
DEFAULT_LOCAL_BASE = PROJECT_ROOT / "gpu-worker" / "model_cache" / "ncf"
DEFAULT_ENV_FILE = PROJECT_ROOT / "gpu-worker" / ".env"

logger = logging.getLogger(__name__)


def load_r2_credentials(env_path: Path) -> dict:
    if not env_path.exists():
        raise FileNotFoundError(f"R2 env file not found: {env_path}")

    creds = {}
    for line in env_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        creds[key.strip()] = value.strip()

    required = ["GPU_WORKER_R2_ENDPOINT", "GPU_WORKER_R2_ACCESS_KEY",
                "GPU_WORKER_R2_SECRET_KEY", "GPU_WORKER_R2_BUCKET"]
    missing = [k for k in required if not creds.get(k)]
    if missing:
        raise ValueError(f"Missing R2 keys in {env_path}: {', '.join(missing)}")

    return {
        "endpoint": creds["GPU_WORKER_R2_ENDPOINT"],
        "access_key": creds["GPU_WORKER_R2_ACCESS_KEY"],
        "secret_key": creds["GPU_WORKER_R2_SECRET_KEY"],
        "bucket": creds["GPU_WORKER_R2_BUCKET"],
    }


def list_versions(s3_client, bucket: str) -> list[str]:
    response = s3_client.list_objects_v2(Bucket=bucket, Prefix="models/ncf/", Delimiter="/")
    prefixes = response.get("CommonPrefixes", [])
    versions = [p["Prefix"].split("/")[-2] for p in prefixes if p["Prefix"].split("/")[-2].startswith("v")]
    return sorted(versions, reverse=True)


def download_version(s3_client, bucket: str, version: str, local_base: Path) -> Path:
    prefix = f"models/ncf/{version}/"
    target_dir = local_base / version
    target_dir.mkdir(parents=True, exist_ok=True)

    response = s3_client.list_objects_v2(Bucket=bucket, Prefix=prefix)
    contents = response.get("Contents", [])
    if not contents:
        raise FileNotFoundError(f"No objects in R2 at prefix: {prefix}")

    for obj in contents:
        key = obj["Key"]
        rel = key[len(prefix):]
        if not rel:
            continue
        local_file = target_dir / rel
        logger.info("Downloading %s -> %s", key, local_file)
        s3_client.download_file(bucket, key, str(local_file))

    return target_dir


def main():
    parser = argparse.ArgumentParser(description="Fetch NCF ONNX model from R2 bucket to local cache.")
    parser.add_argument("version", nargs="?", default=None,
                        help="Model version to fetch (e.g. v20260513_004159). Default: latest in bucket.")
    parser.add_argument("--list", action="store_true", help="List available versions and exit.")
    parser.add_argument("--env", type=str, default=str(DEFAULT_ENV_FILE),
                        help=f"Path to .env with R2 credentials (default: {DEFAULT_ENV_FILE})")
    parser.add_argument("--local-base", type=str, default=str(DEFAULT_LOCAL_BASE),
                        help=f"Local model_cache/ncf directory (default: {DEFAULT_LOCAL_BASE})")
    args = parser.parse_args()

    logging.basicConfig(level=logging.INFO,
                        format="%(asctime)s [%(levelname)-7s] %(message)s",
                        datefmt="%H:%M:%S")

    try:
        import boto3
    except ImportError:
        logger.error("boto3 is required. Install with: pip install boto3")
        sys.exit(1)

    creds = load_r2_credentials(Path(args.env))
    s3 = boto3.client(
        "s3",
        endpoint_url=creds["endpoint"],
        aws_access_key_id=creds["access_key"],
        aws_secret_access_key=creds["secret_key"],
        region_name="auto",
    )

    if args.list:
        versions = list_versions(s3, creds["bucket"])
        logger.info("Available versions in s3://%s/models/ncf/", creds["bucket"])
        for v in versions:
            logger.info("  %s", v)
        return

    version = args.version
    if version is None:
        versions = list_versions(s3, creds["bucket"])
        if not versions:
            logger.error("No model versions found in R2.")
            sys.exit(1)
        version = versions[0]
        logger.info("No version specified, using latest: %s", version)

    target_dir = download_version(s3, creds["bucket"], version, Path(args.local_base))
    logger.info("Model %s downloaded to: %s", version, target_dir)

    files = list(target_dir.iterdir())
    logger.info("Files: %s", ", ".join(f.name for f in files))


if __name__ == "__main__":
    main()
