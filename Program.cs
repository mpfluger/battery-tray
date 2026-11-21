using BatteryTray.Devices;

namespace BatteryTray;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "BatteryTray.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();
        using var app = new TrayApp(new BluetoothDevices(), new RazerDevices(), new LogitechDevices(), new PlayStationControllers());
        Application.Run(app);
    }
}
