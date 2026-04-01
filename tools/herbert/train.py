#!/usr/bin/env python3
import argparse
import sys

sys.stdout.reconfigure(line_buffering=True)
sys.stderr.reconfigure(line_buffering=True)

def download_from_gcs(gcs_path: str, local_path: str):
    from google.cloud import storage

    bucket_name = gcs_path.split("/")[2]
    blob_path = "/".join(gcs_path.split("/")[3:])

    client = storage.Client()
    bucket = client.bucket(bucket_name)
    blob = bucket.blob(blob_path)
    blob.download_to_filename(local_path)
    print(f"Downloaded {gcs_path} -> {local_path}")

def upload_to_gcs(local_path: str, gcs_path: str):
    from pathlib import Path
    from google.cloud import storage

    bucket_name = gcs_path.split("/")[2]
    blob_prefix = "/".join(gcs_path.split("/")[3:])

    client = storage.Client()
    bucket = client.bucket(bucket_name)

    local_path = Path(local_path)
    if local_path.is_file():
        blob = bucket.blob(blob_prefix)
        blob.upload_from_filename(str(local_path))
        print(f"Uploaded {local_path} -> {gcs_path}")
    else:
        for file_path in local_path.rglob("*"):
            if file_path.is_file():
                blob_path = f"{blob_prefix}/{file_path.relative_to(local_path)}"
                blob = bucket.blob(blob_path)
                blob.upload_from_filename(str(file_path))
                print(f"Uploaded {file_path}")

def get_secret(secret_id: str) -> str:
    from google.cloud import secretmanager

    client = secretmanager.SecretManagerServiceClient()
    name = f"projects/smakoszherbert/secrets/{secret_id}/versions/latest"
    response = client.access_secret_version(request={"name": name})
    return response.payload.data.decode("UTF-8").strip()

def upload_to_r2(local_path: str, bucket: str, prefix: str):
    from pathlib import Path
    import boto3

    s3 = boto3.client(
        "s3",
        endpoint_url=get_secret("r2-endpoint"),
        aws_access_key_id=get_secret("r2-access-key"),
        aws_secret_access_key=get_secret("r2-secret-key"),
    )

    skip = {"training_args.bin", "metrics.txt"}
    local_path = Path(local_path)
    for file_path in local_path.rglob("*"):
        if file_path.is_file() and file_path.name not in skip:
            key = f"{prefix}/{file_path.relative_to(local_path)}"
            s3.upload_file(str(file_path), bucket, key)
            print(f"R2: uploaded {key}")

def compute_metrics(eval_pred):
    from sklearn.metrics import accuracy_score, precision_recall_fscore_support

    predictions, labels = eval_pred
    predictions = predictions.argmax(axis=-1)

    accuracy = accuracy_score(labels, predictions)
    precision, recall, f1, _ = precision_recall_fscore_support(
        labels, predictions, average="binary"
    )

    return {
        "accuracy": accuracy,
        "precision": precision,
        "recall": recall,
        "f1": f1,
    }

class TokenizedDataset:
    def __init__(self, texts, labels, tokenizer, max_length):
        self.encodings = tokenizer(
            texts, padding="max_length", truncation=True, max_length=max_length
        )
        self.labels = labels

    def __len__(self):
        return len(self.labels)

    def __getitem__(self, idx):
        import torch

        item = {k: torch.tensor(v[idx]) for k, v in self.encodings.items()}
        item["labels"] = torch.tensor(self.labels[idx])
        return item

def main():
    parser = argparse.ArgumentParser(description="Fine-tune HerBERT for toxicity detection (Vertex AI)")
    parser.add_argument("--train_data", type=str, required=True, help="GCS path to train.csv")
    parser.add_argument("--val_data", type=str, required=True, help="GCS path to val.csv")
    parser.add_argument("--test_data", type=str, default=None, help="GCS path to test.csv")
    parser.add_argument("--output_dir", type=str, required=True, help="GCS path for model output")
    parser.add_argument("--model_name", type=str, default="allegro/herbert-base-cased")
    parser.add_argument("--epochs", type=int, default=3)
    parser.add_argument("--batch_size", type=int, default=16)
    parser.add_argument("--learning_rate", type=float, default=2e-5)
    parser.add_argument("--max_length", type=int, default=256)
    parser.add_argument("--r2_bucket", type=str, default="smakosz-models", help="R2 bucket name")
    parser.add_argument("--r2_prefix", type=str, default=None, help="R2 prefix, e.g. herbert/v4 (requires GCP secrets: r2-endpoint, r2-access-key, r2-secret-key)")
    args = parser.parse_args()

    import os
    import torch
    import pandas as pd
    from transformers import (
        AutoTokenizer,
        AutoModelForSequenceClassification,
        TrainingArguments,
        Trainer,
        EarlyStoppingCallback,
    )

    os.makedirs("/tmp/data", exist_ok=True)
    download_from_gcs(args.train_data, "/tmp/data/train.csv")
    download_from_gcs(args.val_data, "/tmp/data/val.csv")
    if args.test_data:
        download_from_gcs(args.test_data, "/tmp/data/test.csv")

    column_map = {"Text": "text", "Class": "label"}
    train_df = pd.read_csv("/tmp/data/train.csv").rename(columns=column_map)
    val_df = pd.read_csv("/tmp/data/val.csv").rename(columns=column_map)
    test_df = pd.read_csv("/tmp/data/test.csv").rename(columns=column_map) if args.test_data else None

    print(f"Train size: {len(train_df)}")
    print(f"Val size: {len(val_df)}")
    if test_df is not None:
        print(f"Test size: {len(test_df)}")

    print("Loading tokenizer...")
    tokenizer = AutoTokenizer.from_pretrained(args.model_name)

    print("Tokenizing train set...")
    train_dataset = TokenizedDataset(
        train_df["text"].tolist(), train_df["label"].tolist(), tokenizer, args.max_length
    )
    print("Tokenizing val set...")
    val_dataset = TokenizedDataset(
        val_df["text"].tolist(), val_df["label"].tolist(), tokenizer, args.max_length
    )
    test_dataset = None
    if test_df is not None:
        print("Tokenizing test set...")
        test_dataset = TokenizedDataset(
            test_df["text"].tolist(), test_df["label"].tolist(), tokenizer, args.max_length
        )

    print("Loading model...")
    model = AutoModelForSequenceClassification.from_pretrained(
        args.model_name,
        num_labels=2,
        id2label={0: "neutral", 1: "toxic"},
        label2id={"neutral": 0, "toxic": 1},
    )

    print("Configuring trainer...")
    training_args = TrainingArguments(
        output_dir="/tmp/checkpoints",
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        per_device_eval_batch_size=args.batch_size * 2,
        learning_rate=args.learning_rate,
        weight_decay=0.01,
        warmup_ratio=0.1,
        eval_strategy="epoch",
        save_strategy="epoch",
        load_best_model_at_end=True,
        metric_for_best_model="f1",
        greater_is_better=True,
        fp16=torch.cuda.is_available(),
        gradient_accumulation_steps=2,
        logging_dir="/tmp/logs",
        logging_steps=50,
        report_to="none",
    )

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
        compute_metrics=compute_metrics,
        callbacks=[EarlyStoppingCallback(early_stopping_patience=2)],
    )

    print("Starting training...")
    trainer.train()

    print("Validation evaluation...")
    val_results = trainer.evaluate(eval_dataset=val_dataset)
    print(f"Val results: {val_results}")

    test_results = {}
    if test_dataset is not None:
        print("Test evaluation (held-out)...")
        test_results = trainer.evaluate(eval_dataset=test_dataset, metric_key_prefix="test")
        print(f"Test results: {test_results}")

    local_output = "/tmp/final_model"
    trainer.save_model(local_output)
    tokenizer.save_pretrained(local_output)

    with open(f"{local_output}/metrics.txt", "w") as f:
        f.write("=== Validation ===\n")
        for key, value in val_results.items():
            f.write(f"{key}: {value}\n")
        if test_results:
            f.write("\n=== Test (held-out) ===\n")
            for key, value in test_results.items():
                f.write(f"{key}: {value}\n")

    print(f"Uploading model to {args.output_dir}...")
    upload_to_gcs(local_output, args.output_dir)

    if args.r2_prefix:
        print(f"Uploading model to R2 ({args.r2_bucket}/{args.r2_prefix})...")
        upload_to_r2(local_output, args.r2_bucket, args.r2_prefix)
    else:
        print("--r2_prefix not set, skipping R2 upload.")

    print("Training completed!")

if __name__ == "__main__":
    main()
