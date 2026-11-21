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
    public static Color Hover => Light ? Color.FromArgb(0xE6, 0xE6, 0xE6) : Color.FromArgb(0x3D, 0x3D, 0x3D);

    /// <summary>The Windows accent color, as chosen in Settings → Personalization → Colors.</summary>
    public static Color Accent =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr
            ? Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF)
            : Color.FromArgb(0x00, 0x78, 0xD4);

    /// <summary>Windows' icon font: Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10.</summary>
    public static readonly string IconFont =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
}
