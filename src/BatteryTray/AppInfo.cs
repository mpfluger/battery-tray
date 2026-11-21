namespace BatteryTray;

static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/mpfluger/battery-tray";

    /// <summary>The release version, without the commit hash the SDK appends.</summary>
    public static string Version => Application.ProductVersion.Split('+')[0];
}
