using System.Drawing;

namespace Gearfuel.Tests;

public class ThemeTests
{
    // Accent colors from Windows' own color picker.
    [Theory]
    [InlineData(0x0078D7)] // default blue
    [InlineData(0x8764B8)] // violet
    [InlineData(0xFFB900)] // gold
    [InlineData(0x00B7C3)] // cyan
    [InlineData(0xE3008C)] // magenta
    public void Accent_IsUsed_WhenItStandsApartFromTheStateColors(int rgb) =>
        Assert.True(Theme.StandsApart(Color.FromArgb(rgb)));

    [Theory]
    [InlineData(0xE81123)] // red
    [InlineData(0xF7630C)] // orange, which reads as red at tray size
    [InlineData(0x107C10)] // green
    [InlineData(0x00B294)] // seafoam
    [InlineData(0x69797E)] // gray
    public void Accent_IsSkipped_WhenItCouldPassForLowChargingOrUnreachable(int rgb) =>
        Assert.False(Theme.StandsApart(Color.FromArgb(rgb)));
}
