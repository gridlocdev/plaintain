using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Plaintain.Control.Controls;
using Puffinote.Text.Document;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// The surface's semantics: typing, caret movement, selection, undo, and the two things that make it a
/// <i>plaintext</i> editor rather than Typage — the wrap toggle and the line-number gutter.
/// </summary>
public class PlainTextEditorTests
{
    /// <summary>An editor laid out at a known size, so the row/column arithmetic has a viewport to work in.</summary>
    private static (PlainTextEditor Editor, Window Window) Open(string text, int caret = 0, bool wrap = false)
    {
        var editor = new PlainTextEditor { Text = text, WordWrap = wrap };
        var window = new Window { Width = 400, Height = 300, Content = editor };
        window.Show();
        window.UpdateLayout();
        editor.Focus();
        editor.Buffer.Selection = Selection.At(caret);
        return (editor, window);
    }

    [AvaloniaFact]
    public void Typing_inserts_at_the_caret()
    {
        var (editor, window) = Open("hello world", caret: 5);
        window.KeyTextInput(",");

        Assert.Equal("hello, world", editor.Text);
        Assert.Equal(6, editor.Buffer.Selection.Caret);
    }

    [AvaloniaFact]
    public void Enter_writes_the_document_s_own_line_ending()
    {
        var (editor, window) = Open("alpha\r\nbeta", caret: 11);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Equal("alpha\r\nbeta\r\n", editor.Text);
        Assert.DoesNotContain('\n', editor.Text.Replace("\r\n", "", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void A_vertical_run_remembers_the_column_it_started_in()
    {
        // Down past a short line and back must land where it started, not at the short line's end —
        // the goal column is what makes a run of ↓↓ feel like one gesture instead of two guesses.
        // "longer line here" | "x" | "another long line"
        var (editor, window) = Open("longer line here\nx\nanother long line", caret: 12);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal(18, editor.Buffer.Selection.Caret); // clamped to the end of "x"

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Assert.Equal(31, editor.Buffer.Selection.Caret); // column 12 of the third line, not its start
    }

    [AvaloniaFact]
    public void Undo_and_redo_walk_the_edit_back_and_forward()
    {
        var (editor, window) = Open("a", caret: 1);
        window.KeyTextInput("b");
        Assert.Equal("ab", editor.Text);

        editor.Undo();
        Assert.Equal("a", editor.Text);
        editor.Redo();
        Assert.Equal("ab", editor.Text);
    }

    [AvaloniaFact]
    public void With_wrap_off_a_line_is_a_row()
    {
        var (editor, _) = Open(string.Join('\n', Enumerable.Repeat(new string('x', 500), 5)));

        Assert.Equal(5, editor.TotalRows);
        Assert.True(editor.CanHorizontallyScroll); // …and the long lines are reached by scrolling sideways
    }

    [AvaloniaFact]
    public void With_wrap_on_a_long_line_becomes_several_rows()
    {
        var (editor, _) = Open(new string('x', 500), wrap: true);

        Assert.True(editor.TotalRows > 1, $"expected the line to fold, got {editor.TotalRows} row(s)");
        Assert.Equal((500 + editor.ColumnsPerRow - 1) / editor.ColumnsPerRow, editor.TotalRows);
        Assert.False(editor.CanHorizontallyScroll); // wrapping means there is nothing to scroll to
    }

    [AvaloniaFact]
    public void The_wrap_row_count_is_arithmetic_not_measurement()
    {
        // The claim the whole geometry rests on: a line's rows are counted in columns, so a document's
        // height is known without laying out or measuring a single row. With nothing to break at, an
        // unbroken run of text still folds exactly at the edge — ceil(columns / columnsPerRow).
        var (editor, _) = Open(string.Join('\n', Enumerable.Range(0, 20).Select(i => new string('x', i * 40))), wrap: true);

        int expected = 0;
        for (int line = 0; line < 20; line++)
        {
            expected += Math.Max(1, ((line * 40) + editor.ColumnsPerRow - 1) / editor.ColumnsPerRow);
        }

        Assert.Equal(expected, editor.TotalRows);
    }

    /// <summary>
    /// The point of word wrap: a fold lands between words, so no word is cut in half by the window's edge.
    /// </summary>
    [AvaloniaFact]
    public void Wrapping_breaks_between_words_not_inside_them()
    {
        var (editor, _) = Open("", wrap: true);
        int perRow = editor.ColumnsPerRow;

        // A line of five-letter words, long enough that the first row cannot hold a whole number of them:
        // the fold has to give up the columns between the last word that fits and the edge.
        string word = "abcde";
        var words = Enumerable.Repeat(word, (perRow / (word.Length + 1)) + 4).ToList();
        editor.Text = string.Join(' ', words);

        Assert.True(editor.TotalRows > 1);
        var (_, secondRowStart) = SplitAtSecondRow(editor);

        Assert.Equal(0, secondRowStart % (word.Length + 1)); // a word's first letter, never its middle
        Assert.True(secondRowStart <= perRow, $"the second row starts at {secondRowStart}, past the fold");
    }

    /// <summary>A word with nowhere to break still has to fit somewhere — it breaks at the edge.</summary>
    [AvaloniaFact]
    public void A_word_longer_than_a_row_still_breaks_at_the_edge()
    {
        var (editor, _) = Open("", wrap: true);
        int perRow = editor.ColumnsPerRow;
        editor.Text = new string('x', perRow * 2);

        Assert.Equal(2, editor.TotalRows);
        Assert.Equal(perRow, SplitAtSecondRow(editor).SecondRowStart);
    }

    /// <summary>
    /// Blanks are allowed to hang past the right edge rather than pushing the next word down a row: a run
    /// of spaces at a fold draws nothing, and moving it would leave the row before it ending early.
    /// </summary>
    [AvaloniaFact]
    public void Trailing_blanks_hang_past_the_edge_instead_of_forcing_a_fold()
    {
        var (editor, _) = Open("", wrap: true);
        int perRow = editor.ColumnsPerRow;
        editor.Text = new string('x', perRow - 2) + "    y"; // the spaces cross the edge; "y" is past it

        Assert.Equal(2, editor.TotalRows);
        Assert.Equal(perRow + 2, SplitAtSecondRow(editor).SecondRowStart); // the row starts at "y"
    }

    /// <summary>
    /// A few hanging blanks are invisible; a whole row of them is a place the caret could hide, and
    /// wrapping has no sideways scroll to go and find it. So the hang is capped at one row's width.
    /// </summary>
    [AvaloniaFact]
    public void A_blank_run_wider_than_the_window_folds_rather_than_hanging_forever()
    {
        var (editor, _) = Open("", wrap: true);
        int perRow = editor.ColumnsPerRow;
        editor.Text = new string(' ', perRow * 4) + "tail";

        Assert.True(editor.TotalRows >= 4, $"the blank run should fold, got {editor.TotalRows} row(s)");

        // Every column of the line is reachable within a row's width of that row's start — which is the
        // whole claim: nothing sits past an edge you cannot scroll to.
        for (int offset = 0; offset <= editor.Text.Length; offset++)
        {
            editor.MoveCaret(offset, extend: false);
            Assert.InRange(editor.CaretRowColumn.Column, 0, perRow);
        }
    }

    /// <summary>The caret's row/column and the offsets a row spans agree about where the fold is.</summary>
    [AvaloniaFact]
    public void The_caret_lands_on_the_row_the_fold_put_its_word_on()
    {
        var (editor, _) = Open("", wrap: true);
        int perRow = editor.ColumnsPerRow;
        editor.Text = new string('a', perRow - 3) + " bcdef";

        int wordStart = perRow - 2; // the offset of "b" — folded onto the second row
        editor.MoveCaret(wordStart + 2, extend: false);

        var (row, column) = editor.CaretRowColumn;
        Assert.Equal(1, row);
        Assert.Equal(2, column);
        Assert.Equal(wordStart, editor.RowStartOffset());
        Assert.Equal(editor.Text.Length, editor.RowEndOffset());
    }

    /// <summary>The offset the second visual row begins at, found through the row/offset map itself.</summary>
    private static (int Rows, int SecondRowStart) SplitAtSecondRow(PlainTextEditor editor)
    {
        editor.MoveCaret(editor.Text.Length, extend: false); // anywhere on the last row
        while (editor.CaretRowColumn.Row > 1)
        {
            editor.MoveByRow(-1, extend: false);
        }

        editor.MoveCaret(editor.RowStartOffset(), extend: false);
        return (editor.TotalRows, editor.Buffer.Selection.Caret);
    }

    [AvaloniaFact]
    public void A_tab_advances_to_the_next_tab_stop_not_by_one()
    {
        // Column arithmetic has to agree with what is painted, or every column below a tab is a lie —
        // and columns are the whole reason anyone opens a log in a monospace editor.
        var (editor, _) = Open("\tx");

        Assert.Equal(PlainTextEditor.TabWidth, editor.ColumnOf(0, 1));
        Assert.Equal(PlainTextEditor.TabWidth + 1, editor.LineColumns(0));
    }

    [AvaloniaFact]
    public void The_gutter_widens_with_the_largest_line_number()
    {
        var (few, _) = Open("a\nb\nc");
        var (many, _) = Open(string.Join('\n', Enumerable.Repeat("x", 5000)));

        Assert.True(many.GutterWidth > few.GutterWidth);
        Assert.Equal(0, new PlainTextEditor { ShowLineNumbers = false }.GutterWidth);
    }

    [AvaloniaFact]
    public void Home_goes_to_the_content_then_to_the_true_start()
    {
        var (editor, _) = Open("    indented", caret: 12);

        editor.MoveCaret(editor.SmartHomeOffset(), extend: false);
        Assert.Equal(4, editor.Buffer.Selection.Caret); // the first real character

        editor.MoveCaret(editor.SmartHomeOffset(), extend: false);
        Assert.Equal(0, editor.Buffer.Selection.Caret); // pressed again: the line's true start
    }

    [AvaloniaFact]
    public void A_double_click_selects_the_word_it_landed_on_and_nothing_else()
    {
        var (editor, _) = Open("alpha beta gamma");
        editor.SelectWordAt(8); // inside "beta"

        Assert.Equal(6, editor.Buffer.Selection.Start);
        Assert.Equal(10, editor.Buffer.Selection.End);
    }

    [AvaloniaFact]
    public void Tab_indents_to_the_next_stop_rather_than_by_a_fixed_four()
    {
        var (editor, window) = Open("ab", caret: 2);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.Equal("ab  ", editor.Text); // column 2 → column 4, so two spaces, not four
    }

    [AvaloniaFact]
    public void The_extent_covers_the_document_while_the_control_stays_viewport_sized()
    {
        // The virtualization contract: a million rows is a tall *extent*, not a tall control.
        var (editor, _) = Open(string.Join('\n', Enumerable.Repeat("line", 5000)));

        Assert.True(editor.Extent.Height > 5000 * editor.RowHeight * 0.9);
        Assert.True(editor.Bounds.Height <= 300);
    }

    /// <summary>
    /// With wrap on, an edit patches the row map instead of rebuilding it: only the touched lines are folded
    /// again and the rest shift. After every kind of edit, including ones that add, remove or re-fold lines
    /// and undo and redo of them, the patched map must match the rows each line actually folds into.
    /// </summary>
    [AvaloniaFact]
    public void Edits_with_wrap_on_keep_the_row_map_exact()
    {
        var random = new Random(7);
        var lines = Enumerable.Range(0, 60).Select(i => string.Join(' ', Enumerable.Repeat("word", random.Next(0, 40))));
        var (editor, window) = Open(string.Join('\n', lines), wrap: true);
        _ = editor.TotalRows; // build the map, so every edit below has one to patch

        string[] inserts = ["x", " ", "\n", "a long run of words to fold the line again and again ", "p\nq\nr", ""];
        for (int step = 0; step < 300; step++)
        {
            int length = editor.Buffer.Document.Length;
            int start = random.Next(0, length + 1);
            int span = random.Next(0, 3) == 0 ? random.Next(0, Math.Min(400, length - start) + 1) : 0;
            switch (random.Next(0, 10))
            {
                case 0:
                    editor.Undo();
                    break;
                case 1:
                    editor.Redo();
                    break;
                default:
                    editor.Buffer.Selection = new Selection(start, start + span);
                    editor.Buffer.Insert(inserts[random.Next(inserts.Length)]);
                    break;
            }

            window.UpdateLayout();
            AssertRowMapMatchesTheFolds(editor, step);
        }
    }

    private static void AssertRowMapMatchesTheFolds(PlainTextEditor editor, int step)
    {
        int row = 0;
        for (int line = 0; line < editor.Buffer.Document.LineCount; line++)
        {
            Assert.True(row == editor.FirstRowOf(line), $"step {step}: line {line} starts on row {editor.FirstRowOf(line)}, not {row}");
            row += editor.RowsInLine(line);
        }

        Assert.Equal(row, editor.TotalRows);
    }

    /// <summary>
    /// A keystroke reshapes only the row it changed. Shaping every visible row on every frame was most of
    /// what a repaint cost, so the rest are reused from the frame before; a theme change, which changes the
    /// colour the text was shaped in, reshapes them all.
    /// </summary>
    [AvaloniaFact]
    public void A_repaint_reshapes_only_the_rows_that_changed()
    {
        var (editor, window) = Open(string.Join('\n', Enumerable.Range(0, 100).Select(i => $"line {i} of the log")), caret: 0);
        editor.ShowLineNumbers = true;
        window.UpdateLayout();
        Paint(editor);
        int firstFrame = editor.RowsShapedForTest;
        Assert.True(firstFrame > 20, $"the first frame shaped {firstFrame} rows and numbers");

        Paint(editor);
        Assert.Equal(firstFrame, editor.RowsShapedForTest); // nothing changed, nothing reshaped

        window.KeyTextInput("x");
        window.UpdateLayout();
        Paint(editor);
        Assert.Equal(firstFrame + 1, editor.RowsShapedForTest); // the typed-in row, and only that one

        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Paint(editor);
        Assert.Equal(firstFrame * 2, editor.RowsShapedForTest - 1);
    }

    /// <summary>
    /// A line past <see cref="PlainTextEditor.LongLineThreshold"/> answers column questions from an index
    /// instead of a walk. Every offset and every column must still land where the walk puts them, tabs and
    /// a surrogate pair included, and an edit must never be answered from the old line's index.
    /// </summary>
    [AvaloniaFact]
    public void Columns_on_a_long_line_match_a_walk_before_and_after_an_edit()
    {
        var rnd = new Random(7);
        var sb = new System.Text.StringBuilder();
        while (sb.Length < 3 * PlainTextEditor.LongLineThreshold)
        {
            sb.Append(rnd.Next(6) switch { 0 => "\t", 1 => "\U0001F600", _ => "abc" });
        }

        string longLine = sb.ToString();
        var (editor, window) = Open("short\r\n" + longLine + "\r\nend", caret: 7 + 100);
        AssertColumnsMatchAWalk(editor, line: 1);

        window.KeyTextInput("\t");
        AssertColumnsMatchAWalk(editor, line: 1);
        editor.Buffer.Selection = Selection.At(7 + 3);
        window.KeyTextInput("xy");
        AssertColumnsMatchAWalk(editor, line: 1);
    }

    private static void AssertColumnsMatchAWalk(PlainTextEditor editor, int line)
    {
        var doc = editor.Buffer.Document;
        int start = doc.GetLineStart(line), end = doc.GetLineEnd(line);
        Assert.True(end - start >= PlainTextEditor.LongLineThreshold);

        var columns = new int[end - start + 1];
        for (int i = start; i < end; i++)
        {
            int c = columns[i - start];
            columns[i - start + 1] = doc[i] == '\t' ? ((c / PlainTextEditor.TabWidth) + 1) * PlainTextEditor.TabWidth : c + 1;
        }

        for (int i = 0; i < columns.Length; i++)
        {
            Assert.Equal(columns[i], editor.ColumnOf(line, start + i));
        }

        Assert.Equal(columns[^1], editor.LineColumns(line));
        for (int column = 0; column <= columns[^1] + 2; column++)
        {
            int expected = Array.FindIndex(columns, c => c >= column);
            Assert.Equal(start + (expected < 0 || expected > end - start ? end - start : expected), editor.OffsetAtColumn(line, column));
        }
    }

    /// <summary>
    /// With wrap off, a long line shapes only the columns the viewport shows, and those columns are the
    /// ones a whole-line draw would have put there: the same text, tabs expanded, at the same x.
    /// </summary>
    [AvaloniaFact]
    public void A_long_line_draws_only_the_columns_on_screen_and_draws_them_where_they_belong()
    {
        var rnd = new Random(11);
        var sb = new System.Text.StringBuilder();
        while (sb.Length < 20_000)
        {
            sb.Append(rnd.Next(8) == 0 ? "\t" : ((char)('a' + rnd.Next(26))).ToString());
        }

        string longLine = sb.ToString();
        var (editor, window) = Open(longLine + "\nx");
        editor.ShowLineNumbers = false;
        window.UpdateLayout();
        Paint(editor); // the horizontal extent grows from what has been drawn
        editor.Offset = new Avalonia.Vector(5003.5, 0);
        Assert.Equal(5003.5, editor.Offset.X);

        var (drawn, x) = editor.DrawnRowForTest(0);
        var (_, shortX) = editor.DrawnRowForTest(1); // a short line starts at column 0, so this is column 0's x
        double width = editor.CharWidthForTest;
        int column = (int)Math.Round((x - shortX) / width);
        Assert.True(drawn.Length < 200, $"drew {drawn.Length} columns of a line {longLine.Length} long");
        Assert.Equal(ExpandTabs(longLine).Substring(column, drawn.Length), drawn);
        Assert.True(column * width <= editor.Offset.X, "the slice starts at or before the left edge");
        Assert.True((column + drawn.Length) * width >= editor.Offset.X + editor.Bounds.Width, "the slice reaches the right edge");
    }

    /// <summary>With wrap on, a long line's rows are its wrap segments, in order, with nothing lost or repeated.</summary>
    [AvaloniaFact]
    public void A_long_wrapped_line_draws_every_segment_in_order()
    {
        string longLine = string.Concat(Enumerable.Range(0, 400).Select(i => $"word{i} "));
        var (editor, _) = Open(longLine + "\nx", wrap: true);
        int rows = editor.RowsInLine(0);
        Assert.True(rows > 10);

        var (_, firstX) = editor.DrawnRowForTest(0);
        var joined = new System.Text.StringBuilder();
        for (int row = 0; row < rows; row++)
        {
            var (text, x) = editor.DrawnRowForTest(row);
            Assert.Equal(firstX, x);
            joined.Append(text);
        }

        Assert.Equal(longLine, joined.ToString());
    }

    private static string ExpandTabs(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (c == '\t')
            {
                sb.Append(' ', PlainTextEditor.TabWidth - (sb.Length % PlainTextEditor.TabWidth));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static void Paint(PlainTextEditor editor)
    {
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(400, 300));
        using var context = bitmap.CreateDrawingContext();
        editor.Render(context);
    }
}
