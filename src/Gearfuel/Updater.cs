using Velopack;
using Velopack.Sources;

namespace Gearfuel;

/// <summary>Installs new GitHub releases in the background.</summary>
static class Updater
{
    static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <param name="restart">Exits the app; the update is applied and the app restarted once it has.</param>
    public static async Task KeepUpdatedAsync(Action restart)
    {
        var manager = new UpdateManager(new GithubSource(AppInfo.RepositoryUrl, accessToken: null, prerelease: false));
        if (!manager.IsInstalled) return; // e.g. started from a build folder

        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                if (await manager.CheckForUpdatesAsync() is not { } update) continue;

                await manager.DownloadUpdatesAsync(update);
                manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: true);
                restart();
                return;
            }
            catch (Exception e)
            {
                Diagnostics.Report($"Update failed: {e.Message}");
            }
        } while (await timer.WaitForNextTickAsync());
    }
}
