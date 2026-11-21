# BatteryTray

A small Windows tray app that shows the battery level of wireless peripherals: one notification-area icon per device, with the percentage drawn on it. Hover an icon for the device name.

- **Green**: charging. **Red**: 20% or less.
- Levels refresh every minute, or on demand via right-click → *Refresh*.
- A device that is asleep keeps showing its last known level (tooltip says *asleep*). A device that disconnects loses its icon.

## Supported devices

| Device | Connection |
| --- | --- |
| Razer DeathAdder V3 HyperSpeed | HyperSpeed USB receiver |
| Logitech G devices with the *Unified Battery* feature (tested: PRO X TKL) | LIGHTSPEED USB receiver |
| Sony DualShock 4, DualSense, DualSense Edge | USB or Bluetooth |

Windows only reports battery levels for Bluetooth LE devices, and none of these use that, so the app talks to each device family in its own protocol over raw HID.

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

`TrayApp` polls a set of battery sources and keeps one `DeviceIcon` per reading in sync. Each source (`Devices/`) finds its devices among the HID collections Windows exposes and queries them:

- **Razer**: request/response feature reports to the receiver, as documented by [OpenRazer](https://github.com/openrazer/openrazer).
- **Logitech**: HID++ 2.0 requests through the receiver, as documented by [Solaar](https://github.com/pwr-Solaar/Solaar).
- **PlayStation**: the battery byte of the controller's regular input reports, as laid out in the Linux `hid-playstation` driver.

To support another model of a known family, add its product ID to that source's table. For a new family, implement `IBatterySource` and register it in `Program.cs`.

## Known limitations

- Razer Synapse and Logitech G HUB talk to the same receivers. The app tells its replies apart from theirs, but under heavy traffic a reading can be skipped until the next refresh.
- Controllers that another app opens exclusively (e.g. DS4Windows with *Hide DS4 Controller* on) can't be read.
