using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plaintain.Components.Core.Documents;

/// <summary>An unsaved-work snapshot written by a shell's snapshot timer.</summary>
public sealed class RecoverySnapshot
{
    /// <summary>The document's file path, or null if it was never saved (an Untitled buffer).</summary>
    public string? Path { get; set; }

    /// <summary>The buffer text at snapshot time.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>When the snapshot was taken (UTC).</summary>
    public DateTime SavedAtUtc { get; set; }
}

/// <summary>
/// Persists a single crash-recovery snapshot of unsaved work to the app-config location. A shell
/// snapshots the buffer here while it is dirty and clears it on a clean save or exit, so a surviving
/// snapshot at startup means the app didn't shut down cleanly — and the user is offered the chance to
/// recover. All operations are best-effort: a failure never interrupts editing.
/// <para>
/// Nothing app-specific is left once the app name is a parameter. It stays text-shaped on purpose: the
/// snapshot is the buffer's text, and generalising it to other kinds of document would be the wrong
/// abstraction.
/// </para>
/// </summary>
public sealed class RecoveryService
{
    private readonly string _path;

    /// <param name="appName">
    /// The app's folder under the platform's application-data directory ("Plaintain"). The snapshot
    /// lives there, beside the rest of the app's own data.
    /// </param>
    /// <param name="directory">An explicit directory, overriding <paramref name="appName"/> (tests).</param>
    public RecoveryService(string appName, string? directory = null)
    {
        var dir = directory ?? Path.Combine(UserDataDirectory.ApplicationData, appName);
        _path = Path.Combine(dir, "recovery.json");
    }

    /// <summary>Write a snapshot of the current (unsaved) buffer.</summary>
    public void Save(string? documentPath, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var snapshot = new RecoverySnapshot { Path = documentPath, Text = text, SavedAtUtc = DateTime.UtcNow };
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, RecoveryJsonContext.Default.RecoverySnapshot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a failed snapshot must never disrupt editing.
        }
    }

    /// <summary>The pending snapshot, or null if there is none (or it is unreadable).</summary>
    public RecoverySnapshot? TryLoad()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize(File.ReadAllText(_path), RecoveryJsonContext.Default.RecoverySnapshot);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt snapshot — ignore it rather than block startup.
        }

        return null;
    }

    /// <summary>Discard the snapshot (after a clean save/exit or once the user declines recovery).</summary>
    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore; a stale snapshot is harmless and will be overwritten on the next save.
        }
    }
}

/// <summary>Source-generated JSON for <see cref="RecoverySnapshot"/>, so recovery works in a Native AOT build.</summary>
[JsonSerializable(typeof(RecoverySnapshot))]
internal sealed partial class RecoveryJsonContext : JsonSerializerContext;
