using Avalonia.Automation.Peers;

namespace Plaintain.Control.Controls;

public sealed partial class PlainTextEditor
{
    /// <summary>
    /// The most text the accessibility value carries. The visible rows are usually a few kilobytes; this
    /// bounds the one case where they are not, a single wrapped or unwrapped line of many megabytes.
    /// </summary>
    internal const int AccessibleTextLimit = 32 * 1024;

    /// <summary>
    /// Raised when the text on screen may have changed: an edit, a scroll, or a resize. The automation peer
    /// listens, so a screen reader hears about it; nothing else needs it.
    /// </summary>
    internal event EventHandler? VisibleTextChanged;

    /// <summary>
    /// The document range the viewport shows: from the start of the first visible row to the end of the
    /// last, at most <see cref="AccessibleTextLimit"/> characters. Only these rows are exposed to
    /// accessibility, because the control virtualizes (ARCHITECTURE §3.2) and copying a 50 MB log into a
    /// string on every keystroke would undo that.
    /// </summary>
    internal (int Start, int Length) VisibleTextRange()
    {
        var doc = Buffer.Document;
        int totalRows = TotalRows;
        if (doc.Length == 0 || totalRows == 0 || _rowHeight <= 0)
        {
            return (0, Math.Min(doc.Length, AccessibleTextLimit));
        }

        int firstRow = Math.Clamp((int)(_offset.Y / _rowHeight), 0, totalRows - 1);
        int lastRow = Math.Clamp((int)((_offset.Y + Bounds.Height) / _rowHeight), firstRow, totalRows - 1);
        var (firstLine, firstSegment) = LineAtRow(firstRow);
        var (lastLine, lastSegment) = LineAtRow(lastRow);

        int start = WordWrap ? OffsetAtColumn(firstLine, SegmentColumns(firstLine, firstSegment).Start) : doc.GetLineStart(firstLine);
        int end = Math.Max(start, RowEndOffsetFor(lastLine, lastSegment));
        int length = Math.Min(end - start, AccessibleTextLimit);
        if (length < end - start && start + length < doc.Length && char.IsLowSurrogate(doc.GetText(start + length, 1)[0]))
        {
            length--; // never end the value on half a surrogate pair
        }

        return (start, length);
    }

    /// <summary>The text of <see cref="VisibleTextRange"/>.</summary>
    internal string VisibleText()
    {
        var (start, length) = VisibleTextRange();
        return Buffer.Document.GetText(start, length);
    }

    /// <summary>
    /// Replace the visible text, as one undoable edit. This is the write half of the accessibility value: a
    /// tool that reads the value, changes it and sets it back changes exactly the text it read, never the
    /// rest of the document.
    /// </summary>
    internal void ReplaceVisibleText(string text)
    {
        var (start, length) = VisibleTextRange();
        Buffer.ReplaceRange(start, length, text ?? string.Empty);
        BringCaretIntoView();
        InvalidateVisual();
    }

    private void RaiseVisibleTextChanged() => VisibleTextChanged?.Invoke(this, EventArgs.Empty);

    protected override AutomationPeer OnCreateAutomationPeer() => new PlainTextEditorAutomationPeer(this);
}
