using Puffinote.Text.Document;

namespace Puffinote.Text.Editing;

/// <summary>
/// The editable document model: the current immutable <see cref="TextDocument"/> snapshot, the
/// caret/selection, the undo history, and a dirty flag. This is the headless, Avalonia-free
/// surface the control drives — the UI never mutates a <see cref="TextDocument"/> directly; it
/// goes through these methods so every change is undoable and the dirty/caret state stays
/// consistent (PRD §10.2: file I/O lives in the shell, editing logic lives here).
/// </summary>
public sealed class DocumentBuffer
{
    private readonly EditHistory _history = new();
    private Selection _selection;
    private DocumentStats? _stats;

    public DocumentBuffer(string? text = null)
    {
        Document = TextDocument.FromString(text);
        _selection = Selection.At(0);
        SavedDocument = Document;
    }

    /// <summary>Raised after any change to the document or selection.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after a change to the document text (not for selection-only moves).</summary>
    public event EventHandler? TextChanged;

    /// <summary>The current immutable snapshot.</summary>
    public TextDocument Document { get; private set; }

    /// <summary>The snapshot last marked saved; the dirty flag compares against it.</summary>
    public TextDocument SavedDocument { get; private set; }

    /// <summary>
    /// The first line affected by the most recent text change — the seam for incremental
    /// re-tokenization. Lines above it are unchanged, so a consumer can reuse whatever it derived
    /// from them and re-derive only from here down. (Typage's Markdown tokenizer is the case this
    /// was written for; a consumer with nothing to re-derive can ignore it.)
    /// </summary>
    public int LastChangedLine { get; private set; }

    /// <summary>
    /// The lines the most recent text change touched: it began on <see cref="LineChange.FirstLine"/> and
    /// replaced <see cref="LineChange.OldLines"/> lines of the old document with
    /// <see cref="LineChange.NewLines"/> lines of the new one. Every line below that run is unchanged apart
    /// from its index, so a consumer that keeps something per line can fix up just that run and shift the
    /// rest instead of recomputing the whole document.
    /// </summary>
    public LineChange LastLineChange { get; private set; }

    /// <summary>The caret/selection.</summary>
    public Selection Selection
    {
        get => _selection;
        set
        {
            var clamped = Clamp(value);
            if (clamped == _selection)
            {
                return;
            }

            // A caret jump ends the current typing-coalescing run (PRD §7.2).
            _history.BreakCoalescing();
            _selection = clamped;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>True when the buffer differs from the last-saved snapshot (drives the title/status dirty mark).</summary>
    public bool IsDirty => !ReferenceEquals(Document, SavedDocument);

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    /// <summary>Replace the selection (or insert at the caret) with <paramref name="text"/>.</summary>
    public void Insert(string text)
    {
        var sel = _selection;
        ReplaceRange(sel.Start, sel.Length, text ?? string.Empty);
    }

    /// <summary>Delete the selection if any, else the grapheme before the caret (Backspace).</summary>
    public void Backspace()
    {
        var sel = _selection;
        if (!sel.IsEmpty)
        {
            ReplaceRange(sel.Start, sel.Length, string.Empty);
            return;
        }

        if (sel.Caret == 0)
        {
            return;
        }

        var prev = PreviousCaret(sel.Caret);
        ReplaceRange(prev, sel.Caret - prev, string.Empty);
    }

    /// <summary>Delete the selection if any, else the grapheme after the caret (Delete/forward-delete).</summary>
    public void Delete()
    {
        var sel = _selection;
        if (!sel.IsEmpty)
        {
            ReplaceRange(sel.Start, sel.Length, string.Empty);
            return;
        }

        if (sel.Caret >= Document.Length)
        {
            return;
        }

        var next = NextCaret(sel.Caret);
        ReplaceRange(sel.Caret, next - sel.Caret, string.Empty);
    }

    /// <summary>Replace <c>[start, start+length)</c> with <paramref name="text"/> as one undoable edit.</summary>
    public void ReplaceRange(int start, int length, string text)
    {
        ReplaceRange(start, length, text, caretAfter: null);
    }

    /// <summary>
    /// Replace <c>[start, start+length)</c> with <paramref name="text"/> as one undoable edit, placing the
    /// post-edit caret at <paramref name="caretAfter"/> when supplied instead of just after the inserted text.
    /// </summary>
    public void ReplaceRange(int start, int length, string text, int? caretAfter)
    {
        var edit = TextEdit.Replace(Document, start, length, text ?? string.Empty);
        if (edit.OldLength == 0 && edit.NewLength == 0)
        {
            return;
        }

        var before = _selection;
        Document = edit.Apply(Document);
        _selection = Selection.At(caretAfter ?? edit.CaretAfter);
        _history.Record(edit, before, _selection);
        RaiseTextChanged(LineChange.Of(Document, edit));
    }

    /// <summary>Undo the most recent step. Returns false if there was nothing to undo.</summary>
    public bool Undo()
    {
        var entry = _history.PopUndo();
        if (entry is null)
        {
            return false;
        }

        var undo = entry.Value.Edit.Invert();
        Document = undo.Apply(Document);
        _selection = Clamp(entry.Value.Before);
        RaiseTextChanged(LineChange.Of(Document, undo));
        return true;
    }

    /// <summary>Redo the most recently undone step. Returns false if there was nothing to redo.</summary>
    public bool Redo()
    {
        var entry = _history.PopRedo();
        if (entry is null)
        {
            return false;
        }

        Document = entry.Value.Edit.Apply(Document);
        _selection = Clamp(entry.Value.After);
        RaiseTextChanged(LineChange.Of(Document, entry.Value.Edit));
        return true;
    }

    /// <summary>Replace the whole document (open / revert / external reload). Clears undo history.</summary>
    public void Load(string? text, bool markSaved = true)
    {
        int oldLines = Document.LineCount;
        Document = TextDocument.FromString(text);
        if (markSaved)
        {
            SavedDocument = Document;
        }

        _selection = Selection.At(0);
        _history.Clear();
        RaiseTextChanged(new LineChange(0, oldLines, Document.LineCount)); // whole document replaced
    }

    /// <summary>
    /// Take the version on disk as <b>one undoable edit</b> — what "Reload" means when a file has changed
    /// underneath an open document. Unlike <see cref="Load"/> this keeps the history: the reload is a step
    /// like any other, so ⌘Z gives back what was in the editor a moment ago if the disk version turns out
    /// not to be what the reader wanted. A reload is also, by definition, clean afterwards, so the new text
    /// becomes the saved baseline — and undoing it therefore makes the document dirty again, which is
    /// exactly what it then is.
    /// </summary>
    public void ReloadFrom(string? text)
    {
        var replacement = text ?? string.Empty;
        if (Document.GetText() == replacement)
        {
            MarkSaved(); // identical bytes: nothing to undo, but the file and the buffer now agree
            return;
        }

        // The caret keeps its offset where the new text is long enough to hold it: the reader was reading
        // somewhere, and a reload that also sent them back to line 1 would lose their place twice over.
        ReplaceRange(0, Document.Length, replacement, caretAfter: Math.Min(_selection.Caret, replacement.Length));
        MarkSaved();
    }

    /// <summary>Mark the current snapshot as the saved baseline (after a successful save).</summary>
    public void MarkSaved() => SavedDocument = Document;

    /// <summary>
    /// The caret-legal offset one grapheme after <paramref name="offset"/>. Operates on the
    /// containing line's text (including its trailing newline) rather than materializing the whole
    /// document, so a caret move is O(line), not O(document). The window includes the <c>\n</c> so
    /// <c>\r\n</c> is still treated as one grapheme when crossing a line boundary.
    /// </summary>
    public int NextCaret(int offset)
    {
        offset = Math.Clamp(offset, 0, Document.Length);
        if (offset >= Document.Length)
        {
            return Document.Length;
        }

        var line = Document.GetPosition(offset).Line;
        var start = Document.GetLineStart(line);
        var end = line + 1 < Document.LineCount ? Document.GetLineStart(line + 1) : Document.Length;
        var window = Document.GetText(start, end - start);
        return start + Grapheme.NextBoundary(window, offset - start);
    }

    /// <summary>The caret-legal offset one grapheme before <paramref name="offset"/> (O(line), see <see cref="NextCaret"/>).</summary>
    public int PreviousCaret(int offset)
    {
        offset = Math.Clamp(offset, 0, Document.Length);
        if (offset <= 0)
        {
            return 0;
        }

        var line = Document.GetPosition(offset).Line;
        var start = Document.GetLineStart(line);
        if (offset == start)
        {
            // At a line start, step back over the previous line's newline (kept whole for \r\n).
            var prevStart = Document.GetLineStart(line - 1);
            var prevWindow = Document.GetText(prevStart, start - prevStart);
            return prevStart + Grapheme.PreviousBoundary(prevWindow, start - prevStart);
        }

        var end = line + 1 < Document.LineCount ? Document.GetLineStart(line + 1) : Document.Length;
        var window = Document.GetText(start, end - start);
        return start + Grapheme.PreviousBoundary(window, offset - start);
    }

    /// <summary>
    /// Live document statistics, cached and recomputed only when the text changes — not on every
    /// caret move. The status bar reads this on every selection change, so computing it eagerly
    /// would scan the whole document on each arrow keypress.
    /// </summary>
    public DocumentStats Stats => _stats ??= DocumentStats.Compute(Document);

    /// <summary>
    /// The same statistics, counted on a background thread so a large document doesn't stall the caller's
    /// thread for the length of the scan (about half a second for 50 MB). Safe because a
    /// <see cref="TextDocument"/> is an immutable snapshot: the count reads the snapshot this was called
    /// on while edits produce new ones. Returns null when the document changed before the count finished,
    /// since the result would describe text that is no longer there; whoever made the change asks again.
    /// Call it from the thread that edits the buffer: the result is cached on that thread when it resumes.
    /// </summary>
    public async Task<DocumentStats?> StatsInBackgroundAsync()
    {
        if (_stats is { } cached)
        {
            return cached;
        }

        var snapshot = Document;
        var stats = await Task.Run(() => DocumentStats.Compute(snapshot)).ConfigureAwait(true);
        if (!ReferenceEquals(snapshot, Document))
        {
            return null;
        }

        _stats = stats;
        return stats;
    }

    private Selection Clamp(Selection sel)
    {
        var len = Document.Length;
        return new Selection(Math.Clamp(sel.Anchor, 0, len), Math.Clamp(sel.Caret, 0, len));
    }

    private void RaiseTextChanged(LineChange change)
    {
        LastChangedLine = change.FirstLine;
        LastLineChange = change;
        _stats = null; // invalidate the cached stats; recomputed lazily on next read
        TextChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The run of lines one text change replaced: starting at <paramref name="FirstLine"/>,
/// <paramref name="OldLines"/> lines before the change became <paramref name="NewLines"/> lines after it.
/// Both counts are at least 1, since even an edit inside one line touches that line.
/// </summary>
public readonly record struct LineChange(int FirstLine, int OldLines, int NewLines)
{
    /// <summary>The run <paramref name="edit"/> replaced, given the document it produced.</summary>
    internal static LineChange Of(TextDocument after, TextEdit edit) => new(
        after.GetPosition(edit.Start).Line,
        edit.Removed.AsSpan().Count('\n') + 1,
        edit.Inserted.AsSpan().Count('\n') + 1);
}
