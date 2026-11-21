namespace Gearfuel;

/// <summary>
/// Raises <see cref="Changed"/> after devices are plugged in, removed, or connect or disconnect
/// over Bluetooth, once things have settled.
/// </summary>
sealed class DeviceWatcher : NativeWindow, IDisposable
{
    const int WmDeviceChange = 0x0219;

    // Windows sends a burst of notifications per device, and a new device needs a moment before it answers.
    readonly System.Windows.Forms.Timer _settle = new() { Interval = 1500 };

    public event EventHandler? Changed;

    public DeviceWatcher()
    {
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        // A hidden top-level window: Windows broadcasts device changes to those.
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDeviceChange)
        {
            _settle.Stop();
            _settle.Start();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        _settle.Dispose();
        DestroyHandle();
    }
}
