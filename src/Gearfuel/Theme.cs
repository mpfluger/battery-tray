using Microsoft.Win32;

namespace Gearfuel;

enum Tone { Normal, Charging, Low, Unreachable }

/// <summary>Colors that match the taskbar's light or dark theme, read fresh on each use.</summary>
static class Theme
{
    static bool Light => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    public static Color For(Tone tone) => (tone, Light) switch
    {
        (Tone.Charging, true) => Color.FromArgb(0x10, 0x7C, 0x10),
        (Tone.Charging, false) => Color.FromArgb(0x6C, 0xCB, 0x5F),
        (Tone.Low, true) => Color.FromArgb(0xC4, 0x2B, 0x1C),
        (Tone.Low, false) => Color.FromArgb(0xFF, 0x6B, 0x6B),
        (Tone.Unreachable, _) => Color.Gray,
        _ => Text,
    };

    public static Color Text => Light ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
    public static Color SecondaryText => Light ? Color.FromArgb(0x5F, 0x5F, 0x5F) : Color.FromArgb(0xA6, 0xA6, 0xA6);
    public static Color Background => Light ? Color.FromArgb(0xF9, 0xF9, 0xF9) : Color.FromArgb(0x2C, 0x2C, 0x2C);
    public static Color Border => Light ? Color.FromArgb(0xCF, 0xCF, 0xCF) : Color.FromArgb(0x48, 0x48, 0x48);
}
