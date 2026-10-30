# AULA F75 battery tray

Run `AulaF75Battery.exe` for a single AULA F75 tray indicator. Hover to see its battery level, click for details, or right-click for refresh, optional startup with Windows, and exit.

The icon shows a green percentage, red at 20% or below, a blue `+` when connected to power, or a grey `?` when unavailable. Remembered readings are labeled **last seen** with their timestamp in the details window. Windows may initially place the icon in its hidden-icons menu.

The keyboard is read once a minute. Low-battery notifications reset after connecting to power or recovery above 25%. Closing the details window leaves the tracker running.

## Hardware support

AULA F75 over its `3554:FA09` 2.4 GHz receiver. Its cable state pins the reported level at 100, so the tracker shows **Plugged in** without a percentage. Bluetooth and wired-only battery readings are not implemented. Only battery/status queries are sent; keyboard settings are not changed.

A sleeping or disconnected keyboard can temporarily show unavailable. The latest local diagnostic snapshot is `%LOCALAPPDATA%\AulaF75Battery\status.txt`.

## Build and check

Windows .NET Framework compiler, no extra packages:

```powershell
.\Build.ps1
.\Probe-Battery.ps1
.\AulaF75Battery.exe --probe
```

Exit the running tray app before rebuilding the same executable. `Build.ps1 -OutputPath .\AulaF75Battery.next.exe` builds a replacement without stopping it. `--probe` writes `probe-result.txt`; `--show` opens the details window on launch.

## Branches

- `main`: AULA F75 only.
- `feat/deathadder-v4-pro`: the saved version with both keyboard and DeathAdder V4 Pro indicators and combined hover tooltips.

Executables are generated and ignored by Git. After switching branches, rebuild and restart the app to run that branch's version.

[Battery protocol reference](https://github.com/ushineko/sanshoku/blob/main/specs/013-aula-f75.md)
