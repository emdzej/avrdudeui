#!/usr/bin/env bash
# Build AvrdudeUI.app for macOS.
#
# Usage:
#   ./build/build.sh                        # Release build → build/AvrdudeUI.app (osx-arm64)
#   ./build/build.sh --clean                # Remove build/ output first
#   RID=osx-x64 ./build/build.sh            # Cross-publish for Intel
#   VERSION=1.2.3 ./build/build.sh          # Stamp Info.plist + assembly version
#   CODESIGN_IDENTITY=- ./build/build.sh    # ad-hoc sign (default)
#   CODESIGN_IDENTITY="Developer ID Application: Name (TEAMID)" ./build/build.sh
#
# Requirements: .NET 10 SDK, macOS.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/AvrdudeUI/AvrdudeUI.csproj"
OUT_DIR="$ROOT/build/out"
APP_DIR="$ROOT/build/AvrdudeUI.app"
TEMPLATE="$ROOT/build/AvrdudeUI.app.template"
RID="${RID:-osx-arm64}"
VERSION="${VERSION:-0.1.0}"
CODESIGN_IDENTITY="${CODESIGN_IDENTITY:--}"

if [[ "${1:-}" == "--clean" ]]; then
    echo "[clean] removing $OUT_DIR and $APP_DIR"
    rm -rf "$OUT_DIR" "$APP_DIR"
fi

# AssemblyVersion / FileVersion are strictly numeric — strip any prerelease
# suffix (e.g. "0.2.0-dev" → "0.2.0") and pad to four segments.
NUMERIC_VERSION="${VERSION%%-*}"
case "$NUMERIC_VERSION" in
    *.*.*.*)  ASM_VERSION="$NUMERIC_VERSION" ;;
    *.*.*)    ASM_VERSION="$NUMERIC_VERSION.0" ;;
    *.*)      ASM_VERSION="$NUMERIC_VERSION.0.0" ;;
    *)        ASM_VERSION="$NUMERIC_VERSION.0.0.0" ;;
esac

echo "[publish] dotnet publish → $OUT_DIR (rid=$RID version=$VERSION asm=$ASM_VERSION)"
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:Version="$VERSION" \
    -p:AssemblyVersion="$ASM_VERSION" \
    -p:FileVersion="$ASM_VERSION" \
    -o "$OUT_DIR"

echo "[assemble] $APP_DIR"
rm -rf "$APP_DIR"
cp -R "$TEMPLATE" "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS"

# Copy the entire publish output into MacOS/ so the executable finds its DLLs, Assets, and Languages next to it.
cp -R "$OUT_DIR/." "$APP_DIR/Contents/MacOS/"

chmod +x "$APP_DIR/Contents/MacOS/AvrdudeUI"

# Stamp the version into Info.plist so Finder / Launchpad show the right one.
if command -v plutil >/dev/null 2>&1; then
    plutil -replace CFBundleVersion            -string "$VERSION" "$APP_DIR/Contents/Info.plist"
    plutil -replace CFBundleShortVersionString -string "$VERSION" "$APP_DIR/Contents/Info.plist"
fi

echo "[codesign] identity=$CODESIGN_IDENTITY"
codesign --force --deep --sign "$CODESIGN_IDENTITY" "$APP_DIR"

echo "[done]"
echo "Bundle: $APP_DIR"
echo "Launch: open '$APP_DIR'"
