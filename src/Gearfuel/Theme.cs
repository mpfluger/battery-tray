using Microsoft.Win32;

namespace Gearfuel;

enum Tone { Normal, Charging, Low, Unreachable }

/// <summary>Colors that match the taskbar's light or dark theme, read fresh on each use.</summary>
static class Theme
{
    /// <summary>The color of rings for devices that are neither low nor charging, as chosen in the menu.</summary>
    public static RingColor RingColor { get; set; }

    public static bool IsLight => Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;

    public static Color For(Tone tone) => (tone, IsLight) switch
    {
        (Tone.Charging, true) => Color.FromArgb(0x10, 0x7C, 0x10),
        (Tone.Charging, false) => Color.FromArgb(0x6C, 0xCB, 0x5F),
        (Tone.Low, true) => Color.FromArgb(0xC4, 0x2B, 0x1C),
        (Tone.Low, false) => Color.FromArgb(0xFF, 0x6B, 0x6B),
        (Tone.Unreachable, _) => Color.Gray,
        _ => RingColor switch
        {
            RingColor.Accent when Accent() is { } accent => accent,
            RingColor.Blue => IsLight ? Color.FromArgb(0x00, 0x5F, 0xB8) : Color.FromArgb(0x60, 0xCD, 0xFF),
            RingColor.Purple => IsLight ? Color.FromArgb(0x6B, 0x3F, 0xC4) : Color.FromArgb(0xC4, 0xA3, 0xFF),
            RingColor.Pink => IsLight ? Color.FromArgb(0xB4, 0x00, 0x9E) : Color.FromArgb(0xFF, 0x8A, 0xD8),
            RingColor.Yellow => IsLight ? Color.FromArgb(0x8A, 0x64, 0x00) : Color.FromArgb(0xFF, 0xD5, 0x4A),
            _ => Text,
        },
    };

    // Windows keeps a palette of shades of the accent color: lightest first, the accent itself in the middle.
    // Like Windows, this uses a lighter shade on dark surfaces and a darker one on light surfaces.
    static Color? Accent()
    {
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", "AccentPalette", null)
            is not byte[] { Length: >= 32 } palette) return null;
        Color Shade(int index) => Color.FromArgb(palette[4 * index], palette[4 * index + 1], palette[4 * index + 2]);
        return StandsApart(Shade(3)) ? Shade(IsLight ? 4 : 1) : null;
    }

    /// <summary>Whether a color can't be mistaken for low (red), charging (green) or unreachable (gray).</summary>
    internal static bool StandsApart(Color color) =>
        color.GetSaturation() >= 0.25f && color.GetHue() is >= 40 and < 330 and not (>= 70 and <= 175);

    public static Color Text => IsLight ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
    public static Color SecondaryText => IsLight ? Color.FromArgb(0x5F, 0x5F, 0x5F) : Color.FromArgb(0xA6, 0xA6, 0xA6);
    // Menu and card colors follow the Windows 10 taskbar menu.
    public static Color Background => IsLight ? Color.FromArgb(0xF2, 0xF2, 0xF2) : Color.FromArgb(0x2A, 0x2A, 0x2A);
    public static Color Hover => IsLight ? Color.FromArgb(0xDA, 0xDA, 0xDA) : Color.FromArgb(0x41, 0x41, 0x41);
    public static Color Separator => IsLight ? Color.FromArgb(0xC4, 0xC4, 0xC4) : Color.FromArgb(0x4F, 0x4F, 0x4F);
    public static Color Border => IsLight ? Color.FromArgb(0xCF, 0xCF, 0xCF) : Color.FromArgb(0x48, 0x48, 0x48);

    /// <summary>Windows' icon font: Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on Windows 10.</summary>
    public static readonly string IconFont =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
}
