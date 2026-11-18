# Plaintain — the task runner (https://just.systems)
#
#   just                 list every recipe
#   just build           build the solution (warnings are errors)
#   just test            run every test suite (headless; no window opens)
#   just check           format-verify + build + test — the gate before a commit
#   just run             launch the desktop app (opens a window)
#   just cli info FILE   run the `plaintain` CLI from source
#   just bench           the 50 MB load-path benchmarks (Release)
#   just install         the `plaintain` CLI onto PATH, and on macOS Plaintain.app into /Applications
#
# Every install has an inverse that undoes exactly what it did, and nothing else:
#   publish ↔ unpublish, install ↔ uninstall, install-cli ↔ uninstall-cli, install-app ↔ uninstall-app.
# Neither uninstall touches PREFIX/bin itself or the app's user data in ~/Library/Application Support.
#
# Recipe dependencies run one at a time, which matters: concurrent `dotnet build`/`dotnet test`
# invocations deadlock on the MSBuild-node locks.

SLN     := "Plaintain.slnx"
CLI     := "Plaintain.Cli"
DESKTOP := "Plaintain.Desktop"
BENCH   := "Plaintain.Benchmarks"
DOTNET  := "dotnet"

CONFIG    := "Release"
ARTIFACTS := "artifacts"
PREFIX    := env("HOME") / ".local"
BINDIR    := PREFIX / "bin"
APPS_DIR  := "/Applications"

RID_OS   := if os() == "macos" { "osx" } else if os() == "windows" { "win" } else { "linux" }
RID_ARCH := if arch() == "aarch64" { "arm64" } else { "x64" }
RID      := RID_OS + "-" + RID_ARCH
OUT      := ARTIFACTS / RID

# Both surfaces run on a machine with no .NET installed. The CLI is one self-extracting file; the
# desktop app is a Native AOT executable with its Avalonia/Skia natives beside it, which is what a
# macOS .app bundle expects (see build/publish-desktop.sh and build/package-mac.sh).
PUBLISH_CLI_FLAGS     := "-c " + CONFIG + " -r " + RID + " --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"

# Removing the .app also withdraws the file-type claims its Info.plist registered. Best-effort: the path
# is macOS-only and uninstall must not fail without it.
LSREGISTER := "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"

# Show every recipe (the default when you run bare `just`).
default:
    @just --list --list-heading $'Plaintain — run `just <recipe>`\n'

# Format-verify, build and test: the definition of done
check: format-check build test

restore:
    {{ DOTNET }} restore {{ SLN }}

build:
    {{ DOTNET }} build {{ SLN }}

test:
    {{ DOTNET }} test {{ SLN }}

# Run the tests whose name contains FILTER
test-filter FILTER:
    {{ DOTNET }} test {{ SLN }} --filter "FullyQualifiedName~{{ FILTER }}"

format:
    {{ DOTNET }} format {{ SLN }}

format-check:
    {{ DOTNET }} format {{ SLN }} --verify-no-changes

clean:
    {{ DOTNET }} clean {{ SLN }}
    rm -rf "{{ ARTIFACTS }}"

# Launch the desktop app (opens a window; needs a real display)
run *ARGS:
    {{ DOTNET }} run --project {{ DESKTOP }} -- {{ ARGS }}

# Run the `plaintain` CLI from source, e.g. `just cli info server.log`
cli *ARGS:
    {{ DOTNET }} run --project {{ CLI }} -- {{ ARGS }}

bench *ARGS:
    {{ DOTNET }} run -c Release --project {{ BENCH }} -- {{ ARGS }}

# Self-contained Release builds of the CLI and the desktop app into artifacts/<rid>/
publish: publish-cli publish-desktop

publish-cli:
    {{ DOTNET }} publish {{ CLI }} {{ PUBLISH_CLI_FLAGS }} -o {{ OUT }}/cli

publish-desktop:
    CONFIG={{ CONFIG }} RID={{ RID }} build/publish-desktop.sh {{ OUT }}/desktop

# Plaintain.app (macOS only)
publish-app:
    CONFIG={{ CONFIG }} RID={{ RID }} OUT={{ OUT }} build/package-mac.sh

# The release download for this machine (desktop app + CLI, one archive) into dist/, e.g. `just package 1.0.0`
package VERSION:
    build/package-release.sh {{ VERSION }} {{ RID }} dist

unpublish:
    rm -rf "{{ OUT }}"
    @rmdir "{{ ARTIFACTS }}" 2>/dev/null || true
    @echo "removed {{ OUT }}"

# Both halves: the CLI onto PATH, and on macOS Plaintain.app into /Applications
[macos]
install: install-cli install-app

# Elsewhere there is no .app, so the CLI alone
[linux]
install: install-cli

install-cli: publish-cli
    @mkdir -p "{{ BINDIR }}"
    install -m 0755 {{ OUT }}/cli/plaintain "{{ BINDIR }}/plaintain"
    @echo "installed {{ BINDIR }}/plaintain — make sure {{ BINDIR }} is on your PATH"

install-app: publish-app
    @mkdir -p "{{ APPS_DIR }}"
    rm -rf "{{ APPS_DIR }}/Plaintain.app"
    cp -R {{ OUT }}/Plaintain.app "{{ APPS_DIR }}/Plaintain.app"
    @echo "installed {{ APPS_DIR }}/Plaintain.app"

[macos]
uninstall: uninstall-cli uninstall-app

[linux]
uninstall: uninstall-cli

uninstall-cli:
    rm -f "{{ BINDIR }}/plaintain"
    @echo "removed {{ BINDIR }}/plaintain"

uninstall-app:
    @{{ LSREGISTER }} -u "{{ APPS_DIR }}/Plaintain.app" >/dev/null 2>&1 || true
    rm -rf "{{ APPS_DIR }}/Plaintain.app"
    @echo "removed {{ APPS_DIR }}/Plaintain.app"
