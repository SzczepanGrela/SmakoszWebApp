#!/usr/bin/env python3
"""
Przygotowanie datasetu BAN-PL do fine-tuningu HerBERT.

Usage:
    python tools/herbert/prepare_dataset.py --input BAN-PL/data/BAN_PL_1.csv
    python tools/herbert/prepare_dataset.py --input data.csv --output-dir ./splits --test-size 0.15
"""

import argparse

def main():
    parser = argparse.ArgumentParser(
        description="Przygotuj dataset BAN-PL do fine-tuningu HerBERT (train/val split)."
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
        help="Katalog wyjsciowy dla train.csv i val.csv (domyslnie: biezacy katalog)",
    )
    parser.add_argument(
        "--test-size",
        type=float,
        default=0.2,
        help="Udzial zbioru walidacyjnego (domyslnie: 0.2)",
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

    # --- Podzial train/val ze stratyfikacja ---
    train_df, val_df = train_test_split(
        df[["text", "label"]],
        test_size=args.test_size,
        random_state=42,
        stratify=df["label"],
    )

    print(f"\nTrain: {len(train_df)}, Val: {len(val_df)}")

    # --- Zapis ---
    train_path = f"{args.output_dir}/train.csv"
    val_path = f"{args.output_dir}/val.csv"
    train_df.to_csv(train_path, index=False)
    val_df.to_csv(val_path, index=False)
    print(f"Zapisano {train_path} i {val_path}")

if __name__ == "__main__":
    main()
