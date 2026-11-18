using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Plaintain.Control.Controls;
using Plaintain.Control.Views;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Puffinote.Components.Commands;
using Puffinote.Text.Document;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>⌥↑/⌥↓ move the selected lines and ⇧⌥↑/⇧⌥↓ copy them, each as one undoable edit.</summary>
public class LineCommandsTests
{
    private static PlainTextEditor Open(string text, int anchor, int caret)
    {
        var editor = new PlainTextEditor { Text = text };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        editor.Focus();
        editor.Buffer.Selection = new Selection(anchor, caret);
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static bool Press(PlainTextEditor editor, Key key, KeyModifiers modifiers)
    {
        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = editor };
        editor.RaiseEvent(args);
        Dispatcher.UIThread.RunJobs();
        return args.Handled;
    }

    [AvaloniaFact]
    public void Alt_up_and_down_move_the_caret_line_and_keep_the_caret_on_it()
    {
        var editor = Open("one\ntwo\nthree\n", 5, 5); // in "two"

        Assert.True(Press(editor, Key.Up, KeyModifiers.Alt));
        Assert.Equal("two\none\nthree\n", editor.Text);
        Assert.Equal(Selection.At(1), editor.Buffer.Selection);

        Press(editor, Key.Down, KeyModifiers.Alt);
        Press(editor, Key.Down, KeyModifiers.Alt);
        Assert.Equal("one\nthree\ntwo\n", editor.Text);
        Assert.Equal(Selection.At(11), editor.Buffer.Selection);
    }

    [AvaloniaFact]
    public void A_move_is_one_undo_step_and_stops_at_the_edges()
    {
        var editor = Open("one\ntwo", 0, 0);
        editor.MoveLines(-1); // already the first line
        Assert.Equal("one\ntwo", editor.Text);

        editor.MoveLines(1);
        editor.Buffer.Undo();
        Assert.Equal("one\ntwo", editor.Text);
    }

    [AvaloniaFact]
    public void Moving_the_last_line_keeps_the_file_without_a_final_newline()
    {
        var editor = Open("one\ntwo", 5, 6); // "w" selected on the last line
        editor.MoveLines(-1);
        Assert.Equal("two\none", editor.Text);
        Assert.Equal(new Selection(1, 2), editor.Buffer.Selection);

        editor.MoveLines(1);
        Assert.Equal("one\ntwo", editor.Text);
        Assert.Equal(new Selection(5, 6), editor.Buffer.Selection);
    }

    [AvaloniaFact]
    public void A_selection_ending_at_a_line_start_moves_only_the_lines_it_covers()
    {
        // "a" and "b" selected by dragging to the start of "c".
        var editor = Open("a\nb\nc\nd\n", 0, 4);
        editor.MoveLines(1);
        Assert.Equal("c\na\nb\nd\n", editor.Text);
        Assert.Equal(new Selection(2, 6), editor.Buffer.Selection);
    }

    [AvaloniaFact]
    public void Each_line_keeps_its_own_line_ending()
    {
        var editor = Open("one\r\ntwo\nthree", 0, 0);
        editor.MoveLines(1);
        Assert.Equal("two\none\r\nthree", editor.Text);
    }

    [AvaloniaFact]
    public void Shift_alt_down_copies_the_lines_below_and_selects_the_copy()
    {
        var editor = Open("one\ntwo\n", 4, 6); // "tw"
        Assert.True(Press(editor, Key.Down, KeyModifiers.Alt | KeyModifiers.Shift));
        Assert.Equal("one\ntwo\ntwo\n", editor.Text);
        Assert.Equal(new Selection(8, 10), editor.Buffer.Selection);

        editor.Buffer.Undo();
        Assert.Equal("one\ntwo\n", editor.Text);
    }

    [AvaloniaFact]
    public void Copying_the_last_line_adds_the_documents_newline_between_the_copies()
    {
        var editor = Open("a\r\nlast", 4, 4);
        editor.DuplicateLines(1);
        Assert.Equal("a\r\nlast\r\nlast", editor.Text);
        Assert.Equal(Selection.At(10), editor.Buffer.Selection);

        editor.DuplicateLines(-1);
        Assert.Equal("a\r\nlast\r\nlast\r\nlast", editor.Text);
        Assert.Equal(Selection.At(10), editor.Buffer.Selection); // the upper of the two stays selected
    }

    [AvaloniaFact]
    public void The_line_commands_are_in_the_catalog_with_their_chords()
    {
        var view = new PlainTextEditorView { Text = "x\ny" };
        var byId = view.EditingCommands.ToDictionary(c => c.Id);
        Assert.Equal(Shortcut.Bare(Key.Up, KeyModifiers.Alt), byId["edit.moveLinesUp"].Shortcut);
        Assert.Equal(Shortcut.Bare(Key.Down, KeyModifiers.Alt | KeyModifiers.Shift), byId["edit.duplicateLinesDown"].Shortcut);
        Assert.True(byId["edit.moveLinesUp"].Shortcut!.Value.YieldsToTextInput); // no menu key-equivalent to steal it from a field

        byId["edit.moveLinesDown"].Execute();
        Assert.Equal("y\nx", view.Text);
    }

    [AvaloniaFact]
    public void In_the_app_window_alt_up_moves_lines_from_the_editor_but_not_from_the_find_box()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"plaintain-lines-{Guid.NewGuid():N}");
        try
        {
            var window = new MainWindow(new SettingsService(Path.Combine(dir, "settings.json")),
                new Puffinote.Components.Core.Documents.RecoveryService("Plaintain", Path.Combine(dir, "recovery")));
            window.Show();
            var view = window.EditorView;
            view.Text = "one\ntwo";
            view.Editor.Focus();
            view.Editor.Buffer.Selection = Selection.At(5);
            Dispatcher.UIThread.RunJobs();

            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Assert.Equal("two\none", view.Text);

            view.ShowFind();
            view.Find.QueryBox.Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Assert.Equal("two\none", view.Text);
            window.Close();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
