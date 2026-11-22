namespace Gearfuel.Tests;

public class PollScheduleTests
{
    static readonly DateTimeOffset LastRead = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset OneMinuteLater = LastRead.AddMinutes(1);

    [Fact]
    public void ReadsEveryTick_WhileSomeoneCanSeeTheIcon() =>
        Assert.True(PollSchedule.ShouldRead(OneMinuteLater, LastRead, locked: false, screenOff: false));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SkipsTicks_WhileLockedOrScreenOff(bool locked, bool screenOff) =>
        Assert.False(PollSchedule.ShouldRead(OneMinuteLater, LastRead, locked, screenOff));

    [Fact]
    public void NeverSkipsLongerThanTheCap_EvenIfAScreenOnEventWasMissed() =>
        Assert.True(PollSchedule.ShouldRead(LastRead + PollSchedule.MaxSkipped, LastRead, locked: false, screenOff: true));
}
