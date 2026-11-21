# BatteryTray

[![CI](https://github.com/mpfluger/battery-tray/actions/workflows/ci.yml/badge.svg)](https://github.com/mpfluger/battery-tray/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/mpfluger/battery-tray)](https://github.com/mpfluger/battery-tray/releases/latest)
[![License: MIT](https://img.shields.io/github/license/mpfluger/battery-tray)](LICENSE)

Battery levels of your wireless mice, keyboards and controllers, right in the Windows taskbar: one icon per device, with its percentage on it.

![Three BatteryTray icons in the taskbar, showing 85, 82 and 88 percent](docs/screenshot.png)

## Install

Download **BatteryTray-win-Setup.exe** from the [latest release](https://github.com/mpfluger/battery-tray/releases/latest) and run it. It installs for your user only (no admin rights needed), sets BatteryTray to start with Windows, and installs the .NET runtime if it's missing. BatteryTray then keeps itself up to date.

The installer isn't code-signed yet, so Windows SmartScreen may warn about it: choose *More info → Run anyway*.

Windows hides new tray icons in the overflow menu; drag them onto the taskbar to keep them in sight. To uninstall, use *Settings → Apps*.

## Supported devices

| Devices | Connection |
| --- | --- |
| Bluetooth devices that Windows itself shows a battery level for in *Settings → Bluetooth & devices* (mostly Bluetooth LE mice, keyboards and headsets) | Bluetooth |
| Razer wireless mice and keyboards | Razer USB receiver, or cable |
| Logitech devices with HID++ 2.0 battery reporting (most current mice and keyboards) | LIGHTSPEED, Bolt or Unifying receiver, or cable |
| Sony DualShock 4, DualSense, DualSense Edge | USB or Bluetooth |

Tested with a Razer DeathAdder V3 HyperSpeed, a Logitech PRO X TKL and a DualShock 4. If your device has a battery but no icon, [open an issue](https://github.com/mpfluger/battery-tray/issues/new?template=device-support.yml).

## Usage

- **Green**: charging. **Red**: 20% or less.
- **Gray**: no answer for 15 minutes (off, or out of range). The icon keeps the last known level, and goes away after an hour. Mice and keyboards that are just asleep keep their normal color.
- Hover an icon for the device's name. Right-click for *Refresh* and *Start with Windows*.

Everything runs locally. The only network access is a daily check for updates on GitHub.

## Build from source

Requires Windows 10 or later and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet test
dotnet run --project src/BatteryTray
```

Pushing a `v*` tag builds the installer and publishes a release.

## How it works

Windows only knows the battery level of some Bluetooth devices. For everything else, BatteryTray talks to each device family in its own protocol over raw HID. Each family is an `IBatterySource` in [`src/BatteryTray/Devices`](src/BatteryTray/Devices):

- **Bluetooth**: the battery level Windows stores on the device's Bluetooth device node.
- **Razer**: request/response feature reports, as documented by [OpenRazer](https://github.com/openrazer/openrazer).
- **Logitech**: HID++ 2.0 requests, directly or through the receiver, as documented by [Solaar](https://github.com/pwr-Solaar/Solaar).
- **PlayStation**: the battery byte in the controller's regular input reports, as laid out in the Linux `hid-playstation` driver.

The tests check each protocol against byte sequences captured from real devices, or against a simulated Logitech receiver.

## Known limitations

- Razer Synapse and Logitech G HUB talk to the same devices. BatteryTray tells its replies apart from theirs, but under heavy traffic a reading can be skipped until the next refresh.
- Logitech devices that only report a battery voltage (some older G mice) aren't supported.
- Controllers that another app opens exclusively (e.g. DS4Windows with *Hide DS4 Controller* on) can't be read.
- Windows doesn't tell whether a Bluetooth device is charging, so those never show as charging.

## License

[MIT](LICENSE)
