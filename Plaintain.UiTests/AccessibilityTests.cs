using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Control.Controls;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// What assistive technology reads from the editor: an editable text control whose value is the text on
/// screen, not the whole document, and which says so when that text changes.
/// </summary>
public class AccessibilityTests
{
    private static (PlainTextEditor Editor, PlainTextEditorAutomationPeer Peer) Open(string text, bool wrap = false)
    {
        var editor = new PlainTextEditor { Text = text, WordWrap = wrap };
        var window = new Window { Width = 500, Height = 300, Content = editor };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var peer = Assert.IsType<PlainTextEditorAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(editor));
        return (editor, peer);
    }

    private static string Lines(int count) => string.Join('\n', Enumerable.Range(0, count).Select(i => $"line {i}"));

    [AvaloniaFact]
    public void The_editor_is_an_editable_text_control_with_a_name()
    {
        var (_, peer) = Open("hello");

        Assert.Equal(AutomationControlType.Edit, peer.GetAutomationControlType());
        Assert.Equal("Text editor", peer.GetName());
        Assert.True(peer.IsKeyboardFocusable());
        Assert.False(peer.GetProvider<IValueProvider>()!.IsReadOnly);
        Assert.Equal("hello", peer.GetProvider<IValueProvider>()!.Value);
    }

    [AvaloniaFact]
    public void The_value_is_the_rows_on_screen_not_the_whole_document()
    {
        var (editor, peer) = Open(Lines(10_000));

        string top = peer.Value;
        Assert.StartsWith("line 0\n", top, StringComparison.Ordinal);
        Assert.DoesNotContain("line 100\n", top, StringComparison.Ordinal);

        editor.RevealLine(5_000);
        string middle = peer.Value;
        Assert.Contains("line 5000\n", middle, StringComparison.Ordinal);
        Assert.InRange(middle.Split('\n').Length, 2, (int)(300 / editor.RowHeight) + 2);
    }

    [AvaloniaFact]
    public void A_wrapped_long_line_exposes_only_its_visible_rows()
    {
        var (editor, peer) = Open(string.Concat(Enumerable.Range(0, 20_000).Select(i => $"w{i % 10} ")), wrap: true);

        editor.Offset = new Vector(0, editor.RowHeight * 500);
        var (start, length) = editor.VisibleTextRange();

        Assert.True(start > 0, "the value starts at the first row on screen, not at the start of the line");
        Assert.True(length < editor.ColumnsPerRow * ((300 / editor.RowHeight) + 2));
        Assert.Equal(editor.Buffer.Document.GetText(start, length), peer.Value);
    }

    [AvaloniaFact]
    public void A_huge_unwrapped_line_is_capped()
    {
        var (_, peer) = Open(new string('x', 1_000_000));
        Assert.Equal(PlainTextEditor.AccessibleTextLimit, peer.Value.Length);
    }

    [AvaloniaFact]
    public void Typing_and_scrolling_announce_the_new_value_once_per_burst()
    {
        var (editor, peer) = Open(Lines(1_000));
        _ = peer.Value; // assistive technology has read it, so it wants to hear about changes
        var changes = new List<AutomationPropertyChangedEventArgs>();
        peer.PropertyChanged += (_, e) => changes.Add(e);

        editor.Buffer.Insert("a");
        editor.Buffer.Insert("b");
        Dispatcher.UIThread.RunJobs();

        var change = Assert.Single(changes);
        Assert.Equal(ValuePatternIdentifiers.ValueProperty, change.Property);
        Assert.StartsWith("abline 0", (string?)change.NewValue, StringComparison.Ordinal);

        editor.Offset = new Vector(0, editor.RowHeight * 500); // a scroll alone, with no layout pass
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, changes.Count);
        Assert.Contains("line 500", (string?)changes[1].NewValue, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Nothing_is_announced_before_anything_has_read_the_value()
    {
        var (editor, peer) = Open("abc");
        var changes = 0;
        peer.PropertyChanged += (_, _) => changes++;

        editor.Buffer.Insert("x");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, changes);
    }

    [AvaloniaFact]
    public void A_relayout_that_changes_nothing_on_screen_announces_nothing()
    {
        var (editor, peer) = Open(Lines(100));
        _ = peer.Value;
        var changes = 0;
        peer.PropertyChanged += (_, _) => changes++;

        editor.InvalidateMeasure();
        editor.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, changes);
    }

    [AvaloniaFact]
    public void Setting_the_value_replaces_only_the_visible_text_as_one_undoable_edit()
    {
        var (editor, peer) = Open(Lines(1_000));
        editor.RevealLine(500);
        var (start, length) = editor.VisibleTextRange();
        string before = editor.Text;

        peer.SetValue("replaced");

        Assert.Equal(before[..start] + "replaced" + before[(start + length)..], editor.Text);
        editor.Undo();
        Assert.Equal(before, editor.Text);
    }
}
