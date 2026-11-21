using System.Diagnostics;
using BatteryTray.Devices;

namespace BatteryTray;

/// <summary>Polls the battery sources and keeps one tray icon per device in sync.</summary>
sealed class TrayApp : ApplicationContext
{
    static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    readonly IBatterySource[] _sources;
    readonly Dictionary<string, DeviceIcon> _icons = [];
    readonly ContextMenuStrip _menu = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = (int)PollInterval.TotalMilliseconds };
    // Keeps the menu (and Exit) reachable while no device is found.
    readonly NotifyIcon _placeholder;
    bool _refreshing;

    public TrayApp(params IBatterySource[] sources)
    {
        _sources = sources;
        _menu.Items.Add("Refresh", null, async (_, _) => await RefreshAsync());
        _menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _placeholder = new NotifyIcon { Icon = IconRenderer.Render("–", Tone.Normal), Text = "No devices found", ContextMenuStrip = _menu };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var readings = await Task.WhenAll(_sources.Select(ReadAsync));
            Show(readings.SelectMany(r => r).ToList());
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

    void Show(IReadOnlyList<BatteryReading> readings)
    {
        foreach (var reading in readings)
        {
            if (!_icons.TryGetValue(reading.Id, out var icon))
            {
                if (reading.Percent is null) continue;
                _icons[reading.Id] = icon = new DeviceIcon(_menu);
            }
            icon.Update(reading);
        }

        foreach (var goneId in _icons.Keys.Except(readings.Select(r => r.Id)).ToList())
        {
            _icons[goneId].Dispose();
            _icons.Remove(goneId);
        }

        _placeholder.Visible = _icons.Count == 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            foreach (var icon in _icons.Values) icon.Dispose();
            _placeholder.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
