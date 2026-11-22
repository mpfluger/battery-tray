using System.Runtime.InteropServices;

namespace Gearfuel.Devices;

/// <summary>
/// Xbox controllers, through Windows' XInput, which reads them over the Xbox Wireless Adapter as well as
/// Bluetooth. XInput reports the level in four steps only.
/// </summary>
sealed partial class XboxControllers : IBatterySource
{
    const int Slots = 4; // XInput's player slots
    const byte Alkaline = 2, NickelMetalHydride = 3; // battery types; wired, disconnected and unknown have no level

    public Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices)
    {
        // Windows also reports the exact level of an Xbox controller connected over Bluetooth, which the Bluetooth
        // source reads; which slot such a controller has is unknown, so XInput steps aside then.
        if (hidDevices.Any(IsBluetoothXboxController)) return Task.FromResult<IReadOnlyList<BatteryReading>>([]);

        var readings = new List<BatteryReading>();
        for (var slot = 0; slot < Slots; slot++)
        {
            if (XInputGetBatteryInformation(slot, devType: 0, out var battery) == 0 && Reading(slot, battery.Type, battery.Level) is { } reading)
                readings.Add(reading);
        }
        return Task.FromResult<IReadOnlyList<BatteryReading>>(readings);
    }

    /// <summary>Maps XInput's empty, low, medium and full to levels that color the rings the same way.</summary>
    internal static BatteryReading? Reading(int slot, byte type, byte level) =>
        type is Alkaline or NickelMetalHydride && level <= 3
            ? new($"xinput:{slot}", slot == 0 ? "Xbox controller" : $"Xbox controller {slot + 1}", level switch { 0 => 5, 1 => 20, 2 => 60, _ => 100 }, Charging: false)
            : null;

    static bool IsBluetoothXboxController(HidDevice hid) =>
        hid is { VendorId: 0x045E, UsagePage: 0x01, Usage: 0x05 }
        && (hid.Path.Contains("{00001124-", StringComparison.OrdinalIgnoreCase) || hid.Path.Contains("{00001812-", StringComparison.OrdinalIgnoreCase));

    [StructLayout(LayoutKind.Sequential)]
    struct BatteryInformation
    {
        public byte Type, Level;
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial int XInputGetBatteryInformation(int userIndex, byte devType, out BatteryInformation battery);
}
