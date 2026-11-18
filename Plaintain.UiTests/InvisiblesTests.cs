using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Control.Controls;
using Plaintain.Control.Views;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Puffinote.Components.Core.Documents;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>Show Invisibles: tabs as →, trailing spaces as ·, CRLF endings as ¤.</summary>
public class InvisiblesTests
{
    private const char Tab = PlainTextEditor.TabMark;
    private const char Space = PlainTextEditor.SpaceMark;
    private const char Crlf = PlainTextEditor.CrlfMark;

    private static PlainTextEditor Open(string text, double width = 600)
    {
        var editor = new PlainTextEditor { Text = text, ShowInvisibles = true };
        var window = new Window { Width = width, Height = 400, Content = editor };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    [AvaloniaFact]
    public void Tabs_trailing_spaces_and_crlf_are_marked_at_their_columns()
    {
        var editor = Open("a\tb c  \r\nx\n\ty");

        // "a" then a tab to column 4, "b c" and two trailing spaces, then the CRLF.
        Assert.Equal<(char, int)>([(Tab, 1), (Space, 7), (Space, 8), (Crlf, 9)], editor.InvisiblesForTest(0));
        Assert.Empty(editor.InvisiblesForTest(1)); // an LF ending gets no mark
        Assert.Equal<(char, int)>([(Tab, 0)], editor.InvisiblesForTest(2));
    }

    [AvaloniaFact]
    public void A_whitespace_only_line_is_all_trailing()
    {
        var editor = Open("  \t ");
        Assert.Equal<(char, int)>([(Space, 0), (Space, 1), (Tab, 2), (Space, 4)], editor.InvisiblesForTest(0));
    }

    [AvaloniaFact]
    public void A_wrapped_line_marks_its_crlf_only_on_its_last_row()
    {
        var editor = Open(new string('w', 300) + " \r\nnext", width: 300);
        editor.WordWrap = true;
        Dispatcher.UIThread.RunJobs();
        int rows = editor.FirstRowOf(1);
        Assert.True(rows > 1);

        Assert.Empty(editor.InvisiblesForTest(0));
        var last = editor.InvisiblesForTest(rows - 1);
        Assert.Equal([Space, Crlf], last.Select(m => m.Mark));
        Assert.Equal(301, last[^1].Column);
    }

    [AvaloniaFact]
    public void The_marks_are_drawn_only_while_the_toggle_is_on()
    {
        var view = new PlainTextEditorView { Text = "a\tb  " };
        var window = new Window { Width = 600, Height = 300, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var command = view.EditingCommands.Single(c => c.Id == "view.showInvisibles");
        Assert.False(command.IsToggleActive);
        Assert.Equal(0, MarksDrawn(view.Editor));

        command.Execute();
        Assert.True(command.IsToggleActive);
        Assert.Equal(3, MarksDrawn(view.Editor)); // the tab and the two trailing spaces
    }

    [AvaloniaFact]
    public void The_toggle_is_remembered_in_settings()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"plaintain-invisibles-{Guid.NewGuid():N}");
        try
        {
            var settings = new SettingsService(Path.Combine(dir, "settings.json"));
            var window = new MainWindow(settings, new RecoveryService("Plaintain", Path.Combine(dir, "recovery")));
            window.EditorView.ToggleInvisibles();
            Assert.True(new SettingsService(Path.Combine(dir, "settings.json")).Settings.ShowInvisibles);

            var reopened = new MainWindow(new SettingsService(Path.Combine(dir, "settings.json")),
                new RecoveryService("Plaintain", Path.Combine(dir, "recovery")));
            Assert.True(reopened.EditorView.Editor.ShowInvisibles);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static int MarksDrawn(PlainTextEditor editor)
    {
        editor.UpdateLayout();
        int before = editor.InvisiblesDrawnForTest;
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(600, 300)))
        using (var context = bitmap.CreateDrawingContext())
        {
            editor.Render(context);
        }

        return editor.InvisiblesDrawnForTest - before;
    }
}
