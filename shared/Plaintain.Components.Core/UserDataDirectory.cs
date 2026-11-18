namespace Plaintain.Components.Core;

/// <summary>Where the app keeps its own data: settings, recovery snapshots, user themes.</summary>
public static class UserDataDirectory
{
    /// <summary>
    /// The platform's per-user application-data directory: <c>~/Library/Application Support</c> on macOS,
    /// <c>%APPDATA%</c> on Windows and <c>$XDG_CONFIG_HOME</c> (or <c>~/.config</c>) on Linux.
    /// </summary>
    /// <remarks>
    /// On macOS this is built from <c>HOME</c> rather than taken from
    /// <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>, which asks the OS and ignores
    /// <c>HOME</c>. It is the same directory for a real user, but only this way can a test process point
    /// the app somewhere else, so the tests never read or write the developer's real settings.
    /// </remarks>
    public static string ApplicationData
    {
        get
        {
            if (OperatingSystem.IsMacOS())
            {
                string? home = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(home))
                {
                    return Path.Combine(home, "Library", "Application Support");
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
    }
}
