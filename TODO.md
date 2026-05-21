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

## Radio / rig

**Shipped today:** ICOM IC-910, IC-9700 (CI-V), dummy rig.

### Additional rig drivers (investigate & implement)

- [ ] **ICOM IC-9100** — CI-V (closely related to IC-910 / IC-9700; confirm satellite mode, Main/Sub, tone commands, and default CI-V address vs IC-910H).
- [ ] **Yaesu FT-847** — Yaesu CAT serial protocol; satellite/split/VFO layout and doppler update rate limits.
- [ ] **Yaesu FT-817** — Yaesu CAT (817/817ND variants); often single-VFO or simple split — map to Main/Sub or A/B model in `RigController`.
- [ ] **Kenwood TS-2000** — Kenwood CAT; satellite operation and sub-tone support per manual.

**Per-driver work (each rig)**

- Protocol client (bytes, timeouts, no retry on doppler writes where needed).
- `IRigDriver` implementation + `RigType` + Settings radio list.
- Pass init: satellite/split, VFO select, mode, CTCSS on uplink VFO where applicable.
- Hook into `RigController` (thresholds, Main/Sub vs A/B, region tone vs tone-squelch if relevant).
- Golden/unit tests where possible; hardware smoke test on a real pass.

**References to check**

- QTrig `lib/icom.py` (ICOM only today).
- Manufacturer CAT manuals / Hamlib rig caps (good cross-check for command sets).
- OscarWatch: `OscarWatch/Rig/`, `RigDriverFactory`, `RigSettings.DefaultCivAddressFor` (ICOM hex) — Yaesu/Kenwood will need address/baud fields or rig-specific settings.

### ICOM (existing)

- [ ] IC-9700: validate CAT on hardware (satellite mode, Main/Sub, CTCSS on Sub, doppler).
- [ ] Optional: RIT CI-V on IC-9700 if interactive VFO tuning needs it later.

---

## Transponder database editor (follow-ups)

- [ ] Import / export JSON (pick file path).
- [ ] “Open database folder” in Explorer/Finder.
- [ ] Pick satellite name from TLE catalog when adding an entry (name alignment).

---

## Cloudlog

- [x] Radio API v2 (`/index.php/api/radio`) on satellite select / frequency update (Settings → Cloudlog).
- [ ] Optional: Wavelog-specific testing; legacy satellite JSON fields (`uplink_freq`, `satmode`) if needed for older installs.

---

## General

- [ ] Document workflow: editing repo `satellite_database.json` vs user AppData copy vs future remote sync.
