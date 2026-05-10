import sys
from pathlib import Path

GPU_WORKER_ROOT = Path(__file__).resolve().parent.parent
if str(GPU_WORKER_ROOT) not in sys.path:
    sys.path.insert(0, str(GPU_WORKER_ROOT))
