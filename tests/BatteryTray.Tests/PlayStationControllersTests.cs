using BatteryTray.Devices;

namespace BatteryTray.Tests;

public class PlayStationControllersTests
{
    [Fact]
    public void DualShock4OverBluetooth_ReadsCapturedReport()
    {
        // Status byte captured from a DualShock 4 on battery: level 8, no cable.
        var report = Report(0x11, statusOffset: 32, status: 0x08);

        Assert.Equal((85, false), PlayStationControllers.ParseBattery(report, bluetooth: true, isDualSense: false));
    }

    [Theory]
    [InlineData(0x15, 55, true)] // cable, level 5: charging
    [InlineData(0x1A, 100, false)] // cable, level 10: full
    [InlineData(0x1B, 100, false)] // cable, level 11: full
    [InlineData(0x00, 5, false)] // battery, empty
    public void DualShock4OverUsb_DecodesLevelAndCable(byte status, int percent, bool charging)
    {
        var report = Report(0x01, statusOffset: 30, status);

        Assert.Equal((percent, charging), PlayStationControllers.ParseBattery(report, bluetooth: false, isDualSense: false));
    }

    [Theory]
    [InlineData(0x31, true, 54)]
    [InlineData(0x01, false, 53)]
    public void DualSense_DecodesLevelAndChargingState(byte reportId, bool bluetooth, int statusOffset)
    {
        Assert.Equal((65, false), PlayStationControllers.ParseBattery(Report(reportId, statusOffset, 0x06), bluetooth, isDualSense: true));
        Assert.Equal((35, true), PlayStationControllers.ParseBattery(Report(reportId, statusOffset, 0x13), bluetooth, isDualSense: true));
        Assert.Equal((100, false), PlayStationControllers.ParseBattery(Report(reportId, statusOffset, 0x2A), bluetooth, isDualSense: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BluetoothReportsWithoutBattery_AreSkipped(bool isDualSense)
    {
        // Until the calibration report is read, Bluetooth controllers send short 0x01 reports.
        Assert.Null(PlayStationControllers.ParseBattery(Report(0x01, statusOffset: 9, status: 0x08), bluetooth: true, isDualSense));
    }

    static byte[] Report(byte id, int statusOffset, byte status)
    {
        var report = new byte[78];
        report[0] = id;
        report[statusOffset] = status;
        return report;
    }
}
