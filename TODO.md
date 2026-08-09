# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Small dialog for triggering keyer memories on the IC-9700

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

**Per new driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

## Rotator

See [building rotator drivers](documents/building-rotator-drivers.md).

- [ ] **SPID LAN/TCP** — MD-01 and similar over Ethernet (TCP port 23); serial SPID (Rot1Prog / Rot2Prog) is implemented
- [ ] **Slew lead / mechanical lag** — command slightly ahead of look angle
- [ ] **Smart450 early Extended commit:** today east-of-north passes stay in primary until az is below 45°, then flip to 361–450° for the west wrap (`EastDescentMaxDeg` in `RotatorAzimuthPlanner`). AOS near 90° is already at the Extended edge (command 450°). Consider pre-committing to Extended from AOS (or earlier than 45°) when the pass will cross north, so the mast is already in the overlap band. Field note: upcoming AO-7 with AOS ~90°. Pass Visualiser tooltip preview shows the late flip.

## Operations & UX

- [ ] **Pass radar gallery (phase 2+)** — follow-ups after pass-planner gallery (phase 1 shipped):
  - [x] **Horizon mask** — per-station obstructions (trees, buildings) drawn on polar plots; pass prediction uses `max(mask(az), min elevation)`; optional grey/clip below mask
  - [ ] **Direction arrows** along the pass track
  - [ ] **Single-colour path mode** — optional planner-style track (vs sunlit/eclipse segments)
  - [ ] **Denser elevation rings** — optional 15° / 45° / 75° rings on pass polar plots
- [ ] **Auto-focus satellite on pass** — when enabled, sat rises above threshold and map overlay focuses without a manual click
- [ ] **Align track-start elevations** — rotator default −3° vs rig default −70°; document or unify “start tracking at” with overrides
- [x] **Park rotator after pass** (optional) — Settings → Rotator; default on
- [ ] **Pause CAT at pass end** (optional) — configurable AOS/LOS rig behaviour

## Larger projects (lower priority)

- [ ] Reuse WSJT-X DSP/decoder code for a simple satellite-focused duplex UI (FT modes)
- [ ] Native SSTV decoder for common sat modes with sync, etc.
- [ ] Built-in packet interface

---

## Completed (archive)

### General & map

- [x] Pole footprint banding at high latitudes
- [x] Doppler strategy buttons in frequency panel (Full / TX fixed / RX fixed), per mode in settings

### Satellite database

- [x] Transponder database: import / export JSON (file picker)
- [x] Transponder database: pick satellite name from TLE catalog when adding an entry

### Radio / rig

- [x] Dual radio support (Settings → Dual radio; e.g. FT-818 pairs)
- [x] **Yaesu FT-817 / FT-818** — dual-radio endpoints only (one VFO per radio)
- [x] **ICOM IC-705** — dual-radio endpoints only (CI-V; mix with FT-817/818)
- [x] **ICOM IC-905** — dual-radio endpoints only (CI-V; VHF/UHF/SHF; default address `AC`)
- [x] **Yaesu FT-991 / FT-991A** — dual-radio endpoints only (ASCII CAT; mix with other dual legs)
- [x] **SDR downlink (rigctl TCP)** — dual-radio downlink via SDR++/SDR Connect rigctl server; bidirectional frequency
- [x] **FlexRadio (SmartSDR)** — single-radio full duplex; LAN discovery; RX/TX slices; stub + controller tests
- [x] ICOM IC-9100 driver (CI-V; satellite/Main/Sub/tone path as IC-9700)
- [x] Hardware validation: IC-9700, IC-9100, FT-847 (satellite mode, doppler, tones)

### Rotator & UI

- [x] All button labels centered app-wide (horizontal and vertical)
- [x] Rotator park button shows “Parked” when parked
- [x] Manual rotator positioning dialog
- [x] Standby no longer persists CAT pause to settings on app close

### Operations & UX

- [x] **Pass radar gallery (phase 1)** — pass planner satellite filter; gallery window with polar plots (4 per row), screenshot export; help in `passes.html`

### Recording

- [x] Satellite pass audio recording (WAV via PortAudio; Settings → Recording)

### Backup

- [x] File → Import / Export — Settings and Transponder Database (`settings.json` and `satellite_database.json`)
