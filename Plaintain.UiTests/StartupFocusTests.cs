using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Puffinote.Components.Core.Documents;
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
}
