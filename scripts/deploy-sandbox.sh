#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
YARG_MANAGED="${YARG_MANAGED:-$ROOT/yarg-sandbox/YARG.app/Contents/Resources/Data/Managed}"

if [[ ! -f "$YARG_MANAGED/Assembly-CSharp.dll" ]]; then
  echo "Sandbox Managed folder not found at: $YARG_MANAGED" >&2
  exit 1
fi

echo "Building mod against sandbox Managed..."
dotnet build "$ROOT/src/mod/BardQuest.Mod/BardQuest.Mod.csproj" -c Debug -p:YargManaged="$YARG_MANAGED" -p:ModVersion=0.0.0-dev

echo "Building updater..."
dotnet build "$ROOT/src/installer/BardQuest.Updater/BardQuest.Updater.csproj" -c Release

echo "Installing (copy DLLs + patch seam) into sandbox..."
dotnet "$ROOT/src/installer/BardQuest.Updater/bin/Release/net10.0/BardQuest.Updater.dll" \
  install "$YARG_MANAGED" "$ROOT/src/mod/BardQuest.Mod/bin/Debug"

echo "BardQuest installed."
