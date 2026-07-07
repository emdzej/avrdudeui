#!/usr/bin/env bash
# Build AvrdudeUI.app for macOS (arm64).
#
# Usage:
#   ./build/build.sh                 # Release build → build/AvrdudeUI.app
#   ./build/build.sh --clean         # Remove build/ output first
#   CODESIGN_IDENTITY=- ./build/build.sh    # ad-hoc sign (default)
#   CODESIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)" ./build/build.sh
#
# Requirements: .NET 10 SDK, macOS.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/AvrdudeUI/AvrdudeUI.csproj"
OUT_DIR="$ROOT/build/out"
APP_DIR="$ROOT/build/AvrdudeUI.app"
TEMPLATE="$ROOT/build/AvrdudeUI.app.template"
RID="osx-arm64"

# Ad-hoc sign by default — enough for local runs. Override with your team ID for distribution.
CODESIGN_IDENTITY="${CODESIGN_IDENTITY:--}"

if [[ "${1:-}" == "--clean" ]]; then
    echo "[clean] removing $OUT_DIR and $APP_DIR"
    rm -rf "$OUT_DIR" "$APP_DIR"
fi

echo "[publish] dotnet publish → $OUT_DIR (rid=$RID)"
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$OUT_DIR"

echo "[assemble] $APP_DIR"
rm -rf "$APP_DIR"
cp -R "$TEMPLATE" "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS"

# Copy the entire publish output into MacOS/ so the executable finds its DLLs, Assets, and Languages next to it.
cp -R "$OUT_DIR/." "$APP_DIR/Contents/MacOS/"

# Guarantee the main binary is executable.
chmod +x "$APP_DIR/Contents/MacOS/AvrdudeUI"

echo "[codesign] identity=$CODESIGN_IDENTITY"
codesign --force --deep --sign "$CODESIGN_IDENTITY" "$APP_DIR"

echo "[done]"
echo "Bundle: $APP_DIR"
echo "Launch: open '$APP_DIR'"
