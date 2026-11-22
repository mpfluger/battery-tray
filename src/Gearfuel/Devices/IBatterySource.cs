namespace Gearfuel.Devices;

/// <param name="Id">Stable for as long as the device stays connected.</param>
record BatteryReading(string Id, string Name, int Percent, bool Charging);

/// <summary>Finds the devices of one kind and reads the batteries of those that answer.</summary>
interface IBatterySource
{
    /// <param name="hidDevices">The HID collections present now, listed once per refresh for all sources.</param>
    Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices);
}
