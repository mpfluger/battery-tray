using Microsoft.Win32.SafeHandles;

namespace BatteryTray.Devices;

/// <summary>
/// Razer wireless devices (directly or through a receiver), queried with Razer's 90-byte
/// feature-report protocol (reverse-engineered by the OpenRazer project).
/// </summary>
sealed class RazerDevices : IBatterySource
{
    const ushort Razer = 0x1532;
    const int ReportLength = 91; // report id + 90-byte message
    const byte PowerClass = 0x07, GetLevel = 0x80, GetCharging = 0x84;
    const byte StatusSuccess = 0x02;

    // Each model answers to one transaction id; these cover the known ones, newest models first.
    static readonly byte[] TransactionIds = [0x1F, 0x3F, 0xFF];

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var readings = new List<BatteryReading>();
        var devices = HidDevice.All().Where(d => d.VendorId == Razer && d.FeatureReportLength == ReportLength);
        foreach (var device in devices.DistinctBy(d => d.ProductId))
        {
            using var handle = device.OpenForFeatures();
            foreach (var transactionId in TransactionIds)
            {
                // No answer: wrong id, or the device is asleep. A level of 0 is taken as "no battery".
                if (await QueryAsync(handle, transactionId, GetLevel) is not (> 0 and var level)) continue;

                var charging = await QueryAsync(handle, transactionId, GetCharging) == 1;
                readings.Add(new(device.Path, device.ProductName, level * 100 / 255, charging));
                break;
            }
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

        // Razer Synapse talks to the same device, so a reply may answer its request instead; retry then.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!HidDevice.SetFeature(handle, request)) return null;
            await Task.Delay(50); // a receiver relays the request over the air

            var reply = new byte[ReportLength];
            if (!HidDevice.GetFeature(handle, reply)) return null;
            if (reply[2] == transactionId && reply[7] == PowerClass && reply[8] == command)
                return reply[1] == StatusSuccess ? reply[10] : null;
        }
        return null;
    }
}
