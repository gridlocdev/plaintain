# Development

Plaintain is a C# app on .NET 10 and [Avalonia](https://avaloniaui.net) 12, with a custom-drawn editing surface and no NuGet packages beyond the .NET libraries and Avalonia itself. The rules in `AGENTS.md` at the repository root apply to everyone working on it, and the code layout and how the editor draws a 50 MB file are in [architecture.md](architecture.md).

## Getting set up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [just](https://just.systems).

```bash
just                    # list every recipe
just build              # build the solution; warnings are errors
just test               # run every test suite, headless (no window opens)
just test-filter Name   # run the tests whose name contains Name
just check              # format check, build and test: the gate before a commit
just format             # apply the formatter
just run                # launch the desktop app (opens a window)
just cli info file.log  # run the plaintain CLI from source
just bench              # BenchmarkDotNet over the 50 MB load path (Release)
just clean              # remove build output
```

## Checking your work

```bash
just check
```

`just check` is the definition of done. Check UI changes through the headless tests in `Plaintain.UiTests` rather than by opening a window.

`HermeticProfileTests.Every_user_data_location_resolves_inside_the_throwaway_profile` currently fails on macOS with the .NET 10.0.4xx SDK, once in each test assembly, because `ApplicationData` still resolves to the real `~/Library/Application Support`. Those three failures are known; anything else is yours.

To measure cold start without showing a window, run a built app with a scratch directory:

```bash
PLAINTAIN_STARTUP_PROBE=/tmp/plaintain-probe /Applications/Plaintain.app/Contents/MacOS/Plaintain
```

It opens the real window off-screen and transparent, reads settings from that directory, prints what it read and `first-frame-ms N`, then exits.

## Building and installing

```bash
just publish          # self-contained Release builds of the CLI and the app into artifacts/<rid>/
just publish-app      # Plaintain.app (macOS only)
just install          # the CLI into ~/.local/bin, and on macOS Plaintain.app into /Applications
just install-cli      # only the CLI
just install-app      # only the app
just uninstall        # undo just install; your settings stay
just unpublish        # remove artifacts/
```

Each install has an inverse that undoes exactly what it did. To put the CLI somewhere other than `~/.local/bin`, override `PREFIX`: `just PREFIX=/usr/local install-cli`.

The CLI publishes as one self-extracting file. The desktop app publishes with Native AOT, so reflection-based code can fail there even when the tests pass. Serialise JSON through a source-generated `JsonSerializerContext` such as `SettingsJsonContext`. `build/publish-desktop.sh` does the AOT publish, and with Homebrew's source-built .NET SDK it swaps in Microsoft's NativeAOT runtime pack so the app doesn't link Homebrew's OpenSSL and Brotli. `build/package-mac.sh` wraps the result in `Plaintain.app`.

## Releasing

Pushing a `v*` tag runs `.github/workflows/release.yml`. It runs the tests on macOS, Linux and Windows, packages each platform on its own runner with `build/package-release.sh` (Native AOT only targets the OS it builds on), and publishes a GitHub release with the archives. Before you tag, set `CFBundleShortVersionString` in `Plaintain.Desktop/Info.plist` to the new version: the packaging stops if the two disagree.

To build this machine's download locally, run `just package 1.0.0`. It goes into `dist/`.

## Shared libraries

The projects under `shared/` hold the parts that aren't specific to a plain text editor: `Plaintain.Text` (the document buffer, encodings and search), `Plaintain.Components.Core` (file locks, recovery snapshots and the CLI host) and `Plaintain.Components` (theming, commands and window chrome). The requirements Plaintain was built against are in `docs/plans/`.
