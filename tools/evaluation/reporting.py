"""
Evaluation report - collect results, print to console, save to JSON.
"""

import json
import logging
from datetime import datetime
from pathlib import Path

logger = logging.getLogger(__name__)

class EvaluationReport:
    """Collects evaluation metrics and outputs a formatted report."""

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
    ) -> dict:
        """Assemble all metrics into a single report dictionary."""
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
        return self.data

    def print_report(self) -> None:
        """Print a human-readable evaluation report to the logger."""
        if not self.data:
            logger.warning("No data collected. Run collect() first.")
            return

        d = self.data
        m = d["metrics"]
        ds = d["dataset"]

        lines = [
            "",
            "=" * 60,
            "  NCF Model Evaluation Report",
            "=" * 60,
            "",
            f"  Model:      {d['model_path']}",
            f"  Generated:  {d['generated_at']}",
            "",
            "  Dataset",
            f"    Users evaluated:        {ds['users_evaluated']}",
            f"    Users skipped (no map):  {ds['users_skipped_no_mapping']}",
            f"    Dishes total:            {ds['dishes_total']}",
            f"    Restaurants total:        {ds['restaurants_total']}",
            f"    Pairs evaluated:          {ds['pairs_evaluated']}",
            "",
            "  Rating Accuracy",
            f"    RMSE:  {m['rmse']:.4f}",
            f"    MAE:   {m['mae']:.4f}",
            "",
            "  Ranking Quality",
        ]

        for k_label, val in m["hit_rate"].items():
            lines.append(f"    Hit Rate {k_label}:  {val:.4f}")
        for k_label, val in m["ndcg"].items():
            lines.append(f"    NDCG {k_label}:      {val:.4f}")

        lines.extend(
            [
                "",
                f"  Coverage:  {m['coverage']:.4f}",
                "",
                "=" * 60,
            ]
        )

        for line in lines:
            logger.info(line)

    def save_json(self, path: str) -> None:
        """Save the full report to a JSON file."""
        if not self.data:
            logger.warning("No data collected. Run collect() first.")
            return

        output_path = Path(path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, "w", encoding="utf-8") as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)

        logger.info("Report saved to %s", output_path)
