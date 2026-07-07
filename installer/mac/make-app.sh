#!/usr/bin/env bash
# Wraps a self-contained osx-arm64 publish output into an unsigned .app bundle.
# Usage: make-app.sh <publish-dir> <version> <out-dir>
set -euo pipefail

PUBLISH_DIR="$1"
VERSION="$2"
OUT_DIR="$3"

APP="$OUT_DIR/BardQuest Updater.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"

cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/BardQuest.Updater"

# Generate the app icon (.icns) from the committed logo PNG, if the macOS icon
# tools are available. The .icns is built at package time — only the source PNG
# is committed. If the logo or tools are missing, the bundle is still produced
# (just without a custom icon).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOGO="$SCRIPT_DIR/../../img/BardQuest-logo.png"
ICON_PLIST=""
if [ -f "$LOGO" ] && command -v sips >/dev/null 2>&1 && command -v iconutil >/dev/null 2>&1; then
  mkdir -p "$APP/Contents/Resources"
  work="$(mktemp -d)"
  iconset="$work/BardQuest.iconset"
  mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$LOGO" --out "$iconset/icon_${size}x${size}.png" >/dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$LOGO" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$iconset" -o "$APP/Contents/Resources/BardQuest.icns"
  rm -rf "$work"
  ICON_PLIST='  <key>CFBundleIconFile</key><string>BardQuest</string>'
else
  echo "warning: logo or icon tools missing — bundling without a custom icon" >&2
fi

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>BardQuest Updater</string>
  <key>CFBundleDisplayName</key><string>BardQuest Updater</string>
  <key>CFBundleIdentifier</key><string>com.bardquest.updater</string>
  <key>CFBundleVersion</key><string>${VERSION}</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleExecutable</key><string>BardQuest.Updater</string>
${ICON_PLIST}
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

echo "Built $APP"
