#!/usr/bin/env python3
"""
Fine-tuning HerBERT dla hate speech detection.
Uruchamiany na Vertex AI Custom Training.

Usage:
    python tools/herbert/train.py \
        --train_data gs://<BUCKET>/datasets/train.csv \
        --val_data gs://<BUCKET>/datasets/val.csv \
        --output_dir gs://<BUCKET>/models/v1
"""

import argparse

def download_from_gcs(gcs_path: str, local_path: str):
    """Pobiera plik z GCS."""
    from google.cloud import storage

    bucket_name = gcs_path.split("/")[2]
    blob_path = "/".join(gcs_path.split("/")[3:])

    client = storage.Client()
    bucket = client.bucket(bucket_name)
    blob = bucket.blob(blob_path)
    blob.download_to_filename(local_path)
    print(f"Downloaded {gcs_path} -> {local_path}")

def upload_to_gcs(local_path: str, gcs_path: str):
    """Uploaduje plik/folder do GCS."""
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

def compute_metrics(eval_pred):
    """Oblicza metryki ewaluacji."""
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

def main():
    parser = argparse.ArgumentParser(
        description="Fine-tuning HerBERT dla hate speech detection (Vertex AI)."
    )
    parser.add_argument("--train_data", type=str, required=True,
                        help="Sciezka GCS do train.csv (gs://<BUCKET>/datasets/train.csv)")
    parser.add_argument("--val_data", type=str, required=True,
                        help="Sciezka GCS do val.csv (gs://<BUCKET>/datasets/val.csv)")
    parser.add_argument("--output_dir", type=str, required=True,
                        help="Sciezka GCS do zapisu modelu (gs://<BUCKET>/models/v1)")
    parser.add_argument("--model_name", type=str, default="allegro/herbert-base-cased",
                        help="Nazwa modelu bazowego (domyslnie: allegro/herbert-base-cased)")
    parser.add_argument("--epochs", type=int, default=3,
                        help="Liczba epok (domyslnie: 3)")
    parser.add_argument("--batch_size", type=int, default=16,
                        help="Batch size (domyslnie: 16)")
    parser.add_argument("--learning_rate", type=float, default=2e-5,
                        help="Learning rate (domyslnie: 2e-5)")
    parser.add_argument("--max_length", type=int, default=256,
                        help="Maksymalna dlugosc tokenizacji (domyslnie: 256)")
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
    from datasets import Dataset

    # === 1. Pobierz dane z GCS ===
    os.makedirs("/tmp/data", exist_ok=True)
    download_from_gcs(args.train_data, "/tmp/data/train.csv")
    download_from_gcs(args.val_data, "/tmp/data/val.csv")

    # === 2. Zaladuj dane ===
    train_df = pd.read_csv("/tmp/data/train.csv")
    val_df = pd.read_csv("/tmp/data/val.csv")

    print(f"Train size: {len(train_df)}")
    print(f"Val size: {len(val_df)}")

    # === 3. Tokenizer ===
    tokenizer = AutoTokenizer.from_pretrained(args.model_name)

    def tokenize_function(examples):
        return tokenizer(
            examples["text"],
            padding="max_length",
            truncation=True,
            max_length=args.max_length,
        )

    # === 4. Przygotuj datasety ===
    train_dataset = Dataset.from_pandas(train_df)
    val_dataset = Dataset.from_pandas(val_df)

    train_dataset = train_dataset.map(tokenize_function, batched=True)
    val_dataset = val_dataset.map(tokenize_function, batched=True)

    train_dataset.set_format("torch", columns=["input_ids", "attention_mask", "label"])
    val_dataset.set_format("torch", columns=["input_ids", "attention_mask", "label"])

    # === 5. Model ===
    model = AutoModelForSequenceClassification.from_pretrained(
        args.model_name,
        num_labels=2,
        id2label={0: "neutral", 1: "toxic"},
        label2id={"neutral": 0, "toxic": 1},
    )

    # === 6. Training Arguments ===
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

    # === 7. Trainer ===
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=train_dataset,
        eval_dataset=val_dataset,
        compute_metrics=compute_metrics,
        callbacks=[EarlyStoppingCallback(early_stopping_patience=2)],
    )

    # === 8. Trenuj ===
    print("Starting training...")
    trainer.train()

    # === 9. Ewaluacja finalna ===
    print("Final evaluation...")
    eval_results = trainer.evaluate()
    print(f"Eval results: {eval_results}")

    # === 10. Zapisz model ===
    local_output = "/tmp/final_model"
    trainer.save_model(local_output)
    tokenizer.save_pretrained(local_output)

    with open(f"{local_output}/metrics.txt", "w") as f:
        for key, value in eval_results.items():
            f.write(f"{key}: {value}\n")

    # === 11. Upload do GCS ===
    print(f"Uploading model to {args.output_dir}...")
    upload_to_gcs(local_output, args.output_dir)

    print("Training completed!")

if __name__ == "__main__":
    main()
