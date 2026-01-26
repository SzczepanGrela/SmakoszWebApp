"""
GPU Validation Script for CFTrainer

Checks ROCm/CUDA availability and GPU capabilities before training.
Run this before starting training to ensure GPU is properly configured.
"""

import sys
import os
from typing import Dict, Any, Optional

def check_rocm_installation() -> Dict[str, Any]:
    """Check if ROCm is installed and accessible"""
    result = {
        'rocm_installed': False,
        'rocm_version': None,
        'rocm_path': None
    }

    # Check ROCm path
    rocm_path = os.environ.get('ROCM_PATH', '/opt/rocm')
    if os.path.exists(rocm_path):
        result['rocm_installed'] = True
        result['rocm_path'] = rocm_path

        # Try to get version
        version_file = os.path.join(rocm_path, '.info', 'version')
        if os.path.exists(version_file):
            with open(version_file, 'r') as f:
                result['rocm_version'] = f.read().strip()

    return result

def check_pytorch_gpu() -> Dict[str, Any]:
    """Check PyTorch GPU/CUDA availability"""
    result = {
        'pytorch_version': None,
        'cuda_available': False,
        'cuda_version': None,
        'device_count': 0,
        'devices': []
    }

    try:
        import torch
        result['pytorch_version'] = torch.__version__
        result['cuda_available'] = torch.cuda.is_available()

        if result['cuda_available']:
            result['cuda_version'] = torch.version.cuda or 'ROCm'
            result['device_count'] = torch.cuda.device_count()

            for i in range(result['device_count']):
                props = torch.cuda.get_device_properties(i)
                device_info = {
                    'index': i,
                    'name': props.name,
                    'total_memory_gb': round(props.total_memory / 1e9, 2),
                    'multi_processor_count': props.multi_processor_count,
                    'major': props.major,
                    'minor': props.minor
                }
                result['devices'].append(device_info)

    except ImportError:
        result['error'] = 'PyTorch not installed'
    except Exception as e:
        result['error'] = str(e)

    return result

def check_memory_available() -> Dict[str, Any]:
    """Check available GPU memory"""
    result = {
        'memory_checks': []
    }

    try:
        import torch
        if torch.cuda.is_available():
            for i in range(torch.cuda.device_count()):
                torch.cuda.set_device(i)
                total = torch.cuda.get_device_properties(i).total_memory
                allocated = torch.cuda.memory_allocated(i)
                cached = torch.cuda.memory_reserved(i)
                free = total - allocated - cached

                result['memory_checks'].append({
                    'device': i,
                    'total_gb': round(total / 1e9, 2),
                    'allocated_gb': round(allocated / 1e9, 2),
                    'cached_gb': round(cached / 1e9, 2),
                    'free_gb': round(free / 1e9, 2)
                })
    except Exception as e:
        result['error'] = str(e)

    return result

def test_gpu_computation() -> Dict[str, Any]:
    """Test basic GPU computation"""
    result = {
        'test_passed': False,
        'computation_time_ms': None
    }

    try:
        import torch
        import time

        if not torch.cuda.is_available():
            result['error'] = 'CUDA not available'
            return result

        # Simple matrix multiplication test
        device = torch.device('cuda:0')

        # Warmup
        a = torch.randn(1000, 1000, device=device)
        b = torch.randn(1000, 1000, device=device)
        c = torch.matmul(a, b)
        torch.cuda.synchronize()

        # Timed test
        start = time.perf_counter()
        for _ in range(10):
            c = torch.matmul(a, b)
        torch.cuda.synchronize()
        end = time.perf_counter()

        result['test_passed'] = True
        result['computation_time_ms'] = round((end - start) * 100, 2)  # per operation

        # Cleanup
        del a, b, c
        torch.cuda.empty_cache()

    except Exception as e:
        result['error'] = str(e)

    return result

def estimate_batch_size(model_memory_mb: int = 500,
                        sample_memory_mb: float = 0.1) -> Dict[str, Any]:
    """Estimate maximum batch size based on available memory"""
    result = {
        'recommended_batch_size': None,
        'max_batch_size': None
    }

    try:
        import torch
        if torch.cuda.is_available():
            props = torch.cuda.get_device_properties(0)
            total_memory_mb = props.total_memory / 1e6

            # Reserve 20% for safety
            available_mb = total_memory_mb * 0.8 - model_memory_mb

            max_batch = int(available_mb / sample_memory_mb)
            recommended_batch = min(max_batch, 8192)  # Cap at 8192

            # Round to power of 2
            recommended_batch = 2 ** int(recommended_batch).bit_length() // 2

            result['max_batch_size'] = max_batch
            result['recommended_batch_size'] = recommended_batch
            result['available_memory_mb'] = round(available_mb, 0)

    except Exception as e:
        result['error'] = str(e)

    return result

def run_full_validation() -> Dict[str, Any]:
    """Run all validation checks"""
    print("=" * 60)
    print("CFTrainer GPU Validation")
    print("=" * 60)

    results = {}
    all_passed = True

    # 1. ROCm Installation
    print("\n[1/5] Checking ROCm installation...")
    rocm = check_rocm_installation()
    results['rocm'] = rocm
    if rocm['rocm_installed']:
        print(f"  ✅ ROCm found at: {rocm['rocm_path']}")
        if rocm['rocm_version']:
            print(f"  ✅ ROCm version: {rocm['rocm_version']}")
    else:
        print("  ⚠️  ROCm not found (may use CUDA instead)")

    # 2. PyTorch GPU
    print("\n[2/5] Checking PyTorch GPU support...")
    pytorch = check_pytorch_gpu()
    results['pytorch'] = pytorch
    if pytorch.get('error'):
        print(f"  ❌ Error: {pytorch['error']}")
        all_passed = False
    elif pytorch['cuda_available']:
        print(f"  ✅ PyTorch version: {pytorch['pytorch_version']}")
        print(f"  ✅ CUDA/ROCm available: Yes")
        print(f"  ✅ Device count: {pytorch['device_count']}")
        for dev in pytorch['devices']:
            print(f"     GPU {dev['index']}: {dev['name']} ({dev['total_memory_gb']} GB)")
    else:
        print(f"  ❌ CUDA not available - will use CPU (slow!)")
        all_passed = False

    # 3. Memory Check
    print("\n[3/5] Checking GPU memory...")
    memory = check_memory_available()
    results['memory'] = memory
    if memory.get('error'):
        print(f"  ❌ Error: {memory['error']}")
    else:
        for mem in memory['memory_checks']:
            print(f"  ✅ GPU {mem['device']}: {mem['free_gb']:.1f} GB free / {mem['total_gb']:.1f} GB total")

    # 4. Computation Test
    print("\n[4/5] Testing GPU computation...")
    compute = test_gpu_computation()
    results['compute'] = compute
    if compute['test_passed']:
        print(f"  ✅ Matrix multiplication test passed")
        print(f"  ✅ Avg time per 1000x1000 matmul: {compute['computation_time_ms']:.2f} ms")
    else:
        print(f"  ❌ Test failed: {compute.get('error', 'Unknown error')}")
        all_passed = False

    # 5. Batch Size Recommendation
    print("\n[5/5] Estimating optimal batch size...")
    batch = estimate_batch_size()
    results['batch'] = batch
    if batch['recommended_batch_size']:
        print(f"  ✅ Recommended batch size: {batch['recommended_batch_size']}")
        print(f"  ✅ Max theoretical batch size: {batch['max_batch_size']}")
    else:
        print(f"  ⚠️  Could not estimate batch size")

    # Summary
    print("\n" + "=" * 60)
    if all_passed:
        print("✅ ALL CHECKS PASSED - Ready for training!")
        print(f"\nRecommended command:")
        batch_size = batch.get('recommended_batch_size', 4096)
        print(f"  python train.py --batch-size {batch_size}")
    else:
        print("❌ SOME CHECKS FAILED")
        print("\nTroubleshooting:")
        print("  1. Verify ROCm installation: rocm-smi")
        print("  2. Reinstall PyTorch: pip install torch --index-url https://download.pytorch.org/whl/rocm6.2")
        print("  3. Check GPU drivers: dmesg | grep -i amdgpu")
    print("=" * 60)

    results['all_passed'] = all_passed
    return results

if __name__ == "__main__":
    results = run_full_validation()
    sys.exit(0 if results['all_passed'] else 1)
