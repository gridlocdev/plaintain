# Plaintain

A Notepad-class plaintext editor for macOS (and Linux), built with C# and Avalonia. Use it when a file is just text.

- Opens any decodable file exactly as it is: monospace, uniform rows, a line-number gutter, word wrap off by default.
- Keeps encoding and line endings byte for byte. A file whose bytes aren't valid UTF-8 opens read-only, so it is never rewritten through a guess.
- Stays fast on a 50 MB log, because the surface draws only the visible rows.

It doesn't highlight syntax, fold JSON, or show a CSV as a table. Structured config is still text.

## Getting started

You need the .NET 10 SDK and [just](https://just.systems).

```bash
just build
```

```bash
just test
```

```bash
just run
```

Install the `plaintain` CLI into `~/.local/bin`, and on macOS `Plaintain.app` into `/Applications`:

```bash
just install
```

`just uninstall` undoes it. Run bare `just` to see every recipe.

## The `plaintain` CLI

```bash
plaintain server.log          # open it in Plaintain
plaintain                     # open Plaintain, empty
plaintain info server.log     # encoding, line endings, and line/word/character/byte counts
```

`plaintain info` exits non-zero when the encoding had to be guessed, so a script can act on "this isn't text I can vouch for" without parsing the output.

## Docs

- [docs/PRD.md](docs/PRD.md): what it does and why
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how it is built, including the state of accessibility
- [docs/proposals/0003-plaintain.md](docs/proposals/0003-plaintain.md): the original design rationale
- [AGENTS.md](AGENTS.md): working in this repo

## History

Plaintain started inside the Puffinote repo as `features/EditPlaintext/`, next to sibling editors that shared the `shared/` libraries. It moved to its own repo with its history kept. The `shared/` projects (`Puffinote.Text`, `Puffinote.Components`, `Puffinote.Components.Core`) came along and keep their old names.

## License

MIT. See [LICENSE](LICENSE).
