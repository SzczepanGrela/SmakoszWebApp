import logging
from pathlib import Path

import torch

logger = logging.getLogger(__name__)

def export_to_onnx(
    model: torch.nn.Module,
    output_path: Path,
    embedding_dim: int,
) -> Path:
    """Export a trained NCF model to ONNX format."""
    model.eval()
    device = next(model.parameters()).device

    dummy_user = torch.tensor([0], dtype=torch.long, device=device)
    dummy_dish = torch.tensor([0], dtype=torch.long, device=device)

    onnx_path = output_path / "ncf_model.onnx"
    onnx_path.parent.mkdir(parents=True, exist_ok=True)

    torch.onnx.export(
        model,
        (dummy_user, dummy_dish),
        str(onnx_path),
        input_names=["user_id", "dish_id"],
        output_names=["predicted_rating"],
        dynamic_axes={
            "user_id": {0: "batch"},
            "dish_id": {0: "batch"},
            "predicted_rating": {0: "batch"},
        },
        opset_version=17,
    )

    logger.info("ONNX model exported to %s", onnx_path)
    return onnx_path

def upload_onnx_to_r2(s3_client, bucket: str, onnx_path: Path, version: str) -> str:
    """Upload ONNX model to R2 and return the key."""
    key = f"ncf/{version}/ncf_model.onnx"
    s3_client.upload_file(str(onnx_path), bucket, key)
    logger.info("Uploaded ONNX model to R2: %s", key)
    return key
