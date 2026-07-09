#!/usr/bin/env bash
# Regenerate AvrdudeUI.icns from the master PNG.
#
# Usage:
#   ./build/icon/make-icns.sh
#
# Emits build/AvrdudeUI.app.template/Contents/Resources/AvrdudeUI.icns —
# the .app.template is version-controlled so the icon becomes part of the
# assembled bundle after build/build.sh runs.

set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
MASTER="$HERE/AvrdudeUI.png"
ICONSET="$HERE/AvrdudeUI.iconset"
OUT_DIR="$ROOT/build/AvrdudeUI.app.template/Contents/Resources"
OUT_ICNS="$OUT_DIR/AvrdudeUI.icns"

if [[ ! -f "$MASTER" ]]; then
    echo "Master PNG missing — run: python3 $HERE/generate.py" >&2
    exit 1
fi

echo "[iconset] rasterizing 1024→16 into $ICONSET"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"

# macOS iconutil expects these exact filenames.
# Format: icon_<size>x<size>[@2x].png
declare -a SIZES=(
    "16"    "icon_16x16.png"
    "32"    "icon_16x16@2x.png"
    "32"    "icon_32x32.png"
    "64"    "icon_32x32@2x.png"
    "128"   "icon_128x128.png"
    "256"   "icon_128x128@2x.png"
    "256"   "icon_256x256.png"
    "512"   "icon_256x256@2x.png"
    "512"   "icon_512x512.png"
    "1024"  "icon_512x512@2x.png"
)

for ((i=0; i<${#SIZES[@]}; i+=2)); do
    size="${SIZES[i]}"
    name="${SIZES[i+1]}"
    sips -z "$size" "$size" "$MASTER" --out "$ICONSET/$name" >/dev/null
done

echo "[iconset] packing → $OUT_ICNS"
mkdir -p "$OUT_DIR"
iconutil -c icns "$ICONSET" -o "$OUT_ICNS"

echo "[done] $OUT_ICNS"
