namespace Gearfuel.Devices;

/// <param name="Id">Stable for as long as the device stays connected.</param>
record BatteryReading(string Id, string Name, int Percent, bool Charging)
{
    /// <summary>The level as precisely as the device reports it, which helps the forecast notice small drops sooner.</summary>
    public double Level { get; init; } = Percent;

    /// <summary>
    /// The HID collection the reading came through, if any. Once it's gone, so is the device (a controller switched
    /// off, a receiver unplugged), unlike a device that's just asleep behind a receiver that's still there.
    /// </summary>
    public string? Connection { get; init; }
}

/// <summary>Finds the devices of one kind and reads the batteries of those that answer.</summary>
interface IBatterySource
{
    /// <param name="hidDevices">The HID collections present now, listed once per refresh for all sources.</param>
    Task<IReadOnlyList<BatteryReading>> ReadAsync(IReadOnlyList<HidDevice> hidDevices);
}
