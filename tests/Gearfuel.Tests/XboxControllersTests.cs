using Gearfuel.Devices;

namespace Gearfuel.Tests;

public class XboxControllersTests
{
    [Theory]
    [InlineData(0, 5)] // empty
    [InlineData(1, 20)] // low: red, like any device at 20%
    [InlineData(2, 60)]
    [InlineData(3, 100)]
    public void BatteryLevel_MapsToAPercentage(byte level, int percent) =>
        Assert.Equal(percent, XboxControllers.Reading(0, type: 3, level)?.Percent);

    [Theory]
    [InlineData(0)] // disconnected
    [InlineData(1)] // wired, e.g. DS4Windows' virtual controller
    [InlineData(0xFF)] // unknown
    public void ControllerWithoutABatteryLevel_IsSkipped(byte type) =>
        Assert.Null(XboxControllers.Reading(0, type, level: 3));

    [Fact]
    public void ControllersAreNamedBySlot() =>
        Assert.Equal(["Xbox controller", "Xbox controller 2"], new[] { 0, 1 }.Select(slot => XboxControllers.Reading(slot, 2, 3)!.Name));
}
