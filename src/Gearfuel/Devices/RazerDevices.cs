using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Gearfuel.Devices;

/// <summary>
/// Razer wireless devices (directly or through a receiver), queried with Razer's 90-byte
/// feature-report protocol (reverse-engineered by the OpenRazer project).
/// </summary>
sealed class RazerDevices : IBatterySource
{
    const ushort Razer = 0x1532;
    const int ReportLength = 91; // report id + 90-byte message
    internal const byte InfoClass = 0x00, GetSerial = 0x82, SerialLength = 22;
    internal const byte PowerClass = 0x07, GetLevel = 0x80, GetCharging = 0x84;
    const byte StatusSuccess = 0x02;

    // Each model answers to one transaction id; these cover the known ones, newest models first.
    static readonly byte[] TransactionIds = [0x1F, 0x3F, 0xFF];

    public async Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices)
    {
        var readings = new List<BatteryReading>();
        var devices = hidDevices.Where(d => d.VendorId == Razer && d.FeatureReportLength == ReportLength);
        foreach (var device in devices)
        {
            using var handle = device.OpenForFeatures();
            foreach (var transactionId in TransactionIds)
            {
                // No answer: wrong id, or the device is asleep. A level of 0 is taken as "no battery".
                if (await QueryAsync(handle, transactionId, PowerClass, GetLevel, 2) is not [_, > 0 and var level, ..]) continue;

                var charging = await QueryAsync(handle, transactionId, PowerClass, GetCharging, 2) is [_, 1, ..];
                // A mouse charging over its cable shows up as a second device; the serial ties both to one.
                var serial = Serial(await QueryAsync(handle, transactionId, InfoClass, GetSerial, SerialLength));
                readings.Add(new(serial is null ? device.Path : $"razer:{serial}", device.ProductName, level * 100 / 255, charging) { Level = level * 100.0 / 255, Connection = device.Path });
                break;
            }
        }
        // Some devices answer on more than one collection; the serial tells those apart from two of the same model.
        return readings.DistinctBy(r => r.Id).ToList();
    }

    static async Task<byte[]?> QueryAsync(SafeFileHandle handle, byte transactionId, byte commandClass, byte command, byte argumentCount)
    {
        var request = Request(transactionId, commandClass, command, argumentCount);

        // Razer Synapse talks to the same device, so a reply may answer its request instead; retry then.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!HidDevice.SetFeature(handle, request)) return null;
            await Task.Delay(50); // a receiver relays the request over the air

            var reply = new byte[ReportLength];
            if (!HidDevice.GetFeature(handle, reply)) return null;
            if (ParseReply(reply, request) is (true, var arguments)) return arguments;
        }
        return null;
    }

    internal static byte[] Request(byte transactionId, byte commandClass, byte command, byte argumentCount)
    {
        var request = new byte[ReportLength];
        request[2] = transactionId;
        request[6] = argumentCount;
        request[7] = commandClass;
        request[8] = command;
        request[89] = request[3..89].Aggregate((checksum, b) => (byte)(checksum ^ b));
        return request;
    }

    /// <returns>
    /// Whether the reply answers <paramref name="request"/>, and if so its arguments,
    /// which are null when the device didn't respond (asleep, off or the wrong transaction id).
    /// </returns>
    internal static (bool AnswersRequest, byte[]? Arguments) ParseReply(byte[] reply, byte[] request)
    {
        var answersRequest = reply[2] == request[2] && reply[7] == request[7] && reply[8] == request[8];
        return (answersRequest, answersRequest && reply[1] == StatusSuccess ? reply[9..89] : null);
    }

    internal static string? Serial(byte[]? arguments) =>
        arguments is null ? null : Encoding.ASCII.GetString(arguments, 0, SerialLength).TrimEnd('\0', ' ') is { Length: > 0 } serial ? serial : null;
}
