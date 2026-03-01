import logging
import os
import subprocess
import sys
import time
from functools import wraps
from pathlib import Path

import docker
from flask import Flask, jsonify, request

import config

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    stream=sys.stdout,
)
logger = logging.getLogger("homelab-api")

app = Flask(__name__)

def require_token(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        token = request.headers.get("X-API-Token")
        if not token or token != config.API_TOKEN:
            return jsonify({"error": "Unauthorized"}), 401
        return f(*args, **kwargs)
    return decorated

def get_blockers():
    blockers = []

    if Path(config.LOCKFILE).exists():
        blockers.append({
            "id": "lockfile",
            "name": "Lockfile active",
            "detail": f"File {config.LOCKFILE} exists - administrator is working",
        })

    try:
        result = subprocess.run(["who"], capture_output=True, text=True, timeout=5)
        lines = result.stdout.strip().splitlines()
        if lines:
            users = {}
            for line in lines:
                user = line.split()[0]
                users[user] = users.get(user, 0) + 1
            parts = [f"{u} ({c} sessions)" if c > 1 else u for u, c in users.items()]
            blockers.append({
                "id": "users",
                "name": "Logged-in users",
                "detail": f"Users: {', '.join(parts)}",
            })
    except Exception as e:
        logger.warning("Failed to check logged-in users: %s", e)

    try:
        client = docker.from_env()
        for name in config.DOCKER_BLOCKERS.split(","):
            name = name.strip()
            if not name:
                continue
            try:
                container = client.containers.get(name)
                if container.status == "running":
                    blockers.append({
                        "id": f"docker_{name}",
                        "name": f"Container {name} running",
                        "detail": f"Container {name} status: {container.status}",
                    })
            except docker.errors.NotFound:
                pass
        client.close()
    except Exception as e:
        logger.warning("Failed to check Docker containers: %s", e)

    for pattern in config.PROCESS_BLOCKERS.split(","):
        pattern = pattern.strip()
        if not pattern:
            continue
        try:
            result = subprocess.run(
                ["pgrep", "-f", pattern],
                capture_output=True, text=True, timeout=5,
            )
            if result.returncode == 0 and result.stdout.strip():
                blockers.append({
                    "id": f"process_{pattern}",
                    "name": f"Process {pattern} active",
                    "detail": f"Matched by: pgrep -f {pattern}",
                })
        except Exception as e:
            logger.warning("Failed to check process %s: %s", pattern, e)

    return blockers

def get_cpu_temps():
    temps = []
    thermal_base = Path("/sys/class/thermal")
    if not thermal_base.exists():
        return temps
    for zone in sorted(thermal_base.iterdir()):
        if not zone.name.startswith("thermal_zone"):
            continue
        try:
            temp_raw = (zone / "temp").read_text().strip()
            type_name = (zone / "type").read_text().strip()
            temps.append({
                "zone": zone.name,
                "type": type_name,
                "temp_c": round(int(temp_raw) / 1000, 1),
            })
        except Exception:
            continue
    return temps

def get_memory_info():
    try:
        meminfo = {}
        for line in Path("/proc/meminfo").read_text().splitlines():
            parts = line.split()
            if len(parts) >= 2:
                meminfo[parts[0].rstrip(":")] = int(parts[1])
        total = meminfo.get("MemTotal", 0)
        available = meminfo.get("MemAvailable", 0)
        used = total - available
        return {
            "total_mb": round(total / 1024),
            "used_mb": round(used / 1024),
            "available_mb": round(available / 1024),
            "usage_percent": round(used / total * 100, 1) if total else 0,
        }
    except Exception:
        return {"total_mb": 0, "used_mb": 0, "available_mb": 0, "usage_percent": 0}

def get_load_average():
    try:
        parts = Path("/proc/loadavg").read_text().split()
        return {
            "load_1m": float(parts[0]),
            "load_5m": float(parts[1]),
            "load_15m": float(parts[2]),
        }
    except Exception:
        return {"load_1m": 0, "load_5m": 0, "load_15m": 0}

def get_uptime():
    try:
        raw = Path("/proc/uptime").read_text().split()[0]
        seconds = int(float(raw))
        days = seconds // 86400
        hours = (seconds % 86400) // 3600
        minutes = (seconds % 3600) // 60
        return {"seconds": seconds, "human": f"{days}d {hours}h {minutes}m"}
    except Exception:
        return {"seconds": 0, "human": "unknown"}

def get_disk_usage():
    try:
        st = os.statvfs("/")
        total = st.f_blocks * st.f_frsize
        available = st.f_bavail * st.f_frsize
        used = total - available
        return {
            "total_gb": round(total / (1024 ** 3), 1),
            "used_gb": round(used / (1024 ** 3), 1),
            "available_gb": round(available / (1024 ** 3), 1),
            "usage_percent": round(used / total * 100, 1) if total else 0,
        }
    except Exception:
        return {"total_gb": 0, "used_gb": 0, "available_gb": 0, "usage_percent": 0}

@app.route("/api/health")
def health():
    return jsonify({"status": "ok", "timestamp": int(time.time())})

@app.route("/api/stats")
@require_token
def stats():
    return jsonify({
        "cpu_temps": get_cpu_temps(),
        "memory": get_memory_info(),
        "load": get_load_average(),
        "uptime": get_uptime(),
        "disk": get_disk_usage(),
    })

@app.route("/api/docker")
@require_token
def docker_status():
    try:
        client = docker.from_env()
        containers = []
        for c in client.containers.list(all=True):
            containers.append({
                "name": c.name,
                "status": c.status,
                "image": ",".join(c.image.tags) if c.image.tags else c.image.short_id,
                "started_at": c.attrs.get("State", {}).get("StartedAt", ""),
            })
        client.close()
        return jsonify({"containers": containers})
    except Exception as e:
        logger.error("Docker unavailable: %s", e)
        return jsonify({"error": "Docker unavailable"}), 503

@app.route("/api/blockers")
@require_token
def blockers():
    b = get_blockers()
    return jsonify({
        "can_shutdown": len(b) == 0,
        "blocker_count": len(b),
        "blockers": b,
    })

@app.route("/api/gpu-worker/start", methods=["POST"])
@require_token
def gpu_worker_start():
    try:
        client = docker.from_env()
        try:
            container = client.containers.get("gpu-worker")
            if container.status == "running":
                return jsonify({"success": True, "message": "Already running"})
            container.start()
            logger.info("gpu-worker container started")
            return jsonify({"success": True, "message": "Container started"})
        except docker.errors.NotFound:
            logger.info("gpu-worker container not found, running docker compose up")
            result = subprocess.run(
                ["docker", "compose", "-f", "docker-compose.gpu.yml", "up", "-d"],
                cwd=config.GPU_WORKER_COMPOSE_DIR,
                capture_output=True, text=True, timeout=120,
            )
            if result.returncode == 0:
                return jsonify({"success": True, "message": "Container created and started"})
            logger.error("docker compose up failed: %s", result.stderr)
            return jsonify({"success": False, "message": result.stderr}), 500
        finally:
            client.close()
    except Exception as e:
        logger.error("gpu-worker start failed: %s", e)
        return jsonify({"success": False, "message": str(e)}), 500

@app.route("/api/gpu-worker/stop", methods=["POST"])
@require_token
def gpu_worker_stop():
    try:
        client = docker.from_env()
        try:
            container = client.containers.get("gpu-worker")
            if container.status != "running":
                return jsonify({"success": True, "message": "Already stopped"})
            container.stop()
            logger.info("gpu-worker container stopped")
            return jsonify({"success": True, "message": "Container stopped"})
        except docker.errors.NotFound:
            return jsonify({"success": True, "message": "Container not found"})
        finally:
            client.close()
    except Exception as e:
        logger.error("gpu-worker stop failed: %s", e)
        return jsonify({"success": False, "message": str(e)}), 500

@app.route("/api/gpu-worker/status")
@require_token
def gpu_worker_status():
    try:
        client = docker.from_env()
        try:
            container = client.containers.get("gpu-worker")
            started_at = container.attrs.get("State", {}).get("StartedAt", "")
            return jsonify({"status": container.status, "started_at": started_at})
        except docker.errors.NotFound:
            return jsonify({"status": "not_found", "started_at": None})
        finally:
            client.close()
    except Exception as e:
        logger.error("gpu-worker status check failed: %s", e)
        return jsonify({"error": str(e)}), 503

@app.route("/api/shutdown", methods=["POST"])
@require_token
def shutdown():
    b = get_blockers()
    if b:
        return jsonify({
            "success": False,
            "message": "Shutdown blocked",
            "blocker_count": len(b),
            "blockers": b,
        }), 409

    try:
        logger.info("Initiating safe shutdown via sudo poweroff")
        subprocess.Popen(["sudo", "/usr/sbin/poweroff"])
        return jsonify({"success": True, "message": "Shutdown initiated"})
    except Exception as e:
        logger.error("Failed to execute poweroff: %s", e)
        return jsonify({
            "success": False,
            "message": f"Failed to execute poweroff: {e}",
        }), 500

if __name__ == "__main__":
    config.validate()
    logger.info("Starting homelab-api (port=%d)", config.PORT)
    app.run(host="0.0.0.0", port=config.PORT)
