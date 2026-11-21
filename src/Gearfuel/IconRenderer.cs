using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Gearfuel;

enum Tone { Normal, Charging, Low, Unreachable }

/// <summary>Draws short text as a tray icon, in colors that suit the current taskbar theme.</summary>
static partial class IconRenderer
{
    static readonly FontFamily Font = new("Segoe UI");

    public static Icon Render(string text, Tone tone)
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var path = new GraphicsPath())
        using (var brush = new SolidBrush(ColorFor(tone)))
        {
            // Draw the text as large as possible: shrink it to the icon's width, then center it.
            path.AddString(text, Font, (int)FontStyle.Bold, emSize: size.Height * 1.25f, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = path.GetBounds();
            var scale = Math.Min(1, size.Width / bounds.Width);
            using var transform = new Matrix();
            transform.Translate(size.Width / 2f, size.Height / 2f);
            transform.Scale(scale, scale);
            transform.Translate(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2);
            path.Transform(transform);

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillPath(brush, path);
        }

        // Icon.FromHandle doesn't own the handle, so keep an owning copy and free the original.
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    static Color ColorFor(Tone tone)
    {
        var lightTaskbar = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1;
        return (tone, lightTaskbar) switch
        {
            (Tone.Charging, true) => Color.FromArgb(0x10, 0x7C, 0x10),
            (Tone.Charging, false) => Color.FromArgb(0x6C, 0xCB, 0x5F),
            (Tone.Low, true) => Color.FromArgb(0xC4, 0x2B, 0x1C),
            (Tone.Low, false) => Color.FromArgb(0xFF, 0x6B, 0x6B),
            (Tone.Unreachable, _) => Color.Gray,
            (_, true) => Color.Black,
            _ => Color.White,
        };
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint handle);
}
