#!/usr/bin/env bash
# Rebuilds src/AltMiUstMu.Web/wwwroot/css/app.css on every change. Run `dotnet build` once first (it downloads the CLI).
set -euo pipefail
cd "$(dirname "$0")/../src/AltMiUstMu.Web"
BIN=$(ls ../../.tools/*/tailwindcss-* 2>/dev/null | head -1)
if [ -z "$BIN" ]; then echo "Tailwind CLI not found; run 'dotnet build' first." >&2; exit 1; fi
exec "$BIN" -i Styles/app.css -o wwwroot/css/app.css --watch
