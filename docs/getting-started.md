# Getting started

Plaintain runs on macOS 11 or later (Apple silicon or Intel), Windows 10 or later (x64), and Linux (x64) with a desktop. It is built ahead of time into a native program, so it doesn't need .NET installed.

## Download

Get the file for your system from the [latest release](https://github.com/gridlocdev/plaintain/releases/latest). The release page says how to install each one.

- **macOS:** unzip it, move `Plaintain.app` to `/Applications`, and put `plaintain` in a folder on your `PATH`, such as `~/.local/bin`. The app isn't notarized, so the first time you open it, macOS asks you to allow it in **System Settings ▸ Privacy & Security**.
- **Windows:** extract the zip and run `plaintain\desktop\Plaintain.exe`. The command is `plaintain\cli\plaintain.exe`. SmartScreen may warn you the first time: choose **More info**, then **Run anyway**.
- **Linux:** extract the archive and run `plaintain/desktop/Plaintain`. The command is `plaintain/cli/plaintain`.

## Build it from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [just](https://just.systems).

```bash
just install
```

This puts `Plaintain.app` in `/Applications` and the `plaintain` command in `~/.local/bin`. Use `just install-app` or `just install-cli` for only one of them. `just uninstall` removes both and leaves your settings alone.

On Linux, `just install` installs only the command. Start the app from the repository with `just run`. On Windows, use the download, or `just run` from the repository.

## First run

Open Plaintain from `/Applications`, or open a file from the terminal:

```bash
plaintain server.log
```

`.txt`, `.text` and `.log` files open in Plaintain from Finder. Config and data files such as `.conf`, `.ini`, `.json` and `.yaml` list it under **Open With**, without taking over the app they already open in.

## The `plaintain` command

```bash
plaintain server.log          # open it in Plaintain
plaintain                     # open Plaintain, empty
plaintain info server.log     # encoding, line endings, and line/word/character/byte counts
```

`plaintain info` exits non-zero when it had to guess the encoding, so a script can act on "this isn't text I can vouch for" without parsing the output.

## Where your settings live

On macOS, settings, crash-recovery snapshots and your own colour themes are in `~/Library/Application Support/Plaintain`. Open **Preferences** with ⌘, to change the theme, font size, word wrap, auto-save and recovery snapshots.
