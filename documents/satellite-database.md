# Satellite transponder database

OscarWatch keeps **orbits** (TLEs) and **frequencies/modes** (transponder database) separate. This document describes where that data lives, how updates are published, and how in-app sync works.

## Data sources

| Layer | Location | Role |
|-------|----------|------|
| **Remote** | [tle.oscarwatch.org/satellite_database.json](https://tle.oscarwatch.org/satellite_database.json) | Canonical published updates — new satellites and new/edited transponder modes |
| **Bundled** | `OscarWatch/Assets/satellite_database.json` | Shipped with each app release; fallback when no user file exists |
| **User** | `%AppData%/OscarWatch/satellite_database.json` | Your working copy after first edit or after a merge from the remote URL |

TLEs are fetched from the same host: [tle.oscarwatch.org](https://tle.oscarwatch.org/).

## Current app behaviour

The app loads **user → bundled** (see `SatelliteDatabaseService`).

- **Satellites → Update transponder database…** (or **Check for updates** in the database editor) downloads the remote file and opens a merge dialog.
- **Settings → Tracking → Check for transponder database updates on startup** (on by default) runs the same check when the app opens; the dialog appears only when updates exist.
- New satellites and modes from the server are **selected by default**; you can uncheck any before applying.
- **Conflicts** (same satellite name and mode type, different fields) keep your local copy unless you tick **Use published version**.
- **Restore defaults** deletes the user file and reloads the **bundled** database from the installation — not the remote URL.

## JSON schema (per mode)

```json
{
  "name": "SO-50",
  "modes": [
    {
      "type": "FM VOICE",
      "downlink": 436795,
      "uplink": 145850,
      "downlink_mode": "FMN",
      "uplink_mode": "FMN",
      "doppler": "NOR",
      "ctcss": 67.0,
      "ctcss_arm": 74.4
    }
  ]
}
```

| Field | Notes |
|-------|--------|
| `name` | Must match the TLE catalogue name closely enough for lookup (see aliases in `SatelliteDatabaseService`) |
| `type` | Label shown in the frequency panel mode list; unique per satellite |
| `downlink` / `uplink` | kHz; use **`0` uplink** for beacon-only / receive-only (SSTV, telemetry, CW beacon) |
| `downlink_mode` / `uplink_mode` | Rig mode strings: `FM`, `FMN`, `USB`, `LSB`, `CW`, `DATA-USB`, `DATA-LSB`, … |
| `doppler` | `NOR` or `REV` |
| `ctcss` / `ctcss_arm` | Optional Hz; FM access and arm tones |

**Publishing rules:**

- Frequencies must be JSON **numbers**, not strings (`437350` not `"437350"`).
- Validate with the in-app editor or `SatelliteDatabaseFile.ValidateEntries` before publishing.
- Prefer stable `type` names so merges do not create duplicate modes after renames.

## Name matching and TLEs

The transponder database is keyed by **satellite name**, not NORAD ID. Names should align with [tle.oscarwatch.org](https://tle.oscarwatch.org/) TLE entries. OscarWatch registers common aliases (e.g. `AO-7` → `AO-07`, `ISS (ZARYA)` → `ISS`). If a spacecraft has TLEs but no database entry, the frequency panel stays empty until a entry exists locally or via sync.

## Contributing updates

1. **GitHub** — pull requests updating `satellite_database.json` / `OscarWatch/Assets/satellite_database.json` (and the repo root copy if kept in sync).
2. **Remote publish** — maintained copies on [tle.oscarwatch.org](https://tle.oscarwatch.org/) for operators between app releases.

Include a short note in the PR: source (official satellite page, operator reports, coordination with AMSAT/ARISS, etc.). Frequency changes on active spacecraft should reflect confirmed operating status, not rumour.

## Privacy and network

Remote sync is a **read-only HTTPS GET**. No station data, callsign, or settings are uploaded. Merge prompts are the consent gate for writing anything to `%AppData%`.

## Related code

| Piece | Path |
|-------|------|
| Load / lookup | `OscarWatch.Core/Services/SatelliteDatabaseService.cs` |
| JSON I/O | `OscarWatch.Core/Services/SatelliteDatabaseFile.cs` |
| Editor | `OscarWatch.Core/Services/SatelliteDatabaseEditor.cs` |
| UI | `OscarWatch/Views/SatelliteDatabaseWindow.axaml` |
| Remote URL constant | `SatelliteDatabasePaths.RemoteDatabaseUrl` |
