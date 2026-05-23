# OscarWatch — application TODO

Tracked ideas and deferred work. Not a commitment order; items may be split or dropped.

---

## Satellite database

### Remote sync & merge (deferred — not in current scope)

Pull an authoritative `satellite_database.json` from a remote URL and merge it with the user’s local database (`%AppData%/OscarWatch/satellite_database.json`) when the remote copy has changed.

**Goals**

- Check for updates on a schedule and/or on demand (e.g. menu: “Update transponder database”).
- Download remote JSON (HTTPS, versioned URL or ETag/Last-Modified).
- **Merge**, not blind overwrite: preserve local-only satellites/modes and user edits where appropriate.
- Surface what changed (new satellites, new modes, updated frequencies) before or after apply.
- Fall back to bundled + local data if the network request fails.

**Open design questions**

- Source URL (project-hosted file, community mirror, user-configurable?).
- Merge rules: match on satellite `name` + mode `type`? How to handle renames and conflicts?
- Whether “Restore defaults” resets to bundled only or also re-fetches remote.
- Optional: checksum or `version` field in JSON for cheap change detection.

**Existing building blocks**

- Local editor: **Satellites → Manage transponder database…**
- `SatelliteDatabaseFile` load/save/validate
- `SatelliteDatabaseService.Reload()` after save
- Bundled asset: `OscarWatch/Assets/satellite_database.json`

---

## Transponder database editor (follow-ups)

- [ ] Import / export JSON (pick file path).
- [ ] “Open database folder” in Explorer/Finder.
- [ ] Pick satellite name from TLE catalog when adding an entry (name alignment).

---

## Radio / rig

**Shipped today:** ICOM IC-910, IC-9700 (CI-V), dummy rig; linear REV/NOR doppler, interactive Main VFO, TX/RX offset spinners, 150 ms CAT loop.

### Doppler & CAT timing (high priority)

- [ ] **Predictive doppler** — adaptive lookahead on range rate for smoother linear tracking on steep passes (e.g. high latitude).
- [ ] **Instantaneous range rate** — use propagator range velocity instead of 1 s Δrange from OrbitTools.
- [ ] **Faster / adaptive CAT loop** — evaluate lower than fixed 150 ms or rate-based timing near TCA.
- [ ] **Linear doppler threshold** — default 50 Hz; consider 20 Hz default or surfacing recommendation in Settings.
- [ ] **Doppler behaviour matrix** — document NOR/REV, offsets, predictive, loop timing, knob threshold in README.

### Offsets & migration

- [ ] **External offset import** — read third-party `config.ini` `[offset_profiles]` (SAT, mode, RX, TX) into `frequencySelections` / `modeOffsets`.
- [ ] **Remember offsets UX** — optional default-on per satellite, or one-shot prompt (“Save offset for FO-29?”).
- [ ] **Stored-but-ignored hint** — when `rememberOffsets` is false but `modeOffsets` has non-zero values, show UI hint so file vs spinner mismatch is obvious.

### Pass debugging & validation

- [ ] **Pass-debug log** (optional, Settings) — log overlay offsets, computed RX/TX, CAT writes, knob/manual state, `vfo_not_moving`; invaluable when doppler drifts mid-pass.
- [ ] **Hardware validation checklist** — tick off after real passes:
  - [ ] IC-910 + FO-29 REV (TX offset lowers Radio TX; RX doppler inverted).
  - [ ] IC-910 + RS-44 REV (RX offset; interactive knob pairing).
  - [ ] SO-50 FM (non-interactive, CTCSS arm/access on tone change).
  - [ ] IC-9700 (satellite mode, Main/Sub, CTCSS on Sub, doppler).

### ICOM (existing)

- [ ] IC-9700: validate CAT on hardware (satellite mode, Main/Sub, CTCSS on Sub, doppler).
- [ ] Optional: **RIT/XIT** CI-V on IC-9700 if interactive VFO tuning still awkward.
- [ ] **PTT-aware TX writes** — don’t select/write Sub VFO while transmitting (evaluate for IC-910/9700).

### Additional rig drivers (investigate & implement)

- [ ] **ICOM IC-9100** — CI-V (closely related to IC-910 / IC-9700; confirm satellite mode, Main/Sub, tone commands, and default CI-V address vs IC-910H).
- [x] **Yaesu FT-847** — Yaesu CAT serial protocol; satellite/SAT RX/TX VFOs (beta — verify on hardware).
- [ ] **Yaesu FT-817** — Yaesu CAT (817/817ND variants); often single-VFO or simple split — map to Main/Sub or A/B model in `RigController`.
- [x] **Kenwood TS-2000** — Kenwood ASCII CAT; cross-band SATL (beta — verify on hardware: SATL + TRACE off).

**Per-driver work (each rig)**

- Protocol client (bytes, timeouts, no retry on doppler writes where needed).
- `IRigDriver` implementation + `RigType` + Settings radio list.
- Pass init: satellite/split, VFO select, mode, CTCSS on uplink VFO where applicable.
- Hook into `RigController` (thresholds, Main/Sub vs A/B, region tone vs tone-squelch if relevant).
- Golden/unit tests where possible; hardware smoke test on a real pass.

**References to check**

- Manufacturer CAT manuals / Hamlib rig caps (good cross-check for command sets).
- OscarWatch: `OscarWatch/Rig/`, `RigDriverFactory`, `DopplerFrequencyCalculator`, `RigController`.

---

## Cloudlog

- [x] Radio API v2 (`/index.php/api/radio`) on satellite select / frequency update (Settings → Cloudlog).
- [ ] **Test connection** button in Settings with clear success/failure (improve “missing api key” / invalid key messaging).
- [ ] Optional: only push updates when satellite elevation ≥ configurable threshold.
- [ ] Optional: retry/backoff and last-success timestamp in UI.

---

## Rotator

**Shipped today:** Yaesu GS-232 / EasyComm, 0–450° azimuth setting, park button, track when sat above threshold.

### Smart azimuth path (450° unwrap)

On a **450°** rotator (e.g. Yaesu G-5500 + GS-232), commands can use **361–450°** to stay on the “far” side of north instead of winding back through 0°. Today we pass compass azimuth **0–360°** straight to the driver — fine for most of a pass, but **bad at the north wrap** (e.g. pass **NE → N → W**: azimuth 350° → 10° can command a **~340° slew** when **~20°** via 370° would do).

**Goal**

- When `AzimuthRange` is **450°**, map each target look angle to the **shortest rotator command** in **0–450**, not always `az mod 360`.
- Keep a **rotator azimuth state** (last commanded extended az) across ticks; for target `t` (0–360), consider candidates such as `t` and `t + 360` (clamped to max), pick minimum rotation from current.
- Optional: peek **next pass azimuth** (1–2 s ahead) to avoid choosing a branch that flips at the next update.

**Example**

| Compass az | Naive command | Smart command (at 350°) |
|------------|---------------|-------------------------|
| 10° (just W of N) | W010 → long way from 350° | W370 (10 + 360) → ~20° continue CW |
| 340° | W340 | W340 (short path from 350°) |

**Work**

- [x] `RotatorAzimuthPlanner` (Core): `ResolveCommandAz(lastCommandAz, targetAzDeg, maxAz)` → extended az 0–450.
- [x] Use in `RotatorController.TryTrack` instead of raw `look.AzimuthDeg`.
- [x] Reset extended az on new pass / disconnect / manual park.
- [x] Settings toggle: **Smart 450° azimuth** (default on when range is 450°).
- [x] Unit tests: north wrap NE→N→W sequence, 360-only mode unchanged.
- [x] Display: optional show **commanded** az vs **compass** az in sidebar if they differ.

**References:** `RotatorController`, `Gs232Rotator.SetPosition`, `RotatorSettings.MaxAzimuthDeg`, `rotator.py` (clamp only today).

### Other rotator improvements

- [ ] **Slew lead / mechanical lag** — command az/el slightly ahead of current look angle (rotators lag vs 1 s position updates).
- [ ] **Stop on park / disconnect / app exit** — send GS-232 `S` when parking, disconnecting, or closing (present in `rotator.py`, not wired in app lifecycle).
- [ ] Optional: configurable **minimum move** / **max slew rate** to reduce wear on marginal installs.

---

## Operations & UX

- [ ] **COM port conflict guard** — verify rotator and rig cannot share the same COM port; clear blocking message in UI (partially exists — confirm and test).
- [ ] **Auto-focus satellite on pass** — when enabled sat rises above threshold, switch map overlay / focus to that spacecraft without manual click.
- [ ] **Align track-start elevations** — rotator default −3° vs rig default −70° in settings; document or offer unified “start tracking at” with per-device overrides.
- [ ] **Settings backup / export** — export/import `settings.json` (stations, offsets, rig/rotator) for portable ops or reinstall.
- [ ] **AOS/LOS rig behaviour** (optional) — auto pause CAT or park rotator at end of pass; configurable.

---

## Documentation

- [ ] **NOR vs REV** — one page: database tag, doppler sign on Radio row, offset sign (positive TX on REV lowers Radio TX), knob pairing.
- [ ] **Settings vs repo database** — editing repo `satellite_database.json` vs `%AppData%/OscarWatch/satellite_database.json` vs bundled asset vs future remote sync.
- [ ] **Offset storage** — `frequencySelections`, Remember checkbox, per-mode `modeOffsets`.
- [ ] **Doppler behaviour matrix** — link from README; NOR/REV, offsets, CAT timing, interactive VFO.
- [ ] **Pre-pass checklist** — rebuild, Remember offsets, overlay vs sidebar freqs, Pause CAT, linear threshold.

---

## Larger projects (lower priority)

- [ ] **Web / remote API** — optional status and remote control over HTTP/WebSocket.
- [ ] **WSJT-X / grid tracker / audio** integration.
- [ ] **Pass recording** — log freqs + az/el + range rate over time for post-pass review.
- [ ] **Windows installer / auto-update** for releases.
- [ ] **Linux / macOS** field testing — rig CAT, rotator serial, voice announcements on non-Windows platforms.

---

## General

- [ ] Document workflow: editing repo `satellite_database.json` vs user AppData copy vs future remote sync (see **Documentation** above — consolidate when written).
