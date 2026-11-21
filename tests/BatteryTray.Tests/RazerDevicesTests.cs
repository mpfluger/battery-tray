using BatteryTray.Devices;

namespace BatteryTray.Tests;

/// <remarks>Byte sequences captured from a DeathAdder V3 HyperSpeed receiver.</remarks>
public class RazerDevicesTests
{
    readonly byte[] _request = RazerDevices.Request(transactionId: 0x1F, RazerDevices.GetLevel);

    [Fact]
    public void Request_MatchesWhatTheDeviceAccepts()
    {
        var expected = new byte[91];
        expected[2] = 0x1F; // transaction id
        expected[6] = 0x02; // argument count
        expected[7] = 0x07; // power class
        expected[8] = 0x80; // get battery level
        expected[89] = 0x85; // XOR checksum of bytes 3..88

        Assert.Equal(expected, _request);
    }

    [Fact]
    public void SuccessfulReply_YieldsTheLevel() =>
        Assert.Equal((true, (byte?)0xE1), RazerDevices.ParseReply(Reply("00 02 1F 00 00 00 02 07 80 00 E1"), _request));

    [Fact]
    public void ReplyFromAsleepDevice_AnswersButHasNoValue() =>
        Assert.Equal((true, (byte?)null), RazerDevices.ParseReply(Reply("00 04 1F 00 00 00 02 07 80 00 00"), _request));

    [Fact]
    public void ReplyToRazerSynapsesRequest_IsNotOurs() =>
        Assert.False(RazerDevices.ParseReply(Reply("00 05 1D 00 00 00 08 0F 03 00 00"), _request).AnswersRequest);

    static byte[] Reply(string hex)
    {
        var reply = new byte[91];
        Convert.FromHexString(hex.Replace(" ", "")).CopyTo(reply, 0); // as captured, starting with the report id
        return reply;
    }
}
