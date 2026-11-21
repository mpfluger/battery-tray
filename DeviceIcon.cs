using BatteryTray.Devices;

namespace BatteryTray;

/// <summary>A tray icon showing one device's battery percentage.</summary>
sealed class DeviceIcon(ContextMenuStrip menu) : IDisposable
{
    const int LowPercent = 20;

    readonly NotifyIcon _notifyIcon = new() { ContextMenuStrip = menu };
    BatteryReading? _last;

    public DateTime LastSeen { get; private set; }

    public void Update(BatteryReading reading)
    {
        _last = reading;
        LastSeen = DateTime.Now;
        var tone = reading.Charging ? Tone.Charging : reading.Percent <= LowPercent ? Tone.Low : Tone.Normal;
        Show(tone, $"{reading.Name}: {reading.Percent}%{(reading.Charging ? ", charging" : "")}");
    }

    /// <summary>Dims the icon for a device that stopped answering (asleep, off or out of range).</summary>
    public void MarkUnreachable()
    {
        if (_last is not null) Show(Tone.Unreachable, $"{_last.Name}: {_last.Percent}% (last seen {LastSeen:t})");
    }

    void Show(Tone tone, string tooltip)
    {
        var previous = _notifyIcon.Icon;
        _notifyIcon.Icon = IconRenderer.Render(_last!.Percent.ToString(), tone);
        previous?.Dispose();
        _notifyIcon.Text = tooltip;
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}
