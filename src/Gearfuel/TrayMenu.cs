using System.Runtime.InteropServices;

namespace Gearfuel;

/// <summary>
/// The tray icon's menu, as a native Windows menu: it looks and behaves like the system's own
/// tray menus, in spacing, check marks and theme.
/// </summary>
static partial class TrayMenu
{
    public enum Command { Repository = 1, Refresh, StartWithWindows, Exit }

    /// <returns>The chosen command, or null when the menu was dismissed.</returns>
    public static Command? Show(nint owner, Point at, bool startWithWindows)
    {
        var menu = CreatePopupMenu();
        try
        {
            AppendMenuW(menu, MfString, (nint)Command.Repository, $"Gearfuel {AppInfo.Version}");
            AppendMenuW(menu, MfSeparator, 0, null);
            AppendMenuW(menu, MfString, (nint)Command.Refresh, "Refresh");
            AppendMenuW(menu, MfString | (startWithWindows ? MfChecked : 0), (nint)Command.StartWithWindows, "Start with Windows");
            AppendMenuW(menu, MfSeparator, 0, null);
            AppendMenuW(menu, MfString, (nint)Command.Exit, "Exit");

            MatchTaskbarTheme();
            // Both as the TrackPopupMenu documentation prescribes for notification icons: without them,
            // the menu doesn't close when clicking elsewhere.
            SetForegroundWindow(owner);
            var screen = Screen.FromPoint(at).Bounds;
            var opensUpward = at.Y > screen.Top + screen.Height / 2;
            var command = TrackPopupMenuEx(menu, TpmReturnCmd | TpmRightButton | (opensUpward ? TpmBottomAlign : 0), at.X, at.Y, owner, 0);
            PostMessageW(owner, WmNull, 0, 0);
            return command == 0 ? null : (Command)command;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    // Windows themes an app's menus dark only through this undocumented uxtheme call (Windows 10 1903+),
    // which apps such as Notepad++ rely on too. Earlier versions keep light menus.
    static void MatchTaskbarTheme()
    {
        if (Environment.OSVersion.Version.Build < 18362) return;
        const int forceDark = 2, forceLight = 3;
        SetPreferredAppMode(Theme.IsLight ? forceLight : forceDark);
        FlushMenuThemes();
    }

    const uint MfString = 0x0, MfChecked = 0x8, MfSeparator = 0x800;
    const uint TpmRightButton = 0x2, TpmBottomAlign = 0x20, TpmReturnCmd = 0x100;
    const uint WmNull = 0x0;

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenuW(nint menu, uint flags, nint id, string? text);

    [LibraryImport("user32.dll")]
    private static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint owner, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    private static partial int SetPreferredAppMode(int mode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    private static partial void FlushMenuThemes();
}
