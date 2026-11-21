using System.Text;

namespace BatteryTray.Devices;

/// <summary>
/// Logitech devices that speak HID++ 2.0 (documented by Logitech and the Solaar project):
/// behind a LIGHTSPEED, Bolt or Unifying receiver, or plugged in directly.
/// </summary>
sealed class LogitechDevices : IBatterySource
{
    const ushort Logitech = 0x046D;
    static readonly ushort[] Receivers = [0xC52B, 0xC532, 0xC539, 0xC53A, 0xC53F, 0xC541, 0xC545, 0xC547, 0xC548];
    static readonly byte[] ReceiverSlots = [1, 2, 3, 4, 5, 6];
    const byte DirectDevice = 0xFF; // device index of a device that isn't behind a receiver

    const byte LongReport = 0x11, ReportLength = 20;
    const byte SoftwareId = 0x0B; // tags our requests, so replies to G HUB's can be told apart
    const ushort DeviceNameFeature = 0x0005, BatteryStatusFeature = 0x1000, UnifiedBatteryFeature = 0x1004;
    static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync()
    {
        var readings = new List<BatteryReading>();
        foreach (var hid in HidDevice.All())
        {
            // HID++ long reports have their own vendor-defined collection.
            if (hid.VendorId != Logitech || hid.UsagePage != 0xFF00 || hid.Usage != 0x02) continue;

            await using var stream = hid.OpenStream();
            byte[] candidates = Receivers.Contains(hid.ProductId) ? ReceiverSlots : [DirectDevice];
            foreach (var device in await OnlineAsync(stream, candidates))
            {
                if (await ReadBatteryAsync(stream, device) is not (var percent, var charging)) continue;
                var name = await ReadNameAsync(stream, device) ?? hid.ProductName;
                readings.Add(new($"{hid.Path}#{device}", name, percent, charging));
            }
        }
        return readings;
    }

    // Pings all candidates at once, so absent ones cost a single timeout.
    static async Task<List<byte>> OnlineAsync(FileStream stream, byte[] candidates)
    {
        const byte ping = 1; // root feature, getProtocolVersion
        foreach (var device in candidates) await stream.WriteAsync(Request(device, feature: 0, ping));

        var online = new List<byte>();
        await foreach (var reply in RepliesAsync(stream))
        {
            if (reply[2] == 0 && reply[3] == FunctionByte(ping) && candidates.Contains(reply[1])) online.Add(reply[1]);
            if (online.Count == candidates.Length) break;
        }
        return online;
    }

    static async Task<(int Percent, bool Charging)?> ReadBatteryAsync(FileStream stream, byte device)
    {
        if (await CallAsync(stream, device, UnifiedBatteryFeature, function: 1) is [var percent, _, var status, ..])
            return (percent, status is 1 or 2); // charging, charging slowly
        if (await CallAsync(stream, device, BatteryStatusFeature, function: 0) is [var level, _, var oldStatus, ..])
            return (level, oldStatus is 1 or 2 or 4); // recharging, almost full, slow recharge
        return null; // e.g. devices that only report a voltage
    }

    static async Task<string?> ReadNameAsync(FileStream stream, byte device)
    {
        if (await FeatureIndexAsync(stream, device, DeviceNameFeature) is not byte feature
            || await SendAsync(stream, device, feature, function: 0) is not [var length, ..]) return null;

        var name = new StringBuilder();
        while (name.Length < length && await SendAsync(stream, device, feature, function: 1, (byte)name.Length) is { } chunk)
            name.Append(Encoding.ASCII.GetString(chunk, 0, Math.Min(chunk.Length, length - name.Length)));
        return name.ToString();
    }

    static async Task<byte[]?> CallAsync(FileStream stream, byte device, ushort featureId, byte function) =>
        await FeatureIndexAsync(stream, device, featureId) is byte feature ? await SendAsync(stream, device, feature, function) : null;

    // Features live at device-specific indexes, looked up through the root feature at index 0.
    static async Task<byte?> FeatureIndexAsync(FileStream stream, byte device, ushort featureId) =>
        await SendAsync(stream, device, feature: 0, function: 0, (byte)(featureId >> 8), (byte)featureId) is [> 0 and var index, ..] ? index : null;

    /// <returns>The reply's parameters, or null on an error reply or when the device doesn't answer.</returns>
    static async Task<byte[]?> SendAsync(FileStream stream, byte device, byte feature, byte function, params byte[] parameters)
    {
        await stream.WriteAsync(Request(device, feature, function, parameters));
        await foreach (var reply in RepliesAsync(stream))
        {
            if (reply[1] != device) continue;
            if (reply[2] == feature && reply[3] == FunctionByte(function)) return reply[4..];
            if (reply[2] == 0xFF && reply[3] == feature && reply[4] == FunctionByte(function)) return null; // error reply
        }
        return null;
    }

    static byte[] Request(byte device, byte feature, byte function, params byte[] parameters)
    {
        var request = new byte[ReportLength];
        request[0] = LongReport;
        request[1] = device;
        request[2] = feature;
        request[3] = FunctionByte(function);
        parameters.CopyTo(request, 4);
        return request;
    }

    static byte FunctionByte(byte function) => (byte)(function << 4 | SoftwareId);

    // Incoming long reports until the timeout.
    static async IAsyncEnumerable<byte[]> RepliesAsync(FileStream stream)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var reply = new byte[ReportLength];
            try
            {
                await stream.ReadExactlyAsync(reply, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            yield return reply;
        }
    }
}
