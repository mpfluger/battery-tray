namespace Gearfuel;

static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/mpfluger/gearfuel";

    /// <summary>The release version, without the commit hash the SDK appends.</summary>
    public static string Version => Application.ProductVersion.Split('+')[0];

    /// <summary>Where Gearfuel keeps its files: the install folder, so uninstalling removes them too.</summary>
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gearfuel");
}
