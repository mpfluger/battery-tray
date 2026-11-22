using Gearfuel.Devices;

namespace Gearfuel;

/// <param name="Id">The reading's, so identical devices stay apart.</param>
/// <param name="TimeLeft">Null while unknown.</param>
/// <param name="SinceSeen">Time awake since the device last answered; the other values are from then.</param>
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
    // Devices that stop answering may just be asleep: they show as unreachable for a while, then go away.
    // Devices whose connection is gone go away right away.
    static readonly TimeSpan ForgetAfter = TimeSpan.FromHours(1);
    // Updates come at least every minute while the PC is awake, so a longer gap is the PC asleep, which doesn't count:
    // a device can't answer then.
    static readonly TimeSpan Asleep = TimeSpan.FromMinutes(3);
    // Right after the PC wakes, devices take a few seconds to come back, so one missing then isn't gone yet.
    static readonly TimeSpan WakingUp = TimeSpan.FromSeconds(30);

    readonly Dictionary<string, (BatteryReading Reading, TimeSpan Unanswered)> _devices = [];
    DateTimeOffset? _lastUpdate;
    DateTimeOffset _awakeSince;

    /// <param name="connections">The HID collections present now; null when unknown.</param>
    public IReadOnlyList<DeviceStatus> Update(IReadOnlyList<BatteryReading> readings, DateTimeOffset now, IReadOnlySet<string>? connections = null)
    {
        var elapsed = now - (_lastUpdate ?? now);
        _lastUpdate = now;
        if (elapsed > Asleep || elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
            _awakeSince = now;
        }
        foreach (var id in _devices.Keys.ToList())
            _devices[id] = (_devices[id].Reading, _devices[id].Unanswered + elapsed);

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
            _devices[reading.Id] = (reading, TimeSpan.Zero);
            forecast.Observe(reading.Id, reading.Level, reading.Charging, now);
        }
        foreach (var id in _devices.Where(d => d.Value.Unanswered > ForgetAfter || IsGone(d.Value.Reading)).Select(d => d.Key).ToList())
            _devices.Remove(id);

        bool IsGone(BatteryReading reading) =>
            now - _awakeSince >= WakingUp && reading.Connection is { } connection && connections?.Contains(connection) == false;

        return _devices.Values
            .Select(d => new DeviceStatus(
                d.Reading.Id, d.Reading.Name, d.Reading.Percent, d.Reading.Charging,
                forecast.Estimate(d.Reading.Id, d.Reading.Level, d.Reading.Charging),
                d.Unanswered))
            .OrderBy(d => d.Charging ? 2 : d.Low ? 0 : 1) // devices that need charging, then the rest, then charging ones
            .ThenBy(d => d.Percent)
            .ToList();
    }
}
