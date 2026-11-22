using Gearfuel.Devices;
using Velopack;

namespace Gearfuel;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Handles the installer's hooks (which exit the process) and must run first.
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => StartWithWindows.Enabled = true)
            .OnAfterUpdateFastCallback(_ => StartWithWindows.RefreshAfterUpdate())
            .OnBeforeUninstallFastCallback(_ => StartWithWindows.Enabled = false)
            .Run();

        using var mutex = new Mutex(initiallyOwned: true, "Gearfuel.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp(new BluetoothDevices(), new RazerDevices(), new LogitechDevices(), new PlayStationControllers(), new XboxControllers(), new Headsets());
        _ = Updater.KeepUpdatedAsync(restart: app.ExitThread);
        Application.Run(app);
    }
}
