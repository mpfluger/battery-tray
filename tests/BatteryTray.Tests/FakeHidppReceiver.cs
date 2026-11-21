using System.Text;
using System.Threading.Channels;

namespace BatteryTray.Tests;

/// <summary>A HID++ 2.0 device as a fake receiver presents it.</summary>
/// <param name="BatteryFeature">0x1004 (Unified Battery), 0x1000 (Battery Status) or 0x1001 (voltage only).</param>
/// <param name="ChargingStatus">The battery feature's raw charging status code.</param>
sealed record FakeHidppDevice(string Name, byte Percent, ushort BatteryFeature = 0x1004, byte ChargingStatus = 0)
{
    // A device's feature table: position = index. Root is always 0, the rest in a device-specific order.
    ushort[] Features => [0x0000, 0x0001, 0x0003, 0x0005, 0x1B04, BatteryFeature];

    /// <returns>The reply parameters, or null for an error reply.</returns>
    public byte[]? Handle(byte featureIndex, int function, byte[] parameters) =>
        (featureIndex < Features.Length ? Features[featureIndex] : -1, function) switch
        {
            (0x0000, 0) => [(byte)Math.Max(0, Array.IndexOf(Features, (ushort)(parameters[0] << 8 | parameters[1])))],
            (0x0000, 1) => [4, 2, parameters[2]], // ping: protocol 4.2, echoing the ping byte
            (0x0005, 0) => [(byte)Name.Length],
            (0x0005, 1) => Encoding.ASCII.GetBytes(Name.PadRight(parameters[0] + 16).Substring(parameters[0], 16)),
            (0x1004, 1) => [Percent, 0x08, ChargingStatus, 0],
            (0x1000, 0) => [Percent, 0, ChargingStatus],
            _ => null,
        };
}

/// <summary>
/// The long-report collection of a receiver: answers requests like the devices in its slots would.
/// </summary>
sealed class FakeHidppReceiver(Dictionary<byte, FakeHidppDevice> devices) : Stream
{
    readonly Channel<byte[]> _replies = Channel.CreateUnbounded<byte[]>();

    /// <summary>Precede each reply with one to another app's identical request, as G HUB would cause.</summary>
    public bool OtherAppActive { get; init; }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var request = buffer.ToArray();
        var (slot, feature, functionByte) = (request[1], request[2], request[3]);
        // An empty slot gets no long reply: the receiver's error comes on its short-report collection.
        if (devices.TryGetValue(slot, out var device))
        {
            if (OtherAppActive) Reply(slot, feature, (byte)(functionByte & 0xF0 | 0x01), [0xEE, 0xEE, 0xEE]);

            var parameters = device.Handle(feature, functionByte >> 4, request[4..]);
            if (parameters is null) Reply(slot, 0xFF, feature, [functionByte, 0x02]); // error: invalid argument
            else Reply(slot, feature, functionByte, parameters);
        }
        return ValueTask.CompletedTask;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var reply = await _replies.Reader.ReadAsync(cancellationToken);
        reply.CopyTo(buffer);
        return reply.Length;
    }

    void Reply(byte slot, byte feature, byte functionByte, byte[] parameters)
    {
        var reply = new byte[20];
        (reply[0], reply[1], reply[2], reply[3]) = ((byte)0x11, slot, feature, functionByte);
        parameters.CopyTo(reply, 4);
        _replies.Writer.TryWrite(reply);
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
