#!/bin/bash
# rename-game-modes.sh
# Renames ToutAs -> AllTrumps and SansAs -> NoTrumps across the entire codebase
#
# Handles all case variants:
#   PascalCase: ToutAs -> AllTrumps,  SansAs -> NoTrumps
#   camelCase:  toutAs -> allTrumps,  sansAs -> noTrumps
#   Display:    "Tout As" -> "All Trumps",  "Sans As" -> "No Trumps"
#   kebab-case: tout-as -> all-trumps,  sans-as -> no-trumps
#
# Safe: GameMode is stored as int in DB, no migration needed.

set -euo pipefail

cd "$(dirname "$0")"

echo "=== Renaming ToutAs -> AllTrumps and SansAs -> NoTrumps ==="
echo ""

# Find all text source files, excluding build/dependency directories
FILES=$(find . -type f \( \
  -name "*.cs" -o -name "*.ts" -o -name "*.tsx" -o -name "*.js" -o \
  -name "*.md" -o -name "*.html" -o -name "*.css" -o -name "*.scss" -o \
  -name "*.json" -o -name "*.yaml" -o -name "*.yml" \
  \) \
  ! -path "./.git/*" \
  ! -path "*/bin/*" \
  ! -path "*/obj/*" \
  ! -path "*/node_modules/*" \
  ! -path "*/dist/*" \
  ! -path "*/.angular/*")

# Filter to only files that actually contain a match
MATCHED=$(echo "$FILES" | xargs grep -rl 'ToutAs\|SansAs\|Tout As\|Sans As\|Sans as\|tout-as\|sans-as\|toutAs\|sansAs' 2>/dev/null || true)

if [ -z "$MATCHED" ]; then
  echo "No files found with matching patterns."
  exit 0
fi

COUNT=$(echo "$MATCHED" | wc -l)
echo "Found $COUNT files to update:"
echo "$MATCHED" | sed 's/^/  /'
echo ""

# Apply replacements in safe order:
# 1. Space-separated display strings (most specific)
# 2. kebab-case CSS classes
# 3. camelCase local variables (before PascalCase to avoid partial matches)
# 4. PascalCase enum/type members (most general)
echo "$MATCHED" | xargs sed -i \
  -e 's/Tout As/All Trumps/g' \
  -e 's/Sans As/No Trumps/g' \
  -e 's/Sans as/No Trumps/g' \
  -e 's/tout-as/all-trumps/g' \
  -e 's/sans-as/no-trumps/g' \
  -e 's/toutAs/allTrumps/g' \
  -e 's/sansAs/noTrumps/g' \
  -e 's/ToutAs/AllTrumps/g' \
  -e 's/SansAs/NoTrumps/g'

echo "Replacements applied."
echo ""

# Also update the .claude memory file if it exists
MEMORY_FILE="$HOME/.claude/projects/C--Users-haga-source-repos-Giretra/memory/MEMORY.md"
if [ -f "$MEMORY_FILE" ]; then
  echo "Updating .claude memory file..."
  sed -i \
    -e 's/ToutAs/AllTrumps/g' \
    -e 's/SansAs/NoTrumps/g' \
    "$MEMORY_FILE"
  echo "  $MEMORY_FILE"
  echo ""
fi

echo "=== Summary of changes ==="
git diff --stat
echo ""
echo "Run 'dotnet build' and 'dotnet test' to verify everything compiles and passes."
