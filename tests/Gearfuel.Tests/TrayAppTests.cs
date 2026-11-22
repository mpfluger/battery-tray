namespace Gearfuel.Tests;

public class TrayAppTests
{
    [Fact]
    public void Summary_NamesEachDevice_ForScreenReaders()
    {
        IReadOnlyList<DeviceStatus> devices =
        [
            new("pad", "DualShock 4", 18, false, null, TimeSpan.Zero),
            new("mouse", "DeathAdder", 85, false, null, TimeSpan.Zero),
            new("headset", "Arctis Nova 7", 60, true, null, TimeSpan.Zero),
        ];

        Assert.Equal("Gearfuel: DualShock 4 18%, low; DeathAdder 85%; Arctis Nova 7 60%, charging", TrayApp.Summary(devices));
    }

    [Fact]
    public void Summary_WithoutDevices_SaysSo() =>
        Assert.Equal("Gearfuel: no devices with a battery found", TrayApp.Summary([]));
}
