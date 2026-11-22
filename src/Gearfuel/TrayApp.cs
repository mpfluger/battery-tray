using System.Diagnostics;
using Gearfuel.Devices;
using Microsoft.Win32;

namespace Gearfuel;

/// <summary>Polls the battery sources and shows the result as one tray icon, with a card listing every device on hover.</summary>
sealed class TrayApp : ApplicationContext
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    static readonly TimeSpan HoverRefreshAfter = TimeSpan.FromSeconds(5);

    readonly IBatterySource[] _sources;
    readonly Forecast _forecast = new(Forecast.DefaultPath);
    readonly DeviceTracker _tracker;
    readonly NotifyIcon _notifyIcon;
    readonly HoverCard _card = new();
    readonly DeviceWatcher _deviceWatcher = new();
    readonly TrayMenu _menu = new();
    readonly ChargeReminders _reminders = new();
    Settings _settings = Settings.Load(Settings.DefaultPath);
    readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = (int)PollInterval.TotalMilliseconds };
    // A device that just arrived may not answer yet; a second look a little later catches it.
    readonly System.Windows.Forms.Timer _followUpTimer = new() { Interval = 3000 };
    // Windows reports the pointer moving over a tray icon, but not leaving it, so the card checks for that itself.
    readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 100 };
    IReadOnlyList<DeviceStatus> _devices = [];
    DateTimeOffset _lastRefresh;
    Point _hoverPoint;
    bool _refreshing, _refreshAgain, _menuOpen;
    long _menuClosedAt;

    public TrayApp(params IBatterySource[] sources)
    {
        _sources = sources;
        _tracker = new(_forecast);
        Theme.RingColor = _settings.RingColor;

        // No tooltip text: it would cover the card. Shown once the first readings are in, rather than
        // flashing empty rings first.
        _notifyIcon = new NotifyIcon { Icon = RingIcon.Render([]) };
        _notifyIcon.MouseMove += (_, _) => ShowCard();
        _notifyIcon.MouseUp += (_, e) => { if (e.Button is MouseButtons.Left or MouseButtons.Right) ShowMenu(); };
        _hoverTimer.Tick += (_, _) => HideCardIfPointerLeft();

        _deviceWatcher.Changed += (_, _) =>
        {
            _ = RefreshAsync();
            _followUpTimer.Stop();
            _followUpTimer.Start();
        };
        _followUpTimer.Tick += (_, _) =>
        {
            _followUpTimer.Stop();
            _ = RefreshAsync();
        };
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.UserPreferenceChanged += OnAppearanceChanged;
        SystemEvents.DisplaySettingsChanged += OnAppearanceChanged;

        _pollTimer.Tick += (_, _) => _ = RefreshAsync();
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
            var hidDevices = await Task.Run(HidDevice.All);
            var readings = await Task.WhenAll(_sources.Select(source => ReadAsync(source, hidDevices)));
            _lastRefresh = DateTimeOffset.Now;
            _devices = _tracker.Update(readings.SelectMany(r => r).ToList(), _lastRefresh);
            UpdateIcon();
            if (_card.Visible) _card.ShowNear(_hoverPoint, _devices);
            // Shown as notifications, which wait while Focus Assist is on, e.g. during a full-screen game.
            foreach (var reminder in _reminders.Update(_devices))
                if (_settings.ChargeReminders) _notifyIcon.ShowBalloonTip(0, reminder.Title, reminder.Text, ToolTipIcon.None);
        }
        catch (Exception e)
        {
            // Refreshes run unobserved, so this is the only place a failure can still be seen: in Copy diagnostics.
            Diagnostics.Report($"Refresh failed: {e}");
        }
        finally
        {
            _refreshing = false;
            _notifyIcon.Visible = true;
        }
        if (_refreshAgain)
        {
            _refreshAgain = false;
            await RefreshAsync();
        }
    }

    // Sources block on device I/O, so they run off the UI thread; one failing must not hide the others.
    static async Task<IReadOnlyList<BatteryReading>> ReadAsync(IBatterySource source, IReadOnlyList<HidDevice> hidDevices)
    {
        try
        {
            return await Task.Run(() => source.ReadAsync(hidDevices));
        }
        catch (Exception e)
        {
            Diagnostics.Report($"{source.GetType().Name} failed: {e}");
            return [];
        }
    }

    void UpdateIcon()
    {
        var previous = _notifyIcon.Icon;
        _notifyIcon.Icon = RingIcon.Render(_devices);
        previous?.Dispose();
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _ = RefreshAsync();
    }

    // Windows may end the process without Exit being chosen, so the history is saved now.
    void OnSessionEnding(object? sender, SessionEndingEventArgs e) => _forecast.Save();

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock) _ = RefreshAsync();
    }

    // Theme, accent color or display scaling changed: redraw with the new colors and icon size.
    void OnAppearanceChanged(object? sender, EventArgs e) => UpdateIcon();

    void ShowMenu()
    {
        // Clicking the icon while the menu is open closes the menu, and Windows then passes that same click
        // on to the icon; it must not reopen the menu.
        if (Environment.TickCount64 - _menuClosedAt < 300) return;

        HideCard();
        _menuOpen = true;
        _menu.Show(Cursor.Position,
        [
            new($"Gearfuel {AppInfo.Version}", () => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true })),
            new("Copy diagnostics", CopyDiagnostics),
            TrayMenuItem.Separator,
            new("Ring color", Submenu: [.. Enum.GetValues<RingColor>().Select(color => new TrayMenuItem(Name(color), () => SetRingColor(color), Checked: color == Theme.RingColor))]),
            new("Charge reminders", () => Save(_settings with { ChargeReminders = !_settings.ChargeReminders }), Checked: _settings.ChargeReminders),
            new("Start with Windows", () => StartWithWindows.Enabled = !StartWithWindows.Enabled, Checked: StartWithWindows.Enabled),
            TrayMenuItem.Separator,
            new("Exit", ExitThread, TrayMenu.ExitIcon),
        ]);
        _menuOpen = false;
        _menuClosedAt = Environment.TickCount64;

        static string Name(RingColor color) => color switch
        {
            RingColor.Automatic => "Default",
            RingColor.Accent => "Windows accent color",
            _ => color.ToString(),
        };
    }

    void CopyDiagnostics()
    {
        IReadOnlyList<HidDevice> hidDevices = [];
        try
        {
            hidDevices = HidDevice.All();
        }
        catch (Exception e)
        {
            Diagnostics.Report($"Listing HID devices failed: {e.Message}");
        }
        Clipboard.SetText(Diagnostics.Describe(_devices, hidDevices));
    }

    void SetRingColor(RingColor color)
    {
        Theme.RingColor = color;
        Save(_settings with { RingColor = color });
        UpdateIcon();
    }

    void Save(Settings settings)
    {
        _settings = settings;
        settings.Save(Settings.DefaultPath);
    }

    void ShowCard()
    {
        if (_menuOpen) return;
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
            SystemEvents.SessionEnding -= OnSessionEnding;
            _forecast.Save();
            SystemEvents.UserPreferenceChanged -= OnAppearanceChanged;
            SystemEvents.DisplaySettingsChanged -= OnAppearanceChanged;
            _deviceWatcher.Dispose();
            _menu.Dispose();
            _pollTimer.Dispose();
            _followUpTimer.Dispose();
            _hoverTimer.Dispose();
            _card.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
