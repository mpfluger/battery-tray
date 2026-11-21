using System.Text;

namespace BatteryTray.Devices;

/// <summary>
/// Logitech devices behind a LIGHTSPEED receiver, queried with HID++ 2.0
/// (documented by Logitech and the Solaar project).
/// </summary>
sealed class LogitechReceivers : IBatterySource
{
    const ushort Logitech = 0x046D;
    static readonly ushort[] LightspeedReceivers = [0xC539, 0xC53A, 0xC53F, 0xC541, 0xC545, 0xC547];

    const byte LongReport = 0x11, ReportLength = 20;
    const byte DeviceIndex = 1; // a LIGHTSPEED receiver pairs a single device
    const byte SoftwareId = 0x0B; // tags our requests, so replies to G HUB's can be told apart
    const ushort DeviceNameFeature = 0x0005, UnifiedBatteryFeature = 0x1004;
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var readings = new List<BatteryReading>();
        foreach (var receiver in HidDevice.All())
        {
            // HID++ long reports have their own vendor-defined collection.
            if (receiver.VendorId != Logitech || !LightspeedReceivers.Contains(receiver.ProductId)
                || receiver.UsagePage != 0xFF00 || receiver.Usage != 0x02) continue;

            await using var stream = receiver.OpenStream();
            var status = await CallAsync(stream, UnifiedBatteryFeature, function: 1);
            var name = status is null ? null : await ReadNameAsync(stream);
            readings.Add(new(receiver.Path, name ?? "Logitech device", status?[0], status?[2] is 1 or 2));
        }
        return readings;
    }

    static async Task<string?> ReadNameAsync(FileStream stream)
    {
        if (await FeatureIndexAsync(stream, DeviceNameFeature) is not byte feature
            || await SendAsync(stream, feature, function: 0) is not [var length, ..]) return null;

        var name = new StringBuilder();
        while (name.Length < length && await SendAsync(stream, feature, function: 1, (byte)name.Length) is { } chunk)
            name.Append(Encoding.ASCII.GetString(chunk, 0, Math.Min(chunk.Length, length - name.Length)));
        return name.ToString();
    }

    static async Task<byte[]?> CallAsync(FileStream stream, ushort featureId, byte function) =>
        await FeatureIndexAsync(stream, featureId) is byte feature ? await SendAsync(stream, feature, function) : null;

    // Features live at device-specific indexes, looked up through the root feature at index 0.
    static async Task<byte?> FeatureIndexAsync(FileStream stream, ushort featureId) =>
        await SendAsync(stream, feature: 0, function: 0, (byte)(featureId >> 8), (byte)featureId) is [> 0 and var index, ..] ? index : null;

    /// <returns>The reply's parameters, or null on an error reply or when the device doesn't answer.</returns>
    static async Task<byte[]?> SendAsync(FileStream stream, byte feature, byte function, params byte[] parameters)
    {
        var request = new byte[ReportLength];
        request[0] = LongReport;
        request[1] = DeviceIndex;
        request[2] = feature;
        request[3] = (byte)(function << 4 | SoftwareId);
        parameters.CopyTo(request, 4);
        await stream.WriteAsync(request);

        using var timeout = new CancellationTokenSource(Timeout);
        var reply = new byte[ReportLength];
        try
        {
            while (true)
            {
                await stream.ReadExactlyAsync(reply, timeout.Token);
                if (reply[1] != DeviceIndex) continue;
                if (reply[2] == feature && reply[3] == request[3]) return reply[4..];
                if (reply[2] == 0xFF && reply[3] == feature && reply[4] == request[3]) return null; // error reply
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
