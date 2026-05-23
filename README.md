# OscarWatch

Desktop satellite tracking for amateur radio operators. Track passes, live azimuth/elevation, ground tracks, and visibility footprints on a world map using TLEs from [tle.oscarwatch.org](https://tle.oscarwatch.org/).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Build and run

```bash
dotnet build OscarWatch.slnx
dotnet run --project OscarWatch/OscarWatch.csproj
```

For lower memory use, prefer **Release** (`-c Release`). Debug builds include Avalonia diagnostics overhead.

```bash
dotnet run -c Release --project OscarWatch/OscarWatch.csproj
```

## Features

- **World map** — equirectangular Earth texture with satellite subpoint, ground track, and visibility footprint overlays (including correct rendering near the poles)
- **Sky plot** — polar view of satellite azimuth/elevation relative to your station; click to focus a spacecraft on the map
- **TLE catalog** — fetched from `https://tle.oscarwatch.org/`, cached under `%AppData%/OscarWatch/`
- **TLE auto-update** — manual refresh, on startup (if stale), or every 6 hours while running (Settings → Tracking)
- **Satellite picker** — choose which spacecraft to track
- **Pass predictions** — upcoming passes with TCA (time of closest approach / max elevation), min-elevation and min-duration filters
- **Pass planner** — multi-station profiles (home / portable), pass quality filters, and `.ics` calendar export for contest or field-day planning
- **Mutual pass finder** — find passes visible from two stations at once (Passes → Mutual pass finder)
- **Live telemetry** — azimuth, elevation, range, and altitude updated every second (UTC)
- **Voice announcements** — optional spoken “rising” alerts when a satellite crosses a configurable elevation while ascending (e.g. “Alpha Oscar Zero Seven is rising”); Settings → Voice
- **Doppler frequencies** — draggable overlay on the world map with transponder modes from the satellite database, live radio/sat uplink & downlink, TX/RX offsets, and CTCSS (access/arm)
- **Transponder database editor** — Satellites → Manage transponder database… (add/edit satellites and modes; saved under `%AppData%/OscarWatch/satellite_database.json`)
- **Radio CAT** — ICOM IC-910 / IC-9700 via CI-V (doppler tracking, satellite mode, Main/Sub VFOs, Sub uplink CTCSS); Settings → Radio
- **Appearance** — light, dark, or system theme (sky plot adapts; world map image stays light)

## Settings

Open **Settings** from the menu. Tabs:

| Tab | Purpose |
|-----|---------|
| **Station** | Display name, latitude/longitude, Maidenhead grid square, altitude ASL |
| **Tracking** | Minimum pass elevation, prediction window, TLE auto-update mode |
| **Appearance** | Light / dark / system theme |
| **Voice** | Enable announcements, trigger elevation (default −3°), voice selection, test button |
| **Rotator** | GS-232 serial rotator (manual + pass tracking) |
| **Radio** | IC-910 / IC-9700 CI-V (COM port, region, doppler thresholds, pause CAT) |
| **Cloudlog** | Radio API URL, API key, radio name (default OscarWatch); posts SAT uplink/downlink when tracking |

Settings are stored in `%AppData%/OscarWatch/settings.json`.

Planned work (including deferred **remote transponder-database sync/merge**) is listed in [TODO.md](TODO.md).

### Voice announcements (platform notes)

| Platform | Text-to-speech |
|----------|----------------|
| **Windows** | Built-in (`System.Speech`) |
| **macOS** | Built-in (`/usr/bin/say`) |
| **Linux** | Requires `espeak-ng` (preferred) or `spd-say` on `PATH` |

If TTS is unavailable, the Voice tab shows a notice and announcements are skipped.

## Settings location

| File | Path |
|------|------|
| Settings | `%AppData%/OscarWatch/settings.json` |
| TLE cache | `%AppData%/OscarWatch/tle-cache.txt` |
| Logs | `%AppData%/OscarWatch/logs/` (daily rolling `oscarwatch-YYYYMMDD.log`, 14 days retained) |

Open the log folder from **Help → Open logs folder**. Unhandled crashes and rig/rotator/CAT errors are written here (not API keys).

## Cross-platform publish

### GitHub Actions

Two workflows — different jobs:

| Workflow | When it runs | What you get |
|----------|----------------|--------------|
| [**CI**](.github/workflows/build.yml) | Every push / PR to `main` | Build + tests only (one Linux job, no downloads) |
| [**Publish**](.github/workflows/publish.yml) | Manual run, or tag `v*` | Installable packages per platform |

**Publish** artifacts:

| Artifact | Runtime |
|----------|---------|
| `OscarWatch-win-x64` | Windows x64 |
| `OscarWatch-osx-arm64` | macOS Apple Silicon |
| `OscarWatch-osx-x64` | macOS Intel |
| `OscarWatch-linux-x64` | Linux x64 |
| `OscarWatch-linux-arm64` | Linux ARM64 (Raspberry Pi 64-bit OS) |

- **Manual run:** Actions → **Publish** → **Run workflow**
- **Release:** `git tag v1.0.0 && git push origin v1.0.0` → GitHub Release with all archives

### Local publish

```bash
# Windows
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r win-x64 --self-contained

# macOS (Apple Silicon)
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r osx-arm64 --self-contained

# macOS (Intel)
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r osx-x64 --self-contained

# Linux x64
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r linux-x64 --self-contained

# Raspberry Pi (64-bit OS)
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r linux-arm64 --self-contained
```

Publish profiles are under `OscarWatch/Properties/PublishProfiles/` (e.g. `dotnet publish -p:PublishProfile=win-x64`).

## Orbit propagation

Uses [OrbitTools](http://www.zeptomoby.com/satellites/) Public Edition via [Zeptomoby.OrbitTools.Orbit](https://www.nuget.org/packages/Zeptomoby.OrbitTools.Orbit) (non-commercial). For commercial use or the Track Library (pass engine, iso-elevation footprints), obtain a [Professional license](http://www.zeptomoby.com/satellites/editionInfo.htm).

## Project structure

| Project | Role |
|---------|------|
| `OscarWatch.Core` | Models, TLE parser, settings, Maidenhead grid, orbit interfaces, voice/phonetics |
| `OscarWatch.Orbit` | OrbitTools adapters, pass predictor, ground geometry |
| `OscarWatch` | Avalonia UI |

## Developer docs

- [documents/](documents/) — how to add **radio** and **rotator** drivers (`IRigDriver`, `IRotatorDriver`)

## Accessibility

UI work should follow [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) (contrast, color-blind-safe tracking colors, keyboard, no color-only meaning).

## License

Copyright © 2025–2026 **Peter Goodhall (MM9SQL)**.

OscarWatch is free software: you can redistribute it and/or modify it under the terms of the [GNU Affero General Public License v3.0 or later](https://www.gnu.org/licenses/agpl-3.0.html) (AGPL-3.0-or-later). See [LICENSE.md](LICENSE.md) for the full license text.

If you run a modified version as a network service, AGPL requires making the corresponding source available to users who interact with it over the network.

Third-party components (OrbitTools, Avalonia, TLE sources, map imagery, etc.) have their own licenses — see [CREDITS.md](CREDITS.md) and the Orbit propagation section above.

## Credits

See [CREDITS.md](CREDITS.md).
