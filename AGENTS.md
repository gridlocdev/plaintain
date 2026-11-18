# Plaintain: agent guide

Plaintain is a Notepad-class plaintext editor in C# and Avalonia. It opens any decodable file, claims `.txt` and similar types in Finder, and its CLI is `plaintain`. Read [docs/plans/prd.md](docs/plans/prd.md) (what and why) and [docs/architecture.md](docs/architecture.md) (how). When this file and a doc disagree, the doc wins on design detail and this file wins on process.

## Layout

| Path | What it is |
| --- | --- |
| `Plaintain.Control/` | The editor surface `PlainTextEditor` (custom-drawn, see below), `PlainTextEditorView`, `FindBar`, and the command catalog `PlainTextEditingCommands`. |
| `Plaintain.Desktop/` | The app shell (assembly `Plaintain`): window, menu, status bar, file dialogs, recents, settings. |
| `Plaintain.Cli/` | The `plaintain` CLI: a launcher plus one read-only verb, `info`. No Avalonia. |
| `Plaintain.UiTests/` | Headless Avalonia UI tests. No window opens. |
| `Plaintain.Cli.Tests/` | CLI tests, in process. |
| `Plaintain.Benchmarks/` | BenchmarkDotNet over the 50 MB load path. |
| `shared/` | Libraries below the app: `Plaintain.Text` (piece table, graphemes, selection, undo, search, stats, file codec), `Plaintain.Components` (commands, shortcuts, theming, shell services), `Plaintain.Components.Core` (CLI harness, file locks), and `TestSupport` (hermetic test profile). |
| `build/package-mac.sh` | Builds `Plaintain.app`. |

Dependencies go one way: `Plaintain.Desktop` → `Plaintain.Control` → `shared/`.

## How the editor is built

`PlainTextEditor` is a custom-drawn Avalonia `Control`, split across `PlainTextEditor.*.cs` partials. It paints the gutter, rows, selection and caret itself with `FormattedText`. It is not a `TextBox`, a web view, `contenteditable` or CodeMirror. It implements `IScrollable`, so it stays viewport-sized and draws only visible rows. Its automation peer (`PlainTextEditorAutomationPeer`) exposes the visible rows as an editable text value. See architecture.md §6 before you work on accessibility.

## Commands

```bash
just build                 # dotnet build Plaintain.slnx
just test                  # every suite, headless
just test-filter SomeTest
just cli info server.log
just check                 # format-verify + build + test: the gate
```

Done means `just check` passes. Warnings are errors (`TreatWarningsAsErrors=true`), so "builds" means zero warnings.


Check your work headlessly through `Plaintain.UiTests` or `plaintain info`. `just run` opens a visible window, so don't use it to check changes.

## Golden rules

1. **Uniform rows.** Every row is the same height and every character the same width, because the font is monospace. So a line's row count is `ceil(columns / columnsPerRow)`, which is what makes a 50 MB log workable. Don't add proportional fonts, variable row heights or per-line measurement to the layout path without reopening PRD §3.
2. **No format-aware rendering.** No syntax highlighting, JSON folding or CSV tables.
3. **Every command that acts on the document lives in `Plaintain.Control`** (`PlainTextEditingCommands`). The shell adds the window, file dialogs, recents and settings, nothing else.
4. **Byte-for-byte file handling.** Keep encoding and line endings exactly. A file whose bytes aren't valid UTF-8 opens read-only. See PRD §4.
5. **Guard the supply chain.** The trusted base is the .NET BCL plus first-party Avalonia. A new NuGet package goes into the allowlist in `Directory.Build.targets` with a stated reason, and only after the owner agrees. Package versions are pinned centrally in `Directory.Packages.props`. Never npm.

## Gotchas

- Anything that makes the control's own size depend on the document breaks virtualization silently. The tests still pass and a large file freezes. The document's real size lives in `Extent`.
- Columns are expanded columns: a tab moves to the next 4-stop, so `ColumnOf` and `OffsetAtColumn` are not string indices.
- `FormattedText` has no tab stops, so the renderer expands tabs to spaces before it draws.
- The test-only analyzer relaxations in `.editorconfig` match `*Tests/` folders at any depth. Keep test project names ending in `Tests`.
- The desktop app publishes with Native AOT (`PublishAot` in `Plaintain.Desktop.csproj`), so reflection-based code fails there even when tests pass. Serialise JSON through a source-generated `JsonSerializerContext` (see `SettingsJsonContext`), and don't use `init` on a JSON property that has a default: the generated reader sets it to null when the key is missing.
- `build/publish-desktop.sh` does the AOT publish for `just publish-desktop` and `just publish-app`. With Homebrew's source-built .NET SDK it swaps in Microsoft's portable NativeAOT runtime pack from NuGet, so the app doesn't link Homebrew's OpenSSL and Brotli.
- To measure cold start without showing a window, run the built app with `PLAINTAIN_STARTUP_PROBE=<scratch dir>`. It opens the real window off-screen and transparent, reads settings and recovery files from that directory, prints what it read and `first-frame-ms N`, then exits.

## Git

- Conventional Commits with an area scope: `feat(editor): …`, `fix(cli): …`, `docs: …`.
- Small, coherent commits. Commit only when `just check` is green apart from the known failure above.
- Rebase onto `main` and fast-forward merge. No merge commits.
