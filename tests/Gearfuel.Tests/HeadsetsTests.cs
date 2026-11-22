using Gearfuel.Devices;
using Battery = Gearfuel.Devices.Headsets.Battery;

namespace Gearfuel.Tests;

// Replies laid out as HeadsetControl documents them; none of these headsets has been tried yet.
public class HeadsetsTests
{
    [Fact]
    public async Task CloudII_ReadsLevelAndCharging_FromTwoRequests()
    {
        var ask = Answering(
            [0x06, 0xFF, 0xBB, 0x02, 0, 0x0F, 0xA0, 64],
            [0x06, 0xFF, 0xBB, 0x03, 1]);

        Assert.Equal(new Battery(64, Charging: true), await Headsets.CloudIIAsync(ask));
    }

    [Fact]
    public async Task ArctisNova7_InSteps_ScalesToPercent() =>
        Assert.Equal(new Battery(75, Charging: false), await Headsets.ArctisNova7Async(Answering([0x00, 0xB0, 0, 3, 3]), inSteps: true));

    [Fact]
    public async Task ArctisNova7_Off_IsNotListed() =>
        Assert.Null(await Headsets.ArctisNova7Async(Answering([0x00, 0xB0, 0, 80, 0]), inSteps: false));

    [Fact]
    public async Task Arctis7_LevelZero_IsTakenAsOff() =>
        Assert.Null(await Headsets.Arctis7Async(Answering([0x06, 0x18, 0])));

    [Fact]
    public async Task CorsairVoid_IgnoresTheMicrophoneBit_AndReadsCharging() =>
        Assert.Equal(new Battery(55, Charging: true), await Headsets.CorsairVoidAsync(Answering([0x64, 0, 0x80 | 55, 177, 5])));

    [Fact]
    public async Task UnrelatedReports_AreSkipped()
    {
        Headsets.Ask ask = (request, answers) => Task.FromResult(new byte[][] { [0x01, 0x02], [0x64, 0, 40, 177, 1] }.FirstOrDefault(answers));

        Assert.Equal(new Battery(40, Charging: false), await Headsets.CorsairVoidAsync(ask));
    }

    // Answers each request with the first of the given reports that matches it, padded like a real input report.
    static Headsets.Ask Answering(params byte[][] reports) => (request, answers) =>
        Task.FromResult(reports.Select(r => r.Concat(new byte[64 - r.Length]).ToArray()).FirstOrDefault(answers));
}
