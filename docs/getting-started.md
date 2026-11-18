# Getting started

You build Plaintain from source and install it on your Mac. On Linux you can build and run it from source.

## What you need

- macOS on Apple silicon or Intel, or Linux with a desktop.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) and [just](https://just.systems), to build it.

The installed app doesn't need .NET: it is built ahead of time into a native program.

## Install it

```bash
just install
```

This puts `Plaintain.app` in `/Applications` and the `plaintain` command in `~/.local/bin`. Use `just install-app` or `just install-cli` for only one of them. `just uninstall` removes both and leaves your settings alone.

On Linux, `just install` installs only the command. Start the app from the repository with `just run`.

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
