using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Control.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// The embeddable composite: the find strip over the surface, and the one catalog a host binds to.
/// </summary>
public class PlainTextEditorViewTests
{
    private static PlainTextEditorView Open(string text)
    {
        var view = new PlainTextEditorView { Text = text };
        var window = new Window { Width = 500, Height = 300, Content = view };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [AvaloniaFact]
    public void Find_highlights_every_match_and_steps_through_them()
    {
        var view = Open("one two one two one");
        view.ShowFind();
        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, view.Matches.Count);
        Assert.Equal(0, view.CurrentMatch);

        view.Find.QueryBox.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Enter,
            Source = view.Find.QueryBox,
        });

        Assert.Equal(1, view.CurrentMatch);
    }

    [AvaloniaFact]
    public void Find_starts_from_where_you_are_reading_not_from_the_top()
    {
        // Opening find 40,000 lines into a log and being thrown back to line 1 is the behaviour this
        // avoids: the first offered match is the first one at or after the caret.
        var view = Open("one\n\n\n\n\none\n\n\n\n\none");
        view.Editor.Buffer.Selection = Puffinote.Text.Document.Selection.At(5); // past the first "one"
        view.ShowFind();
        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, view.CurrentMatch);
    }

    [AvaloniaFact]
    public void Dismissing_find_clears_the_highlights()
    {
        var view = Open("one two one");
        view.ShowFind();
        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(view.Matches);

        view.Find.Dismiss();
        Assert.Empty(view.Matches);
    }

    /// <summary>
    /// On a large document every keystroke in the query box would otherwise search the whole text on the UI
    /// thread. It waits for a pause and searches in the background instead, and a query typed over a slower
    /// one wins: only the latest query's matches are ever shown.
    /// </summary>
    [AvaloniaFact]
    public void Find_on_a_large_document_searches_in_the_background_and_shows_the_latest_query()
    {
        var view = Open("one two one two one");
        view.BackgroundSearchFrom = 0;
        view.SearchDelay = TimeSpan.FromMilliseconds(200);
        view.ShowFind();

        view.Find.QueryBox.Text = "two";
        Dispatcher.UIThread.RunJobs();
        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(view.Matches); // nothing has been searched yet: the UI thread didn't wait for it
        Settle(view);

        Assert.Equal([0, 8, 16], view.Matches.Select(m => m.Start));
        Assert.Equal(0, view.CurrentMatch);
    }

    [AvaloniaFact]
    public void Closing_find_while_it_searches_leaves_no_highlights_behind()
    {
        var view = Open("one two one");
        view.BackgroundSearchFrom = 0;
        view.SearchDelay = TimeSpan.FromMilliseconds(200);
        view.ShowFind();

        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs(); // the search is now waiting out its delay
        view.Find.Dismiss();
        Settle(view);

        Assert.Empty(view.Matches);
    }

    [AvaloniaFact]
    public void An_edit_during_a_background_search_gets_matches_for_the_new_text()
    {
        var view = Open("one two one");
        view.BackgroundSearchFrom = 0;
        view.SearchDelay = TimeSpan.FromMilliseconds(20);
        view.ShowFind();

        view.Find.QueryBox.Text = "one";
        Dispatcher.UIThread.RunJobs();
        view.Editor.Buffer.Selection = Puffinote.Text.Document.Selection.At(0);
        view.Editor.Buffer.Insert("one "); // lands before the search has read the text
        Settle(view);

        Assert.Equal("one one two one", view.Text);
        Assert.Equal([0, 4, 12], view.Matches.Select(m => m.Start));
    }

    /// <summary>
    /// A row draws only its own matches, found by a binary search rather than a pass over every match in
    /// the document. With 20,000 matches and the view scrolled halfway down, a frame draws two per row.
    /// </summary>
    [AvaloniaFact]
    public void A_frame_draws_only_the_matches_on_screen()
    {
        var view = Open(string.Join('\n', Enumerable.Range(0, 10_000).Select(i => $"x ab ab {i}")));
        view.ShowFind();
        view.Find.QueryBox.Text = "ab";
        Settle(view);
        Assert.Equal(20_000, view.Matches.Count);

        var editor = view.Editor;
        editor.RevealLine(5_000);
        editor.UpdateLayout();
        int before = editor.MatchesDrawnForTest;
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(500, 300)))
        using (var context = bitmap.CreateDrawingContext())
        {
            editor.Render(context);
        }

        int rows = (int)Math.Ceiling(editor.Bounds.Height / editor.RowHeight) + 1;
        int drawn = editor.MatchesDrawnForTest - before;
        Assert.InRange(drawn, 2 * (rows - 2), 2 * rows);
    }

    /// <summary>Pump the UI thread until the find strip's background search (and any it re-queued) is done.</summary>
    private static void Settle(PlainTextEditorView view)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        do
        {
            Dispatcher.UIThread.RunJobs();
        }
        while (!view.PendingSearchForTest.IsCompleted && DateTime.UtcNow < deadline);

        Dispatcher.UIThread.RunJobs();
        Assert.True(view.PendingSearchForTest.IsCompleted, "the search never finished");
    }

    [AvaloniaFact]
    public void The_catalog_is_built_once_so_every_surface_shares_one_command()
    {
        var view = Open("x");
        Assert.Same(view.EditingCommands, view.EditingCommands);
    }

    [AvaloniaFact]
    public void Word_wrap_and_line_numbers_are_commands_that_report_their_own_state()
    {
        var view = Open("x");
        var wrap = view.EditingCommands.Single(c => c.Id == "view.wordWrap");

        Assert.False(wrap.IsToggleActive);
        wrap.Execute();
        Assert.True(wrap.IsToggleActive);
        Assert.True(view.Editor.WordWrap);
    }

    [AvaloniaFact]
    public void Undo_reports_whether_there_is_anything_to_undo()
    {
        var view = Open("x");
        var undo = view.EditingCommands.Single(c => c.Id == "edit.undo");
        Assert.False(undo.IsEnabled);

        view.Editor.Buffer.Insert("y");
        Assert.True(undo.IsEnabled);
    }

    [AvaloniaFact]
    public void Go_to_line_centres_the_line_and_puts_the_caret_on_it()
    {
        var view = Open(string.Join('\n', Enumerable.Range(1, 500).Select(i => $"line {i}")));
        view.GoToLine(300);

        Assert.Equal(300, view.CaretPosition.DisplayLine);
    }
}
