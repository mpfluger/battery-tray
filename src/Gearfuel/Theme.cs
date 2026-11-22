using Microsoft.Win32;

namespace Gearfuel;

enum Tone { Normal, Charging, Low, Unreachable }

/// <summary>Colors that match the taskbar's light or dark theme, read fresh on each use.</summary>
static class Theme
{
    public static bool IsLight => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    public static Color For(Tone tone) => (tone, IsLight) switch
    {
        (Tone.Charging, true) => Color.FromArgb(0x10, 0x7C, 0x10),
        (Tone.Charging, false) => Color.FromArgb(0x6C, 0xCB, 0x5F),
        (Tone.Low, true) => Color.FromArgb(0xC4, 0x2B, 0x1C),
        (Tone.Low, false) => Color.FromArgb(0xFF, 0x6B, 0x6B),
        (Tone.Unreachable, _) => Color.Gray,
        _ => Text,
    };

    public static Color Text => IsLight ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
    public static Color SecondaryText => IsLight ? Color.FromArgb(0x5F, 0x5F, 0x5F) : Color.FromArgb(0xA6, 0xA6, 0xA6);
    // Menu and card colors follow the Windows 10 taskbar menu.
    public static Color Background => IsLight ? Color.FromArgb(0xF2, 0xF2, 0xF2) : Color.FromArgb(0x2B, 0x2B, 0x2B);
    public static Color Hover => IsLight ? Color.FromArgb(0xDA, 0xDA, 0xDA) : Color.FromArgb(0x41, 0x41, 0x41);
    public static Color Separator => IsLight ? Color.FromArgb(0xC4, 0xC4, 0xC4) : Color.FromArgb(0x5C, 0x5C, 0x5C);
    public static Color Border => IsLight ? Color.FromArgb(0xCF, 0xCF, 0xCF) : Color.FromArgb(0x48, 0x48, 0x48);

    /// <summary>Windows' icon font: Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10.</summary>
    public static readonly string IconFont =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
}
