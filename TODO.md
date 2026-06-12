# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Small dialog for triggering keyer memories on the IC-9700

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

- [ ] **ICOM IC-905** — CI-V single-radio candidate

**Per new driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

## Rotator

See [building rotator drivers](documents/building-rotator-drivers.md).

- [ ] **SPID LAN/TCP** — MD-01 and similar over Ethernet (TCP port 23); serial SPID (Rot1Prog / Rot2Prog) is implemented
- [ ] **Slew lead / mechanical lag** — command slightly ahead of look angle
- [ ] **Stop on park** — GS-232 `S` before park moves (stop on disconnect/exit via driver dispose today)
- [ ] Optional: minimum move / max slew rate limits

## Operations & UX

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
- [x] **Yaesu FT-991 / FT-991A** — dual-radio endpoints only (ASCII CAT; mix with other dual legs)
- [x] ICOM IC-9100 driver (CI-V; satellite/Main/Sub/tone path as IC-9700)
- [x] Hardware validation: IC-9700, IC-9100, FT-847 (satellite mode, doppler, tones)

### Rotator & UI

- [x] All button labels centered app-wide (horizontal and vertical)
- [x] Rotator park button shows “Parked” when parked
- [x] Manual rotator positioning dialog
- [x] Standby no longer persists CAT pause to settings on app close

### Recording

- [x] Satellite pass audio recording (WAV via PortAudio; Settings → Recording)

### Backup

- [x] File → Import / Export — Settings and Transponder Database (`settings.json` and `satellite_database.json`)
