#!/usr/bin/env python3
"""
Przygotowanie datasetu BAN-PL do fine-tuningu HerBERT.

Podział 80/10/10 (train/val/test) ze stratyfikacją:
  - train: trening modelu
  - val: early stopping i dobór hiperparametrów
  - test: finalna ewaluacja (nigdy nie wpływa na trening)

Usage:
    python tools/herbert/prepare_dataset.py --input BAN-PL/data/BAN_PL_1.csv
    python tools/herbert/prepare_dataset.py --input data.csv --output-dir ./splits --val-size 0.1 --test-size 0.1
"""

import argparse

def main():
    parser = argparse.ArgumentParser(
        description="Przygotuj dataset BAN-PL do fine-tuningu HerBERT (train/val/test split)."
    )
    parser.add_argument(
        "--input",
        type=str,
        required=True,
        help="Sciezka do pliku CSV z danymi BAN-PL",
    )
    parser.add_argument(
        "--output-dir",
        type=str,
        default=".",
        help="Katalog wyjsciowy dla train.csv, val.csv i test.csv (domyslnie: biezacy katalog)",
    )
    parser.add_argument(
        "--val-size",
        type=float,
        default=0.1,
        help="Udzial zbioru walidacyjnego (domyslnie: 0.1)",
    )
    parser.add_argument(
        "--test-size",
        type=float,
        default=0.1,
        help="Udzial zbioru testowego (domyslnie: 0.1)",
    )
    args = parser.parse_args()

    import pandas as pd
    from sklearn.model_selection import train_test_split

    # --- Zaladuj dane ---
    df = pd.read_csv(args.input)

    print(f"Shape: {df.shape}")
    print(f"Columns: {df.columns.tolist()}")
    print(f"First rows:\n{df.head()}")
    print(f"\nValue counts:\n{df.iloc[:, -1].value_counts()}")

    # --- Rename kolumn do text/label (dostosuj nazwy po inspekcji) ---
    # df = df.rename(columns={"oryginalna_kolumna_tekst": "text", "oryginalna_kolumna_label": "label"})
    # df = df[["text", "label"]]

    data = df[["text", "label"]]

    # --- Podzial train / (val+test) ze stratyfikacja ---
    holdout_size = args.val_size + args.test_size
    train_df, holdout_df = train_test_split(
        data,
        test_size=holdout_size,
        random_state=42,
        stratify=data["label"],
    )

    # --- Podzial (val+test) na val i test ---
    val_ratio = args.val_size / holdout_size
    val_df, test_df = train_test_split(
        holdout_df,
        test_size=1.0 - val_ratio,
        random_state=42,
        stratify=holdout_df["label"],
    )

    print(f"\nTrain: {len(train_df)}, Val: {len(val_df)}, Test: {len(test_df)}")

    # --- Zapis ---
    train_path = f"{args.output_dir}/train.csv"
    val_path = f"{args.output_dir}/val.csv"
    test_path = f"{args.output_dir}/test.csv"
    train_df.to_csv(train_path, index=False)
    val_df.to_csv(val_path, index=False)
    test_df.to_csv(test_path, index=False)
    print(f"Zapisano {train_path}, {val_path} i {test_path}")

if __name__ == "__main__":
    main()
