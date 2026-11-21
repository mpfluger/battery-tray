using Microsoft.Win32.SafeHandles;

namespace BatteryTray.Devices;

/// <summary>
/// Razer wireless mice behind their HyperSpeed receiver, queried with Razer's 90-byte
/// feature-report protocol (reverse-engineered by the OpenRazer project).
/// </summary>
sealed class RazerMice : IBatterySource
{
    const ushort Razer = 0x1532;

    // Receiver product id → mouse name and the transaction id the mouse answers to.
    static readonly Dictionary<ushort, (string Name, byte TransactionId)> Receivers = new()
    {
        [0x00C5] = ("DeathAdder V3 HyperSpeed", 0x1F),
    };

    const int ReportLength = 91; // report id + 90-byte message
    const byte PowerClass = 0x07, GetLevel = 0x80, GetCharging = 0x84;
    const byte StatusSuccess = 0x02;

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var readings = new List<BatteryReading>();
        foreach (var device in HidDevice.All())
        {
            if (device.VendorId != Razer || device.FeatureReportLength != ReportLength
                || !Receivers.TryGetValue(device.ProductId, out var mouse)) continue;

            using var handle = device.OpenForFeatures();
            var level = await QueryAsync(handle, mouse.TransactionId, GetLevel);
            var charging = await QueryAsync(handle, mouse.TransactionId, GetCharging);
            readings.Add(new(device.Path, mouse.Name, level * 100 / 255, charging == 1));
        }
        return readings;
    }

    static async Task<byte?> QueryAsync(SafeFileHandle handle, byte transactionId, byte command)
    {
        var request = new byte[ReportLength];
        request[2] = transactionId;
        request[6] = 2; // argument count
        request[7] = PowerClass;
        request[8] = command;
        request[89] = request[3..89].Aggregate((checksum, b) => (byte)(checksum ^ b));

        // Razer Synapse talks to the same receiver, so a reply may answer its request instead; retry then.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!HidDevice.SetFeature(handle, request)) return null;
            await Task.Delay(50); // the receiver relays the request over the air

            var reply = new byte[ReportLength];
            if (!HidDevice.GetFeature(handle, reply)) return null;
            if (reply[2] == transactionId && reply[7] == PowerClass && reply[8] == command)
                return reply[1] == StatusSuccess ? reply[10] : null; // other statuses: the mouse is asleep or off
        }
        return null;
    }
}
