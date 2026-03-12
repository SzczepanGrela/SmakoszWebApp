from pathlib import Path

EVALUATION_ROOT = Path(__file__).parent.resolve()
TOOLS_ROOT = EVALUATION_ROOT.parent
GENERATOR_ROOT = TOOLS_ROOT / "generator"
PROJECT_ROOT = TOOLS_ROOT.parent

DEFAULT_MODEL_BASE = PROJECT_ROOT / "gpu-worker" / "model_cache" / "ncf"

DEFAULT_TOP_N = 10
DEFAULT_K_VALUES = [5, 10]
DEFAULT_MIN_REVIEWS = 3
DEFAULT_OUTPUT = "evaluation_report.json"
