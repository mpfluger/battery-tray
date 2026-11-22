namespace Gearfuel.Tests;

public class ForecastTests
{
    static readonly DateTimeOffset Start = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    readonly Forecast _forecast = new(path: null);

    [Fact]
    public void NewDevice_ShowsHoursOfUseLeft_AfterAnHourOfUse()
    {
        Use(_forecast, "mouse", Start, TimeSpan.FromMinutes(30), 100, 97);
        Assert.Null(_forecast.Estimate("mouse", 97, charging: false));

        Use(_forecast, "mouse", Start.AddMinutes(30), TimeSpan.FromMinutes(30), 97, 94); // 6 points an hour

        AssertAbout(TimeSpan.FromHours(94 / 6.0), ofUse: true, _forecast.Estimate("mouse", 94, charging: false));
    }

    [Fact]
    public void AfterADay_ShowsDaysLeft_AtTheUsualDailyUse()
    {
        Use(_forecast, "pad", Start, TimeSpan.FromHours(2), 100, 90);
        _forecast.Observe("pad", 90, charging: false, Start.AddDays(1)); // switched off until the next day

        AssertAbout(TimeSpan.FromDays(9), ofUse: false, _forecast.Estimate("pad", 90, charging: false)); // 10 points a day
    }

    [Fact]
    public void Charging_KeepsWhatWasLearned()
    {
        Use(_forecast, "pad", Start, TimeSpan.FromHours(2), 100, 90);
        Use(_forecast, "pad", Start.AddDays(1), TimeSpan.FromMinutes(30), 90, 100, charging: true);
        _forecast.Observe("pad", 100, charging: false, Start.AddDays(1).AddMinutes(31));

        AssertAbout(TimeSpan.FromDays(10), ofUse: false, _forecast.Estimate("pad", 100, charging: false));
    }

    [Fact]
    public void Charging_EstimatesTimeUntilFull()
    {
        Use(_forecast, "mouse", Start, TimeSpan.FromMinutes(20), 40, 50, charging: true); // half a point a minute

        AssertAbout(TimeSpan.FromMinutes(100), ofUse: false, _forecast.Estimate("mouse", 50, charging: true));
    }

    [Fact]
    public void SleepingDevice_CountsNeitherTheTimeAsleepNorWhatItLost()
    {
        Use(_forecast, "keys", Start, TimeSpan.FromHours(1), 100, 94);
        Use(_forecast, "keys", Start.AddHours(9), TimeSpan.FromHours(1), 91, 85); // lost 3 points asleep, or on another PC

        AssertAbout(TimeSpan.FromHours(85 / 6.0), ofUse: true, _forecast.Estimate("keys", 85, charging: false));
    }

    [Fact]
    public void RiseWithoutCharging_MeansChargedElsewhere_AndIsNotCounted()
    {
        var end = Use(_forecast, "pad", Start, TimeSpan.FromHours(1), 100, 94);
        Use(_forecast, "pad", end.AddMinutes(1), TimeSpan.FromHours(1), 99, 93);

        AssertAbout(TimeSpan.FromHours(93 / 6.0), ofUse: true, _forecast.Estimate("pad", 93, charging: false));
    }

    [Fact]
    public void RecentWeeks_CountMost()
    {
        // Two weeks of light use, then a week of three times as much.
        for (var day = 0; day < 21; day++)
            Use(_forecast, "mouse", Start.AddDays(day), TimeSpan.FromHours(1), 90, day < 14 ? 85 : 75);

        // 15 points a day would leave 6 days; the three weeks' average, 8.3 points a day, nearly 11.
        var left = _forecast.Estimate("mouse", 90, charging: false)!.Value.Duration;
        Assert.InRange(left.TotalDays, 6, 8);
    }

    [Fact]
    public void HistorySurvivesARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gearfuel-forecast-{Guid.NewGuid()}.json");
        try
        {
            Use(new Forecast(path), "pad", Start, TimeSpan.FromHours(1), 100, 94);

            AssertAbout(TimeSpan.FromHours(94 / 6.0), ofUse: true, new Forecast(path).Estimate("pad", 94, charging: false));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavingAtShutdown_KeepsReadingsSinceTheLastAutomaticSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gearfuel-forecast-{Guid.NewGuid()}.json");
        try
        {
            var forecast = new Forecast(path);
            var end = Use(forecast, "pad", Start, TimeSpan.FromHours(1), 100, 94); // saved automatically at the end
            Use(forecast, "pad", end, TimeSpan.FromMinutes(6), 94, 93);
            forecast.Save();

            // 7 points in 66 minutes; without the save, 6 points in 60.
            AssertAbout(TimeSpan.FromHours(93 * 1.1 / 7), ofUse: true, new Forecast(path).Estimate("pad", 93, charging: false));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HistoryFromBeforeVersion16_StartsOver()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gearfuel-forecast-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, """{"pad":{"Since":"2026-10-01T09:00:00+00:00","Percent":85,"Charging":false}}""");
            var forecast = new Forecast(path);
            Use(forecast, "pad", Start, TimeSpan.FromHours(1), 100, 94);

            AssertAbout(TimeSpan.FromHours(94 / 6.0), ofUse: true, forecast.Estimate("pad", 94, charging: false));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Readings a minute apart, as Gearfuel takes them while a device is in use.</summary>
    /// <returns>The time of the last reading.</returns>
    internal static DateTimeOffset Use(Forecast forecast, string id, DateTimeOffset from, TimeSpan duration, int fromPercent, int toPercent, bool charging = false)
    {
        var minutes = (int)duration.TotalMinutes;
        for (var minute = 0; minute <= minutes; minute++)
            forecast.Observe(id, fromPercent + (toPercent - fromPercent) * minute / minutes, charging, from.AddMinutes(minute));
        return from.AddMinutes(minutes);
    }

    // History fades a little even within a test, so estimates are compared to within 2%.
    static void AssertAbout(TimeSpan expected, bool ofUse, TimeLeft? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(ofUse, actual.Value.OfUse);
        Assert.InRange(actual.Value.Duration.TotalMinutes, expected.TotalMinutes * 0.98, expected.TotalMinutes * 1.02);
    }
}
