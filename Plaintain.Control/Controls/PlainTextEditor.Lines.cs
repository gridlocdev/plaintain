using Puffinote.Text.Document;

namespace Plaintain.Control.Controls;

public sealed partial class PlainTextEditor
{
    /// <summary>
    /// The lines the selection covers, as one block: where it starts, where its text ends (before the last
    /// line's own line ending) and where it ends (after that ending, or at the end of the document when the
    /// last line has none). A selection that ends at the very start of a line doesn't take that line, so
    /// selecting two whole lines by dragging to the start of the third moves two lines, not three.
    /// </summary>
    private readonly record struct LineBlock(int FirstLine, int LastLine, int Start, int BodyEnd, int End)
    {
        public int Length => End - Start;

        /// <summary>The block's own line ending, empty when it is the last line of a document with no final newline.</summary>
        public bool HasEnding => End > BodyEnd;
    }

    private LineBlock SelectedLines()
    {
        var doc = Buffer.Document;
        var sel = Buffer.Selection;
        int first = doc.GetPosition(sel.Start).Line;
        int last = doc.GetPosition(sel.End).Line;
        if (last > first && sel.End == doc.GetLineStart(last))
        {
            last--;
        }

        return BlockOf(doc, first, last);
    }

    private static LineBlock BlockOf(TextDocument doc, int first, int last)
    {
        int start = doc.GetLineStart(first);
        int end = last + 1 < doc.LineCount ? doc.GetLineStart(last + 1) : doc.Length;
        int bodyEnd = doc.GetLineEnd(last);
        if (bodyEnd > start && doc[bodyEnd - 1] == '\r' && bodyEnd < doc.Length)
        {
            bodyEnd--; // a CRLF's \r belongs to the ending, not the text
        }

        return new LineBlock(first, last, start, bodyEnd, end);
    }

    /// <summary>
    /// Swap the selected lines with the line above (<paramref name="direction"/> -1) or below (+1), as one
    /// undoable edit, keeping the selection on the lines that moved. Each line keeps its own line ending, so a
    /// CRLF file stays CRLF; when the swap involves the last line of a file with no final newline, the ending
    /// moves with the position, not the text, and the file still ends without one.
    /// </summary>
    public void MoveLines(int direction)
    {
        var doc = Buffer.Document;
        var block = SelectedLines();
        int neighbourLine = direction < 0 ? block.FirstLine - 1 : block.LastLine + 1;
        if (neighbourLine < 0 || neighbourLine >= doc.LineCount)
        {
            return;
        }

        var neighbour = BlockOf(doc, neighbourLine, neighbourLine);
        var (upper, lower) = direction < 0 ? (neighbour, block) : (block, neighbour);

        // The lower piece goes first. If it has no ending (it's the file's last line), it borrows the upper
        // piece's, and the upper piece, now last, gives its ending up.
        string lowerText = doc.GetText(lower.Start, lower.Length);
        string upperText = doc.GetText(upper.Start, upper.Length);
        string replacement = lower.HasEnding
            ? lowerText + upperText
            : lowerText + doc.GetText(upper.BodyEnd, upper.End - upper.BodyEnd) + doc.GetText(upper.Start, upper.BodyEnd - upper.Start);

        // Where the block's first character lands: the region's start when it moves up, or after the
        // neighbour (with whatever ending it now carries) when it moves down.
        int newStart = direction < 0
            ? upper.Start
            : upper.Start + (lower.HasEnding ? lower.Length : lower.Length + (upper.End - upper.BodyEnd));
        ApplyLineEdit(upper.Start, lower.End - upper.Start, replacement, newStart - block.Start);
    }

    /// <summary>
    /// Copy the selected lines below themselves (<paramref name="direction"/> +1, the selection follows the
    /// copy) or above themselves (-1, the selection stays on the upper of the two), as one undoable edit.
    /// </summary>
    public void DuplicateLines(int direction)
    {
        var doc = Buffer.Document;
        var block = SelectedLines();
        string body = doc.GetText(block.Start, block.BodyEnd - block.Start);
        string ending = block.HasEnding ? doc.GetText(block.BodyEnd, block.End - block.BodyEnd) : NewlineOf(doc);

        if (direction < 0)
        {
            ApplyLineEdit(block.Start, 0, body + ending, shift: 0);
        }
        else if (block.HasEnding)
        {
            ApplyLineEdit(block.End, 0, body + ending, shift: block.Length);
        }
        else
        {
            ApplyLineEdit(block.End, 0, ending + body, shift: body.Length + ending.Length);
        }
    }

    private void ApplyLineEdit(int start, int length, string text, int shift)
    {
        var sel = Buffer.Selection;
        Buffer.ReplaceRange(start, length, text, sel.Caret + shift);
        Buffer.Selection = new Selection(sel.Anchor + shift, sel.Caret + shift);
        ClearGoalColumn();
        BringCaretIntoView();
        InvalidateVisual();
    }
}
