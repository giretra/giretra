#!/bin/sh
# Builds the netstandard2.1 contracts and copies the DLLs into the Unity project.
# Usage: sh sync-unity-contracts.sh [target-dir]
# Default target: ../giretra.mobile/Assets/Plugins/Giretra
set -e

DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="${1:-$DIR/../giretra.mobile/Assets/Plugins/Giretra}"

dotnet build "$DIR/src/Giretra.Contracts/Giretra.Contracts.csproj" -c Release -f netstandard2.1

mkdir -p "$TARGET"
cp "$DIR/src/Giretra.Contracts/bin/Release/netstandard2.1/"*.dll "$TARGET/"

echo "Synced to $TARGET:"
ls -1 "$TARGET"/*.dll
