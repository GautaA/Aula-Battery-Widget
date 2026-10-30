# AULA Battery Widget

## Getting started

If you already have `AulaF75Battery.exe`, double-click it. No build or test commands are needed to use the app. If you only have the source code, follow the [development guide](docs/development.md) to create the EXE first.

Run `AulaF75Battery.exe` for an AULA keyboard battery indicator in the Windows tray. Right-click **Keyboard model** to choose your keyboard. Hover to see its level, click for details, or right-click for refresh, optional startup with Windows, and exit. The executable name stays the same to preserve existing startup shortcuts.

The default profile is F75. Your selection is remembered. Receiver IDs are shared across keyboards, so the app does not guess the model from a USB ID. It tracks one selected keyboard at a time; if multiple compatible receivers are connected, use the **Receiver** menu to select one.

## Showcase

![AULA F75 tray indicator and hover tooltip showing 90% battery](docs/images/aula-f75-tray.png)

The icon shows a green percentage, red at 20% or below, a blue `+` when connected to power, or a grey `?` when unavailable. Remembered readings are labeled **last seen** with their timestamp in the details window. Windows may initially place the icon in its hidden-icons menu.

The keyboard is read once a minute. Low-battery notifications reset after connecting to power or recovery above 25%. Closing the details window leaves the tracker running.

## Model profiles and validation

| Profile | Implementation | Validation |
| --- | --- | --- |
| F75 | Original 2.4 GHz battery reader | Tested on physical hardware |
| F99 Pro | Shared receiver reader, with documented information-query fallback | Not yet tested on an F99 Pro here |
| F75 Max | Dedicated 32-byte receiver protocol | Documented elsewhere; Windows hardware testing needed |
| S75 Pro | 32-byte protocol with both documented receiver identities | Documented elsewhere; hardware testing needed |
| F87 / F87 Pro | Shared receiver compatibility mode | Battery behavior unverified |
| F65 / F75 Pro / F99 / F108 / F108 Pro | Compatibility mode for recognized receiver protocols | Exact model/revision compatibility unverified |

**A selectable profile is not a promise that every revision is supported.** Compatibility mode only sends battery getters to known USB IDs with matching vendor collections and report sizes. Unknown receivers display unavailable. Use **Export receiver diagnostics...** to save USB IDs and capabilities for investigating other variants; nothing is uploaded automatically, and serial numbers and device paths are omitted.

Battery queries use the 2.4 GHz receiver. Bluetooth and wired-only battery readings are not implemented. The original F75 cable state pins its level at 100, so it shows **Plugged in** without a percentage. The F99 Pro information fallback and F75 Max/S75 Pro protocol do not provide a verified charging state; the app labels this as unavailable rather than estimating it. Only battery/status queries are sent; keyboard settings and firmware are not changed.

See [compatibility and protocol notes](docs/compatibility.md) for receiver requirements, sources, and remaining hardware checks.

A sleeping or disconnected keyboard can temporarily show unavailable. The latest local diagnostic snapshot is `%LOCALAPPDATA%\AulaF75Battery\status.txt`.

## Development

To build from source or contribute, see the [development guide](docs/development.md).

[Battery protocol reference](https://github.com/ushineko/sanshoku/blob/main/specs/013-aula-f75.md)
