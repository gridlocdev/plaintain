using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Plaintain.Components.Core.Documents;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// Typing right after launch reaches the editor. On macOS the window can become key after it opened, and
/// the editor's focus was sometimes lost on the way, so the first keystrokes went nowhere.
/// </summary>
public sealed class StartupFocusTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"plaintain-focus-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private MainWindow Open()
    {
        var window = new MainWindow(new SettingsService(Path.Combine(_dir, "settings.json")),
            new RecoveryService("Plaintain", Path.Combine(_dir, "recovery")));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Leaves nothing focused, the state the window can be in when macOS makes it key.</summary>
    private static void LoseFocus(MainWindow window)
    {
        window.EditorView.ShowFind();
        Dispatcher.UIThread.RunJobs();
        window.EditorView.Find.IsVisible = false; // hiding the focused box drops focus without handing it on
        Dispatcher.UIThread.RunJobs();
        Assert.Null(window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public void Typing_after_launch_reaches_the_editor()
    {
        var window = Open();
        window.KeyTextInput("abc");
        Assert.Equal("abc", window.EditorView.Text);
    }

    [AvaloniaFact]
    public void Activating_the_window_with_nothing_focused_focuses_the_editor()
    {
        var window = Open();
        LoseFocus(window);

        window.Activate();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("abc");
        Assert.Equal("abc", window.EditorView.Text);
    }

    [AvaloniaFact]
    public void Activating_the_window_leaves_the_find_box_focused()
    {
        var window = Open();
        window.EditorView.ShowFind();
        Dispatcher.UIThread.RunJobs();

        window.Activate();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(window.EditorView.Find.QueryBox, window.FocusManager!.GetFocusedElement());
    }

    [AvaloniaFact]
    public void Switching_back_from_another_window_lets_you_type()
    {
        var window = Open();
        window.KeyTextInput("a");
        var other = new Window();
        other.Show();
        other.Activate();
        Dispatcher.UIThread.RunJobs();

        window.Activate();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("b");
        Assert.Equal("ab", window.EditorView.Text);
        other.Close();
    }

    [AvaloniaFact]
    public void Typing_after_releasing_command_reaches_the_editor()
    {
        var window = Open();

        // macOS reports ⌘ as a key-down and a key-up of its own; the letters typed next come as text only.
        window.KeyPressQwerty(PhysicalKey.MetaLeft, RawInputModifiers.Meta);
        window.KeyReleaseQwerty(PhysicalKey.MetaLeft, RawInputModifiers.None);
        window.KeyTextInput("a");
        Assert.Equal("a", window.EditorView.Text);
    }

    [AvaloniaFact]
    public void Typing_after_command_tab_away_and_back_reaches_the_editor()
    {
        var window = Open();

        // ⌘ goes down here, and comes up in whichever app ⌘-Tab switched to, so this window never hears it.
        // Hiding and showing the window stands in for the switch: the headless platform doesn't reliably
        // deactivate one window when another activates.
        window.KeyPressQwerty(PhysicalKey.MetaLeft, RawInputModifiers.Meta);
        window.Hide();
        Dispatcher.UIThread.RunJobs();
        window.Show();
        window.Activate();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("b");
        Assert.Equal("b", window.EditorView.Text);
    }
}
