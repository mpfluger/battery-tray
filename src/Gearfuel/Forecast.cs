using System.Text.Json;

namespace Gearfuel;

/// <param name="Duration">Until empty, or until full while charging.</param>
/// <param name="OfUse">Counts hours of use rather than calendar time, as how much the device is used a day isn't known yet.</param>
/// <param name="AtLeast">A floor: the device has barely drained yet, so it lasts at least this long.</param>
readonly record struct TimeLeft(TimeSpan Duration, bool OfUse, bool AtLeast = false);

/// <summary>
/// Estimates how long each device's battery lasts, from two things it learns per device: how fast the level drops
/// while the device is in use, and how many points it loses a day. A device counts as in use while it answers: idle
/// mice and keyboards fall asleep, and controllers are switched off. Recent days count most; charging resets nothing.
/// </summary>
sealed class Forecast(string? path)
{
    public static string DefaultPath => Path.Combine(AppInfo.DataDirectory, "forecast.json");

    // Gearfuel reads every minute, so readings further apart mean the device slept (or the PC did) in between.
    static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(3);
    // What was learned weighs half as much four days later: the forecast follows a change in how a device is used
    // within a few days, yet averages over about two weeks.
    static readonly TimeSpan HalfLife = TimeSpan.FromDays(4);
    const int DaysKept = 14;
    // Readings wobble by a point or two; a bigger rise without charging means the device was charged elsewhere.
    const int Jitter = 2;
    // Enough change for a first, rough pace; estimates sharpen as more points are lost.
    const double MinChange = 2;
    static readonly TimeSpan MinUse = TimeSpan.FromHours(1), MinCharging = TimeSpan.FromMinutes(10);
    static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(10);

    /// <summary>What's known about one device.</summary>
    internal sealed class History
    {
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastSeen { get; set; }
        public double Percent { get; set; }
        public bool Charging { get; set; }

        // Points lost and hours in use (not charging), and points gained and hours charging. These sums fade over
        // time; their ratios are the paces.
        public double Drained { get; set; }
        public double UseHours { get; set; }
        public double Charged { get; set; }
        public double ChargeHours { get; set; }

        /// <summary>Points lost in each of the last two weeks' days, counted in 24 hours from <see cref="FirstSeen"/>.</summary>
        public Dictionary<int, double> DailyDrain { get; set; } = [];

        public int Day(DateTimeOffset time) => (int)((time - FirstSeen) / TimeSpan.FromDays(1));
    }

    readonly Dictionary<string, History> _devices = Load(path);
    DateTimeOffset _lastSave;

    public void Observe(string id, double percent, bool charging, DateTimeOffset now)
    {
        if (!_devices.TryGetValue(id, out var device))
        {
            _devices[id] = new History { FirstSeen = now, LastSeen = now, Percent = percent, Charging = charging };
            Save(now);
            return;
        }
        var elapsed = now - device.LastSeen;
        if (elapsed <= TimeSpan.Zero) return;

        var keep = Math.Pow(0.5, elapsed / HalfLife);
        device.Drained *= keep;
        device.UseHours *= keep;
        device.Charged *= keep;
        device.ChargeHours *= keep;

        // Only changes between consecutive readings count: across a gap, the device may have been used elsewhere.
        var change = percent - device.Percent;
        if (elapsed <= MaxGap && charging == device.Charging)
        {
            if (charging)
            {
                device.Charged += change;
                device.ChargeHours += elapsed.TotalHours;
            }
            else if (change <= Jitter)
            {
                // A small rise is a wobble, and cancels the drop before it.
                device.Drained -= change;
                device.UseHours += elapsed.TotalHours;
                var today = device.Day(now);
                device.DailyDrain[today] = device.DailyDrain.GetValueOrDefault(today) - change;
                device.DailyDrain.Keys.Where(day => day <= today - DaysKept).ToList().ForEach(day => device.DailyDrain.Remove(day));
            }
        }
        device.LastSeen = now;
        device.Percent = percent;
        device.Charging = charging;
        if (now - _lastSave >= SaveEvery) Save(now);
    }

    /// <returns>Null until there's enough history.</returns>
    public TimeLeft? Estimate(string id, double percent, bool charging)
    {
        if (!_devices.TryGetValue(id, out var device)) return null;

        if (charging)
        {
            return Reached(device.Charged, MinChange) && Reached(device.ChargeHours, MinCharging.TotalHours)
                ? new(TimeSpan.FromHours((100 - percent) * device.ChargeHours / device.Charged), OfUse: false)
                : null;
        }
        if (!Reached(device.UseHours, MinUse.TotalHours)) return null;
        if (!Reached(device.Drained, MinChange))
        {
            // Too little lost yet for a pace. But a whole point would have shown, so it loses at most about a point
            // in this many hours of use, and the level lasts at least this many times as long.
            return new(TimeSpan.FromHours(percent * device.UseHours / Math.Max(device.Drained, 1)), OfUse: true, AtLeast: true);
        }

        // Once there's a whole day, the points lost a day cover both how fast it drains and how much it's used.
        // Only whole days count, so a day's estimate doesn't swing with when in the day the device is used.
        var today = device.Day(device.LastSeen);
        double drained = 0, weights = 0;
        for (var day = Math.Max(0, today - DaysKept); day < today; day++)
        {
            var weight = Math.Pow(0.5, (today - day) / HalfLife.TotalDays);
            drained += weight * device.DailyDrain.GetValueOrDefault(day);
            weights += weight;
        }
        return drained > 0
            ? new(TimeSpan.FromDays(percent * weights / drained), OfUse: false)
            : new(TimeSpan.FromHours(percent * device.UseHours / device.Drained), OfUse: true);
    }

    // The sums start fading as soon as they're collected, so an hour of use adds up to a little under an hour.
    static bool Reached(double sum, double threshold) => sum >= threshold * 0.95;

    static Dictionary<string, History> Load(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
            {
                // Files from before version 1.6 hold a different record, which reads as never seen; those start over.
                var devices = JsonSerializer.Deserialize<Dictionary<string, History>>(File.ReadAllText(path)) ?? [];
                return devices.Where(d => d.Value.LastSeen != default).ToDictionary();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Diagnostics.Report($"Starting without battery history: {e.Message}");
        }
        return [];
    }

    /// <summary>Saves what was learned since the last save, which happens every few minutes on its own.</summary>
    public void Save() => Save(DateTimeOffset.Now);

    void Save(DateTimeOffset now)
    {
        _lastSave = now;
        if (path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_devices));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Report($"Couldn't save battery history: {e.Message}");
        }
    }
}
