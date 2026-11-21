namespace BatteryTray.Devices;

/// <param name="Id">Stable for as long as the device stays connected.</param>
record BatteryReading(string Id, string Name, int Percent, bool Charging);

/// <summary>Finds the devices of one kind and reads the batteries of those that answer.</summary>
interface IBatterySource
{
    Task<IReadOnlyList<BatteryReading>> ReadAsync();
}
