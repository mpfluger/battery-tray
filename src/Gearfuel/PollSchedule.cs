namespace Gearfuel;

/// <summary>
/// Whether a timer tick reads the devices. Polling is skipped while nobody can see the icon,
/// but never stopped: a skipped tick needs no event to resume, and a wrong idea of the screen
/// or lock state delays readings by at most <see cref="MaxSkipped"/>.
/// </summary>
static class PollSchedule
{
    public static readonly TimeSpan MaxSkipped = TimeSpan.FromMinutes(15);

    public static bool ShouldRead(DateTimeOffset now, DateTimeOffset lastRead, bool locked, bool screenOff) =>
        !(locked || screenOff) || now - lastRead >= MaxSkipped;
}
