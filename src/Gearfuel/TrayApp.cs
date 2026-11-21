using System.Diagnostics;
using Gearfuel.Devices;

namespace Gearfuel;

/// <summary>Polls the battery sources and shows the result as one tray icon, with a card listing every device on hover.</summary>
sealed class TrayApp : ApplicationContext
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    readonly IBatterySource[] _sources;
    readonly DeviceTracker _tracker = new(new Forecast(Forecast.DefaultPath));
    readonly ContextMenuStrip _menu = new();
    readonly Icon _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)!;
    readonly NotifyIcon _notifyIcon;
    readonly HoverCard _card = new();
    readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = (int)PollInterval.TotalMilliseconds };
    // Windows reports the pointer moving over a tray icon, but not leaving it, so the card checks for that itself.
    readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 100 };
    IReadOnlyList<DeviceStatus> _devices = [];
    Point _hoverPoint;
    bool _refreshing;

    public TrayApp(params IBatterySource[] sources)
    {
        _sources = sources;
        var startWithWindows = new ToolStripMenuItem("Start with Windows") { Checked = StartWithWindows.Enabled, CheckOnClick = true };
        startWithWindows.CheckedChanged += (_, _) => StartWithWindows.Enabled = startWithWindows.Checked;
        _menu.Items.Add($"Gearfuel {AppInfo.Version}", null, (_, _) => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true }));
        _menu.Items.Add("Refresh", null, async (_, _) => await RefreshAsync());
        _menu.Items.Add(startWithWindows);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _menu.Opening += (_, _) => HideCard();

        // No tooltip text: it would cover the card.
        _notifyIcon = new NotifyIcon { Icon = _appIcon, ContextMenuStrip = _menu, Visible = true };
        _notifyIcon.MouseMove += (_, _) => ShowCard();
        _hoverTimer.Tick += (_, _) => HideCardIfPointerLeft();

        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var readings = await Task.WhenAll(_sources.Select(ReadAsync));
            _devices = _tracker.Update(readings.SelectMany(r => r).ToList(), DateTimeOffset.Now);

            var previous = _notifyIcon.Icon;
            _notifyIcon.Icon = _devices.Count == 0 ? _appIcon : RingIcon.Render(_devices);
            if (previous != _appIcon) previous?.Dispose();
            if (_card.Visible) _card.ShowNear(_hoverPoint, _devices);
        }
        finally
        {
            _refreshing = false;
        }
    }

    // Sources block on device I/O, so they run off the UI thread; one failing must not hide the others.
    static async Task<IReadOnlyList<BatteryReading>> ReadAsync(IBatterySource source)
    {
        try
        {
            return await Task.Run(source.ReadAsync);
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"{source.GetType().Name} failed: {e}");
            return [];
        }
    }

    void ShowCard()
    {
        if (_menu.Visible) return;
        _hoverPoint = Cursor.Position;
        _card.ShowNear(_hoverPoint, _devices);
        _hoverTimer.Start();
    }

    void HideCardIfPointerLeft()
    {
        // The icon's exact bounds aren't available, so "left" means moved further than an icon's width.
        var pointer = Cursor.Position;
        var reach = SystemInformation.SmallIconSize.Width;
        if (_card.Bounds.Contains(pointer) || Math.Abs(pointer.X - _hoverPoint.X) <= reach && Math.Abs(pointer.Y - _hoverPoint.Y) <= reach) return;
        HideCard();
    }

    void HideCard()
    {
        _hoverTimer.Stop();
        _card.Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Dispose();
            _hoverTimer.Dispose();
            _card.Dispose();
            if (_notifyIcon.Icon != _appIcon) _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _appIcon.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
