using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Gearfuel;

/// <summary>The tray icon's menu, drawn to match the hover card and the taskbar theme.</summary>
sealed class TrayMenu : ContextMenuStrip
{
    const char RefreshGlyph = '', PowerGlyph = '', CloseGlyph = '';

    readonly HeaderItem _header = new();
    readonly ToolStripMenuItem _refresh = new("Refresh");
    readonly ToggleItem _startWithWindows = new("Start with Windows");
    readonly ToolStripMenuItem _quit = new("Quit Gearfuel");
    readonly Icon _appIcon;
    bool _keepOpen;

    public TrayMenu(Icon appIcon, Action refresh, Action quit)
    {
        _appIcon = appIcon;
        Renderer = new MenuRenderer();
        ShowCheckMargin = false;
        Items.AddRange([_header, new ToolStripSeparator(), _refresh, _startWithWindows, new ToolStripSeparator(), _quit]);

        _header.Click += (_, _) => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });
        _refresh.Click += (_, _) => refresh();
        _startWithWindows.Checked = StartWithWindows.Enabled;
        _startWithWindows.CheckedChanged += (_, _) => StartWithWindows.Enabled = _startWithWindows.Checked;
        _quit.Click += (_, _) => quit();

        // Flipping the switch keeps the menu open, like a settings toggle.
        ItemClicked += (_, e) => _keepOpen = e.ClickedItem == _startWithWindows;
        Closing += (_, e) =>
        {
            e.Cancel = _keepOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked;
            _keepOpen = false;
        };
        ApplyScale();
    }

    // Colored on each opening, so the menu follows the theme; resized when the display scaling changed.
    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        if (Font.Size != FontSize) ApplyScale();
        _header.Image?.Dispose();
        using (var icon = new Icon(_appIcon, ImageScalingSize)) _header.Image = icon.ToBitmap();
        SetGlyph(_refresh, RefreshGlyph);
        SetGlyph(_startWithWindows, PowerGlyph);
        SetGlyph(_quit, CloseGlyph);
        base.OnOpening(e);
    }

    float DpiScale => DeviceDpi / 96f;
    float FontSize => 12 * DpiScale; // 9 pt at 100%

    // The menu measures its items before it opens, so sizes have to be in place by then.
    void ApplyScale()
    {
        int S(float pixels) => (int)Math.Round(pixels * DpiScale);
        var previousFont = Font;
        Font = new Font("Segoe UI", FontSize, GraphicsUnit.Pixel);
        previousFont.Dispose();
        Padding = new Padding(S(4));
        ImageScalingSize = new Size(S(16), S(16));
        foreach (ToolStripItem item in Items)
        {
            if (item is ToolStripMenuItem) item.Padding = new Padding(0, S(6), S(8), S(6));
        }
        PerformLayout();
    }

    void SetGlyph(ToolStripItem item, char glyph)
    {
        var size = ImageScalingSize;
        var image = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(image))
        using (var path = new GraphicsPath())
        using (var family = new FontFamily(Theme.IconFont))
        using (var brush = new SolidBrush(Theme.Text))
        {
            // Centered by the glyph's own outline: icon font glyphs don't sit in the middle of their text box.
            path.AddString(glyph.ToString(), family, (int)FontStyle.Regular, size.Height * 0.85f, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = path.GetBounds();
            using var center = new Matrix();
            center.Translate(size.Width / 2f - bounds.X - bounds.Width / 2, size.Height / 2f - bounds.Y - bounds.Height / 2);
            path.Transform(center);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillPath(brush, path);
        }
        item.Image?.Dispose();
        item.Image = image;
    }

    internal static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = 2 * radius;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>The app's name in bold, followed by its version.</summary>
    sealed class HeaderItem() : ToolStripMenuItem("Gearfuel")
    {
        public string Version => AppInfo.Version;
    }

    /// <summary>A menu item with an on/off switch on its right, in place of a check mark.</summary>
    sealed class ToggleItem : ToolStripMenuItem
    {
        public ToggleItem(string text) : base(text)
        {
            CheckOnClick = true;
            // The menu sizes its width from text columns only; blank shortcut text reserves room for the switch.
            ShortcutKeyDisplayString = "   "; // em spaces
        }

        float Scale => (Owner?.DeviceDpi ?? 96) / 96f;
        SizeF SwitchSize => new(34 * Scale, 18 * Scale);
        float VisibleWidth => Owner is null ? Width : Owner.ClientSize.Width - Bounds.X - Owner.Padding.Right;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new RectangleF(new PointF(VisibleWidth - SwitchSize.Width - 14 * Scale, (Height - SwitchSize.Height) / 2), SwitchSize);
            var knobSize = SwitchSize.Height - 8 * Scale;
            var knobX = Checked ? track.Right - 4 * Scale - knobSize : track.X + 4 * Scale;
            var knob = new RectangleF(knobX, track.Y + 4 * Scale, knobSize, knobSize);

            using var path = RoundedRectangle(track, SwitchSize.Height / 2);
            if (Checked)
            {
                using var fill = new SolidBrush(Theme.Accent);
                g.FillPath(fill, path);
                g.FillEllipse(Brushes.White, knob);
            }
            else
            {
                using var outline = new Pen(Theme.SecondaryText, Scale);
                using var dot = new SolidBrush(Theme.SecondaryText);
                g.DrawPath(outline, path);
                g.FillEllipse(dot, knob);
            }
        }
    }

    sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        static float Scale(ToolStripItemRenderEventArgs e) => (e.ToolStrip?.DeviceDpi ?? 96) / 96f;

        // Items can be laid out wider than the menu window; this is the part that shows.
        static float VisibleWidth(ToolStripItemRenderEventArgs e) =>
            e.ToolStrip is { } menu ? menu.ClientSize.Width - e.Item.Bounds.X - menu.Padding.Right : e.Item.Width;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Background);

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

        // Centered on the row like the text, and without the box the base renderer puts behind a checked item's icon.
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image is null) return;
            var size = e.ImageRectangle.Size;
            e.Graphics.DrawImage(e.Image, new Rectangle(new Point(e.ImageRectangle.X + (int)(3 * Scale(e)), (e.Item.Height - size.Height) / 2), size));
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            var scale = Scale(e);
            var bounds = new RectangleF(2 * scale, scale, VisibleWidth(e) - 4 * scale, e.Item.Height - 2 * scale);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundedRectangle(bounds, 4 * scale);
            using var brush = new SolidBrush(Theme.Hover);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Centered on the whole row, so text lines up with the icons; the menu's own text box sits high.
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var font = e.TextFont ?? e.Item.Font;
            var x = e.TextRectangle.X + (int)(5 * Scale(e)); // matches the icons' indent
            var bounds = new Rectangle(x, 0, e.Item.Width - x, e.Item.Height);
            if (e.Item is not HeaderItem header)
            {
                // The switch item's blank shortcut text needs no drawing.
                if (e.Text != e.Item.Text) return;
                TextRenderer.DrawText(e.Graphics, e.Text, font, bounds, Theme.Text, flags);
                return;
            }

            using var bold = new Font(font, FontStyle.Bold);
            var nameWidth = TextRenderer.MeasureText(e.Graphics, header.Text, bold, bounds.Size, flags).Width;
            TextRenderer.DrawText(e.Graphics, header.Text, bold, bounds, Theme.Text, flags);
            var versionX = bounds.X + nameWidth + (int)(6 * Scale(e));
            TextRenderer.DrawText(e.Graphics, header.Version, font, bounds with { X = versionX, Width = bounds.Right - versionX }, Theme.SecondaryText, flags);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var scale = Scale(e);
            var y = e.Item.Height / 2;
            using var pen = new Pen(Theme.Border, Math.Max(1, scale));
            e.Graphics.DrawLine(pen, 8 * scale, y, VisibleWidth(e) - 8 * scale, y);
        }
    }
}
