# BatteryTray

A small Windows tray app that shows the battery level of wireless peripherals: one notification-area icon per device, with the percentage drawn on it. Hover an icon for the device name.

- **Green**: charging. **Red**: 20% or less. **Gray**: the device stopped answering (asleep, off or out of range); the icon shows its last known level and goes away after an hour.
- Levels refresh every minute, or on demand via right-click → *Refresh*.

## Supported devices

| Devices | Connection |
| --- | --- |
| Bluetooth devices that Windows itself shows a battery level for in *Settings → Bluetooth* (mostly Bluetooth LE mice, keyboards and headsets) | Bluetooth |
| Razer wireless mice and keyboards | Razer USB receiver, or cable |
| Logitech devices with HID++ 2.0 battery reporting (most current mice and keyboards) | LIGHTSPEED, Bolt or Unifying receiver, or cable |
| Sony DualShock 4, DualSense, DualSense Edge | USB or Bluetooth |

Tested with a Razer DeathAdder V3 HyperSpeed, a Logitech PRO X TKL and a DualShock 4. Other models in these families are expected to work, but are untested.

Windows only tracks battery levels for some Bluetooth devices. For the others, the app talks to each device family in its own protocol over raw HID.

## Build and run

Requires Windows 10 or later and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet run
```

To install it, publish and start the executable at sign-in:

```powershell
dotnet publish -c Release -o publish
```

Then put a shortcut to `publish\BatteryTray.exe` in the Startup folder (`Win+R` → `shell:startup`).

Windows hides new tray icons in the overflow menu; drag them onto the taskbar to keep them visible.

## How it works

`TrayApp` polls a set of battery sources and keeps one `DeviceIcon` per device in sync. Each source (`Devices/`) finds the devices of one family and reads the batteries of those that answer:

- **Bluetooth**: the battery level Windows stores on the device's Bluetooth device node.
- **Razer**: request/response feature reports, as documented by [OpenRazer](https://github.com/openrazer/openrazer).
- **Logitech**: HID++ 2.0 requests, directly or through the receiver, as documented by [Solaar](https://github.com/pwr-Solaar/Solaar).
- **PlayStation**: the battery byte of the controller's regular input reports, as laid out in the Linux `hid-playstation` driver.

To support a new device family, implement `IBatterySource` and register it in `Program.cs`.

## Known limitations

- Razer Synapse and Logitech G HUB talk to the same devices. The app tells its replies apart from theirs, but under heavy traffic a reading can be skipped until the next refresh.
- Logitech devices that only report a battery voltage (some older G mice) aren't supported.
- Controllers that another app opens exclusively (e.g. DS4Windows with *Hide DS4 Controller* on) can't be read.
- Windows doesn't tell whether a Bluetooth device is charging, so those never show as charging.
