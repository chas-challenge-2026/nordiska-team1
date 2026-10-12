#!/usr/bin/env bash
set -euo pipefail

# Script to apply EF Core migrations across all Nordiska modules
# Usage: ./migrate-database.sh [CONNECTION_STRING]
# Or set MIGRATION_CONNECTION_STRING environment variable

CONNECTION="${1:-${MIGRATION_CONNECTION_STRING:-${ConnectionStrings__MigrationDatabase:-}}}"

if [ -z "$CONNECTION" ]; then
  echo "Error: Migration connection string is required." >&2
  echo "Provide it as argument or set MIGRATION_CONNECTION_STRING." >&2
  exit 1
fi

export MIGRATION_CONNECTION_STRING="$CONNECTION"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$BACKEND_DIR"

echo "Restoring dotnet-ef tool..."
dotnet tool restore

MODULES=("Banking" "Inbox" "Faq" "Reporting")

for MODULE in "${MODULES[@]}"; do
  echo "=========================================="
  echo "Applying migrations for: $MODULE"
  echo "=========================================="
  
  dotnet tool run dotnet-ef database update \
    --project "src/Modules/$MODULE/Nordiska.Modules.$MODULE.csproj" \
    --startup-project "src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj" \
    --context "${MODULE}DbContext"
    
  echo "Successfully migrated $MODULE."
done

echo "=========================================="
echo "All module database migrations applied successfully!"
echo "=========================================="