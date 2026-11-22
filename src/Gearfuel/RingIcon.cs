using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Gearfuel;

readonly record struct Ring(float Level, Tone Tone);

/// <summary>
/// Draws up to two battery rings: the most urgent device outside, the next inside. Two is what
/// stays legible at the smallest tray icon size, 16 px.
/// </summary>
static class RingIcon
{
    public static Ring For(DeviceStatus device) => new(device.Percent / 100f, device.Tone);

    public static Icon Render(IReadOnlyList<DeviceStatus> devices)
    {
        var size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            Draw(graphics, new RectangleF(0, 0, size.Width, size.Height),
                outer: devices.Count > 0 ? For(devices[0]) : null,
                inner: devices.Count > 1 ? For(devices[1]) : null);
        }
        return ToIcon(bitmap);
    }

    /// <summary>Draws each given ring over a faint full circle (its track), filling clockwise from 12 o'clock.</summary>
    public static void Draw(Graphics graphics, RectangleF bounds, Ring? outer, Ring? inner)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var size = bounds.Width;
        float width = size * 0.15f, gap = size * 0.07f;
        var radius = size / 2 - width / 2 - size * 0.03f;
        if (outer is { } o) DrawRing(o, radius);
        if (inner is { } i) DrawRing(i, radius - width - gap);

        void DrawRing(Ring ring, float r)
        {
            var color = Theme.For(ring.Tone);
            var circle = new RectangleF(bounds.X + size / 2 - r, bounds.Y + size / 2 - r, 2 * r, 2 * r);
            using var track = new Pen(Color.FromArgb(70, color), width);
            graphics.DrawEllipse(track, circle);
            if (ring.Level <= 0) return;
            using var fill = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(fill, circle, -90, 360 * Math.Min(ring.Level, 1));
        }
    }

    // A one-image .ico holding the bitmap as PNG. Unlike Bitmap.GetHicon, this keeps the
    // semi-transparent edge pixels, so curves stay smooth.
    static Icon ToIcon(Bitmap bitmap)
    {
        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);

        using var ico = new MemoryStream();
        using (var writer = new BinaryWriter(ico, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0); // reserved
            writer.Write((short)1); // type: icon
            writer.Write((short)1); // image count
            writer.Write((byte)bitmap.Width);
            writer.Write((byte)bitmap.Height);
            writer.Write((byte)0); // no palette
            writer.Write((byte)0); // reserved
            writer.Write((short)1); // color planes
            writer.Write((short)32); // bits per pixel
            writer.Write((int)png.Length);
            writer.Write(22); // image data offset: header (6) + one entry (16)
            png.WriteTo(ico);
        }
        ico.Position = 0;
        return new Icon(ico);
    }
}
