# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

## General

- [ ] Pole footprint banding at high latitudes
- [ ] Small Dialog for triggering keyer memories on the 9700

---

## Satellite database

### Transponder database editor

- [x] Import / export JSON (file picker)
- [x] Pick satellite name from TLE catalog when adding an entry

---

## Radio / rig

See [building radio drivers](documents/building-radio-drivers.md) for adding rigs.

- [ ] Build in support for two radios e.g FT818 pairs or similar.

### Doppler & CAT timing

- [ ] **Instantaneous range rate** — propagator range velocity instead of 1 s Δrange
- [ ] **Doppler output smoothing** — optional rate limit / EMA on CAT targets (e.g. QRTRigDoppler-style)
- [ ] **Linear doppler threshold** — default 50 Hz; consider 20 Hz or Settings guidance
- [ ] **Doppler behaviour matrix** — NOR/REV, offsets, loop timing, knob threshold (README or `documents/`)

### Additional rig drivers

- [ ] **Yaesu FT-817** — map single-VFO or A/B to `RigController` Main/Sub model
- [ ] Add ICOM IC-705 Support
- [ ] Add ICOM IC-905 Support

**Per driver:** protocol client, `IRigDriver` + `RigType` + Settings list, pass init (SAT/split/VFO/mode/CTCSS), `RigController` hooks, tests + hardware smoke test.

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
