# Development

These steps are for building or contributing to the app. They are not required to run an existing EXE.

## Build and test

Windows .NET Framework compiler, no extra packages:

```powershell
.\Build.ps1
.\Probe-Battery.ps1
.\Test-Popup.ps1
.\Test-Protocols.ps1
.\AulaF75Battery.exe --probe
.\AulaF75Battery.exe --probe --model f99pro
.\AulaF75Battery.exe --diagnostics
```

Exit the running tray app before rebuilding the same executable. `Build.ps1 -OutputPath .\AulaF75Battery.next.exe` builds a replacement without stopping it. `--probe` writes `probe-result.txt`; `--show` opens the details window on launch.

`Test-Popup.ps1` checks hidden startup, window restoration, all model-menu selections, persistence, and validation labels using temporary settings. `Test-Protocols.ps1` checks captured F75 packets, specification-derived F99 Pro/32-byte fixtures, malformed responses, receiver guards, and ambiguous receiver selection. Tests do not establish compatibility with hardware that has not been connected.

`--model` accepts `f75`, `f99pro`, `f75max`, `s75pro`, `f87`, `f87pro`, `f65`, `f75pro`, `f99`, `f108`, or `f108pro`. `--probe` and `--diagnostics` write local `probe-result.txt` and `aula-diagnostics.txt` files. A CLI model override applies to that run; use the tray menu to save a selection.
