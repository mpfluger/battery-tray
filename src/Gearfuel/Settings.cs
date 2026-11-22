using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gearfuel;

enum RingColor { Automatic, Accent, Purple, Yellow }

/// <summary>Choices made in the tray menu. Starting with Windows isn't one: it is the registry's Run entry itself.</summary>
sealed record Settings(RingColor RingColor = RingColor.Automatic)
{
    public static string DefaultPath => Path.Combine(AppInfo.DataDirectory, "settings.json");

    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static Settings Load(string path)
    {
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Diagnostics.Report($"Starting with default settings: {e.Message}");
        }
        return new();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Diagnostics.Report($"Couldn't save settings: {e.Message}");
        }
    }
}
