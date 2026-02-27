#!/usr/bin/env python3
"""
Test fine-tuned HerBERT - inference identyczny z text_moderator.py.

Uzywa torch.sigmoid (nie softmax), probs[1] jako toxicity score.
Logika zgodna z gpu-worker/inference/text_moderator.py:42-46.

Usage:
    python tools/herbert/test_model.py
    python tools/herbert/test_model.py --model-path ./herbert-v1
"""

import argparse

TEST_TEXTS = [
    "Swietna pizza, polecam wszystkim!",
    "Obsluga byla troche powolna, ale jedzenie smaczne",
    "To jest obrzydliwe, nie jedz tam",
    "Wszyscy kelnerzy to zlodzieje",
    "Romantyczna atmosfera, idealne na randke",
]

def main():
    parser = argparse.ArgumentParser(
        description="Test fine-tuned HerBERT - inference identyczny z text_moderator.py."
    )
    parser.add_argument(
        "--model-path",
        type=str,
        default="./herbert-v1",
        help="Sciezka do katalogu z modelem (domyslnie: ./herbert-v1)",
    )
    args = parser.parse_args()

    import torch
    from transformers import AutoTokenizer, AutoModelForSequenceClassification

    tokenizer = AutoTokenizer.from_pretrained(args.model_path)
    model = AutoModelForSequenceClassification.from_pretrained(
        args.model_path, num_labels=2
    )
    model.eval()

    for text in TEST_TEXTS:
        inputs = tokenizer(
            text, return_tensors="pt", truncation=True, max_length=256, padding=True
        )

        with torch.no_grad():
            outputs = model(**inputs)
            logits = outputs.logits
            probs = torch.sigmoid(logits[0])
            toxicity_score = probs[1].item() if probs.shape[0] > 1 else probs[0].item()

        toxicity_score = round(toxicity_score, 4)
        if toxicity_score >= 0.8:
            verdict = "TOXIC"
        elif toxicity_score <= 0.3:
            verdict = "OK"
        else:
            verdict = "REVIEW"

        print(f"[{verdict}] ({toxicity_score:.4f}) {text}")

if __name__ == "__main__":
    main()
