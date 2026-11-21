using Gearfuel.Devices;

namespace Gearfuel;

/// <param name="TimeLeft">Until empty, or until full while charging; null while unknown.</param>
/// <param name="Unreachable">The device hasn't answered for a while; the other values are its last known ones.</param>
sealed record DeviceStatus(string Name, int Percent, bool Charging, TimeSpan? TimeLeft, bool Unreachable)
{
    const int LowPercent = 20;

    /// <summary>Won't last a day at its usual pace, or, until that's known, nearly empty.</summary>
    public bool Low => !Charging && (TimeLeft is { } left ? left < TimeSpan.FromDays(1) : Percent <= LowPercent);

    public Tone Tone => Charging ? Tone.Charging : Low ? Tone.Low : Unreachable ? Tone.Unreachable : Tone.Normal;
}

/// <summary>Keeps the last known state of recently seen devices, most urgent first.</summary>
sealed class DeviceTracker(Forecast forecast)
{
    // Devices that stop answering are often just asleep, so their last level stays as is for a while,
    // then shows as unreachable, then goes away.
    static readonly TimeSpan UnreachableAfter = TimeSpan.FromMinutes(15);
    static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(1);

    readonly Dictionary<string, (BatteryReading Reading, DateTimeOffset LastSeen)> _devices = [];

    public IReadOnlyList<DeviceStatus> Update(IReadOnlyList<BatteryReading> readings, DateTimeOffset now)
    {
        foreach (var reading in readings)
        {
            _devices[reading.Id] = (reading, now);
            forecast.Observe(reading.Id, reading.Percent, reading.Charging, now);
        }
        foreach (var id in _devices.Where(d => now - d.Value.LastSeen > ForgetAfter).Select(d => d.Key).ToList())
            _devices.Remove(id);

        return _devices.Values
            .Select(d => new DeviceStatus(
                d.Reading.Name, d.Reading.Percent, d.Reading.Charging,
                forecast.Estimate(d.Reading.Id, d.Reading.Percent, d.Reading.Charging, now),
                Unreachable: now - d.LastSeen > UnreachableAfter))
            .OrderBy(d => d.Charging ? 2 : d.Low ? 0 : 1) // devices that need charging, then the rest, then charging ones
            .ThenBy(d => d.Percent)
            .ToList();
    }
}
