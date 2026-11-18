using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// The status bar's counts scan the whole document, and <c>Stats</c> caches its answer only until the
/// next change — so asking for them on every keystroke meant a full pass over the file per keystroke.
/// Measured on the benchmark harness at <b>494 ms and 392 MB for one character typed into a 50 MB log</b>,
/// against 23 ms for the edit itself. Typing was 21× the cost of the edit, in the editor whose entire
/// design brief is files that size.
/// </summary>
public class StatusDebounceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"plaintain-status-{Guid.NewGuid():N}");

    public StatusDebounceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a green test over.
        }

        GC.SuppressFinalize(this);
    }

    private MainWindow Open()
    {
        var window = TestShell.NewWindow(new SettingsService(Path.Combine(_dir, $"s-{Guid.NewGuid():N}.json")));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// The caret's position is cheap and must stay immediate — it is the part of the status bar someone is
    /// actually watching while they type.
    /// </summary>
    [AvaloniaFact]
    public void The_caret_position_still_updates_on_every_keystroke()
    {
        var window = Open();
        var buffer = window.EditorView.Editor.Buffer;

        buffer.Insert("hello");
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Col 6", window.StatusPositionForTest, StringComparison.Ordinal);
    }

    /// <summary>
    /// The counts settle once the typing pauses. Asserting through the flush rather than by waiting keeps
    /// the test honest about what it is testing: that the scan happens, not when a timer fires.
    /// </summary>
    [AvaloniaFact]
    public void The_counts_settle_after_the_typing_pauses()
    {
        var window = Open();
        var buffer = window.EditorView.Editor.Buffer;

        buffer.Insert("one\ntwo\nthree");
        Dispatcher.UIThread.RunJobs();
        window.FlushStatusForTest();

        Assert.Contains("3 lines", window.StatusCountsForTest, StringComparison.Ordinal);
        Assert.Contains("13 chars", window.StatusCountsForTest, StringComparison.Ordinal);
    }

    /// <summary>
    /// Opening a file is not typing: there is no burst to wait out, and the counts must be right the moment
    /// the document appears rather than a fifth of a second later.
    /// </summary>
    [AvaloniaFact]
    public void Opening_a_file_reports_its_counts_immediately()
    {
        var path = Path.Combine(_dir, "notes.txt");
        File.WriteAllText(path, "one\ntwo\n");
        var window = Open();

        window.OpenPath(path);

        Assert.Contains("lines", window.StatusCountsForTest, StringComparison.Ordinal);
        Assert.Contains("8 chars", window.StatusCountsForTest, StringComparison.Ordinal);
    }

    /// <summary>
    /// A large file can't be counted before it appears without holding the window for half a second (a
    /// 50 MB log), so it shows the line count, which the document already knows, and counts the rest in
    /// the background. The status bar must end up with the right numbers and the line-ending style.
    /// </summary>
    [AvaloniaFact]
    public void Opening_a_large_file_shows_its_lines_at_once_and_counts_the_rest_in_the_background()
    {
        var path = Path.Combine(_dir, "big.log");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("a line of the log\r\n", 50_000)));
        var window = Open();
        window.CountInBackgroundFrom = 0;

        window.OpenPath(path);
        Assert.Equal("50001 lines · counting…", window.StatusCountsForTest);
        Assert.Equal("UTF-8", window.StatusEncodingForTest);

        Settle(window);
        Assert.Equal("50001 lines · 900000 chars", window.StatusCountsForTest);
        Assert.Equal("UTF-8 · CRLF", window.StatusEncodingForTest);

        // Counts that are already known (a save, a zoom) are shown at once, not counted again.
        window.FlushStatusForTest();
        Assert.Equal("50001 lines · 900000 chars", window.StatusCountsForTest);
    }

    private static void Settle(MainWindow window)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!window.PendingStatusForTest.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(window.PendingStatusForTest.IsCompleted, "the count never finished");
    }

    /// <summary>
    /// After a typing pause the counts come from a background scan, so a key pressed while a 50 MB file is
    /// being counted isn't held behind it. The status bar must still end up with the right numbers.
    /// </summary>
    [AvaloniaFact]
    public async Task The_counts_after_a_pause_are_counted_in_the_background()
    {
        var window = Open();
        var buffer = window.EditorView.Editor.Buffer;

        buffer.Insert("one\ntwo\nthree");
        Dispatcher.UIThread.RunJobs();
        await window.RefreshStatusAfterTypingForTest();

        Assert.Contains("3 lines", window.StatusCountsForTest, StringComparison.Ordinal);
        Assert.Contains("13 chars", window.StatusCountsForTest, StringComparison.Ordinal);
    }
}
