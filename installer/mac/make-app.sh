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
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

echo "Built $APP"
