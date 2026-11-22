namespace Gearfuel.Devices;

/// <summary>
/// Wireless headsets that answer a battery request on a vendor-defined HID collection, as documented by the
/// HeadsetControl project: the request is an output report, the answer an input report. Byte positions below
/// include the report ID; devices without report IDs send a 0 there on Windows.
/// </summary>
sealed class Headsets : IBatterySource
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    internal readonly record struct Battery(int Percent, bool Charging);

    /// <summary>Writes a request and returns the first input report that answers it, or null if none does in time.</summary>
    internal delegate Task<byte[]?> Ask(byte[] request, Func<byte[], bool> answers);

    sealed record Model(string Name, ushort VendorId, ushort[] ProductIds, ushort UsagePage, ushort Usage, Func<Ask, Task<Battery?>> ReadAsync);

    static readonly Model[] Models =
    [
        new("HyperX Cloud II Wireless", 0x03F0, [0x0696, 0x018B], 0xFF90, 0x0303, CloudIIAsync),
        // The first Arctis Nova 7 models report the level in five steps, later ones in percent.
        new("SteelSeries Arctis Nova 7", 0x1038, [0x2202, 0x2206, 0x220A, 0x223A, 0x227A, 0x22A4], 0xFFC0, 0x0001, ask => ArctisNova7Async(ask, inSteps: true)),
        new("SteelSeries Arctis Nova 7", 0x1038, [0x2258, 0x227E, 0x2298, 0x229E, 0x22A1, 0x22A5, 0x22A7, 0x22A9, 0x22AD], 0xFFC0, 0x0001, ask => ArctisNova7Async(ask, inSteps: false)),
        new("SteelSeries Arctis 7", 0x1038, [0x1252, 0x1260, 0x1280, 0x12AD], 0xFF43, 0x0202, Arctis7Async),
        new("Corsair Void", 0x1B1C, [0x0A14, 0x0A16, 0x0A17, 0x0A1A, 0x0A1D, 0x0A2B, 0x0A38, 0x0A4F, 0x0A51, 0x0A52, 0x0A55, 0x0A56, 0x0A75, 0x1B1C, 0x1B23, 0x1B27, 0x1B29, 0x1B2A], 0xFFC5, 0x0001, CorsairVoidAsync),
    ];

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices)
    {
        var readings = new List<BatteryReading>();
        foreach (var hid in hidDevices)
        {
            var model = Models.FirstOrDefault(m => m.VendorId == hid.VendorId && m.ProductIds.Contains(hid.ProductId) && m.UsagePage == hid.UsagePage && m.Usage == hid.Usage);
            if (model is null || hid.InputReportLength == 0 || hid.OutputReportLength == 0) continue;
            try
            {
                await using var stream = hid.OpenStream();
                if (await model.ReadAsync(Asker(stream, hid)) is { } battery)
                    readings.Add(new(hid.Path, hid.ProductName is { Length: > 0 } name ? name : model.Name, battery.Percent, battery.Charging));
            }
            catch (IOException e)
            {
                Diagnostics.Report($"{model.Name} failed: {e.Message}");
            }
        }
        return readings;
    }

    static Ask Asker(Stream stream, HidDevice hid) => async (request, answers) =>
    {
        var report = new byte[hid.OutputReportLength];
        request.CopyTo(report, 0);
        await stream.WriteAsync(report);

        using var timeout = new CancellationTokenSource(Timeout);
        var reply = new byte[hid.InputReportLength];
        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(reply, timeout.Token);
                if (answers(reply)) return reply;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    };

    // One request for the level, a second for whether it's charging.
    internal static async Task<Battery?> CloudIIAsync(Ask ask)
    {
        if (await ask([0x06, 0xFF, 0xBB, 0x02], r => r is [0x06, 0xFF, 0xBB, 0x02, ..]) is not [_, _, _, _, _, _, _, var level, ..]) return null;
        var charging = await ask([0x06, 0xFF, 0xBB, 0x03], r => r is [0x06, 0xFF, 0xBB, 0x03, ..]) is [_, _, _, _, 1, ..];
        return new(Math.Min((int)level, 100), charging);
    }

    // A status report: the level, then 0 while the headset is off, 1 or 2 while charging.
    internal static async Task<Battery?> ArctisNova7Async(Ask ask, bool inSteps) =>
        await ask([0x00, 0xB0], r => r is [0x00, 0xB0, ..]) is [_, _, _, var level, var status and not 0, ..]
            ? new(inSteps ? Math.Min((int)level, 4) * 25 : Math.Min((int)level, 100), status is 1 or 2)
            : null;

    // The level only; 0 is taken as the headset being off, as it has no separate status.
    internal static async Task<Battery?> Arctis7Async(Ask ask) =>
        await ask([0x06, 0x18], r => r is [0x06, ..]) is [_, _, > 0 and <= 100 and var level, ..] ? new(level, Charging: false) : null;

    // The level, whose top bit says whether the microphone is up, then a status: 0 off, 1 or 2 on battery, 4 or 5 charging.
    internal static async Task<Battery?> CorsairVoidAsync(Ask ask) =>
        await ask([0xC9, 0x64], r => r is [0x64, ..]) is [_, _, var level, _, var status and (1 or 2 or 4 or 5), ..]
            ? new(Math.Min(level & 0x7F, 100), status is 4 or 5)
            : null;
}
