# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Small dialog for triggering keyer memories on the IC-9700

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

**Per new driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

## Rotator

See [building rotator drivers](documents/building-rotator-drivers.md).

- [x] **SPID LAN/TCP** (MD-01 / MD-02 over Ethernet, TCP port 23); serial SPID (Rot1Prog / Rot2Prog) remains separate
- [ ] **Slew lead / mechanical lag** (command slightly ahead of look angle)
- [ ] **Smart450 early Extended commit:** today east-of-north passes stay in primary until az is below 45°, then flip to 361–450° for the west wrap (`EastDescentMaxDeg` in `RotatorAzimuthPlanner`). AOS near 90° is already at the Extended edge (command 450°). Consider pre-committing to Extended from AOS (or earlier than 45°) when the pass will cross north, so the mast is already in the overlap band. Field note: upcoming AO-7 with AOS ~90°. Pass Visualiser tooltip preview shows the late flip.

## Larger projects (lower priority)

- [ ] Reuse WSJT-X DSP/decoder code for a simple satellite-focused duplex UI (FT modes)
- [ ] Native SSTV decoder for common sat modes with sync, etc.
- [ ] Built-in packet interface

---