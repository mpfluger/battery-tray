using System.Runtime.InteropServices;

namespace Gearfuel;

/// <summary>
/// The notification-area icon, through Shell_NotifyIcon directly rather than WinForms' NotifyIcon. Version 4 of that
/// API names the icon for screen readers without showing a tooltip over the card, and reports keyboard use.
/// </summary>
sealed unsafe partial class TrayIcon : NativeWindow, IDisposable
{
    // WinForms' NotifyIcon numbered its first icon 1 as well; Windows remembers the icon's place on the taskbar by it.
    const uint IconId = 1;
    const int CallbackMessage = 0x8001; // WM_APP + 1
    static readonly int TaskbarCreated = RegisterWindowMessageW("TaskbarCreated");

    Icon _icon;
    string _text = "Gearfuel";
    bool _shown;

    /// <summary>Clicked with either button, or chosen with the keyboard.</summary>
    public event Action? Activated;

    /// <summary>The pointer moved over the icon.</summary>
    public event Action? Hovered;

    public TrayIcon(Icon icon)
    {
        _icon = icon;
        CreateHandle(new CreateParams());
    }

    public Icon Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            if (_shown) Notify(NimModify);
        }
    }

    /// <summary>What screen readers say for the icon. It isn't shown as a tooltip.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            if (_shown) Notify(NimModify);
        }
    }

    public void Show()
    {
        if (_shown) return;
        _shown = true;
        Add();
    }

    /// <summary>A Windows notification from the icon, held back while Focus Assist is on.</summary>
    public void ShowNotification(string title, string text)
    {
        if (!_shown) return;
        var data = Data(NifInfo);
        Copy(text, data.Info, 256);
        Copy(title, data.InfoTitle, 64);
        data.InfoFlags = NiifRespectQuietTime;
        Shell_NotifyIconW(NimModify, ref data);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == CallbackMessage)
        {
            switch ((int)m.LParam & 0xFFFF)
            {
                case WmMouseMove:
                    Hovered?.Invoke();
                    break;
                case NinSelect or NinKeySelect or WmContextMenu:
                    Activated?.Invoke();
                    break;
            }
            return;
        }
        if (m.Msg == TaskbarCreated && _shown) Add(); // Explorer restarted, with an empty notification area
        base.WndProc(ref m);
    }

    void Add()
    {
        Notify(NimAdd);
        var data = Data(0);
        data.TimeoutOrVersion = 4;
        Shell_NotifyIconW(NimSetVersion, ref data);
    }

    void Notify(uint message)
    {
        var data = Data(0);
        Shell_NotifyIconW(message, ref data);
    }

    NotifyIconData Data(uint flags)
    {
        var data = new NotifyIconData
        {
            Size = sizeof(NotifyIconData),
            Window = Handle,
            Id = IconId,
            Flags = flags | NifMessage | NifIcon | NifTip,
            CallbackMessage = CallbackMessage,
            Icon = _icon.Handle,
        };
        Copy(_text, data.Tip, 128);
        return data;
    }

    static void Copy(string text, char* buffer, int capacity)
    {
        var destination = new Span<char>(buffer, capacity);
        destination.Clear();
        text.AsSpan(0, Math.Min(text.Length, capacity - 1)).CopyTo(destination);
    }

    public void Dispose()
    {
        if (_shown)
        {
            var data = Data(0);
            Shell_NotifyIconW(NimDelete, ref data);
        }
        DestroyHandle();
    }

    const uint NimAdd = 0, NimModify = 1, NimDelete = 2, NimSetVersion = 4;
    const uint NifMessage = 0x1, NifIcon = 0x2, NifTip = 0x4, NifInfo = 0x10;
    const uint NiifRespectQuietTime = 0x80;
    const int WmMouseMove = 0x200, WmContextMenu = 0x7B, NinSelect = 0x400, NinKeySelect = 0x401;

    [StructLayout(LayoutKind.Sequential)]
    struct NotifyIconData
    {
        public int Size;
        public nint Window;
        public uint Id, Flags, CallbackMessage;
        public nint Icon;
        public fixed char Tip[128];
        public uint State, StateMask;
        public fixed char Info[256];
        public uint TimeoutOrVersion;
        public fixed char InfoTitle[64];
        public uint InfoFlags;
        public Guid Item;
        public nint BalloonIcon;
    }

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessageW(string name);
}
