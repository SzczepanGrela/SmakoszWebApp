import json
import logging
from pathlib import Path

import numpy as np
import onnxruntime as ort

from .config import DEFAULT_MODEL_BASE

logger = logging.getLogger(__name__)

def find_latest_model(base_dir: Path | None = None) -> Path:
    base = base_dir or DEFAULT_MODEL_BASE
    if not base.exists():
        raise FileNotFoundError(f"Model base directory does not exist: {base}")

    candidates = sorted(
        [d for d in base.iterdir() if d.is_dir() and d.name.startswith("v")],
        reverse=True,
    )
    if not candidates:
        raise FileNotFoundError(f"No model versions found in: {base}")

    return candidates[0]

class OnnxNcfModel:
    def __init__(self, model_dir: Path):
        onnx_files = list(model_dir.glob("*.onnx"))
        if not onnx_files:
            raise FileNotFoundError(f"No .onnx file found in: {model_dir}")
        onnx_path = onnx_files[0]

        mapping_path = model_dir / "mapping.json"
        if not mapping_path.exists():
            raise FileNotFoundError(
                f"mapping.json not found in: {model_dir}\n"
                "Ensure ncf_trainer.py exported the mapping during training."
            )

        self.session = ort.InferenceSession(
            str(onnx_path), providers=["CPUExecutionProvider"]
        )
        logger.info("Loaded ONNX model: %s", onnx_path.name)

        with open(mapping_path, encoding="utf-8") as f:
            raw = json.load(f)

        self.user_map: dict[int, int] = {int(k): v for k, v in raw["user_map"].items()}
        self.dish_map: dict[int, int] = {int(k): v for k, v in raw["dish_map"].items()}

        self.inv_dish_map: dict[int, int] = {v: k for k, v in self.dish_map.items()}

        logger.info(
            "Mapping loaded: %d users, %d dishes",
            len(self.user_map),
            len(self.dish_map),
        )

    def predict_scores(
        self, user_id: int, dish_ids: list[int]
    ) -> list[tuple[int, float]]:
        if user_id not in self.user_map:
            return []

        mapped_user = self.user_map[user_id]

        valid = [(did, self.dish_map[did]) for did in dish_ids if did in self.dish_map]
        if not valid:
            return []

        db_ids, mapped_dishes = zip(*valid)

        user_arr = np.full(len(mapped_dishes), mapped_user, dtype=np.int64)
        dish_arr = np.array(mapped_dishes, dtype=np.int64)

        input_names = [inp.name for inp in self.session.get_inputs()]
        feeds = {input_names[0]: user_arr, input_names[1]: dish_arr}

        (scores,) = self.session.run(None, feeds)
        scores = scores.flatten()

        return list(zip(db_ids, scores.tolist()))

    def predict_top_n_for_user(
        self,
        user_id: int,
        candidate_dish_ids: list[int],
        top_n: int = 10,
    ) -> list[tuple[int, float]]:
        all_scores = self.predict_scores(user_id, candidate_dish_ids)
        if not all_scores:
            return []

        all_scores.sort(key=lambda x: x[1], reverse=True)
        return all_scores[:top_n]
