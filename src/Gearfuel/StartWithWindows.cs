using Microsoft.Win32;

namespace Gearfuel;

/// <summary>The current user's "run at sign-in" entry for this executable.</summary>
static class StartWithWindows
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Gearfuel";
    const string NameBeforeRename = "BatteryTray"; // the app's name up to v1.0.2

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// Points an existing entry at the updated executable, whose name or path may have changed,
    /// and takes over the entry from before the rename.
    /// </summary>
    public static void RefreshAfterUpdate()
    {
        bool wasEnabled;
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            wasEnabled = key.GetValue(ValueName) is not null || key.GetValue(NameBeforeRename) is not null;
            key.DeleteValue(NameBeforeRename, throwOnMissingValue: false);
        }
        if (wasEnabled) Enabled = true;
    }
}
