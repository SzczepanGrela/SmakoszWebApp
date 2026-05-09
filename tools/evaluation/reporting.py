import json
import logging
from datetime import datetime
from pathlib import Path

logger = logging.getLogger(__name__)

class EvaluationReport:
    def __init__(self):
        self.data: dict = {}

    def collect(
        self,
        *,
        model_path: str,
        num_users: int,
        num_dishes: int,
        num_restaurants: int,
        top_n: int,
        k_values: list[int],
        rmse_val: float,
        mae_val: float,
        hit_rates: dict[int, float],
        ndcg_scores: dict[int, float],
        coverage_val: float,
        users_skipped: int,
        pairs_evaluated: int,
        targets: dict | None = None,
    ) -> dict:
        self.data = {
            "generated_at": datetime.now().isoformat(),
            "model_path": model_path,
            "dataset": {
                "users_evaluated": num_users,
                "users_skipped_no_mapping": users_skipped,
                "dishes_total": num_dishes,
                "restaurants_total": num_restaurants,
                "pairs_evaluated": pairs_evaluated,
            },
            "parameters": {
                "top_n": top_n,
                "k_values": k_values,
            },
            "metrics": {
                "rmse": round(rmse_val, 4),
                "mae": round(mae_val, 4),
                "hit_rate": {f"@{k}": round(v, 4) for k, v in hit_rates.items()},
                "ndcg": {f"@{k}": round(v, 4) for k, v in ndcg_scores.items()},
                "coverage": round(coverage_val, 4),
            },
        }
        if targets:
            self.data["targets"] = targets
            self.data["pass_status"] = self._compute_pass_status(targets)
        return self.data

    @staticmethod
    def _compute_pass_status(targets: dict) -> dict:
        return {k: {"target": v, "passed": False} for k, v in targets.items()}

    @staticmethod
    def _eval_target(metric_value: float, target_value: float, lower_is_better: bool) -> bool:
        return metric_value <= target_value if lower_is_better else metric_value >= target_value

    def print_report(self) -> None:
        if not self.data:
            logger.warning("No data collected. Run collect() first.")
            return

        d = self.data
        m = d["metrics"]
        ds = d["dataset"]
        targets = d.get("targets", {})

        lines = [
            "",
            "=" * 64,
            "  NCF Model Evaluation Report",
            "=" * 64,
            "",
            f"  Model:      {d['model_path']}",
            f"  Generated:  {d['generated_at']}",
            "",
            "  Dataset",
            f"    Users evaluated:           {ds['users_evaluated']}",
            f"    Users skipped (no map):    {ds['users_skipped_no_mapping']}",
            f"    Dishes total:              {ds['dishes_total']}",
            f"    Restaurants total:         {ds['restaurants_total']}",
            f"    Pairs evaluated:           {ds['pairs_evaluated']}",
            "",
            "  Rating Accuracy",
            self._line("RMSE", m["rmse"], targets.get("rmse"), lower_is_better=True),
            self._line("MAE", m["mae"], None),
            "",
            "  Ranking Quality",
        ]

        for k_label, val in m["hit_rate"].items():
            target = targets.get(f"hr{k_label}")
            lines.append(self._line(f"Hit Rate {k_label}", val, target))
        for k_label, val in m["ndcg"].items():
            target = targets.get(f"ndcg{k_label}")
            lines.append(self._line(f"NDCG {k_label}", val, target))

        lines.extend([
            "",
            self._line("Coverage", m["coverage"], targets.get("coverage")),
            "",
            "=" * 64,
        ])

        if targets:
            verdict = "ALL TARGETS MET" if self.all_targets_passed() else "SOME TARGETS MISSED"
            lines.append(f"  Verdict: {verdict}")
            lines.append("=" * 64)

        for line in lines:
            logger.info(line)

    @staticmethod
    def _line(name: str, value: float, target: float | None, lower_is_better: bool = False) -> str:
        base = f"    {name:<26} {value:.4f}"
        if target is None:
            return base
        passed = value <= target if lower_is_better else value >= target
        status = "[PASS]" if passed else "[FAIL]"
        cmp = "<=" if lower_is_better else ">="
        return f"{base}   {status}  (target {cmp} {target:.4f})"

    def all_targets_passed(self) -> bool:
        if "targets" not in self.data:
            return True
        m = self.data["metrics"]
        for key, target in self.data["targets"].items():
            if key == "rmse":
                if m["rmse"] > target:
                    return False
            elif key == "coverage":
                if m["coverage"] < target:
                    return False
            elif key.startswith("hr@"):
                if m["hit_rate"].get(key[2:], 0.0) < target:
                    return False
            elif key.startswith("ndcg@"):
                if m["ndcg"].get(key[4:], 0.0) < target:
                    return False
        return True

    def save_json(self, path: str) -> None:
        if not self.data:
            logger.warning("No data collected. Run collect() first.")
            return

        output_path = Path(path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)

        logger.info("Report saved to %s", output_path)
