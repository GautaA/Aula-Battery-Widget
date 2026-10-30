# Keyboard and mouse battery tray

Run `AulaF75Battery.exe`. The same app now displays two tray icons:

- AULA F75: green percentage, or a blue `+` when connected to power.
- Razer DeathAdder V4 Pro: purple percentage, blue while charging.

Low readings turn red. An unavailable reading shows a grey `?`; the popup labels any remembered percentage as **last seen** with its timestamp. Click either icon to see both batteries. Right-click for refresh, optional startup with Windows, or exit. Windows may initially place the icons in its hidden-icons menu.

Hover over either tray icon to see both battery levels together in a two-line tooltip. Charging and last-seen readings are labeled there too.

Each device is read independently once a minute. Low-battery notifications trigger at 20% or less and reset after charging or recovery above 25%. Closing the details window leaves the tracker running.

## Hardware support

- F75 over its `3554:FA09` 2.4 GHz receiver. Its cable state pins the reported level at 100, so the tracker shows **Plugged in** without a percentage. Bluetooth and wired-only F75 battery readings are not implemented.
- DeathAdder V4 Pro `1532:00BF` receiver and `1532:00BE` wired connection, interface 0. Wireless hardware was verified locally at 45% on battery. Wired support follows the documented protocol but has not yet been tested on this PC. The optional charging query can fail independently without discarding a valid percentage.

Only battery/status queries are sent; no mouse settings are changed. A busy, sleeping, or disconnected device can temporarily show unavailable. The latest local diagnostic snapshot is `%LOCALAPPDATA%\AulaF75Battery\status.txt`.

## Build and check

Windows .NET Framework compiler, no extra packages:

```powershell
.\Build.ps1
.\Test-RazerProtocol.ps1
.\AulaF75Battery.exe --probe
```

Exit the running tray app before rebuilding the same executable. `Build.ps1 -OutputPath .\AulaF75Battery.next.exe` builds a replacement without stopping it. `--probe` writes `probe-result.txt` (keyboard first, mouse second); `--show` opens the details window on launch. Existing startup preferences retain their original registry entry and executable path.

Protocol references:

- [AULA F75 battery protocol](https://github.com/ushineko/sanshoku/blob/main/specs/013-aula-f75.md)
- [Razer power report protocol](https://github.com/va0z-code/razer-battery-tray-indicator/blob/main/src/controller.rs)
- [DeathAdder V4 Pro interface and transaction ID](https://github.com/va0z-code/razer-battery-tray-indicator/blob/main/src/devices.rs)
