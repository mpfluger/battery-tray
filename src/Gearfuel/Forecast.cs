using System.Diagnostics;
using System.Text.Json;

namespace Gearfuel;

/// <summary>
/// Estimates how long each device's battery lasts, from its average rate of change since it last
/// started draining or charging. Over whole days, that average reflects how much the device is used.
/// </summary>
sealed class Forecast(string? path)
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gearfuel", "forecast.json");

    // Readings wobble by a point or two; larger moves against the trend mean the device was charged, or unplugged.
    const int Jitter = 2;
    const int MinChange = 5;
    static readonly TimeSpan MinDraining = TimeSpan.FromDays(1), MinCharging = TimeSpan.FromMinutes(10);

    internal sealed record Phase(DateTimeOffset Since, int Percent, bool Charging);

    readonly Dictionary<string, Phase> _phases = Load(path);

    public void Observe(string id, int percent, bool charging, DateTimeOffset now)
    {
        if (_phases.TryGetValue(id, out var phase) && phase.Charging == charging
            && (charging ? percent >= phase.Percent - Jitter : percent <= phase.Percent + Jitter)) return;

        _phases[id] = new(now, percent, charging);
        Save();
    }

    /// <returns>Time until empty, or until full while charging; null until there's enough history.</returns>
    public TimeSpan? Estimate(string id, int percent, bool charging, DateTimeOffset now)
    {
        if (!_phases.TryGetValue(id, out var phase) || phase.Charging != charging) return null;

        var elapsed = now - phase.Since;
        var change = Math.Abs(percent - phase.Percent);
        if (change < MinChange || elapsed < (charging ? MinCharging : MinDraining)) return null;

        var remaining = charging ? 100 - percent : percent;
        return elapsed * remaining / change;
    }

    static Dictionary<string, Phase> Load(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, Phase>>(File.ReadAllText(path)) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.TraceWarning($"Starting without battery history: {e.Message}");
        }
        return [];
    }

    void Save()
    {
        if (path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_phases));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Couldn't save battery history: {e.Message}");
        }
    }
}
