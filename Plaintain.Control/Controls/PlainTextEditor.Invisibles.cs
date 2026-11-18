using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Plaintain.Control.Controls;

public sealed partial class PlainTextEditor
{
    internal const char TabMark = '→';
    internal const char SpaceMark = '·';
    internal const char CrlfMark = '¤';

    // One shaped glyph per mark, cleared with the other shape caches when the font, size or colours change.
    private readonly Dictionary<char, FormattedText> _invisibleGlyphs = [];
    private readonly List<(char Mark, int Column)> _invisibleScratch = [];

    /// <summary>
    /// The marks row <paramref name="segment"/> of <paramref name="line"/> shows, as expanded columns: every
    /// tab, every space after the line's last visible character, and the line's CRLF ending on its last row.
    /// A plain LF ending gets no mark: it is the usual one, and marking every line would bury the CRLFs.
    /// Without wrap only the columns in the viewport are looked at, so a one-megabyte line costs a screen.
    /// </summary>
    private void CollectInvisibles(int line, int segment, int startColumn, List<(char Mark, int Column)> marks)
    {
        var doc = Buffer.Document;
        int lineStart = doc.GetLineStart(line);
        int lineEnd = doc.GetLineEnd(line);
        bool crlf = lineEnd > lineStart && doc[lineEnd - 1] == '\r' && lineEnd < doc.Length;
        int bodyEnd = lineEnd > lineStart && doc[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;

        int from = Math.Max(lineStart, OffsetAtColumn(line, startColumn));
        int to = Math.Min(bodyEnd, RowEndOffsetFor(line, segment));
        if (!WordWrap && _charWidth > 0)
        {
            int firstColumn = Math.Max(startColumn, (int)(_offset.X / _charWidth) - 1);
            int lastColumn = firstColumn + (int)Math.Ceiling(Bounds.Width / _charWidth) + 2;
            from = Math.Max(from, OffsetAtColumn(line, firstColumn));
            to = Math.Min(to, OffsetAtColumn(line, lastColumn));
        }

        // Where the trailing run starts: walk back from the end over spaces and tabs.
        int trailing = bodyEnd;
        while (trailing > lineStart && doc[trailing - 1] is ' ' or '\t')
        {
            trailing--;
        }

        if (to > from)
        {
            string text = doc.GetText(from, to - from);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\t')
                {
                    marks.Add((TabMark, ColumnOf(line, from + i)));
                }
                else if (c == ' ' && from + i >= trailing)
                {
                    marks.Add((SpaceMark, ColumnOf(line, from + i)));
                }
            }
        }

        if (crlf && RowEndOffsetFor(line, segment) >= bodyEnd)
        {
            marks.Add((CrlfMark, ColumnOf(line, bodyEnd)));
        }
    }

    private void DrawRowInvisibles(DrawingContext context, Palette palette, int line, int segment, int startColumn, double y)
    {
        _invisibleScratch.Clear();
        CollectInvisibles(line, segment, startColumn, _invisibleScratch);
        foreach (var (mark, column) in _invisibleScratch)
        {
            if (!_invisibleGlyphs.TryGetValue(mark, out var glyph))
            {
                glyph = new FormattedText(mark.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    _typeface, ScaledFontSize, palette.GutterText);
                _invisibleGlyphs[mark] = glyph;
            }

            // Centred in its column, so a fallback font's wider or narrower glyph still sits on its character.
            double x = TextLeft - _offset.X + ((column - startColumn) * _charWidth) + ((_charWidth - glyph.Width) / 2);
            context.DrawText(glyph, new Point(x, y + ((_rowHeight - glyph.Height) / 2)));
            InvisiblesDrawnForTest++;
        }
    }

    /// <summary>Test seam: how many marks have been drawn.</summary>
    internal int InvisiblesDrawnForTest { get; private set; }

    /// <summary>Test seam: the marks row <paramref name="row"/> would draw, whether or not they are on.</summary>
    internal IReadOnlyList<(char Mark, int Column)> InvisiblesForTest(int row)
    {
        var (line, segment) = LineAtRow(row);
        var marks = new List<(char Mark, int Column)>();
        CollectInvisibles(line, segment, WordWrap ? SegmentColumns(line, segment).Start : 0, marks);
        return marks;
    }
}
