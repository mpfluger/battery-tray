namespace Gearfuel.Tests;

public class ForecastTests
{
    static readonly DateTimeOffset Start = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    readonly Forecast _forecast = new(path: null);

    [Fact]
    public void Draining_EstimatesTimeLeftFromTheAverageRateSinceTheLastCharge()
    {
        _forecast.Observe("pad", 90, charging: false, Start);
        _forecast.Observe("pad", 60, charging: false, Start.AddDays(1)); // 30 points a day

        Assert.Equal(TimeSpan.FromDays(2), _forecast.Estimate("pad", 60, charging: false, Start.AddDays(1)));
    }

    [Fact]
    public void Draining_NeedsADayOfHistory_SoOneLongSessionDoesNotLookLikeTheUsualPace()
    {
        _forecast.Observe("pad", 100, charging: false, Start);

        Assert.Null(_forecast.Estimate("pad", 70, charging: false, Start.AddHours(3)));
    }

    [Fact]
    public void Charging_EstimatesTimeUntilFull()
    {
        _forecast.Observe("mouse", 40, charging: true, Start);

        Assert.Equal(TimeSpan.FromMinutes(90), _forecast.Estimate("mouse", 55, charging: true, Start.AddMinutes(30)));
    }

    [Fact]
    public void Charging_StartsTheHistoryOver()
    {
        _forecast.Observe("pad", 90, charging: false, Start);
        _forecast.Observe("pad", 70, charging: true, Start.AddDays(1));
        _forecast.Observe("pad", 100, charging: false, Start.AddDays(1).AddHours(2));

        Assert.Null(_forecast.Estimate("pad", 80, charging: false, Start.AddDays(1).AddHours(12)));
    }

    [Fact]
    public void SmallUpwardWobble_DoesNotStartTheHistoryOver()
    {
        _forecast.Observe("keys", 80, charging: false, Start);
        _forecast.Observe("keys", 81, charging: false, Start.AddHours(10));

        Assert.Equal(TimeSpan.FromHours(40), _forecast.Estimate("keys", 50, charging: false, Start.AddDays(1)));
    }

    [Fact]
    public void HistorySurvivesARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gearfuel-forecast-{Guid.NewGuid()}.json");
        try
        {
            new Forecast(path).Observe("pad", 90, charging: false, Start);

            Assert.Equal(TimeSpan.FromDays(2), new Forecast(path).Estimate("pad", 60, charging: false, Start.AddDays(1)));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
