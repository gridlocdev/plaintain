# Plaintain

Plaintain is a plain text editor for files that are just text: logs, config files, notes, a `LICENSE` with no extension. It shows a file exactly as it is and saves it back exactly as it was.

![Plaintain in its dark theme with server.log open. The title bar reads server.log. Under it, the find strip searches for ERROR and shows 1 of 2 matches, with the Aa, Word and Regex options off. Below, timestamped log lines in a monospace font fill the window with line numbers in the gutter, and the ERROR on line 20 is highlighted. The status bar reads Ln 20, Col 32 on the left and 61 lines, 4033 chars, UTF-8, LF on the right.](docs/images/plaintain.webp)

- **Byte for byte.** Encoding and line endings are kept exactly, and a file that isn't valid UTF-8 opens read-only instead of being saved through a guess.
- **Fast on big logs.** A 50 MB log opens and scrolls smoothly, and a log that another program is writing to is followed as it grows.
- **Editing you'd expect.** Find and replace with regex and whole-word options, multiple selections with ⌘D, line moves with ⌥↑ and ⌥↓, and a `plaintain` command for the terminal.

## Getting started

Download it for macOS, Windows or Linux from the [latest release](https://github.com/gridlocdev/plaintain/releases/latest). Each download has the app and the `plaintain` command, and needs nothing else installed.

See [Getting started](docs/getting-started.md) for first steps and for building it from source.

## Learn more

- [Features](docs/features.md) · [Controls](docs/controls.md) · [Colour themes](docs/themes.md)
- [All documentation](docs/README.md) · [Working on the code](docs/development.md)

## License

[MIT](LICENSE)
