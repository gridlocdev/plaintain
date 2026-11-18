#!/usr/bin/env bash
#
# Publish Plaintain.Desktop as a Native AOT executable (PublishAot is set in its csproj) into OUT.
#
# Used by `just publish-desktop` and build/package-mac.sh. It exists for one workaround: a .NET SDK
# built from source, such as Homebrew's `dotnet` formula, ships a NativeAOT runtime pack marked
# nonportable, which links Homebrew's OpenSSL and Brotli. The resulting app then only runs on a Mac
# with those Homebrew libraries installed. On macOS, when the SDK's pack is like that, this points the
# SDK at a copy of its pack folder without that one pack, so the SDK downloads Microsoft's portable
# pack from NuGet instead. With Microsoft's own SDK the pack is portable and nothing changes.
#
# Usage:
#   build/publish-desktop.sh OUT          # CONFIG (default Release) and RID (default: this machine)
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:?usage: build/publish-desktop.sh OUT}"
CONFIG="${CONFIG:-Release}"

if [ -z "${RID:-}" ]; then
    case "$(uname -s)-$(uname -m)" in
        Darwin-arm64) RID="osx-arm64" ;;
        Darwin-*)     RID="osx-x64" ;;
        *-aarch64)    RID="linux-arm64" ;;
        *)            RID="linux-x64" ;;
    esac
fi

EXTRA=()
if [ "$(uname -s)" = "Darwin" ]; then
    # `dotnet --list-sdks` prints "10.0.403 [/path/to/sdk]"; the packs folder sits beside that sdk folder.
    SDK_DIR="$(dotnet --list-sdks | tail -1 | sed -E 's/^[^[]*\[(.*)\]$/\1/')"
    PACKS="$(dirname "$SDK_DIR")/packs"
    AOT_PACK="Microsoft.NETCore.App.Runtime.NativeAOT.$RID"
    if compgen -G "$PACKS/$AOT_PACK/*/runtimes/$RID/native/nonportable.txt" >/dev/null; then
        SHADOW="$ROOT/obj/portable-packs"
        echo "==> The SDK's $AOT_PACK is nonportable; using Microsoft's from NuGet"
        rm -rf "$SHADOW"
        mkdir -p "$SHADOW"
        for pack in "$PACKS"/*; do
            [ "$(basename "$pack")" = "$AOT_PACK" ] || ln -s "$pack" "$SHADOW/"
        done
        EXTRA+=("-p:NetCoreTargetingPackRoot=$SHADOW")
    fi
fi

echo "==> Publishing Plaintain.Desktop ($CONFIG, $RID, Native AOT)"
rm -rf "$OUT"
dotnet publish "$ROOT/Plaintain.Desktop/Plaintain.Desktop.csproj" -c "$CONFIG" -r "$RID" -o "$OUT" ${EXTRA[@]+"${EXTRA[@]}"}
