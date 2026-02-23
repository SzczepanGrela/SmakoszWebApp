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

echo "=== Applying SQL objects (functions, views, indexes, triggers) ==="
PSQL_ARGS=(-h "${DB_HOST:-localhost}" -p "${DB_PORT:-5432}" -U "${DB_USER:-postgres}" -d "${DB_NAME:-smakosz_db}" -q)

# Apply in dependency order: Functions -> Views -> Indexes -> Triggers
for folder in Functions Views Indexes Triggers; do
    if [ -d "$SQL_DIR/$folder" ]; then
        for sql_file in "$SQL_DIR/$folder"/*.sql; do
            [ -f "$sql_file" ] || continue
            echo "  Applying: $folder/$(basename "$sql_file")"
            PGPASSWORD="$DB_PASSWORD" psql "${PSQL_ARGS[@]}" -f "$sql_file"
        done
    fi
done

echo "=== Generating mock data ==="
cd "$GENERATOR_DIR"
python main.py --generate --verbose
