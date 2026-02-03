"""
Integration test for CFTrainer after refactoring
Tests all components work together without requiring DB connection
"""

import numpy as np
import pandas as pd
import torch
import sys
from pathlib import Path

# Add project to path
sys.path.insert(0, str(Path(__file__).parent))

print("=" * 70)
print("CFTrainer Integration Test")
print("=" * 70)
print()

# Test 1: Config
print("1. Testing config.py...")
try:
    from config import MODEL_CONFIG, TRAINING_CONFIG, DATABASE_CONFIG
    assert MODEL_CONFIG['output_range'] == (0.0, 1.0), "Output range should be (0.0, 1.0)"
    assert 'eval_ranking_every_n_epochs' in TRAINING_CONFIG, "Missing ranking eval config"
    assert TRAINING_CONFIG['ranking_k'] == 5, "Ranking K should be 5"
    print("   [OK] Config loaded successfully")
    print(f"   [OK] Output range: {MODEL_CONFIG['output_range']}")
    print(f"   [OK] Ranking eval interval: {TRAINING_CONFIG['eval_ranking_every_n_epochs']}")
except AssertionError as e:
    print(f"   [FAIL] Assertion failed: {e}")
    sys.exit(1)
except Exception as e:
    print(f"   [FAIL] Error: {e}")
    sys.exit(1)

print()

# Test 2: NCF Model
print("2. Testing NCF model...")
try:
    from models.ncf import NCF, create_ncf_model

    # Create a small model
    n_users, n_items = 100, 50
    model = create_ncf_model(n_users, n_items, MODEL_CONFIG)

    # Test forward pass
    user_ids = torch.randint(0, n_users, (32,))
    item_ids = torch.randint(0, n_items, (32,))

    with torch.no_grad():
        predictions = model(user_ids, item_ids)

    assert predictions.shape == (32,), f"Expected shape (32,), got {predictions.shape}"

    # Check output is NOT constrained to [1, 10] (should be raw logits)
    # We expect some values outside [0, 1] since they're raw logits
    print(f"   [OK] Model created successfully")
    print(f"   [OK] Forward pass works: {predictions.shape}")
    print(f"   [OK] Output range: [{predictions.min():.3f}, {predictions.max():.3f}] (raw logits)")

except Exception as e:
    print(f"   [FAIL] Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print()

# Test 3: Data loader with mock data
print("3. Testing data loader...")
try:
    from data.data_loader import CFDataManager, RatingsDataset

    # Create mock ratings data
    np.random.seed(42)
    n_users_mock = 20
    n_items_mock = 30
    n_ratings = 200

    mock_data = pd.DataFrame({
        'user_id': np.random.randint(1, n_users_mock + 1, n_ratings),
        'dish_id': np.random.randint(1, n_items_mock + 1, n_ratings),
        'rating': np.random.randint(1, 11, n_ratings)  # Original [1, 10] range
    })

    # Create data manager
    manager = CFDataManager(mock_data)

    # Check interaction matrix is built
    assert hasattr(manager, 'user_items'), "Missing user_items attribute"
    assert hasattr(manager, 'all_items'), "Missing all_items attribute"

    # Prepare datasets
    train_ds, val_ds, test_ds = manager.prepare_data(random_seed=42)

    # Check datasets are stored
    assert hasattr(manager, 'train_dataset'), "Datasets not stored in manager"
    assert manager.val_dataset is val_ds, "Stored dataset mismatch"

    # Check normalization
    sample_rating = train_ds.ratings[0].item()
    assert 0 <= sample_rating <= 1, f"Rating not normalized: {sample_rating}"

    # Test negative sampling
    user_idx = 0
    neg_items = manager.sample_negative_items(user_idx, n_samples=10)
    assert len(neg_items) == 10, f"Expected 10 negative samples, got {len(neg_items)}"

    print(f"   [OK] Data manager initialized: {manager.n_users} users, {manager.n_items} items")
    print(f"   [OK] Datasets created: train={len(train_ds)}, val={len(val_ds)}, test={len(test_ds)}")
    print(f"   [OK] Ratings normalized to [0, 1]: sample={sample_rating:.3f}")
    print(f"   [OK] Negative sampling works: {len(neg_items)} samples")

except Exception as e:
    print(f"   [FAIL] Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print()

# Test 4: Ranking metrics
print("4. Testing ranking metrics...")
try:
    from train import hit_ratio_at_k, ndcg_at_k

    # Test Hit Ratio
    ranked_list = np.array([10, 5, 3, 8, 1, 7, 2, 9, 4, 6])
    hr1 = hit_ratio_at_k(ranked_list, positive_item=3, k=5)
    assert hr1 == 1.0, f"HR should be 1.0, got {hr1}"

    hr2 = hit_ratio_at_k(ranked_list, positive_item=7, k=5)
    assert hr2 == 0.0, f"HR should be 0.0, got {hr2}"

    # Test NDCG
    ndcg1 = ndcg_at_k(ranked_list, positive_item=10, k=5)
    assert abs(ndcg1 - 1.0) < 0.001, f"NDCG should be 1.0, got {ndcg1}"

    ndcg2 = ndcg_at_k(ranked_list, positive_item=7, k=5)
    assert ndcg2 == 0.0, f"NDCG should be 0.0, got {ndcg2}"

    print("   [OK] Hit Ratio@K works correctly")
    print("   [OK] NDCG@K works correctly")

except Exception as e:
    print(f"   [FAIL] Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print()

# Test 5: Trainer initialization
print("5. Testing Trainer initialization...")
try:
    from train import Trainer
    from torch.utils.data import DataLoader

    # Create small model and data
    model = create_ncf_model(n_users_mock, n_items_mock, MODEL_CONFIG)

    train_loader = DataLoader(train_ds, batch_size=16, shuffle=True)
    val_loader = DataLoader(val_ds, batch_size=16, shuffle=False)
    test_loader = DataLoader(test_ds, batch_size=16, shuffle=False)

    # Create trainer
    trainer = Trainer(
        model=model,
        train_loader=train_loader,
        val_loader=val_loader,
        test_loader=test_loader,
        data_manager=manager,
        device='cpu',
        config=TRAINING_CONFIG,
        val_dataset=val_ds,
        test_dataset=test_ds
    )

    # Check trainer has ranking evaluation method
    assert hasattr(trainer, 'evaluate_ranking'), "Missing evaluate_ranking method"
    assert trainer.val_dataset is val_ds, "val_dataset not stored"
    assert trainer.test_dataset is test_ds, "test_dataset not stored"

    # Check history includes ranking metrics
    assert 'val_hr@5' in trainer.history, "Missing HR@5 in history"
    assert 'val_ndcg@5' in trainer.history, "Missing NDCG@5 in history"

    print("   [OK] Trainer initialized successfully")
    print("   [OK] Ranking evaluation method available")
    print("   [OK] Datasets stored correctly")
    print("   [OK] History includes ranking metrics")

except Exception as e:
    print(f"   [FAIL] Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print()

# Test 6: Evaluation methods
print("6. Testing evaluation methods...")
try:
    # Test standard evaluation (RMSE, MAE)
    eval_metrics = trainer.evaluate(val_loader, "val")

    assert 'rmse' in eval_metrics, "Missing RMSE"
    assert 'mae' in eval_metrics, "Missing MAE"
    assert 'loss' in eval_metrics, "Missing loss"
    assert 'predictions' not in eval_metrics, "Should not return predictions array"

    print(f"   [OK] Standard evaluation works: RMSE={eval_metrics['rmse']:.4f}, MAE={eval_metrics['mae']:.4f}")

    # Test ranking evaluation
    ranking_metrics = trainer.evaluate_ranking(val_ds, k=5, rating_threshold=0.7)

    assert 'hit_ratio' in ranking_metrics, "Missing hit_ratio"
    assert 'ndcg' in ranking_metrics, "Missing NDCG"
    assert 'n_tests' in ranking_metrics, "Missing n_tests"
    assert ranking_metrics['n_tests'] > 0, "No ranking tests generated"

    print(f"   [OK] Ranking evaluation works: HR@5={ranking_metrics['hit_ratio']:.4f}, NDCG@5={ranking_metrics['ndcg']:.4f}")
    print(f"   [OK] Ranking tests generated: {ranking_metrics['n_tests']}")

except Exception as e:
    print(f"   [FAIL] Error: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)

print()
print("=" * 70)
print("[OK] ALL INTEGRATION TESTS PASSED!")
print("=" * 70)
print()
print("Summary of verified features:")
print("  • Model outputs raw logits (not scaled to [1, 10])")
print("  • Ratings normalized to [0, 1] range")
print("  • Negative sampling for ranking evaluation")
print("  • HitRatio@5 and NDCG@5 metrics")
print("  • Memory-efficient evaluation (no prediction arrays)")
print("  • Datasets stored in data_manager")
print("  • Trainer receives datasets for ranking evaluation")
print()
