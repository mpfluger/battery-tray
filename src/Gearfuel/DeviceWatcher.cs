using System.Runtime.InteropServices;
using Gearfuel.Devices;

namespace Gearfuel;

/// <summary>
/// Raises <see cref="Changed"/> when HID devices arrive or leave: plugged in, removed, or
/// connected over Bluetooth.
/// </summary>
sealed partial class DeviceWatcher : NativeWindow, IDisposable
{
    const int WmDeviceChange = 0x0219;

    // Windows sends a burst of notifications per device, one per HID collection.
    readonly System.Windows.Forms.Timer _settle = new() { Interval = 300 };
    readonly nint _registration;

    public event EventHandler? Changed;

    public DeviceWatcher()
    {
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
        CreateHandle(new CreateParams());

        var filter = new DeviceInterfaceFilter
        {
            Size = Marshal.SizeOf<DeviceInterfaceFilter>(),
            DeviceType = 5, // device interface
            ClassGuid = HidDevice.InterfaceClass,
        };
        _registration = RegisterDeviceNotificationW(Handle, ref filter, 0);
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
        if (_registration != 0) UnregisterDeviceNotification(_registration);
        _settle.Dispose();
        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DeviceInterfaceFilter
    {
        public int Size, DeviceType, Reserved;
        public Guid ClassGuid;
        public short Name;
    }

    [LibraryImport("user32.dll")]
    private static partial nint RegisterDeviceNotificationW(nint recipient, ref DeviceInterfaceFilter filter, int flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterDeviceNotification(nint registration);
}
