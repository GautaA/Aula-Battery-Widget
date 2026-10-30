# Compatibility and protocol notes

Research and implementation date: 30 October 2026. Only the original F75 has been tested with this Windows app on physical hardware. Other profiles remain experimental, even when another project reports successful hardware tests.

## Receiver recognition

Recognition reads HID attributes and capabilities instead of relying on fixed interface/collection numbers. Every query revalidates the opened handle. The app rejects unknown IDs, wrong collections, and unexpected report sizes before writing. Ordinary keyboard/mouse input collections are never queried.

| Reader | USB VID:PID | Usage page | Windows input/output lengths | Battery request |
| --- | --- | --- | --- | --- |
| Shared 0x13 | `3554:FA09` | `FF02`, usage `0002` | 20 / 20 bytes | `13 4A`, zeros, additive checksum `5D` |
| 32-byte radio | `05AC:024F` or `0C45:FEFE` | `FF60` | 33 / 33 bytes, including report ID zero | `00 20 01`, zeros, checksum `21` at index 32 |

The second protocol is implemented for F75 Max and S75 Pro. F75 Max is documented with `05AC:024F`; S75 Pro is documented with both receiver identities. The generic raw receiver identity alone cannot distinguish these models. A user's model selection controls the display label and allowed protocol family, not a claim of automatic model identification.

F75, F99 Pro, F87, and F87 Pro profiles accept the shared 0x13 reader. F65, F75 Pro, F99, F108, and F108 Pro profiles offer compatibility mode across the two recognized families. For these latter profiles, this is a guarded way to test an existing protocol on a matching receiver, not a discovered model-specific implementation. Other revisions may use different IDs or protocols and will remain unavailable until investigated.

## Battery semantics

- Shared 0x4A reply: require a complete, single-page report, matching command, two-byte payload, additive checksum, known power state, and 1–100 level. State `10` means plugged in and its percentage is discarded. State `01` yields a battery level. Zero and unknown states are unavailable, not zero charge.
- F99 Pro fallback: after two unanswered 0x4A queries, send the documented read-only `0x0B` information query. It answers `0x0A`; require a checksum-valid, single-page four-byte payload, level at byte 6, and documented wireless connection byte 7 equal to `01`. Charging interpretation is unverified and displayed as unavailable. A generic unsolicited information frame is never interpreted as an answer to the 0x4A battery getter.
- 32-byte reply: require Windows report ID zero, `20 01 00` command/length/sequence, 1–100 level, and additive checksum. No charging flag is inferred. The strict full-frame/checksum requirement may reject undocumented firmware variants; captures are needed before relaxing it.
- Before a query, queued input is flushed. Reads/writes have bounded waits, stale/unrelated packets are rejected, and no settings, lighting, pairing, or firmware commands are implemented.

## Sources

- [F75 protocol with captured battery frames](https://github.com/ushineko/sanshoku/blob/main/specs/013-aula-f75.md)
- [F99 Pro protocol, including 0x0B information response](https://github.com/dnviti/aula-x99-pro-driver-linux/blob/master/PROTOCOL.md)
- [F75/F87 receiver-family notes](https://github.com/free-soldier28/Aula-Manager/blob/main/docs/PROTOCOL.md)
- [F75 Max battery packet and response handling](https://github.com/VitalyArt/Aula-F75-Max-Driver/blob/main/Sources/AulaF75MaxDriver/WirelessAulaDevice.swift)
- [S75 Pro identities](https://github.com/livvaa/AulaToolkit/blob/main/src/shared/models/s75pro.ts), [framing](https://github.com/livvaa/AulaToolkit/blob/main/src/shared/protocols/aula04.ts), and [battery exchange](https://github.com/livvaa/AulaToolkit/blob/main/src/main/hid/drivers/aula04Driver.ts)
- [F108 Pro research](https://github.com/Punkster81/AULA-F108-Driver): documents wired configuration, not a validated battery getter. Its wired `0C45:800A` identity is deliberately not treated as a battery receiver.
- [F65 revision differences](https://github.com/goyamamoto/F65-CustomFirmware): custom firmware research does not establish stock receiver compatibility.

## Hardware validation still needed

For each additional model and revision, collect receiver diagnostics, confirm a fresh level against the keyboard/vendor software, then check asleep/off, charging, full charge, unplug/replug, and simultaneous receivers. Do not advertise a revision as tested based only on protocol fixtures or selecting its name. Bluetooth and wired-only battery support remain out of scope for the current readers.

Diagnostics list HID product names, USB IDs, usage pages, and report lengths. No serial numbers, typed keys, device paths, or automatic network uploads are included. The local settings file remembers the receiver path solely to reconnect to the user's selected device.
