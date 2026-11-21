namespace BatteryTray.Devices;

/// <param name="Id">Stable for as long as the device stays connected.</param>
/// <param name="Percent">Null when the device is connected but unreachable, e.g. asleep.</param>
record BatteryReading(string Id, string Name, int? Percent, bool Charging);

/// <summary>Finds the connected devices of one kind and reads their batteries.</summary>
interface IBatterySource
{
    Task<IReadOnlyList<BatteryReading>> ReadAsync();
}
