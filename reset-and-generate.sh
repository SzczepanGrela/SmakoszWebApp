#!/bin/bash
# Full database reset + data generation pipeline
# Usage: ./reset-and-generate.sh

set -e

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
INFRA_DIR="$REPO_ROOT/src/Smakosz.Infrastructure"
GENERATOR_DIR="$REPO_ROOT/tools/generator"
SQL_DIR="$INFRA_DIR/SqlObjects"

# Load .env for DB connection
if [ -f "$REPO_ROOT/.env" ]; then
    set -a
    source "$REPO_ROOT/.env"
    set +a
fi

echo "=== Dropping database ==="
cd "$INFRA_DIR"
DOTNET_ROLL_FORWARD=Major dotnet ef database drop --force

echo "=== Applying migrations ==="
DOTNET_ROLL_FORWARD=Major dotnet ef database update

echo "=== Applying SQL objects (functions, views, indexes) ==="
PGPASSWORD="$DB_PASSWORD" psql -h "${DB_HOST:-localhost}" -p "${DB_PORT:-5432}" -U "${DB_USER:-postgres}" -d "${DB_NAME:-smakosz_db}" -q \
    -f "$SQL_DIR/Functions/f_unaccent.sql" \
    -f "$SQL_DIR/Views/search_autocomplete.sql" \
    -f "$SQL_DIR/Indexes/search_indexes.sql"

echo "=== Generating mock data ==="
cd "$GENERATOR_DIR"
python main.py --generate --verbose
