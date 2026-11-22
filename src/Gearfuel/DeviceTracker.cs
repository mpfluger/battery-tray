using Gearfuel.Devices;

namespace Gearfuel;

/// <param name="Id">The reading's, so identical devices stay apart.</param>
/// <param name="TimeLeft">Null while unknown.</param>
/// <param name="SinceSeen">Since the device last answered; the other values are from then.</param>
sealed record DeviceStatus(string Id, string Name, int Percent, bool Charging, TimeLeft? TimeLeft, TimeSpan SinceSeen)
{
    const int LowPercent = 20;
    // Devices that stop answering are often just asleep, so their last level stays as is for a while.
    static readonly TimeSpan UnreachableAfter = TimeSpan.FromMinutes(15);

    public bool Unreachable => SinceSeen > UnreachableAfter;

    /// <summary>Won't last a day at its usual pace, or, until that's known, nearly empty.</summary>
    public bool Low => !Charging && (TimeLeft is { OfUse: false } left ? left.Duration < TimeSpan.FromDays(1) : Percent <= LowPercent);

    public Tone Tone => Charging ? Tone.Charging : Low ? Tone.Low : Unreachable ? Tone.Unreachable : Tone.Normal;
}

/// <summary>Keeps the last known state of recently seen devices, most urgent first.</summary>
sealed class DeviceTracker(Forecast forecast)
{
    // Devices that stop answering show as unreachable for a while, then go away.
    static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(1);

    readonly Dictionary<string, (BatteryReading Reading, DateTimeOffset LastSeen)> _devices = [];

    public IReadOnlyList<DeviceStatus> Update(IReadOnlyList<BatteryReading> readings, DateTimeOffset now)
    {
        var answered = readings.Select(r => r.Id).ToHashSet();
        foreach (var reading in readings)
        {
            // A device that switches connection, e.g. plugged in to charge, can show up under a new id
            // while its old one goes quiet: the quiet one with the same name is the same device.
            if (!_devices.ContainsKey(reading.Id))
            {
                foreach (var moved in _devices.Where(d => d.Value.Reading.Name == reading.Name && !answered.Contains(d.Key)).Select(d => d.Key).ToList())
                    _devices.Remove(moved);
            }
            _devices[reading.Id] = (reading, now);
            forecast.Observe(reading.Id, reading.Level, reading.Charging, now);
        }
        foreach (var id in _devices.Where(d => now - d.Value.LastSeen > ForgetAfter).Select(d => d.Key).ToList())
            _devices.Remove(id);

        return _devices.Values
            .Select(d => new DeviceStatus(
                d.Reading.Id, d.Reading.Name, d.Reading.Percent, d.Reading.Charging,
                forecast.Estimate(d.Reading.Id, d.Reading.Level, d.Reading.Charging),
                now - d.LastSeen))
            .OrderBy(d => d.Charging ? 2 : d.Low ? 0 : 1) // devices that need charging, then the rest, then charging ones
            .ThenBy(d => d.Percent)
            .ToList();
    }
}
