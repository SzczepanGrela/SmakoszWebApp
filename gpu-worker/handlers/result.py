def make_result(*, model_name: str, model_version: str, verdict: str | None = None, **scores: float) -> dict:
    result = {"model_name": model_name, "model_version": model_version}
    if verdict is not None:
        result["verdict"] = verdict
    result.update(scores)
    return result
