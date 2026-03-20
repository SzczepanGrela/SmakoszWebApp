#!/bin/bash
# Full database reset + data generation pipeline
# Usage: ./reset-and-generate.sh

set -e

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$REPO_ROOT/src/Smakosz.Infrastructure"
GENERATOR_DIR="$REPO_ROOT/tools/generator"

echo "=== Dropping database ==="
cd "$INFRA_DIR"
DOTNET_ROLL_FORWARD=Major dotnet ef database drop --force

echo "=== Applying migrations ==="
DOTNET_ROLL_FORWARD=Major dotnet ef database update

echo "=== Generating mock data ==="
cd "$GENERATOR_DIR"
python main.py --generate --verbose
