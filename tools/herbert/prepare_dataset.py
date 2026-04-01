#!/usr/bin/env python3
import argparse

def main():
    parser = argparse.ArgumentParser(description="Prepare BAN-PL dataset (train/val/test split)")
    parser.add_argument("--input", type=str, required=True, help="Path to BAN-PL CSV file")
    parser.add_argument("--output-dir", type=str, default=".", help="Output directory for split CSVs")
    parser.add_argument("--val-size", type=float, default=0.1, help="Validation set ratio (default: 0.1)")
    parser.add_argument("--test-size", type=float, default=0.1, help="Test set ratio (default: 0.1)")
    args = parser.parse_args()

    import pandas as pd
    from sklearn.model_selection import train_test_split

    df = pd.read_csv(args.input)

    print(f"Shape: {df.shape}")
    print(f"Columns: {df.columns.tolist()}")
    print(f"First rows:\n{df.head()}")
    print(f"\nValue counts:\n{df.iloc[:, -1].value_counts()}")

    data = df[["Text", "Class"]]

    holdout_size = args.val_size + args.test_size
    train_df, holdout_df = train_test_split(
        data,
        test_size=holdout_size,
        random_state=42,
        stratify=data["Class"],
    )

    val_ratio = args.val_size / holdout_size
    val_df, test_df = train_test_split(
        holdout_df,
        test_size=1.0 - val_ratio,
        random_state=42,
        stratify=holdout_df["Class"],
    )

    print(f"\nTrain: {len(train_df)}, Val: {len(val_df)}, Test: {len(test_df)}")

    train_path = f"{args.output_dir}/train.csv"
    val_path = f"{args.output_dir}/val.csv"
    test_path = f"{args.output_dir}/test.csv"
    train_df.to_csv(train_path, index=False)
    val_df.to_csv(val_path, index=False)
    test_df.to_csv(test_path, index=False)
    print(f"Saved {train_path}, {val_path}, {test_path}")

if __name__ == "__main__":
    main()
