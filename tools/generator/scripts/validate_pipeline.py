#!/usr/bin/env python3
"""
Validate pipeline dependency graph.

Checks for:
- Circular dependencies
- Missing phase registrations
- Correct topological ordering
- Provides visualization of dependency tree

Usage:
    python scripts/validate_pipeline.py
    python scripts/validate_pipeline.py --verbose
"""

import argparse
import sys
from collections import defaultdict
from pathlib import Path

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent.parent))

from generators import (
    CitiesPhase,
    CuisineTypesPhase,
    DishesPhase,
    HeroImagesPhase,
    IngredientsPhase,
    RestaurantsPhase,
    ReviewsPhase,
    SocialGraphPhase,
    SystemConfigPhase,
    TagsPhase,
    UsersPhase,
)
from orchestration import PhaseRegistry
from utils.logging_config import LoggingConfig

def setup_phase_registry() -> PhaseRegistry:
    """Setup and register all phases."""
    registry = PhaseRegistry()

    registry.register(SystemConfigPhase())
    registry.register(CitiesPhase())
    registry.register(CuisineTypesPhase())
    registry.register(HeroImagesPhase())
    registry.register(IngredientsPhase())
    registry.register(TagsPhase())
    registry.register(RestaurantsPhase())
    registry.register(DishesPhase())
    registry.register(UsersPhase())
    registry.register(ReviewsPhase())
    registry.register(SocialGraphPhase())

    return registry

def visualize_dependencies(registry: PhaseRegistry, verbose: bool = False):
    """Visualize dependency tree."""
    print("\n" + "=" * 80)
    print("DEPENDENCY GRAPH")
    print("=" * 80)

    all_phases = registry.get_all()

    # Build dependency map
    deps_map = {}
    reverse_deps = defaultdict(list)

    for phase in all_phases:
        metadata = phase.metadata
        deps_map[metadata.phase_id] = metadata.dependencies

        for dep in metadata.dependencies:
            reverse_deps[dep].append(metadata.phase_id)

    # Print phases with their dependencies
    for phase in sorted(all_phases, key=lambda p: p.metadata.phase_id):
        metadata = phase.metadata
        phase_id = metadata.phase_id

        print(f"\n{phase_id}")
        print(f"  Name: {metadata.display_name}")
        print(f"  Dependencies: {metadata.dependencies if metadata.dependencies else 'None (root phase)'}")
        print(f"  Required Tables: {', '.join(metadata.required_tables)}")
        print(f"  Estimated Duration: {metadata.estimated_duration}s")

        if verbose and phase_id in reverse_deps:
            print(f"  Dependent Phases: {', '.join(reverse_deps[phase_id])}")

def validate_dependency_resolution(registry: PhaseRegistry):
    """Validate that all phases can be resolved without cycles."""
    print("\n" + "=" * 80)
    print("DEPENDENCY RESOLUTION VALIDATION")
    print("=" * 80)

    all_phases = registry.get_all()
    all_phase_ids = [p.metadata.phase_id for p in all_phases]

    # Test resolution for each phase
    errors = []

    for phase in all_phases:
        phase_id = phase.metadata.phase_id

        try:
            resolved = registry.resolve_dependencies([phase_id])
            print(f"\n[OK] {phase_id}")
            print(f"  Execution order: {' -> '.join(resolved)}")

            # Validate that this phase is last
            if resolved[-1] != phase_id:
                errors.append(f"  [FAIL] ERROR: {phase_id} not at end of resolution chain!")

            # Validate dependencies come before this phase
            for dep in phase.metadata.dependencies:
                if dep not in resolved:
                    errors.append(f"  [FAIL] ERROR: Dependency {dep} not in resolution chain!")
                elif resolved.index(dep) >= resolved.index(phase_id):
                    errors.append(f"  [FAIL] ERROR: Dependency {dep} comes after {phase_id}!")

        except Exception as e:
            errors.append(f"[FAIL] {phase_id}: FAILED - {e}")

    # Test full pipeline resolution
    print("\n" + "-" * 80)
    print("FULL PIPELINE RESOLUTION")
    print("-" * 80)

    try:
        full_order = registry.resolve_dependencies(all_phase_ids)
        print("\n[OK] Full pipeline can be resolved")
        print(f"  Execution order ({len(full_order)} phases):")

        for i, phase_id in enumerate(full_order, 1):
            print(f"    {i:2d}. {phase_id}")

        # Validate no duplicates
        if len(full_order) != len(set(full_order)):
            errors.append("[FAIL] ERROR: Duplicate phases in resolution!")

    except Exception as e:
        errors.append(f"[FAIL] Full pipeline resolution FAILED: {e}")

    return errors

def check_missing_registrations(registry: PhaseRegistry):
    """Check for missing phase registrations."""
    print("\n" + "=" * 80)
    print("REGISTRATION CHECK")
    print("=" * 80)

    all_phases = registry.get_all()

    # Expected phases
    expected = {
        "phase0_config",
        "phase1_cities",
        "phase1_cuisines",
        "phase1_hero",
        "phase1_ingredients",
        "phase1_tags",
        "phase2_restaurants",
        "phase3_dishes",
        "phase4_users",
        "phase5_reviews",
        "phase6_social",
    }

    registered = {p.metadata.phase_id for p in all_phases}

    missing = expected - registered
    extra = registered - expected

    if not missing and not extra:
        print("\n[OK] All expected phases registered")
        print(f"  Total phases: {len(registered)}")
    else:
        if missing:
            print(f"\n[FAIL] Missing phases: {', '.join(missing)}")
        if extra:
            print(f"\n[WARN] Extra phases: {', '.join(extra)}")

    return list(missing), list(extra)

def main():
    parser = argparse.ArgumentParser(
        description="Validate pipeline dependency graph",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )

    parser.add_argument("--verbose", "-v", action="store_true", help="Show detailed information")

    args = parser.parse_args()

    # Setup logging
    LoggingConfig.setup(level="ERROR")  # Suppress logs during validation

    print("=" * 80)
    print("PIPELINE VALIDATION TOOL")
    print("=" * 80)

    try:
        # Setup registry
        registry = setup_phase_registry()

        # 1. Check registrations
        missing, extra = check_missing_registrations(registry)

        # 2. Visualize dependencies
        visualize_dependencies(registry, verbose=args.verbose)

        # 3. Validate dependency resolution
        errors = validate_dependency_resolution(registry)

        # Summary
        print("\n" + "=" * 80)
        print("VALIDATION SUMMARY")
        print("=" * 80)

        if not missing and not extra and not errors:
            print("\n[OK] ALL VALIDATIONS PASSED")
            print("  - All phases registered")
            print("  - No circular dependencies")
            print("  - All dependencies can be resolved")
            print("  - Topological ordering correct")
            sys.exit(0)
        else:
            print("\n[FAIL] VALIDATION FAILED")

            if missing:
                print(f"\n  Missing phases: {len(missing)}")
                for m in missing:
                    print(f"    - {m}")

            if extra:
                print(f"\n  Extra phases: {len(extra)}")
                for e in extra:
                    print(f"    - {e}")

            if errors:
                print(f"\n  Resolution errors: {len(errors)}")
                for err in errors:
                    print(f"    {err}")

            sys.exit(1)

    except Exception as e:
        print(f"\n[FAIL] FATAL ERROR: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()
