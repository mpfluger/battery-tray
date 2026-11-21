using BatteryTray.Devices;

namespace BatteryTray;

/// <summary>A tray icon showing one device's battery percentage.</summary>
sealed class DeviceIcon(ContextMenuStrip menu) : IDisposable
{
    const int LowPercent = 20;

    readonly NotifyIcon _notifyIcon = new() { ContextMenuStrip = menu };
    string _summary = "";

    /// <summary>Shows the reading; a reading without a percentage keeps the last known one.</summary>
    public void Update(BatteryReading reading)
    {
        if (reading.Percent is not int percent)
        {
            _notifyIcon.Text = $"{_summary} (asleep)";
            return;
        }

        var tone = reading.Charging ? Tone.Charging : percent <= LowPercent ? Tone.Low : Tone.Normal;
        var previous = _notifyIcon.Icon;
        _notifyIcon.Icon = IconRenderer.Render(percent.ToString(), tone);
        previous?.Dispose();

        _summary = $"{reading.Name}: {percent}%{(reading.Charging ? ", charging" : "")}";
        _notifyIcon.Text = _summary;
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}
