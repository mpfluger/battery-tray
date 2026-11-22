namespace Gearfuel.Tests;

public sealed class SettingsTests : IDisposable
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"gearfuel-settings-{Guid.NewGuid()}.json");

    [Fact]
    public void SavedChoice_LoadsBack()
    {
        new Settings(RingColor.Purple).Save(_path);

        Assert.Equal(RingColor.Purple, Settings.Load(_path).RingColor);
    }

    [Theory]
    [InlineData(null)] // first start
    [InlineData("""{"RingColor":"Teal"}""")] // a color a later version added, or a typo
    [InlineData("not json")]
    public void MissingOrUnreadableFile_FallsBackToTheDefaults(string? contents)
    {
        if (contents is not null) File.WriteAllText(_path, contents);

        Assert.Equal(new Settings(), Settings.Load(_path));
    }

    public void Dispose() => File.Delete(_path);
}
