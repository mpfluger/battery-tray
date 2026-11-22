using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Gearfuel;

/// <param name="Click">Runs after the menu has closed.</param>
sealed record TrayMenuItem(string Text, Action? Click = null, char? Icon = null, bool Checked = false, IReadOnlyList<TrayMenuItem>? Submenu = null)
{
    public static readonly TrayMenuItem Separator = new("");

    public bool IsSeparator => Click is null && Submenu is null;
}

/// <summary>
/// The tray icon's menu: a native Windows menu. On Windows 10, Gearfuel draws its items to the measurements and
/// colors of the taskbar's own menu, as Explorer does. Windows still provides the frame, shadow, submenu arrows and
/// keyboard handling.
/// </summary>
sealed unsafe partial class TrayMenu : NativeWindow, IDisposable
{
    public const char ExitIcon = (char)0xE711; // Segoe icon font: Cancel
    const char CheckIcon = (char)0xE73E; // CheckMark

    // Measured from the Windows 10 taskbar menu, in pixels at 100% scaling.
    const int RowHeight = 32, SeparatorHeight = 9, TextIndent = 28, IconColumn = 32, RightPadding = 24, TopPadding = 4, BottomPadding = 8, MinWidth = 180;

    // Windows 11 draws native menus in its own modern style already; owner-drawn rows would look out of place there.
    static readonly bool DrawsItems = Environment.OSVersion.Version.Build < 22000;

    // Every item of the open menu and its submenus. An item's index here is its item data in measure and draw
    // messages, and its index + 1 its command id.
    readonly List<(TrayMenuItem Item, IReadOnlyList<TrayMenuItem> Siblings, int Position)> _entries = [];
    nint _background;

    public TrayMenu() => CreateHandle(new CreateParams());

    public void Show(Point at, IReadOnlyList<TrayMenuItem> items)
    {
        _entries.Clear();
        _background = CreateSolidBrush(ColorTranslator.ToWin32(Theme.Background));
        var menu = Build(items);
        int command;
        try
        {
            MatchTaskbarTheme();
            // Both as the TrackPopupMenu documentation prescribes for notification icons: without them,
            // the menu doesn't close when clicking elsewhere.
            SetForegroundWindow(Handle);
            var screen = Screen.FromPoint(at).Bounds;
            var opensUpward = at.Y > screen.Top + screen.Height / 2;
            command = TrackPopupMenuEx(menu, TpmReturnCmd | TpmRightButton | (opensUpward ? TpmBottomAlign : 0), at.X, at.Y, Handle, 0);
            PostMessageW(Handle, WmNull, 0, 0);
        }
        finally
        {
            DestroyMenu(menu); // and its submenus
            DeleteObject(_background);
        }
        if (command > 0) _entries[command - 1].Item.Click?.Invoke();
    }

    nint Build(IReadOnlyList<TrayMenuItem> items)
    {
        var menu = CreatePopupMenu();
        for (var position = 0; position < items.Count; position++)
        {
            var item = items[position];
            var index = _entries.Count;
            _entries.Add((item, items, position));
            var flags = (item.IsSeparator ? MfSeparator : 0) | (item.Checked ? MfChecked : 0) | (item.Submenu is null ? 0 : MfPopup);
            var id = item.Submenu is { } submenu ? Build(submenu) : index + 1;
            if (DrawsItems) AppendMenuW(menu, flags | MfOwnerDraw, id, index);
            else AppendMenuTextW(menu, flags, id, item.IsSeparator ? null : item.Text);
        }
        if (DrawsItems)
        {
            // Paints the frame's inner padding in the same color as the items.
            var info = new MenuInfo { Size = sizeof(MenuInfo), Mask = MimBackground, Background = _background };
            SetMenuInfo(menu, ref info);
        }
        return menu;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmMeasureItem:
                Measure((MeasureItem*)m.LParam);
                m.Result = 1;
                return;
            case WmDrawItem:
                Draw((DrawItem*)m.LParam);
                m.Result = 1;
                return;
        }
        base.WndProc(ref m);
    }

    float Scale => GetDpiForWindow(Handle) / 96f;

    int S(float pixels) => (int)Math.Round(pixels * Scale);

    Font CreateFont() => new("Segoe UI", 12 * Scale, GraphicsUnit.Pixel); // 9 pt at 100%

    // The first and last rows of each menu carry its top and bottom padding.
    (int Top, int Bottom) EdgePadding(int index)
    {
        var (_, siblings, position) = _entries[index];
        return (position == 0 ? S(TopPadding) : 0, position == siblings.Count - 1 ? S(BottomPadding) : 0);
    }

    void Measure(MeasureItem* measure)
    {
        var index = (int)measure->ItemData;
        var (item, siblings, _) = _entries[index];
        using var font = CreateFont();
        var widest = siblings.Max(i => TextRenderer.MeasureText(i.Text, font, Size.Empty, TextFormatFlags.NoPrefix).Width);
        // Windows adds room for a check mark to every owner-drawn item's width; take it back.
        measure->ItemWidth = (uint)Math.Max(S(MinWidth), S(TextIndent) + widest + S(RightPadding) - GetSystemMetrics(SmCxMenuCheck));
        var (top, bottom) = EdgePadding(index);
        measure->ItemHeight = (uint)(S(item.IsSeparator ? SeparatorHeight : RowHeight) + top + bottom);
    }

    void Draw(DrawItem* draw)
    {
        var index = (int)draw->ItemData;
        var item = _entries[index].Item;
        using var g = Graphics.FromHdc(draw->Dc);
        var bounds = Rectangle.FromLTRB(draw->Item.Left, draw->Item.Top, draw->Item.Right, draw->Item.Bottom);
        var (top, bottom) = EdgePadding(index);
        var row = Rectangle.FromLTRB(bounds.Left, bounds.Top + top, bounds.Right, bounds.Bottom - bottom);
        var selected = (draw->State & OdsSelected) != 0 && !item.IsSeparator;

        using (var background = new SolidBrush(Theme.Background)) g.FillRectangle(background, bounds);
        if (selected)
        {
            using var hover = new SolidBrush(Theme.Hover);
            g.FillRectangle(hover, row);
        }

        if (item.IsSeparator)
        {
            using var line = new Pen(Theme.Separator, Math.Max(1, (int)Scale));
            var y = row.Top + row.Height / 2;
            g.DrawLine(line, row.Left + S(8), y, row.Right - S(8), y);
            return;
        }

        if ((item.Checked ? CheckIcon : item.Icon) is { } glyph) DrawGlyph(g, glyph, new RectangleF(row.Left, row.Top, S(IconColumn), row.Height));

        using var font = CreateFont();
        var textBounds = Rectangle.FromLTRB(row.Left + S(TextIndent), row.Top, row.Right - S(RightPadding), row.Bottom);
        TextRenderer.DrawText(g, item.Text, font, textBounds, Theme.Text, selected ? Theme.Hover : Theme.Background,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    // Centered by the glyph's own outline: icon font glyphs don't sit in the middle of their text box.
    void DrawGlyph(Graphics g, char glyph, RectangleF cell)
    {
        using var family = new FontFamily(Theme.IconFont);
        using var path = new GraphicsPath();
        path.AddString(glyph.ToString(), family, (int)FontStyle.Regular, 14 * Scale, PointF.Empty, StringFormat.GenericTypographic);
        var outline = path.GetBounds();
        using var center = new Matrix();
        center.Translate(cell.X + cell.Width / 2 - outline.X - outline.Width / 2, cell.Y + cell.Height / 2 - outline.Y - outline.Height / 2);
        path.Transform(center);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Theme.Text);
        g.FillPath(brush, path);
    }

    // Windows themes an app's menus dark only through this undocumented uxtheme call (Windows 10 1903+),
    // which apps such as Notepad++ rely on too. It colors the frame; the items are drawn here.
    static void MatchTaskbarTheme()
    {
        if (Environment.OSVersion.Version.Build < 18362) return;
        const int forceDark = 2, forceLight = 3;
        SetPreferredAppMode(Theme.IsLight ? forceLight : forceDark);
        FlushMenuThemes();
    }

    public void Dispose() => DestroyHandle();

    const int WmMeasureItem = 0x2C, WmDrawItem = 0x2B, WmNull = 0x0;
    const uint MfChecked = 0x8, MfPopup = 0x10, MfSeparator = 0x800, MfOwnerDraw = 0x100;
    const uint TpmRightButton = 0x2, TpmBottomAlign = 0x20, TpmReturnCmd = 0x100;
    const uint MimBackground = 0x2, OdsSelected = 0x1;
    const int SmCxMenuCheck = 71;

    [StructLayout(LayoutKind.Sequential)]
    struct MeasureItem
    {
        public uint ControlType, ControlId, ItemId, ItemWidth, ItemHeight;
        public nint ItemData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DrawItem
    {
        public uint ControlType, ControlId, ItemId, Action, State;
        public nint Window, Dc;
        public Rect Item;
        public nint ItemData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MenuInfo
    {
        public int Size;
        public uint Mask, Style, MaxHeight;
        public nint Background;
        public uint ContextHelpId;
        public nint MenuData;
    }

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenuW(nint menu, uint flags, nint id, nint itemData);

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenuTextW(nint menu, uint flags, nint id, string? text);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetMenuInfo(nint menu, ref MenuInfo info);

    [LibraryImport("user32.dll")]
    private static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateSolidBrush(int color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint gdiObject);

    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    private static partial int SetPreferredAppMode(int mode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    private static partial void FlushMenuThemes();
}
