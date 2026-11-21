namespace BatteryTray.Devices;

/// <summary>
/// DualShock 4 and DualSense controllers over USB or Bluetooth, read from their input reports
/// (layouts as in the Linux hid-playstation driver).
/// </summary>
sealed class PlayStationControllers : IBatterySource
{
    const ushort Sony = 0x054C;

    static readonly Dictionary<ushort, string> Models = new()
    {
        [0x05C4] = "DualShock 4",
        [0x09CC] = "DualShock 4",
        [0x0CE6] = "DualSense",
        [0x0DF2] = "DualSense Edge",
    };

    const byte CalibrationReport = 0x05;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var readings = new List<BatteryReading>();
        foreach (var device in HidDevice.All())
        {
            if (device.VendorId != Sony || !Models.TryGetValue(device.ProductId, out var model)) continue;

            if (await ReadBatteryAsync(device, isDualSense: model.StartsWith("DualSense")) is (var percent, var charging))
                readings.Add(new(device.Path, model, percent, charging));
        }
        return readings;
    }

    static async Task<(int Percent, bool Charging)?> ReadBatteryAsync(HidDevice device, bool isDualSense)
    {
        // Paths of Bluetooth HID devices contain the HID service UUID.
        var bluetooth = device.Path.Contains("{00001124-", StringComparison.OrdinalIgnoreCase);
        await using var stream = device.OpenStream();

        // Over Bluetooth, controllers send battery-less reports until the calibration report is read.
        if (bluetooth)
        {
            var calibration = new byte[device.FeatureReportLength];
            calibration[0] = CalibrationReport;
            HidDevice.GetFeature(stream.SafeFileHandle, calibration);
        }

        using var timeout = new CancellationTokenSource(Timeout);
        var report = new byte[device.InputReportLength];
        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(report, timeout.Token);
                if (StatusOffset(report[0], bluetooth, isDualSense) is not int offset) continue;

                var status = report[offset];
                var level = status & 0x0F; // tenths of a full charge
                var charging = isDualSense ? status >> 4 == 1 : (status & 0x10) != 0 && level < 10;
                return (Math.Min(level * 10 + 5, 100), charging);
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // Where the battery status byte sits in each input report that has one.
    static int? StatusOffset(byte reportId, bool bluetooth, bool isDualSense) => (reportId, bluetooth, isDualSense) switch
    {
        (0x01, false, false) => 30,
        (0x11, true, false) => 32,
        (0x01, false, true) => 53,
        (0x31, true, true) => 54,
        _ => null,
    };
}
