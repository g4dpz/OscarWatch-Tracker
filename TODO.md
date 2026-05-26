# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Pole footprint banding at high latitudes

---

## Satellite database

### Remote sync & merge

**Implemented:** fetch from **`https://tle.oscarwatch.org/satellite_database.json`**, merge dialog, startup check (Settings → Tracking). See [documents/satellite-database.md](documents/satellite-database.md).

**Possible follow-ups:**

- [ ] Optional periodic check while running (like TLE every 6 hours)
- [ ] “Restore defaults” choice: bundled only vs re-fetch remote
- [ ] Optional `version` or `ETag` in published JSON for change detection without full compare

### Transponder database editor

- [ ] Import / export JSON (file picker)
- [ ] “Open database folder” in Explorer/Finder
- [ ] Pick satellite name from TLE catalog when adding an entry

---

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

- [ ] Build in support for two radios e.g FT818 pairs or similar.

### Doppler & CAT timing

- [ ] **Predictive doppler** — adaptive lookahead on range rate (steep passes / high latitude)
- [ ] **Instantaneous range rate** — propagator range velocity instead of 1 s Δrange
- [ ] **Faster / adaptive CAT loop** — below fixed 150 ms or rate-based timing near TCA
- [ ] **Linear doppler threshold** — default 50 Hz; consider 20 Hz or Settings guidance
- [ ] **Doppler behaviour matrix** — NOR/REV, offsets, predictive, loop timing, knob threshold (README or `documents/`)

### Pass debugging & validation

- [ ] **Pass-debug log** (optional, Settings) — overlay offsets, computed RX/TX, CAT writes, knob/manual state, `vfo_not_moving`


### Additional rig drivers

- [ ] **Yaesu FT-817** — map single-VFO or A/B to `RigController` Main/Sub model
- [ ] Add ICOM IC-705 Support
- [ ] Add ICOM IC-905 Support

**Per driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

---

## Cloudlog

- [ ] Optional: push updates only when satellite elevation ≥ threshold
- [ ] Optional: retry/backoff and last-success timestamp in UI
- [ ] Polish Test connection messaging (invalid/missing API key)

---

## Rotator

See [building rotator drivers](documents/building-rotator-drivers.md).

- [ ] **Slew lead / mechanical lag** — command slightly ahead of look angle
- [ ] **Stop on park** — GS-232 `S` before park moves (Stop on disconnect/exit via driver dispose today)
- [ ] Optional: minimum move / max slew rate limits

---

## Operations & UX

- [ ] **Auto-focus satellite on pass** — when enabled sat rises above threshold, focus map overlay without manual click
- [ ] **Align track-start elevations** — rotator default −3° vs rig default −70°; document or unified “start tracking at” with overrides
- [ ] **Settings backup / export** — import/export `settings.json`
- [ ] **AOS/LOS rig behaviour** (optional) — pause CAT or park rotator at pass end; configurable

---

## Documentation

- [ ] **NOR vs REV** — database tag, doppler sign, offset sign, knob pairing
- [ ] **Settings vs database paths** — repo vs `%AppData%` vs bundled vs [remote sync](documents/satellite-database.md)
- [ ] **Offset storage** — `frequencySelections`, Remember checkbox, `modeOffsets`
- [ ] **Pre-pass checklist** — rebuild, Remember offsets, overlay vs sidebar freqs, Pause CAT, linear threshold
- [ ] Link **doppler behaviour matrix** from README when written (see Radio above)

---

## Larger projects (lower priority)

- [ ] Reuse the WSJT-X DSP/decoder code to build in a simple satellite focused duplex UI for operating FT modes
- [ ] Native SSTV Decoder for common sat modes with sync etc.
- [ ] Built in Packet Interface

---

## Completed (archive)

- [x] All button labels centered app-wide (horizontal and vertical)
- [x] Rotator park button shows “Parked” when parked
- [x] Manual rotator positioning dialog
- [x] Standby no longer persists CAT pause to settings on app close
- [x] ICOM IC-9100 driver (CI-V; satellite/Main/Sub/tone path as IC-9700)
- [x] Hardware validation: IC-9700, IC-9100, FT-847 (satellite mode, doppler, tones)
- [x] Satellite pass audio recording (WAV via PortAudio; Settings → Recording)
