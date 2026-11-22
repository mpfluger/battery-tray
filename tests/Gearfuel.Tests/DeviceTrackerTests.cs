using Gearfuel.Devices;

namespace Gearfuel.Tests;

public class DeviceTrackerTests
{
    static readonly DateTimeOffset Start = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    readonly DeviceTracker _tracker = new(new Forecast(path: null));

    [Fact]
    public void DevicesThatNeedChargingComeFirst_ChargingOnesLast()
    {
        var devices = _tracker.Update([Reading("mouse", 88), Reading("keys", 60, charging: true), Reading("pad", 15), Reading("headset", 50)], Start);

        Assert.Equal(["pad", "headset", "mouse", "keys"], devices.Select(d => d.Name));
    }

    [Fact]
    public void Low_MeansWontLastADay_OnceThePaceIsKnown()
    {
        // Two hours of use, then nothing until the next day.
        for (var minute = 0; minute <= 120; minute++)
            _tracker.Update([Reading("pad", 90 - 50 * minute / 120), Reading("mouse", 30 - 12 * minute / 120)], Start.AddMinutes(minute));
        var devices = _tracker.Update([Reading("pad", 40), Reading("mouse", 18)], Start.AddDays(1));

        var pad = devices.Single(d => d.Name == "pad"); // 50 points a day: 40% lasts about 19 hours
        var mouse = devices.Single(d => d.Name == "mouse"); // 12 points a day: 18% lasts a day and a half
        Assert.True(pad.Low);
        Assert.False(mouse.Low);
    }

    [Fact]
    public void Low_FallsBackToTwentyPercent_UntilThePaceIsKnown()
    {
        var devices = _tracker.Update([Reading("pad", 20), Reading("mouse", 21)], Start);

        Assert.Equal([true, false], devices.Select(d => d.Low));
    }

    [Fact]
    public void SilentDevice_KeepsItsLastLevel_TurnsUnreachable_ThenIsForgotten()
    {
        _tracker.Update([Reading("mouse", 88)], Start);

        Assert.Equal((88, false), Single(Silent(Start, minutes: 10)));
        Assert.Equal((88, true), Single(Silent(Start.AddMinutes(10), minutes: 10)));
        Assert.Empty(Silent(Start.AddMinutes(20), minutes: 41));
    }

    [Fact]
    public void Devices_AreKept_WhileThePCSleeps_UntilTheyAnswerAgain()
    {
        _tracker.Update([new("mouse", "DeathAdder", 80, false) { Connection = "receiver" }], Start, new HashSet<string> { "receiver" });

        // Overnight asleep, then woken: the receiver isn't back yet.
        var devices = _tracker.Update([], Start.AddHours(8), new HashSet<string>());

        Assert.Equal([("DeathAdder", 80, false)], devices.Select(d => (d.Name, d.Percent, d.Unreachable)));
        Assert.Equal(TimeSpan.Zero, devices.Single().SinceSeen);
    }

    [Fact]
    public void DeviceStillMissing_ALittleAfterWaking_IsGone()
    {
        _tracker.Update([new("pad", "DualShock 4", 80, false) { Connection = "pad" }], Start, new HashSet<string> { "pad" });
        _tracker.Update([], Start.AddHours(8), new HashSet<string>());

        Assert.Empty(_tracker.Update([], Start.AddHours(8).AddMinutes(1), new HashSet<string>()));
    }

    [Fact]
    public void DeviceWhoseConnectionIsGone_GoesAwayRightAway()
    {
        _tracker.Update([new("pad", "DualShock 4", 80, false) { Connection = "pad" }], Start, new HashSet<string> { "pad" });

        Assert.Empty(_tracker.Update([], Start.AddMinutes(1), new HashSet<string>()));
    }

    [Fact]
    public void AsleepDevice_BehindAReceiverThatIsStillThere_Stays()
    {
        _tracker.Update([new("mouse", "DeathAdder", 80, false) { Connection = "receiver" }], Start, new HashSet<string> { "receiver" });

        Assert.Single(_tracker.Update([], Start.AddMinutes(1), new HashSet<string> { "receiver" }));
    }

    [Fact]
    public void DeviceSwitchingConnection_IsListedOnce()
    {
        _tracker.Update([new("receiver#1", "DeathAdder", 89, false)], Start);
        // Plugged in to charge: the cable connection answers, the receiver goes quiet.
        var devices = _tracker.Update([new("cable", "DeathAdder", 89, true)], Start.AddMinutes(1));

        Assert.Equal([("DeathAdder", true)], devices.Select(d => (d.Name, d.Charging)));
    }

    [Fact]
    public void IdenticalDevices_AnsweringTogether_AreBothListed()
    {
        _tracker.Update([new("pad1", "DualShock 4", 80, false)], Start);
        var devices = _tracker.Update([new("pad1", "DualShock 4", 80, false), new("pad2", "DualShock 4", 60, false)], Start.AddMinutes(1));

        Assert.Equal(2, devices.Count);
    }

    // Updates every minute, as while the PC is awake, with no device answering.
    IReadOnlyList<DeviceStatus> Silent(DateTimeOffset from, int minutes)
    {
        IReadOnlyList<DeviceStatus> devices = [];
        for (var minute = 1; minute <= minutes; minute++)
            devices = _tracker.Update([], from.AddMinutes(minute));
        return devices;
    }

    static (int, bool) Single(IReadOnlyList<DeviceStatus> devices) => (devices.Single().Percent, devices.Single().Unreachable);

    static BatteryReading Reading(string name, int percent, bool charging = false) => new(name, name, percent, charging);
}
