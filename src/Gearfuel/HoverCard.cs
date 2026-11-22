using System.Runtime.InteropServices;

namespace Gearfuel;

/// <summary>The list of all devices, shown while the pointer rests on the tray icon.</summary>
sealed partial class HoverCard : Form
{
    // Windows 11 rounds its popups' corners and draws their border; Windows 10's are square, with a drawn border.
    static readonly bool RoundsCorners = Environment.OSVersion.Version.Build >= 22000;

    // Layout in pixels at 100% scaling.
    const int Width100 = 300, Padding100 = 10, RowHeight100 = 26, Marker100 = 16, Gap100 = 10, PercentColumn100 = 40, TimeColumn100 = 76;

    const TextFormatFlags Line = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

    IReadOnlyList<DeviceStatus> _devices = [];

    public HoverCard()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
    }

    // Shown on top without taking focus from the app in use.
    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RoundsCorners) return;
        const int cornerPreference = 33, round = 2;
        var preference = round;
        DwmSetWindowAttribute(Handle, cornerPreference, ref preference, sizeof(int));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int toolWindow = 0x80, topmost = 0x8, noActivate = 0x8000000;
            var createParams = base.CreateParams;
            createParams.ExStyle |= toolWindow | topmost | noActivate;
            return createParams;
        }
    }

    /// <summary>Shows the devices next to the pointer, on the side of the taskbar facing the screen.</summary>
    public void ShowNear(Point pointer, IReadOnlyList<DeviceStatus> devices)
    {
        _devices = devices;
        var size = new Size(Scale(Width100), Scale(2 * Padding100 + Math.Max(1, devices.Count) * RowHeight100));
        var area = Screen.FromPoint(pointer).WorkingArea;
        var margin = Scale(8);
        var x = Math.Clamp(pointer.X - size.Width / 2, area.Left + margin, area.Right - size.Width - margin);
        var y = Math.Clamp(pointer.Y - size.Height / 2, area.Top + margin, area.Bottom - size.Height - margin);
        Bounds = new Rectangle(new Point(x, y), size);
        if (!Visible) Show();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // Sized in pixels for this window's monitor; points would follow the system DPI instead.
        using var font = new Font("Segoe UI", Scale(12), GraphicsUnit.Pixel);
        g.Clear(Theme.Background);
        if (!RoundsCorners)
        {
            using var border = new Pen(Theme.Border);
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        int padding = Scale(Padding100), rowHeight = Scale(RowHeight100), marker = Scale(Marker100), gap = Scale(Gap100);
        int percentWidth = Scale(PercentColumn100), timeWidth = Scale(TimeColumn100);
        var nameX = padding + marker + gap;
        var nameWidth = Width - nameX - gap - percentWidth - gap - timeWidth - padding;

        if (_devices.Count == 0)
        {
            TextRenderer.DrawText(g, "No devices with a battery found", font, new Rectangle(padding, padding, Width - 2 * padding, rowHeight), Theme.SecondaryText, Line);
            return;
        }

        for (var i = 0; i < _devices.Count; i++)
        {
            var device = _devices[i];
            var y = padding + i * rowHeight;
            DrawMarker(g, i, new RectangleF(padding, y + (rowHeight - marker) / 2f, marker, marker));

            var textColor = device.Unreachable ? Theme.SecondaryText : Theme.Text;
            var levelColor = device.Low || device.Charging ? Theme.For(device.Tone) : textColor;
            TextRenderer.DrawText(g, device.Name, font, new Rectangle(nameX, y, nameWidth, rowHeight), textColor, Line | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, $"{device.Percent}%", font, new Rectangle(nameX + nameWidth + gap, y, percentWidth, rowHeight), levelColor, Line | TextFormatFlags.Right);
            TextRenderer.DrawText(g, TimeColumn(device), font, new Rectangle(Width - padding - timeWidth, y, timeWidth, rowHeight), device.Low ? levelColor : Theme.SecondaryText, Line | TextFormatFlags.Right);
        }
    }

    // Shows which ring in the tray icon belongs to the row's device; devices without a ring get a dot.
    void DrawMarker(Graphics g, int row, RectangleF bounds)
    {
        var device = _devices[row];
        var track = new Ring(0, Tone.Unreachable);
        switch (row)
        {
            case 0:
                RingIcon.Draw(g, bounds, RingIcon.For(device), _devices.Count > 1 ? track : null);
                break;
            case 1:
                RingIcon.Draw(g, bounds, track, RingIcon.For(device));
                break;
            default:
                var dot = bounds.Width / 4;
                using (var brush = new SolidBrush(Theme.SecondaryText))
                    g.FillEllipse(brush, bounds.X + (bounds.Width - dot) / 2, bounds.Y + (bounds.Height - dot) / 2, dot, dot);
                break;
        }
    }

    static string TimeColumn(DeviceStatus device) => device switch
    {
        { Unreachable: true } => $"{TimeText.Short(device.SinceSeen)} ago",
        { Charging: true, Percent: >= 100 } => "full",
        { TimeLeft: null } => "learning…", // until the forecast has enough history
        { TimeLeft: { OfUse: true } left } => $"~{TimeText.Hours(left.Duration)} use",
        { TimeLeft: { } left } when device.Charging => $"full in {TimeText.Short(left.Duration)}",
        { TimeLeft: { } left } => TimeText.Short(left.Duration),
    };

    int Scale(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96f);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
