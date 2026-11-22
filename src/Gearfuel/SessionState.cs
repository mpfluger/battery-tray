using System.Runtime.InteropServices;

namespace Gearfuel;

/// <summary>Whether the user's session is locked, and whether the screen is off.</summary>
sealed partial class SessionState : NativeWindow, IDisposable
{
    // GUID_CONSOLE_DISPLAY_STATE: Windows reports the current state on registering, then each change.
    static readonly Guid DisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    const int WmPowerBroadcast = 0x0218, PbtPowerSettingChange = 0x8013;

    readonly nint _registration;

    /// <summary>Raised when the screen turns back on.</summary>
    public event EventHandler? ScreenOn;

    public SessionState()
    {
        CreateHandle(new CreateParams());
        var setting = DisplayState;
        _registration = RegisterPowerSettingNotification(Handle, ref setting, 0);
    }

    public bool ScreenOff { get; private set; }

    /// <summary>Asked fresh each time, so a missed lock or unlock event can't leave it wrong.</summary>
    public static bool Locked
    {
        get
        {
            // WTSINFOEXW: a 4-byte level, then the 8-byte-aligned level-1 data: session id, state, flags.
            const int sessionInfoEx = 25, sessionFlagsOffset = 16, lockedFlag = 0;
            if (!WTSQuerySessionInformationW(0, -1, sessionInfoEx, out var buffer, out _)) return false;
            try
            {
                return Marshal.ReadInt32(buffer, sessionFlagsOffset) == lockedFlag;
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
    }

    /// <summary>The user is evidently looking, e.g. at the tray icon, whatever the last notification said.</summary>
    public void UserIsActive() => ScreenOff = false;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPowerBroadcast && m.WParam == PbtPowerSettingChange)
        {
            // POWERBROADCAST_SETTING: the setting's GUID, the data length, then the data: 0 off, 1 on, 2 dimmed.
            var on = Marshal.ReadInt32(m.LParam, 20) != 0;
            var turnedOn = ScreenOff && on;
            ScreenOff = !on;
            if (turnedOn) ScreenOn?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registration != 0) UnregisterPowerSettingNotification(_registration);
        DestroyHandle();
    }

    [LibraryImport("user32.dll")]
    private static partial nint RegisterPowerSettingNotification(nint recipient, ref Guid setting, int flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterPowerSettingNotification(nint registration);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformationW(nint server, int session, int infoClass, out nint buffer, out int bytes);

    [LibraryImport("wtsapi32.dll")]
    private static partial void WTSFreeMemory(nint memory);
}
