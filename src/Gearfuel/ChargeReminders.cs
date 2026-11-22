namespace Gearfuel;

readonly record struct Reminder(string Title, string Text);

/// <summary>
/// Decides when to remind about charging: once per discharge, when a device that won't last the day pauses (falls
/// asleep, or is switched off), so the reminder doesn't interrupt a game; and once per charge, when it's full.
/// </summary>
sealed class ChargeReminders
{
    // Mice and keyboards fall asleep a few idle minutes after the last use; a reading is a minute old at most otherwise.
    static readonly TimeSpan Paused = TimeSpan.FromMinutes(2);
    // Nearly empty is worth a reminder even mid-use.
    const int NearlyEmpty = 5;
    // A rise this large without being seen charging means the device was charged elsewhere.
    const int Recharged = 10;

    readonly Dictionary<string, int> _remindedAt = []; // percent at the reminder, by device ID
    readonly HashSet<string> _charging = [];

    public IReadOnlyList<Reminder> Update(IReadOnlyList<DeviceStatus> devices)
    {
        var low = new List<DeviceStatus>();
        var full = new List<Reminder>();
        foreach (var device in devices)
        {
            if (device.Charging)
            {
                _remindedAt.Remove(device.Id);
                // Only a charge that was seen below full ends in a reminder, not a device that's always plugged in.
                if (device.Percent < 100) _charging.Add(device.Id);
                else if (_charging.Remove(device.Id)) full.Add(new($"{device.Name} is charged", "It's full, so you can unplug it."));
                continue;
            }
            _charging.Remove(device.Id);
            if (_remindedAt.TryGetValue(device.Id, out var at) && device.Percent >= at + Recharged) _remindedAt.Remove(device.Id);

            if (device.Low && (device.SinceSeen >= Paused || device.Percent <= NearlyEmpty) && _remindedAt.TryAdd(device.Id, device.Percent))
                low.Add(device);
        }

        // Several devices running low at once make one reminder, not a stack of them.
        return low switch
        {
            [] => full,
            [var device] => [new($"Charge your {device.Name}", $"{device.Percent}% left{Outlook(device)}."), .. full],
            _ => [new($"Charge {low.Count} devices", string.Join(", ", low.Select(d => $"{d.Name} ({d.Percent}%)"))), .. full],
        };
    }

    static string Outlook(DeviceStatus device) => device.TimeLeft switch
    {
        { OfUse: true } left => $", about {TimeText.Short(left.Duration)} of use",
        { } left => $", about {TimeText.Short(left.Duration)} at your usual pace",
        null => "",
    };
}
