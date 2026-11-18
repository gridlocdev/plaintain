# Plaintain — Architecture

How the plaintext editor is put together. What it does and why is [PRD.md](PRD.md); the design
rationale is [proposal 0003](proposals/0003-plaintain.md).

## 1. Projects

| Project | Role |
|---|---|
| `Plaintain.Control` | The Avalonia control library: `PlainTextEditor` (the custom-drawn surface), `PlainTextEditorView` (the embeddable composite), `FindBar`, and the one command catalog `PlainTextEditingCommands`. |
| `Plaintain.Desktop` | The standalone shell (assembly `Plaintain`): window, menu, status bar, file dialogs, recents, settings. Thin. |
| `Plaintain.Cli` | `plaintain` — a launcher plus one read-only verb. No Avalonia. |
| `Plaintain.UiTests` | Headless Avalonia. The gate, since the devcontainer can't launch a GUI. |
| `Plaintain.Cli.Tests` | End-to-end, in-process, over the same `CliApplication.Run`. |
| `Plaintain.Benchmarks` | BenchmarkDotNet over the 50 MB load path and the geometry claim (§3.3). References only `Puffinote.Text`, so it runs without a display. `just bench`. |

Dependency direction is strictly **shell → control → shared**. Nothing references the Puffinote app.

## 2. What comes from `shared/`

- **`Puffinote.Text`** — the piece table (`TextDocument`), grapheme navigation, `Selection`,
  `DocumentBuffer` + undo, `DocumentSearch`, `DocumentStats`, `TextFileCodec`. Everything semantic.
  Typage stands on the same code, which is what keeps the two editors' carets, undo, and search from
  drifting apart.
- **`Puffinote.Components`** — `AppCommand` / `Shortcut` (so the catalog is the family's shape) and the
  theme tokens the surface paints its chrome with.
- **`Puffinote.Components.Core`** — `CliHost` / `CliException`, the shared CLI run harness.

## 3. The surface

`PlainTextEditor` is one control split across partials:

| Partial | Owns |
|---|---|
| `PlainTextEditor.cs` | Metrics, the row map, options, `IScrollable`, measure/arrange |
| `.Caret.cs` | Row↔offset mapping, movement, the goal column, scrolling the caret into view |
| `.Input.cs` | Keyboard, typing, indent/outdent, undo/redo |
| `.Pointer.cs` | Click, drag-select, double/triple click, the wheel |
| `.MultiSelection.cs` | ⌘D: the extra selections, and the one-edit-for-all-of-them rewrite |
| `.Occurrences.cs` | Which other appearances of the selected text get tinted (PRD §5) |
| `.Rendering.cs` | The paint: gutter, rows, occurrences, selection, matches, caret |
| `.Clipboard.cs` | Cut/copy/paste, with line-ending normalisation on the way in |

### 3.1 Coordinates

Three spaces, and confusing them is the easiest bug to write here:

- **Offset** — a character index into the document. What `Selection` and every edit speak.
- **Column** — an *expanded* column on a line: a tab advances to the next 4-stop, so a column is not a
  string index. `ColumnOf` / `OffsetAtColumn` convert.
- **Row** — a visual row. With wrap off, row == line. With wrap on, `ComputeWrap` walks the line and
  returns the columns its rows begin at — greedily filling a row and breaking at the last word boundary
  that fits, or at the edge when a word is longer than a row. Those starts **partition** the line's
  columns, so every column still belongs to exactly one row; `_rowStarts` (a cumulative map, built only
  while wrapping) converts line ↔ row by binary search.

### 3.2 Virtualization

The control reports a viewport-sized `MeasureOverride` and a document-sized `Extent`, and takes an
`Offset` from the host `ScrollViewer`. `Render` walks only `firstRow..lastRow`. **Anything that makes
the control's own size depend on the document breaks this silently** — every test still passes and a
large file freezes.

The selection-occurrence tint (PRD §5) is built to the same rule, and it is the reason that highlight
only ever considers a single-line selection: each visible row searches *its own line text* — the string
the painter has already read to draw the row — for the selected chunk, so a 50 MB log costs what a
50-line one does and nothing walks the document. The alternative, finding every occurrence up front and
keeping the list, is the shape §3.4 is a cautionary tale about.

The horizontal extent grows from what has been *seen*, not from measuring every line: finding the
longest line in a 50 MB file would undo the point of never touching an off-screen row, and a log reader
expects the bar to settle as they scroll anyway.

### 3.3 What 50 MB actually costs

Proposal 0003 §9.5 said to measure before optimising, and until `Plaintain.Benchmarks` existed nobody
had — the geometry was designed against 50 MB and had never been run against it. Measured on the Linux
devcontainer (arm64, Release, BenchmarkDotNet short job), on a log-shaped file of many short lines:

| | 1 MB | 50 MB |
|---|---|---|
| Decode (validating UTF-8) | 0.45 ms | 18.6 ms |
| Build the buffer (the line-start index) | 0.7 ms | 22.7 ms |
| **Open, end to end** | **1.4 ms** | **41 ms** |
| Allocated to open | 5.9 MB | **297 MB** |
| Caret position lookup, start / end of file | 5 / 6 ns | 9 / 11 ns |
| One screenful (60 lines) from the middle | 1.24 µs | 1.24 µs |

Two things to take from it. **The geometry claim holds**: a screenful from the middle of a 50 MB file
costs exactly what it costs at 1 MB, and a position lookup is nanoseconds at either end — the arithmetic
really is arithmetic, and nothing scales with document size. **Opening allocates about six times the file
size** (a UTF-16 string, plus the piece table and index over it), which is the number to know before
anyone reaches for a bigger target: 41 ms is imperceptible, 300 MB of managed heap for one document is
not nothing.

A surprise worth recording: a file that is *not* valid UTF-8 decodes **faster** than one that is, at
either end of the file, because the Latin-1 fallback is a byte-widening where a UTF-8 read is a
transcode. The strict-validation pass that gets thrown away costs less than the transcode it avoids.

### 3.4 Typing is not the edit — it is the status bar

The same harness, on the same file, measuring one keystroke:

| | 1 MB | 50 MB |
|---|---|---|
| The edit alone (`Insert`) | 0.6 ms | 23 ms |
| The edit **plus reading `Stats`** for the status bar | 9.3 ms | **491 ms** |
| Allocated | 7.8 MB | 392 MB |

`DocumentBuffer.Stats` caches its answer only until the next change, so a status bar that reads it per
keystroke pays a full pass over the document per keystroke: `GetText()` materialises the whole file as one
string, and the word, grapheme and line-ending counts each walk it. On a 50 MB log that is **21× the cost
of the edit it is reporting**, in the editor whose entire design brief is files that size.

The window **debounces** it, as Typage does and to the same 200 ms: the caret's position (which is what someone watches while typing) stays immediate, and the counts settle once the typing pauses.

The scan itself now runs **off the UI thread**. `DocumentBuffer.StatsInBackgroundAsync` counts an immutable snapshot on a background thread, returns the cached answer at once when there is one, and returns null when the document changed during the count, so a stale result is dropped and the edit that changed it asks again. The window uses it in three places:

- **After a typing pause**, always. Keys pressed while a 50 MB count runs are no longer held for half a second; the status bar shows the previous counts until the new ones arrive.
- **On open**, from 1 M characters up (`MainWindow.CountInBackgroundFrom`). The status bar shows the line count, which the read already knows, with "counting…" for the rest. A 50 MB log's window now shows in about 150–210 ms instead of 620–700 ms, and the counts arrive at about 600–700 ms.
- **Normalise Line Endings** counts the endings it offers to rewrite in the background, and builds the rewritten text there too, so only the edit itself (about 90–190 ms at 50 MB) runs on the UI thread.

What is still open: each count is still a full pass, and `DocumentStats.Compute` still materialises the document as one string to make it. Counts maintained incrementally against `LastChangedLine`, the way the line index is, would remove the pass. That is a shared change, since Typage pays the same cost through the same type, and it wants its own measurement first.

## 4. The command catalog

`PlainTextEditingCommands.Build(view)` returns shared `AppCommand`s closed over the view. The view
caches the list, so a menu item, a key binding, and a host's palette all hold the *same* command object
and therefore the same enabled/active state.

One command needs a host: **Go to Line** must ask for a number, and how you ask belongs to whoever owns
the window. The command raises `GoToLineRequested`; the shell answers with its own prompt. The command
is still the editor's — it acts on the document — and only the prompt is the shell's.

## 5. Packaging

`build/package-mac.sh` (or `just publish-app`) builds `Plaintain.app` from `Plaintain.Desktop/Info.plist`.
The justfile carries the full recipe set, each
install with its exact inverse (including the `lsregister -u` withdrawal that removes the file-type
claims when the bundle goes).

**A claimed file arrives two ways, and only one of them is argv.** A launch argument comes in as
`desktop.Args`, read once at startup. macOS does not use it: Finder's "Open With", a double-click on a
claimed `.txt`, and `plaintain <file>` itself — which reaches the installed bundle through Launch
Services — all send a *bundled* app an Apple Event, which Avalonia surfaces as
`IActivatableLifetime.Activated` carrying `FileActivatedEventArgs`. Claiming the type in `Info.plist`
without handling that event is what opened the association onto an empty "Untitled" buffer, with the
file dropped on the floor. `App` handles both, through one `DocumentToOpen` rule — first candidate, must
exist on disk. The mechanism lives in `shared/` (`DocumentActivation` on the app side, `AppLauncher` on the CLI side)
rather than in Plaintain's own projects.

The two arrivals differ in one way that reaches the user: an argument lands before anything is on
screen, while an activation can land over a document being edited (and over the crash-recovery prompt).
So the activation goes through `MainWindow.OpenFromDesktop`, which waits out any open modal — the
overlay hosts one card at a time — and then asks before discarding unsaved work.

**`plaintain <file>` takes any path that exists**, with no extension test, because "opens anything
decodable" is the product (§1). The sibling CLIs refuse an extension their app doesn't open; here that
would be a list of every extension in the world minus a few, which is not a list.

## 6. Accessibility

The editor is **custom-drawn**. `PlainTextEditor` (`Plaintain.Control/Controls/PlainTextEditor*.cs`) is a plain `Avalonia.Controls.Control` that paints every row itself in `Render` with `FormattedText` (§3). It is not a `TextBox`, a web view or `contenteditable`, or CodeMirror or any other editor component, so it gets no text accessibility for free. What it exposes:

- **Its own automation peer**, `PlainTextEditorAutomationPeer`. Its control type is `Edit`, which Avalonia's macOS backend shows as an `AXTextField` named "Text editor". (The other text type, `Document`, becomes `AXStaticText`, which tells assistive technology the text cannot be edited.)
- **The text on screen as its value.** The peer implements `IValueProvider`, which macOS reads as `AXValue`. The value is the text from the first visible row to the last (`VisibleTextRange` in `PlainTextEditor.Accessibility.cs`), at most 32 K characters, never the whole document: the control virtualizes (§3.2), and a 50 MB log must not be copied into a string on every keystroke. Setting the value replaces that same range as one undoable edit, so a tool that reads, changes and writes back the value changes only what it read.
- **Change events.** After an edit, a scroll or a resize, the peer raises a value-changed event (`AXValueChanged`), once per burst and only when the text on screen differs. Nothing is computed until something has read the value.
- **An input-method client** (`PlainTextEditor.InputMethod.cs`, an Avalonia `TextInputMethodClient`). That is the IME and dictation *input* path, separate from the Accessibility API.

Still missing: the caret and the selection. Avalonia 12.0 has no text-range or selected-text provider for a custom control, so `AXSelectedTextRange` and `AXSelectedTextChanged` are not exposed. When Avalonia adds a text pattern, implement it over `TextDocument` and `Selection`, with ranges in document offsets mapped to the visible value.
