using System.Text.Json;
using System.Text.Json.Serialization;
using Puffinote.Components.Shell;
using Puffinote.Components.Theming;

namespace Plaintain.Desktop.Services;

/// <summary>What Plaintain remembers between sessions. Deliberately short.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// Light, dark, or whatever the OS is wearing. Unset means <see cref="ThemePreference.System"/>,
    /// because an app that has never been told which appearance to wear should wear the desktop's.
    /// </summary>
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Whether long lines fold. Off is the Notepad default; the toggle is remembered because
    /// people who want wrap want it for everything, and people who don't never want it once.</summary>
    public bool WordWrap { get; set; }

    public bool ShowLineNumbers { get; set; } = true;

    /// <summary>
    /// Whether the caret's row is washed with a faint tint. On, as the surface has always drawn it — this
    /// setting exists so it can be turned off, not to change what it does by default.
    /// </summary>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>
    /// Whether the other places the selected text appears are tinted while it is selected. On, because
    /// "where else is this?" is the question a plaintext file gets read for; off for anyone whose files
    /// make it answer "everywhere", which is a page that will not sit still.
    /// </summary>
    public bool HighlightSelectionOccurrences { get; set; } = true;

    public double FontSize { get; set; } = 13;

    /// <summary>The reading zoom applied on top of <see cref="FontSize"/>, on the family's shared ladder
    /// (1.0 = 100%).</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>
    /// Seconds of no editing before the open file is written back to disk; 0 turns auto-save off.
    /// Plaintain opens logs and config files as readily as notes, so this only ever runs for a file whose
    /// encoding is confirmed — see the guard in the window.
    /// </summary>
    public int AutoSaveSeconds { get; set; } = AutoSave.DefaultSeconds;

    /// <summary>
    /// Seconds between crash-recovery snapshots of an edited buffer; 0 turns them off. Distinct from
    /// <see cref="AutoSaveSeconds"/>: that writes the <i>document</i> and can only run for a named file
    /// whose encoding is confirmed, this writes a sidecar copy of the buffer and runs for anything —
    /// including the untitled scratch buffer and the file auto-save is refusing to touch, which is
    /// precisely where losing the text would hurt most.
    /// </summary>
    public int RecoverySnapshotSeconds { get; set; } = 30;

    /// <summary>Most-recently-used files, newest first.</summary>
    /// <remarks>
    /// Getter-only and filled in place: the source-generated reader treats an <c>init</c> property like a
    /// constructor argument, so a file without this key would set it to null instead of keeping the empty
    /// list, and the next recent-file push would crash.
    /// </remarks>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<string> RecentFiles { get; } = [];

    /// <summary>Record <paramref name="path"/> as the most recent, de-duplicated and capped.</summary>
    public void PushRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 10)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }
    }
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under the platform's application-data directory.
/// A missing or corrupt file is not an error — it is a first run, and the defaults are the answer.
/// </summary>
public sealed class SettingsService
{
    private readonly string _path;

    public SettingsService(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Plaintain", "settings.json");
        Settings = Read();
    }

    public AppSettings Settings { get; private set; }

    private AppSettings Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, SettingsJsonContext.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a preference is not worth interrupting anyone over.
        }
    }
}

/// <summary>
/// Source-generated JSON for <see cref="AppSettings"/>, so loading and saving work in a Native AOT build,
/// where reflection-based serialization is trimmed away. Enums are written by name ("Dark"), as the rest of
/// the family writes them, so the file says what it means to whoever opens it; numbers still read.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
