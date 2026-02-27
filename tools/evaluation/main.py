"""
NCF Evaluation Pipeline - CLI entry point.

Usage:
    cd tools/generator
    python -m evaluation.main --model-path ../../gpu-worker/model_cache/ncf/v20250226_120000
    python -m evaluation.main --top-n 20 --k-values 5,10,20 --min-reviews 5
    python -m evaluation.main -v --output results.json
"""

import argparse
import logging
import os
import sys
from pathlib import Path

# sys.path setup - same pattern as tests/conftest.py
EVALUATION_ROOT = Path(__file__).parent.resolve()
TOOLS_ROOT = EVALUATION_ROOT.parent
GENERATOR_ROOT = TOOLS_ROOT / "generator"

if str(GENERATOR_ROOT) not in sys.path:
    sys.path.insert(0, str(GENERATOR_ROOT))

# Imports from generator (after sys.path setup)
from config.database import get_connection_params  # noqa: E402
from utils.blueprint_loader import BlueprintLoader  # noqa: E402
from utils.db_connection import DatabaseConnection  # noqa: E402

# Imports from evaluation package
from .config import (  # noqa: E402
    DEFAULT_K_VALUES,
    DEFAULT_MIN_REVIEWS,
    DEFAULT_MODEL_BASE,
    DEFAULT_OUTPUT,
    DEFAULT_TOP_N,
)
from .data_access import EvaluationDAO  # noqa: E402
from .ground_truth import GroundTruthCalculator  # noqa: E402
from .inference import OnnxNcfModel, find_latest_model  # noqa: E402
from .metrics import coverage, hit_rate_at_k, mae, ndcg_at_k, rmse  # noqa: E402
from .reporting import EvaluationReport  # noqa: E402

logger = logging.getLogger(__name__)

# Relevance threshold - dishes rated >= this are considered "relevant"
RELEVANCE_THRESHOLD = 7.0

def resolve_model_path(model_path_arg: str | None) -> Path:
    """Resolve model directory: explicit path or auto-detect latest."""
    if model_path_arg:
        p = Path(model_path_arg)
        if not p.exists():
            raise FileNotFoundError(f"Model path does not exist: {p}")
        return p
    return find_latest_model()

def evaluate(args: argparse.Namespace) -> None:
    """Main evaluation pipeline."""

    # CWD must be generator root for BlueprintLoader("blueprints")
    os.chdir(GENERATOR_ROOT)

    # [1] Load model
    model_dir = resolve_model_path(args.model_path)
    logger.info("Using model: %s", model_dir)
    model = OnnxNcfModel(model_dir)

    # [2] Load data from DB
    with DatabaseConnection(get_connection_params()) as db:
        users = EvaluationDAO.get_test_users(db, args.min_reviews)
        all_dishes = EvaluationDAO.get_all_dishes_enriched(db)
        restaurants = EvaluationDAO.get_all_restaurants_enriched(db)

        # Build lookup maps
        dish_by_id = {d["dish_id"]: d for d in all_dishes}
        restaurant_by_id = {r["restaurant_id"]: r for r in restaurants}
        all_dish_ids = list(dish_by_id.keys())

        # [3] Ground truth calculator
        loader = BlueprintLoader("blueprints")
        vectors_data = loader.load_blueprint("dishes.json")
        gt_calc = GroundTruthCalculator(vectors_data)

        # [4] Per-user evaluation
        all_predicted: list[float] = []
        all_actual: list[float] = []
        all_hr: dict[int, list[float]] = {k: [] for k in args.k_values}
        all_ndcg: dict[int, list[float]] = {k: [] for k in args.k_values}
        all_recommended: set[int] = set()
        users_skipped = 0
        pairs_evaluated = 0

        for user in users:
            user_id = user["user_id"]

            # Get user's reviewed dishes to exclude from candidates
            reviewed = EvaluationDAO.get_user_reviewed_dishes(db, user_id)
            candidates = [did for did in all_dish_ids if did not in reviewed]

            if not candidates:
                users_skipped += 1
                continue

            # Model predictions: top-N
            top_n_predictions = model.predict_top_n_for_user(
                user_id, candidates, args.top_n
            )

            if not top_n_predictions:
                users_skipped += 1
                continue

            recommended_ids = [did for did, _ in top_n_predictions]
            all_recommended.update(recommended_ids)

            # Ground truth for each predicted dish
            relevant_ids: set[int] = set()

            for dish_id, pred_score in top_n_predictions:
                dish = dish_by_id.get(dish_id)
                if dish is None:
                    continue

                rest = restaurant_by_id.get(dish["restaurant_id"])
                if rest is None:
                    continue

                gt_result = gt_calc.calculate_rating(user, dish, rest)
                gt_score = gt_result["overall_rating"]

                all_predicted.append(pred_score)
                all_actual.append(gt_score)
                pairs_evaluated += 1

                if gt_score >= RELEVANCE_THRESHOLD:
                    relevant_ids.add(dish_id)

            # Per-user ranking metrics
            for k in args.k_values:
                all_hr[k].append(hit_rate_at_k(recommended_ids, relevant_ids, k))
                all_ndcg[k].append(ndcg_at_k(recommended_ids, relevant_ids, k))

    # [5] Aggregate metrics
    rmse_val = rmse(all_predicted, all_actual)
    mae_val = mae(all_predicted, all_actual)

    mean_hr = {k: (sum(v) / len(v) if v else 0.0) for k, v in all_hr.items()}
    mean_ndcg = {k: (sum(v) / len(v) if v else 0.0) for k, v in all_ndcg.items()}
    cov = coverage(all_recommended, len(all_dishes))

    # [6] Report
    report = EvaluationReport()
    report.collect(
        model_path=str(model_dir),
        num_users=len(users) - users_skipped,
        num_dishes=len(all_dishes),
        num_restaurants=len(restaurants),
        top_n=args.top_n,
        k_values=args.k_values,
        rmse_val=rmse_val,
        mae_val=mae_val,
        hit_rates=mean_hr,
        ndcg_scores=mean_ndcg,
        coverage_val=cov,
        users_skipped=users_skipped,
        pairs_evaluated=pairs_evaluated,
    )
    report.print_report()
    report.save_json(args.output)

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Evaluate NCF recommendation model against ground truth ratings."
    )
    parser.add_argument(
        "--model-path",
        type=str,
        default=None,
        help=f"Path to model directory (default: latest in {DEFAULT_MODEL_BASE})",
    )
    parser.add_argument(
        "--top-n",
        type=int,
        default=DEFAULT_TOP_N,
        help=f"Number of top recommendations per user (default: {DEFAULT_TOP_N})",
    )
    parser.add_argument(
        "--k-values",
        type=str,
        default=",".join(str(k) for k in DEFAULT_K_VALUES),
        help="Comma-separated K values for HR@K and NDCG@K (default: 5,10)",
    )
    parser.add_argument(
        "--min-reviews",
        type=int,
        default=DEFAULT_MIN_REVIEWS,
        help=f"Minimum reviews per user to include (default: {DEFAULT_MIN_REVIEWS})",
    )
    parser.add_argument(
        "--output",
        type=str,
        default=DEFAULT_OUTPUT,
        help=f"Output JSON path (default: {DEFAULT_OUTPUT})",
    )
    parser.add_argument(
        "-v", "--verbose", action="store_true", help="Enable DEBUG logging"
    )
    parser.add_argument(
        "-q", "--quiet", action="store_true", help="Suppress INFO logging"
    )

    args = parser.parse_args()

    # Parse k_values
    args.k_values = [int(k.strip()) for k in args.k_values.split(",")]

    # Logging setup
    if args.verbose:
        level = logging.DEBUG
    elif args.quiet:
        level = logging.WARNING
    else:
        level = logging.INFO

    logging.basicConfig(
        level=level,
        format="%(asctime)s [%(levelname)-7s] %(name)s: %(message)s",
        datefmt="%H:%M:%S",
    )

    evaluate(args)

if __name__ == "__main__":
    main()
