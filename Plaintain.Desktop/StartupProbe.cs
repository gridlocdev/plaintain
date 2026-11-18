using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Plaintain.Desktop.Services;
using Plaintain.Desktop.Views;
using Puffinote.Components.Core.Documents;

namespace Plaintain.Desktop;

/// <summary>
/// Measures cold start without showing anything: with <c>PLAINTAIN_STARTUP_PROBE</c> set to a scratch
/// directory, the app opens its real window off-screen, transparent and never activated, with no Dock icon,
/// against settings and recovery files in that directory. After the first frame it prints the time since
/// the process started and exits.
/// </summary>
internal static class StartupProbe
{
    private const string Variable = "PLAINTAIN_STARTUP_PROBE";
    private const int OffScreen = -20000;

    /// <summary>The scratch directory, or null when the probe is off.</summary>
    internal static string? Directory { get; } = Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } dir ? dir : null;

    internal static bool IsOn => Directory is not null;

    internal static MainWindow OpenWindow(IClassicDesktopStyleApplicationLifetime desktop, string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var settings = new SettingsService(Path.Combine(directory, "settings.json"));
        var recovery = new RecoveryService("Plaintain", Path.Combine(directory, "recovery"));

        // What the files in the scratch directory read back as. Under Native AOT a JSON type the trimmer
        // dropped fails here, so this line is how a published build shows its settings still load.
        var s = settings.Settings;
        var snapshot = recovery.TryLoad();
        Console.WriteLine($"settings theme={s.Theme} wrap={s.WordWrap} font={s.FontSize} recent={s.RecentFiles.Count}; " +
            $"recovery={(snapshot is null ? "none" : $"{snapshot.Text.Length} chars")}");
        settings.Save(); // and writing them back

        var window = new MainWindow(settings, recovery)
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Opacity = 0,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Position = new PixelPoint(OffScreen, OffScreen),
        };
        window.Opened += (_, _) => window.RequestAnimationFrame(_ => window.RequestAnimationFrame(_ =>
        {
            var elapsed = DateTime.Now - Process.GetCurrentProcess().StartTime;
            Console.WriteLine($"first-frame-ms {elapsed.TotalMilliseconds:F0}");
            desktop.Shutdown();
        }));
        return window;
    }
}
