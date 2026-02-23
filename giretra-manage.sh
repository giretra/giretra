#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
dotnet run --no-launch-profile --project "$SCRIPT_DIR/src/Giretra.Manage" -- "$@"
