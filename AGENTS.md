# Plaintain: agent guide

Plaintain is a Notepad-class plaintext editor in C# and Avalonia. It opens any decodable file, claims `.txt` and similar types in Finder, and its CLI is `plaintain`. Read [docs/PRD.md](docs/PRD.md) (what and why) and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (how). The design rationale is [docs/proposals/0003-plaintain.md](docs/proposals/0003-plaintain.md). When this file and a doc disagree, the doc wins on design detail and this file wins on process.

## Layout

| Path | What it is |
| --- | --- |
| `Plaintain.Control/` | The editor surface `PlainTextEditor` (custom-drawn, see below), `PlainTextEditorView`, `FindBar`, and the command catalog `PlainTextEditingCommands`. |
| `Plaintain.Desktop/` | The app shell (assembly `Plaintain`): window, menu, status bar, file dialogs, recents, settings. |
| `Plaintain.Cli/` | The `plaintain` CLI: a launcher plus one read-only verb, `info`. No Avalonia. |
| `Plaintain.UiTests/` | Headless Avalonia UI tests. No window opens. |
| `Plaintain.Cli.Tests/` | CLI tests, in process. |
| `Plaintain.Benchmarks/` | BenchmarkDotNet over the 50 MB load path. |
| `shared/` | Libraries brought over from the Puffinote repo: `Puffinote.Text` (piece table, graphemes, selection, undo, search, stats, file codec), `Puffinote.Components` (commands, shortcuts, theming, shell services), `Puffinote.Components.Core` (CLI harness, file locks), and `TestSupport` (hermetic test profile). They keep their old names. |
| `build/package-mac.sh` | Builds `Plaintain.app`. |

Dependencies go one way: `Plaintain.Desktop` → `Plaintain.Control` → `shared/`.

## How the editor is built

`PlainTextEditor` is a custom-drawn Avalonia `Control`, split across `PlainTextEditor.*.cs` partials. It paints the gutter, rows, selection and caret itself with `FormattedText`. It is not a `TextBox`, a web view, `contenteditable` or CodeMirror. It implements `IScrollable`, so it stays viewport-sized and draws only visible rows. It has no automation peer of its own, so macOS sees it as a generic `AXGroup`. See ARCHITECTURE.md §6 before you work on accessibility.

## Commands

```bash
just build                 # dotnet build Plaintain.slnx
just test                  # every suite, headless
just test-filter SomeTest
just cli info server.log
just check                 # format-verify + build + test: the gate
```

Done means `just check` passes. Warnings are errors (`TreatWarningsAsErrors=true`), so "builds" means zero warnings.

Known failure: `HermeticProfileTests.Every_user_data_location_resolves_inside_the_throwaway_profile` fails on macOS with the .NET 10.0.4xx SDK, because `ApplicationData` still resolves to the real `~/Library/Application Support`. It failed the same way in the Puffinote repo before the move. Until someone fixes it, `just check` has these three failures (one per test assembly).

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

## Git

- Conventional Commits with an area scope: `feat(editor): …`, `fix(cli): …`, `docs: …`.
- Small, coherent commits. Commit only when `just check` is green apart from the known failure above.
- Rebase onto `main` and fast-forward merge. No merge commits.
