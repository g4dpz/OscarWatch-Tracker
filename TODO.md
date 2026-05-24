# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General Todo Items

- [X] In Settings the button labels arent centered
- [ ] Theres still some banding issues with the Footprints at the poles
- [ ] if rotator is parked it should say "Parked" rather than Park
- [ ] function to move rotator wherever you want quick

---

## Satellite database

### Remote sync & merge (deferred)

Pull an authoritative `satellite_database.json` from a remote URL and **merge** with `%AppData%/OscarWatch/satellite_database.json` (not blind overwrite). Schedule and/or on-demand update; surface changes; fall back to bundled + local on failure.

**Open design:** source URL, merge keys (`name` + mode `type`?), conflict/rename rules, whether “Restore defaults” re-fetches remote, optional `version`/checksum in JSON.

**Building blocks:** Satellites → Manage transponder database…, `SatelliteDatabaseFile`, `SatelliteDatabaseService.Reload()`, bundled `OscarWatch/Assets/satellite_database.json`.

### Transponder database editor

- [ ] Import / export JSON (pick file path).
- [ ] “Open database folder” in Explorer/Finder.
- [ ] Pick satellite name from TLE catalog when adding an entry.

---

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

### Doppler & CAT timing

- [ ] **Predictive doppler** — adaptive lookahead on range rate (steep passes / high latitude).
- [ ] **Instantaneous range rate** — propagator range velocity instead of 1 s Δrange.
- [ ] **Faster / adaptive CAT loop** — below fixed 150 ms or rate-based timing near TCA.
- [ ] **Linear doppler threshold** — default 50 Hz; consider 20 Hz or Settings guidance.
- [ ] **Doppler behaviour matrix** — NOR/REV, offsets, predictive, loop timing, knob threshold (README or `documents/`).

### Pass debugging & validation

- [ ] **Pass-debug log** (optional, Settings) — overlay offsets, computed RX/TX, CAT writes, knob/manual state, `vfo_not_moving`.
- [ ] **Hardware validation** (real passes):
  - [ ] IC-9700 (satellite mode, Main/Sub, CTCSS on Sub, doppler).
  - [ ] FT-847, TS-2000 (SAT mode / SATL, doppler, tones per driver docs).

### ICOM follow-ups

- [ ] Optional **RIT/XIT** CI-V on IC-9700 if interactive VFO tuning is still awkward.
- [ ] **PTT-aware TX writes** — avoid Sub VFO select/write while transmitting (IC-910 / IC-9700).

### Additional rig drivers

- [x] **ICOM IC-9100** — CI-V; same satellite/Main/Sub/tone path as IC-9700 (`16 5A` sat mode, default CI-V `7C`). **Beta** — verify on hardware.
- [ ] **Yaesu FT-817** — map single-VFO or A/B to `RigController` Main/Sub model.

**Per driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

---

## Cloudlog

- [ ] Optional: push updates only when satellite elevation ≥ threshold.
- [ ] Optional: retry/backoff and last-success timestamp in UI.
- [ ] Polish Test connection messaging (invalid/missing API key).

---

## Rotator

See [building rotator drivers](documents/building-rotator-drivers.md).

- [ ] **Slew lead / mechanical lag** — command slightly ahead of look angle.
- [ ] **Stop on park** — GS-232 `S` before park moves (Stop on disconnect/exit via driver dispose today).
- [ ] Optional: minimum move / max slew rate limits.

---

## Operations & UX

- [ ] **Auto-focus satellite on pass** — when enabled sat rises above threshold, focus map overlay without manual click.
- [ ] **Align track-start elevations** — rotator default −3° vs rig default −70°; document or unified “start tracking at” with overrides.
- [ ] **Settings backup / export** — import/export `settings.json`.
- [ ] **AOS/LOS rig behaviour** (optional) — pause CAT or park rotator at pass end; configurable.

---

## Documentation

- [ ] **NOR vs REV** — database tag, doppler sign, offset sign, knob pairing.
- [ ] **Settings vs database paths** — repo vs `%AppData%` vs bundled vs future remote sync.
- [ ] **Offset storage** — `frequencySelections`, Remember checkbox, `modeOffsets`.
- [ ] **Pre-pass checklist** — rebuild, Remember offsets, overlay vs sidebar freqs, Pause CAT, linear threshold.
- [ ] Link **doppler behaviour matrix** from README when written (see Radio above).

---

## Larger projects (lower priority)

- [ ] Satellite pass audio recording.
- [ ] Windows installer / auto-update for releases.
- [ ] Linux / macOS field testing — rig CAT, rotator serial, voice announcements.
