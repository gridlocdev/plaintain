using Plaintain.Components.Core;
using Plaintain.TestSupport;
using Xunit;

namespace Plaintain.UiTests;

/// <summary>
/// The app's own data directory follows the test profile on every OS, so no test touches the developer's
/// real settings or recovery snapshot.
/// </summary>
public class UserDataDirectoryTests
{
    [Fact]
    public void Application_data_resolves_inside_the_throwaway_profile()
    {
        string path = UserDataDirectory.ApplicationData;

        Assert.True(
            path.StartsWith(HermeticProfile.Root, StringComparison.Ordinal),
            $"UserDataDirectory.ApplicationData resolved to {path}, outside {HermeticProfile.Root}");
    }

    [Fact]
    public void On_macOS_it_is_application_support_under_home()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");

        Assert.Equal(
            Path.Combine(HermeticProfile.Root, "Library", "Application Support"),
            UserDataDirectory.ApplicationData);
    }
}
