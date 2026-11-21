using System.Diagnostics;
using System.Reflection;
using Gearfuel.Devices;
using Microsoft.Win32;

namespace Gearfuel;

/// <summary>Polls the battery sources and shows the result as one tray icon, with a card listing every device on hover.</summary>
sealed class TrayApp : ApplicationContext
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan HoverRefreshAfter = TimeSpan.FromSeconds(5);

    readonly IBatterySource[] _sources;
    readonly DeviceTracker _tracker = new(new Forecast(Forecast.DefaultPath));
    readonly Icon _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)!;
    readonly TrayMenu _menu;
    readonly NotifyIcon _notifyIcon;
    readonly HoverCard _card = new();
    readonly DeviceWatcher _deviceWatcher = new();
    readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = (int)PollInterval.TotalMilliseconds };
    // Windows reports the pointer moving over a tray icon, but not leaving it, so the card checks for that itself.
    readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 100 };
    IReadOnlyList<DeviceStatus> _devices = [];
    DateTimeOffset _lastRefresh;
    Point _hoverPoint;
    bool _refreshing, _refreshAgain;

    public TrayApp(params IBatterySource[] sources)
    {
        _sources = sources;
        _menu = new TrayMenu(_appIcon, refresh: () => _ = RefreshAsync(), quit: ExitThread);
        _menu.Opening += (_, _) => HideCard();

        // No tooltip text: it would cover the card.
        _notifyIcon = new NotifyIcon { Icon = _appIcon, ContextMenuStrip = _menu, Visible = true };
        _notifyIcon.MouseMove += (_, _) => ShowCard();
        _notifyIcon.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) ShowMenu(); };
        _hoverTimer.Tick += (_, _) => HideCardIfPointerLeft();

        _deviceWatcher.Changed += (_, _) => _ = RefreshAsync();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.UserPreferenceChanged += OnAppearanceChanged;
        SystemEvents.DisplaySettingsChanged += OnAppearanceChanged;

        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        // A request during a refresh may be about a device that refresh already missed, so it runs again after.
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }
        _refreshing = true;
        try
        {
            var readings = await Task.WhenAll(_sources.Select(ReadAsync));
            _lastRefresh = DateTimeOffset.Now;
            _devices = _tracker.Update(readings.SelectMany(r => r).ToList(), _lastRefresh);
            UpdateIcon();
            if (_card.Visible) _card.ShowNear(_hoverPoint, _devices);
        }
        finally
        {
            _refreshing = false;
        }
        if (_refreshAgain)
        {
            _refreshAgain = false;
            await RefreshAsync();
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

    void UpdateIcon()
    {
        var previous = _notifyIcon.Icon;
        _notifyIcon.Icon = _devices.Count == 0 ? _appIcon : RingIcon.Render(_devices);
        if (previous != _appIcon) previous?.Dispose();
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _ = RefreshAsync();
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock) _ = RefreshAsync();
    }

    // Theme, accent color or display scaling changed: redraw with the new colors and icon size.
    void OnAppearanceChanged(object? sender, EventArgs e) => UpdateIcon();

    // NotifyIcon opens its menu only on right-click; this is the same internal call, which also
    // places the menu next to the taskbar and closes it when clicking elsewhere.
    void ShowMenu() =>
        typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(_notifyIcon, null);

    void ShowCard()
    {
        if (_menu.Visible) return;
        _hoverPoint = Cursor.Position;
        if (!_card.Visible && DateTimeOffset.Now - _lastRefresh > HoverRefreshAfter) _ = RefreshAsync();
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
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.UserPreferenceChanged -= OnAppearanceChanged;
            SystemEvents.DisplaySettingsChanged -= OnAppearanceChanged;
            _deviceWatcher.Dispose();
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
