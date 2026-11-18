using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Control.Controls;
using Puffinote.Text.Document;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// A log someone is watching grows on disk and the editor reloads it. With the caret at the end, the
/// editor follows the new end the way <c>tail -f</c> does; anywhere else, it leaves the reader where they were.
/// </summary>
public class LogFollowTests
{
    private static PlainTextEditor Open(string text)
    {
        var editor = new PlainTextEditor { Text = text };
        var window = new Window { Width = 500, Height = 300, Content = editor };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static string Lines(int from, int count) => string.Concat(Enumerable.Range(from, count).Select(i => $"line {i}\n"));

    private static bool LastRowOnScreen(PlainTextEditor editor) =>
        editor.Offset.Y + editor.Viewport.Height >= (editor.TotalRows * editor.RowHeight) - 0.5;

    [AvaloniaFact]
    public void With_the_caret_at_the_end_a_reload_follows_the_new_end()
    {
        string log = Lines(0, 200);
        var editor = Open(log);
        editor.Buffer.Selection = Selection.At(log.Length);
        editor.BringCaretIntoView();
        Assert.True(LastRowOnScreen(editor));

        string grown = log + Lines(200, 100);
        editor.ReloadFrom(grown);

        Assert.Equal(grown.Length, editor.Buffer.Selection.Caret);
        Assert.True(LastRowOnScreen(editor), "the newest lines are in sight");
    }

    [AvaloniaFact]
    public void A_reader_who_scrolled_up_keeps_their_view_while_the_caret_follows()
    {
        string log = Lines(0, 200);
        var editor = Open(log);
        editor.Buffer.Selection = Selection.At(log.Length);
        editor.Offset = new Vector(0, editor.RowHeight * 50);
        var view = editor.Offset;

        string grown = log + Lines(200, 100);
        editor.ReloadFrom(grown);

        Assert.Equal(grown.Length, editor.Buffer.Selection.Caret);
        Assert.Equal(view, editor.Offset);
    }

    [AvaloniaFact]
    public void A_caret_in_the_middle_keeps_its_offset_and_the_view_stays()
    {
        string log = Lines(0, 200);
        var editor = Open(log);
        editor.Buffer.Selection = Selection.At(100);
        var view = editor.Offset;

        editor.ReloadFrom(log + Lines(200, 100));

        Assert.Equal(100, editor.Buffer.Selection.Caret);
        Assert.Equal(view, editor.Offset);
    }

    [AvaloniaFact]
    public void A_selection_that_ends_at_the_end_is_not_a_caret_at_the_end()
    {
        string log = Lines(0, 10);
        var editor = Open(log);
        editor.Buffer.Selection = new Selection(5, log.Length);

        editor.ReloadFrom(log + Lines(10, 5));

        Assert.NotEqual(editor.Buffer.Document.Length, editor.Buffer.Selection.Caret);
    }

    [AvaloniaFact]
    public void Following_is_still_one_undoable_step()
    {
        string log = Lines(0, 10);
        var editor = Open(log);
        editor.Buffer.Selection = Selection.At(log.Length);

        editor.ReloadFrom(log + Lines(10, 5));
        editor.Undo();

        Assert.Equal(log, editor.Text);
    }
}
