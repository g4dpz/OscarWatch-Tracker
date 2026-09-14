# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Small dialog for triggering keyer memories on the IC-9700

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

**Per new driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.


## Larger projects (lower priority)

- [ ] Reuse WSJT-X DSP/decoder code for a simple satellite-focused duplex UI (FT modes)
- [ ] Native SSTV decoder for common sat modes with sync, etc.
- [ ] Built-in packet interface

---