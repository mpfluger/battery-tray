namespace Gearfuel.Tests;

public class ChargeRemindersTests
{
    readonly ChargeReminders _reminders = new();

    [Fact]
    public void LowDevice_IsRemindedOf_OnlyOnceItPauses()
    {
        Assert.Empty(_reminders.Update([Pad(15, awake: true)])); // mid-game

        var reminder = Assert.Single(_reminders.Update([Pad(15, awake: false)]));
        Assert.Equal("Charge your DualShock 4", reminder.Title);
        Assert.Equal("15% left, about 2h of use.", reminder.Text);
    }

    [Fact]
    public void NearlyEmptyDevice_IsRemindedOf_EvenInUse() =>
        Assert.Single(_reminders.Update([Pad(5, awake: true)]));

    [Fact]
    public void Reminder_ComesOncePerDischarge()
    {
        _reminders.Update([Pad(15, awake: false)]);
        Assert.Empty(_reminders.Update([Pad(14, awake: false)]));

        _reminders.Update([Pad(60, awake: true, charging: true)]);
        Assert.Single(_reminders.Update([Pad(15, awake: false)]));
    }

    [Fact]
    public void DeviceChargedElsewhere_IsRemindedOfAgainNextTime()
    {
        _reminders.Update([Pad(15, awake: false)]);
        _reminders.Update([Pad(90, awake: false)]); // charged on a console, say

        Assert.Single(_reminders.Update([Pad(15, awake: false)]));
    }

    [Fact]
    public void DevicesRunningLowTogether_ShareOneReminder()
    {
        var reminder = Assert.Single(_reminders.Update([Pad(15, awake: false), new("keys", "PRO X TKL", 10, false, null, TimeSpan.FromMinutes(5))]));

        Assert.Equal("Charge 2 devices", reminder.Title);
        Assert.Equal("DualShock 4 (15%), PRO X TKL (10%)", reminder.Text);
    }

    [Fact]
    public void ChargedDevice_IsReportedFull_OncePerCharge()
    {
        _reminders.Update([Pad(95, awake: true, charging: true)]);

        Assert.Equal("DualShock 4 is charged", Assert.Single(_reminders.Update([Pad(100, awake: true, charging: true)])).Title);
        Assert.Empty(_reminders.Update([Pad(100, awake: true, charging: true)]));
    }

    [Fact]
    public void AlwaysPluggedInDevice_IsNeverReportedFull() =>
        Assert.Empty(_reminders.Update([Pad(100, awake: true, charging: true)]));

    [Fact]
    public void IdenticalDevices_AreRemindedOfSeparately()
    {
        _reminders.Update([Pad(15, awake: false)]);

        Assert.Single(_reminders.Update([Pad(15, awake: false), Pad(12, awake: false, id: "pad2")]));
    }

    static DeviceStatus Pad(int percent, bool awake, bool charging = false, string id = "pad1") =>
        new(id, "DualShock 4", percent, charging, charging ? null : new TimeLeft(TimeSpan.FromHours(2), OfUse: true),
            awake ? TimeSpan.Zero : TimeSpan.FromMinutes(3));
}
