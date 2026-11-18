#!/usr/bin/env bash
#
# Build Plaintain.app, a macOS bundle of the Avalonia desktop app.
#
# Publishes Plaintain.Desktop self-contained (no .NET install needed on the target), assembles it
# into Plaintain.app with Plaintain.Desktop/Info.plist, and, when signing credentials are present in
# the environment, code-signs and notarizes it so it opens without Gatekeeper warnings.
#
# Prefer the justfile: `just publish-app`, `just install-app`, which pass CONFIG/RID/OUT through.
#
# Usage:
#   build/package-mac.sh                   # artifacts/<rid>/Plaintain.app
#   RID=osx-x64 build/package-mac.sh       # cross-target Intel
#   CONFIG=Debug build/package-mac.sh      # debug build
#   OUT=/tmp/out build/package-mac.sh      # somewhere other than artifacts/
#
# Optional signing / notarization (skipped when unset):
#   CODESIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)"
#   NOTARY_PROFILE="plaintain-notary"    # a profile stored via `xcrun notarytool store-credentials`
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

APP_NAME="Plaintain"
PROJECT_DIR="Plaintain.Desktop"

# The bundle tooling below (plutil, codesign, ditto, notarytool) is macOS-only. Say so plainly
# rather than failing halfway through with "command not found" — the Linux devcontainer can
# publish these apps (`just publish-<app>`), it just can't bundle them.
if [ "$(uname -s)" != "Darwin" ]; then
    echo "package-mac.sh builds a macOS .app and only runs on macOS (this is $(uname -s))." >&2
    echo "On Linux, 'just publish' still produces a runnable self-contained build." >&2
    exit 1
fi

PROJECT="$ROOT/$PROJECT_DIR/$(basename "$PROJECT_DIR").csproj"
PLIST="$ROOT/$PROJECT_DIR/Info.plist"

CONFIG="${CONFIG:-Release}"
case "$(uname -m)" in
    arm64) HOST_RID="osx-arm64" ;;
    *)     HOST_RID="osx-x64" ;;
esac
RID="${RID:-$HOST_RID}"

# Mirrors the justfile's artifacts/<app>/<rid>/ layout, and reuses its desktop publish directory.
OUT="${OUT:-$ROOT/artifacts/$RID}"
PUBLISH_DIR="$OUT/desktop"
APP="$OUT/$APP_NAME.app"
EXE="$APP/Contents/MacOS/$APP_NAME"

echo "==> Publishing $APP_NAME ($CONFIG, $RID, self-contained)"
rm -rf "$PUBLISH_DIR"
dotnet publish "$PROJECT" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$PUBLISH_DIR"

echo "==> Assembling $APP"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Everything the app loads (managed dlls + native dylibs) lives next to the executable.
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"

cp "$PLIST" "$APP/Contents/Info.plist"

# Optional app icon: drop an .icns at build/<App>.icns to brand the bundle.
if [ -f "$ROOT/build/$APP_NAME.icns" ]; then
    cp "$ROOT/build/$APP_NAME.icns" "$APP/Contents/Resources/$APP_NAME.icns"
else
    echo "    (no build/$APP_NAME.icns — bundle will use the generic app icon)"
fi

chmod +x "$EXE"

# Validate the plist so a typo fails the build rather than a launch.
plutil -lint "$APP/Contents/Info.plist" >/dev/null

if [ -n "${CODESIGN_IDENTITY:-}" ]; then
    echo "==> Code-signing with: $CODESIGN_IDENTITY"
    # Sign nested code first, then the bundle, with the hardened runtime (required for notarization).
    find "$APP/Contents/MacOS" -type f \( -name "*.dylib" -o -name "*.so" \) -print0 \
        | xargs -0 -I{} codesign --force --timestamp --options runtime --sign "$CODESIGN_IDENTITY" {}
    codesign --force --timestamp --options runtime --sign "$CODESIGN_IDENTITY" "$EXE"
    codesign --force --timestamp --options runtime --sign "$CODESIGN_IDENTITY" "$APP"
    codesign --verify --deep --strict --verbose=2 "$APP"
else
    echo "==> Skipping code-signing (set CODESIGN_IDENTITY to enable)"
fi

if [ -n "${NOTARY_PROFILE:-}" ]; then
    echo "==> Notarizing via profile: $NOTARY_PROFILE"
    ZIP="$OUT/$APP_NAME.zip"
    ditto -c -k --keepParent "$APP" "$ZIP"
    xcrun notarytool submit "$ZIP" --keychain-profile "$NOTARY_PROFILE" --wait
    xcrun stapler staple "$APP"
    rm -f "$ZIP"
else
    echo "==> Skipping notarization (set NOTARY_PROFILE to enable)"
fi

echo "==> Done: $APP"
