using Plaintain.Text.Document;

namespace Plaintain.Control.Controls;

public sealed partial class PlainTextEditor
{
    /// <summary>
    /// A line at least this long gets a <see cref="LongLine"/> index instead of a character walk. Below it the
    /// walk costs less than building the index; above it a minified JSON file or a one-line log dump would
    /// otherwise be walked character by character through the piece table on every keystroke and frame.
    /// </summary>
    internal const int LongLineThreshold = 512;

    /// <summary>
    /// The text of one long line and where its tabs are, so the column of any offset (and the offset at any
    /// column) is a binary search instead of a walk from the start of the line. Built from one immutable
    /// document snapshot, so it can never go stale: an edit makes a new snapshot, and the next lookup builds
    /// a new index.
    /// </summary>
    internal sealed class LongLine
    {
        private readonly int[] _tabAt;        // the index in Text of each tab, in order
        private readonly int[] _columnAfter;  // the expanded column just after each of those tabs

        public LongLine(TextDocument document, int line)
        {
            Document = document;
            Line = line;
            Start = document.GetLineStart(line);
            Text = document.GetLineText(line);
            DrawnLength = Text.Length > 0 && Text[^1] == '\r' ? Text.Length - 1 : Text.Length;

            var tabs = new List<int>();
            var after = new List<int>();
            int column = 0, last = 0;
            for (int i = Text.IndexOf('\t', StringComparison.Ordinal); i >= 0; i = Text.IndexOf('\t', i + 1))
            {
                column += i - last;                                   // the plain characters before this tab
                column = ((column / TabWidth) + 1) * TabWidth;        // and the tab itself
                tabs.Add(i);
                after.Add(column);
                last = i + 1;
            }

            _tabAt = [.. tabs];
            _columnAfter = [.. after];
            Columns = ColumnAt(Text.Length);
        }

        public TextDocument Document { get; }

        public int Line { get; }

        /// <summary>The document offset of the line's first character.</summary>
        public int Start { get; }

        /// <summary>The line without its <c>\n</c>, as <see cref="TextDocument.GetLineText"/> returns it.</summary>
        public string Text { get; }

        /// <summary>The length of <see cref="Text"/> without a trailing <c>\r</c>, which is never drawn.</summary>
        public int DrawnLength { get; }

        /// <summary>The expanded width of the whole line, <c>\r</c> included, as <see cref="LineColumns"/> counts it.</summary>
        public int Columns { get; }

        /// <summary>The expanded column the character at <paramref name="index"/> in <see cref="Text"/> starts at.</summary>
        public int ColumnAt(int index)
        {
            // The last tab before index: everything after it is one column per character.
            int lo = 0, hi = _tabAt.Length - 1, tab = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (_tabAt[mid] < index)
                {
                    tab = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return tab < 0 ? index : _columnAfter[tab] + (index - _tabAt[tab] - 1);
        }

        /// <summary>The first index in <see cref="Text"/> at or past <paramref name="column"/>, or the line's length.</summary>
        public int IndexAtColumn(int column)
        {
            // Columns rise strictly with the index, so the first index whose column reaches the target is a search.
            int lo = 0, hi = Text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (ColumnAt(mid) >= column)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }
    }

    // A few recent long lines. A frame or a keystroke asks about the caret's line and the lines on screen;
    // a handful of entries covers that without holding on to many megabyte-sized strings.
    private readonly LongLine?[] _longLines = new LongLine?[4];
    private int _nextLongLine;

    /// <summary>The index for <paramref name="line"/> when it is long enough to need one, otherwise null.</summary>
    internal LongLine? LongLineOf(int line)
    {
        var doc = Buffer.Document;
        if ((uint)line >= (uint)doc.LineCount || doc.GetLineEnd(line) - doc.GetLineStart(line) < LongLineThreshold)
        {
            return null;
        }

        foreach (var cached in _longLines)
        {
            if (cached is not null && cached.Line == line && ReferenceEquals(cached.Document, doc))
            {
                return cached;
            }
        }

        var built = new LongLine(doc, line);
        _longLines[_nextLongLine] = built;
        _nextLongLine = (_nextLongLine + 1) % _longLines.Length;
        return built;
    }
}
