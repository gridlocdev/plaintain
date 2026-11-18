#!/usr/bin/env bash
#
# Package one platform's release download: the desktop app and the `plaintain` CLI, self-contained,
# in one archive. Used by the release workflow, one runner per OS, because the desktop app is
# Native AOT and AOT only targets the OS it runs on (on macOS it can target the other CPU, too).
#
# Usage:
#   build/package-release.sh VERSION [RID] [OUT]    # RID defaults to this machine; OUT to dist/
#
# Output:
#   macOS    OUT/plaintain-VERSION-macos-<arch>.zip      Plaintain/{Plaintain.app, plaintain}
#   Linux    OUT/plaintain-VERSION-linux-<arch>.tar.gz   plaintain/{desktop/Plaintain, cli/plaintain}
#   Windows  OUT/plaintain-VERSION-win-x64.zip           plaintain/{desktop/Plaintain.exe, cli/plaintain.exe}
#
# The Linux and Windows layout is the one `just publish` writes, which is where the CLI looks for
# the desktop app (`cli/../desktop/`). On macOS the CLI opens the app by name, so the .app is
# meant to go in /Applications.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:?usage: build/package-release.sh VERSION [RID] [OUT]}"
VERSION="${VERSION#v}"
RID="${2:-}"
OUT="${3:-$ROOT/dist}"

if [ -z "$RID" ]; then
    case "$(uname -s)-$(uname -m)" in
        Darwin-arm64)  RID="osx-arm64" ;;
        Darwin-*)      RID="osx-x64" ;;
        MINGW*|MSYS*)  RID="win-x64" ;;
        *-aarch64)     RID="linux-arm64" ;;
        *)             RID="linux-x64" ;;
    esac
fi

# The bundle's version is written by hand in Info.plist; a release with a different one would ship
# an app whose About screen and Finder info disagree with its download.
PLIST_VERSION="$(sed -n '/CFBundleShortVersionString/{n;s:.*<string>\(.*\)</string>.*:\1:p;}' "$ROOT/Plaintain.Desktop/Info.plist")"
if [ "$PLIST_VERSION" != "$VERSION" ]; then
    echo "Info.plist says $PLIST_VERSION but this release is $VERSION; update CFBundleShortVersionString first." >&2
    exit 1
fi

STAGE_ROOT="$ROOT/artifacts/release/$RID"
rm -rf "$STAGE_ROOT"
mkdir -p "$STAGE_ROOT" "$OUT"
OUT="$(cd "$OUT" && pwd)" # absolute: the Windows branch zips from inside the staging folder

publish_cli() {
    echo "==> Publishing the plaintain CLI ($RID)"
    dotnet publish "$ROOT/Plaintain.Cli/Plaintain.Cli.csproj" -c Release -r "$RID" --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version="$VERSION" \
        -o "$1" --nologo
    find "$1" -name '*.pdb' -delete
}

case "$RID" in
    osx-*)
        ARCH="${RID#osx-}"
        NAME="plaintain-$VERSION-macos-$ARCH"
        STAGE="$STAGE_ROOT/Plaintain"
        mkdir -p "$STAGE"
        CONFIG=Release RID="$RID" OUT="$STAGE_ROOT/app" "$ROOT/build/package-mac.sh"
        mv "$STAGE_ROOT/app/Plaintain.app" "$STAGE/Plaintain.app"
        publish_cli "$STAGE_ROOT/cli"
        mv "$STAGE_ROOT/cli/plaintain" "$STAGE/plaintain"
        ARCHIVE="$OUT/$NAME.zip"
        rm -f "$ARCHIVE"
        ditto -c -k --keepParent "$STAGE" "$ARCHIVE"
        ;;
    linux-*)
        NAME="plaintain-$VERSION-$RID"
        STAGE="$STAGE_ROOT/plaintain"
        CONFIG=Release RID="$RID" "$ROOT/build/publish-desktop.sh" "$STAGE/desktop"
        find "$STAGE/desktop" \( -name '*.dbg' -o -name '*.pdb' -o -name '*.xml' \) -delete
        publish_cli "$STAGE/cli"
        ARCHIVE="$OUT/$NAME.tar.gz"
        tar -C "$STAGE_ROOT" -czf "$ARCHIVE" plaintain
        ;;
    win-*)
        NAME="plaintain-$VERSION-$RID"
        STAGE="$STAGE_ROOT/plaintain"
        CONFIG=Release RID="$RID" "$ROOT/build/publish-desktop.sh" "$STAGE/desktop"
        find "$STAGE/desktop" \( -name '*.pdb' -o -name '*.xml' \) -delete
        publish_cli "$STAGE/cli"
        ARCHIVE="$OUT/$NAME.zip"
        rm -f "$ARCHIVE"
        (cd "$STAGE_ROOT" && 7z a -tzip -bso0 "$ARCHIVE" plaintain)
        ;;
    *)
        echo "Don't know how to package $RID" >&2
        exit 1
        ;;
esac

echo "==> Built $ARCHIVE ($(du -h "$ARCHIVE" | cut -f1))"
