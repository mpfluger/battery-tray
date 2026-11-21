using Gearfuel.Devices;
using Battery = Gearfuel.Devices.LogitechDevices.Battery;

namespace Gearfuel.Tests;

public class LogitechDevicesTests
{
    [Fact]
    public async Task FindsDevicesInAnyReceiverSlot_WithTheirFullNames()
    {
        var receiver = new FakeHidppReceiver(new()
        {
            [2] = new("MX Master 3S for Business", 64), // name longer than one 16-byte reply
            [5] = new("PRO X TKL", 82, ChargingStatus: 1),
        });

        var batteries = await LogitechDevices.ReadAsync(receiver, LogitechDevices.ReceiverSlots);

        Assert.Equal([new Battery(2, "MX Master 3S for Business", 64, false), new Battery(5, "PRO X TKL", 82, true)], batteries);
    }

    [Fact]
    public async Task DeviceWithOlderBatteryFeature_IsRead()
    {
        var receiver = new FakeHidppReceiver(new() { [1] = new("M720", 50, BatteryFeature: 0x1000, ChargingStatus: 0) });

        Assert.Equal([new Battery(1, "M720", 50, false)], await LogitechDevices.ReadAsync(receiver, LogitechDevices.ReceiverSlots));
    }

    [Fact]
    public async Task DeviceThatOnlyReportsVoltage_IsSkipped()
    {
        var receiver = new FakeHidppReceiver(new() { [1] = new("G703", 70, BatteryFeature: 0x1001) });

        Assert.Empty(await LogitechDevices.ReadAsync(receiver, LogitechDevices.ReceiverSlots));
    }

    [Fact]
    public async Task RepliesToAnotherAppsRequests_AreIgnored()
    {
        var receiver = new FakeHidppReceiver(new() { [1] = new("PRO X TKL", 82) }) { OtherAppActive = true };

        Assert.Equal([new Battery(1, "PRO X TKL", 82, false)], await LogitechDevices.ReadAsync(receiver, LogitechDevices.ReceiverSlots));
    }

    [Fact]
    public async Task DirectlyConnectedDevice_IsReadAtItsOwnIndex()
    {
        var device = new FakeHidppReceiver(new() { [LogitechDevices.DirectDevice] = new("G502 X PLUS", 31) });

        Assert.Equal([new Battery(LogitechDevices.DirectDevice, "G502 X PLUS", 31, false)], await LogitechDevices.ReadAsync(device, [LogitechDevices.DirectDevice]));
    }
}
