import argparse
import logging
import os
import sys
from pathlib import Path

EVALUATION_ROOT = Path(__file__).parent.resolve()
TOOLS_ROOT = EVALUATION_ROOT.parent
GENERATOR_ROOT = TOOLS_ROOT / "generator"

if str(GENERATOR_ROOT) not in sys.path:
    sys.path.insert(0, str(GENERATOR_ROOT))

from config.database import get_connection_params  # noqa: E402
from utils.blueprint_loader import BlueprintLoader  # noqa: E402
from utils.db_connection import DatabaseConnection  # noqa: E402

from .config import (  # noqa: E402
    DEFAULT_K_VALUES,
    DEFAULT_MIN_REVIEWS,
    DEFAULT_MODEL_BASE,
    DEFAULT_OUTPUT,
    DEFAULT_TOP_N,
)
from .config import DEFAULT_MODEL_BASE  # noqa: E402
from .data_access import EvaluationDAO  # noqa: E402
from .fetch_from_r2 import DEFAULT_ENV_FILE, download_version, list_versions, load_r2_credentials  # noqa: E402
from .ground_truth import GroundTruthCalculator  # noqa: E402
from .inference import OnnxNcfModel  # noqa: E402
from .metrics import coverage, hit_rate_at_k, mae, ndcg_at_k, rmse  # noqa: E402
from .reporting import EvaluationReport  # noqa: E402

logger = logging.getLogger(__name__)

RELEVANCE_THRESHOLD = 7.0

def _build_r2_client(env_path: Path):
    try:
        import boto3
    except ImportError as e:
        raise RuntimeError("boto3 is required for R2 download. Install with: pip install boto3") from e

    creds = load_r2_credentials(env_path)
    s3 = boto3.client(
        "s3",
        endpoint_url=creds["endpoint"],
        aws_access_key_id=creds["access_key"],
        aws_secret_access_key=creds["secret_key"],
        region_name="auto",
    )
    return s3, creds["bucket"]


def resolve_model_path(model_path_arg: str | None, model_version_arg: str | None, env_path: Path) -> Path:
    if model_path_arg:
        p = Path(model_path_arg)
        if not p.exists():
            raise FileNotFoundError(f"Model path does not exist: {p}")
        return p

    if model_version_arg:
        local = DEFAULT_MODEL_BASE / model_version_arg
        if local.exists() and any(local.iterdir()):
            logger.info("Using local model: %s", local)
            return local
        logger.info("Model %s not found locally, fetching from R2", model_version_arg)
        s3, bucket = _build_r2_client(env_path)
        return download_version(s3, bucket, model_version_arg, DEFAULT_MODEL_BASE)

    logger.info("No model version specified, looking up latest in R2")
    s3, bucket = _build_r2_client(env_path)
    versions = list_versions(s3, bucket)
    if not versions:
        raise FileNotFoundError("No model versions found in R2 bucket.")
    latest = versions[0]
    local = DEFAULT_MODEL_BASE / latest
    if local.exists() and any(local.iterdir()):
        logger.info("Latest version %s already cached locally: %s", latest, local)
        return local
    logger.info("Downloading latest version %s from R2", latest)
    return download_version(s3, bucket, latest, DEFAULT_MODEL_BASE)

def evaluate(args: argparse.Namespace) -> None:
    os.chdir(GENERATOR_ROOT)

    model_dir = resolve_model_path(args.model_path, args.model_version, Path(args.env))
    logger.info("Using model: %s", model_dir)
    model = OnnxNcfModel(model_dir)

    with DatabaseConnection(get_connection_params()) as db:
        users = EvaluationDAO.get_test_users(db, args.min_reviews)
        all_dishes = EvaluationDAO.get_all_dishes_enriched(db)
        restaurants = EvaluationDAO.get_all_restaurants_enriched(db)

        dish_by_id = {d["dish_id"]: d for d in all_dishes}
        restaurant_by_id = {r["restaurant_id"]: r for r in restaurants}
        all_dish_ids = list(dish_by_id.keys())

        loader = BlueprintLoader("blueprints")
        vectors_data = loader.load_blueprint("dishes.json")
        gt_calc = GroundTruthCalculator(vectors_data)

        all_predicted: list[float] = []
        all_actual: list[float] = []
        all_hr: dict[int, list[float]] = {k: [] for k in args.k_values}
        all_ndcg: dict[int, list[float]] = {k: [] for k in args.k_values}
        all_recommended: set[int] = set()
        users_skipped = 0
        pairs_evaluated = 0

        total_users = len(users)
        progress_step = max(1, total_users // 20)

        for idx, user in enumerate(users, 1):
            if idx == 1 or idx % progress_step == 0 or idx == total_users:
                logger.info("Progress: %d/%d users (%.1f%%)", idx, total_users, 100.0 * idx / total_users)
            user_id = user["user_id"]

            reviewed = EvaluationDAO.get_user_reviewed_dishes(db, user_id)
            candidates = [did for did in all_dish_ids if did not in reviewed]

            if not candidates:
                users_skipped += 1
                continue

            top_n_predictions = model.predict_top_n_for_user(
                user_id, candidates, args.top_n
            )

            if not top_n_predictions:
                users_skipped += 1
                continue

            recommended_ids = [did for did, _ in top_n_predictions]
            all_recommended.update(recommended_ids)

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

            for k in args.k_values:
                all_hr[k].append(hit_rate_at_k(recommended_ids, relevant_ids, k))
                all_ndcg[k].append(ndcg_at_k(recommended_ids, relevant_ids, k))

    rmse_val = rmse(all_predicted, all_actual)
    mae_val = mae(all_predicted, all_actual)

    mean_hr = {k: (sum(v) / len(v) if v else 0.0) for k, v in all_hr.items()}
    mean_ndcg = {k: (sum(v) / len(v) if v else 0.0) for k, v in all_ndcg.items()}
    cov = coverage(all_recommended, len(all_dishes))

    targets = {}
    if args.target_rmse is not None:
        targets["rmse"] = args.target_rmse
    if args.target_ndcg_10 is not None:
        targets["ndcg@10"] = args.target_ndcg_10
    if args.target_hr_10 is not None:
        targets["hr@10"] = args.target_hr_10
    if args.target_coverage is not None:
        targets["coverage"] = args.target_coverage

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
        targets=targets if targets else None,
    )
    report.print_report()
    report.save_json(args.output)

    if targets and not report.all_targets_passed():
        sys.exit(2)

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Evaluate NCF recommendation model against ground truth ratings."
    )
    parser.add_argument(
        "--model-path",
        type=str,
        default=None,
        help="Explicit path to model directory. Overrides version-based lookup.",
    )
    parser.add_argument(
        "--model-version",
        type=str,
        default=None,
        help="Specific model version to use (e.g. v20260513_004159). Downloads from R2 if missing locally. Default: latest in R2.",
    )
    parser.add_argument(
        "--env",
        type=str,
        default=str(DEFAULT_ENV_FILE),
        help=f"Path to .env with R2 credentials for auto-download (default: {DEFAULT_ENV_FILE})",
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
    parser.add_argument("--target-rmse", type=float, default=None, help="Pass threshold for RMSE (lower is better).")
    parser.add_argument("--target-ndcg-10", type=float, default=None, help="Pass threshold for NDCG@10.")
    parser.add_argument("--target-hr-10", type=float, default=None, help="Pass threshold for Hit Rate @10.")
    parser.add_argument("--target-coverage", type=float, default=None, help="Pass threshold for coverage.")
    parser.add_argument(
        "-v", "--verbose", action="store_true", help="Enable DEBUG logging"
    )
    parser.add_argument(
        "-q", "--quiet", action="store_true", help="Suppress INFO logging"
    )

    args = parser.parse_args()

    args.k_values = [int(k.strip()) for k in args.k_values.split(",")]

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
