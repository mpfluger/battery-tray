namespace Gearfuel;

/// <summary>Durations as the card and notifications show them: one number and a unit.</summary>
static class TimeText
{
    public static string Short(TimeSpan time) => time switch
    {
        { TotalHours: < 1 } => $"{Math.Max(1, (int)time.TotalMinutes)}m",
        { TotalDays: < 2 } => $"{(int)time.TotalHours}h",
        { TotalDays: < 14 } => $"{(int)time.TotalDays}d",
        _ => $"{(int)(time.TotalDays / 7)}w",
    };

    /// <summary>Hours of use read best in hours, even past two days.</summary>
    public static string Hours(TimeSpan time) => time.TotalHours < 1 ? Short(time) : $"{(int)time.TotalHours}h";
}
