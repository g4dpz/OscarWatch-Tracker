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
- **Pass recording** — optional automatic WAV capture from a line-in or USB audio device while the **focused** satellite is above configurable elevation thresholds; Settings → Recording. Files save to `%AppData%/OscarWatch/recordings/` by default as `{sat-name}-{yy}-{MM}-{dd}-{HH}-{mm}.wav` (UTC). A red **REC** badge appears on the pass row while recording.
- **Doppler frequencies** — draggable overlay on the world map with transponder modes from the satellite database, live radio/sat uplink & downlink, TX/RX offsets, and CTCSS (access/arm)
- **Transponder database editor** — Satellites → Manage transponder database… (add/edit satellites and modes; saved under `%AppData%/OscarWatch/satellite_database.json`)
- **Radio CAT** — doppler tracking, satellite/split setup, Main/Sub VFOs, uplink CTCSS where supported; Settings → Radio (see [Supported hardware](#supported-hardware))
- **Rotator control** — serial pass tracking and manual park; Settings → Rotator (see [Supported hardware](#supported-hardware))
- **Cloudlog** — optional Radio API v2 uplink/downlink when tracking (Settings → Cloudlog)
- **Appearance** — light, dark, or system theme (sky plot adapts; world map image stays light)

## Supported hardware

OscarWatch talks to rigs and rotators over **serial CAT** (COM port on Windows, device path on Linux/macOS). Rig and rotator must use **different** ports.

### Radios

| Radio | Protocol | Notes |
|-------|----------|--------|
| **ICOM IC-910** | CI-V | Satellite mode, Main/Sub VFOs, Sub uplink CTCSS |
| **ICOM IC-9100** | CI-V | **Beta** — same CI-V layout as IC-9700; default CI-V address `7C` |
| **ICOM IC-9700** | CI-V | Same layout as IC-910 |
| **Yaesu FT-847** | Yaesu CAT | **Beta** — SAT RX/TX VFOs; verify on your hardware |
| **Kenwood TS-2000** | Kenwood ASCII CAT | **Beta** — cross-band SATL; enable SATL and turn TRACE off on the radio |
| **Dummy rig** | — | No serial I/O; for UI and doppler testing without a radio |

All tracked rigs: linear NOR/REV doppler, interactive Main VFO tuning, TX/RX offset spinners, configurable CAT thresholds and pause.

More rigs: see [TODO.md](TODO.md) and [building radio drivers](documents/building-radio-drivers.md).

### Rotators

| Controller | Protocol | Notes |
|------------|----------|--------|
| **Yaesu GS-232** | GS-232 | Yaesu rotators and many GS-232 clones |
| **EasyComm** | EasyComm II | SPID, M2, and other EasyComm-compatible controllers |

Pass tracking when elevation is above the track-start threshold; manual **Park**; azimuth range **360°** or **450°** (e.g. G-5500). On **450°** rotators, optional **smart azimuth** chooses 361–450° commands for the shortest path across north (Settings → Rotator).

More controllers: [building rotator drivers](documents/building-rotator-drivers.md).

## Settings

Open **Settings** from the menu. Tabs:

| Tab | Purpose |
|-----|---------|
| **Station** | Display name, latitude/longitude, Maidenhead grid square, altitude ASL |
| **Tracking** | Minimum pass elevation, prediction window, TLE auto-update mode |
| **Appearance** | Light / dark / system theme |
| **Voice** | Enable announcements, trigger elevation (default −3°), voice selection, test button |
| **Rotator** | Type (GS-232 / EasyComm), COM port, 360°/450° azimuth, smart 450°, park, track-start elevation |
| **Radio** | Rig type, COM port, region (Icom), CI-V address, doppler thresholds, pause CAT |
| **Cloudlog** | Base URL, API key, radio name, test connection; posts SAT uplink/downlink when tracking |

Settings are stored in `%AppData%/OscarWatch/settings.json`.

Planned work (including deferred **remote transponder-database sync/merge**) is listed in [TODO.md](TODO.md).

### Voice announcements (platform notes)

| Platform | Text-to-speech |
|----------|----------------|
| **Windows** | Built-in (`System.Speech`) |
| **macOS** | Built-in (`/usr/bin/say`) |
| **Linux** | Requires `espeak-ng` (preferred) or `spd-say` on `PATH` |

If TTS is unavailable, the Voice tab shows a notice and announcements are skipped.

### Pass recording (platform notes)

Audio capture uses [PortAudio](https://www.portaudio.com/) (via PortAudioSharp2). Native runtimes are included in published builds for Windows x64, macOS (Intel/Apple Silicon), and Linux x64/ARM64.

| Platform | Notes |
|----------|--------|
| **Windows** | Select your radio interface or sound card input in Settings → Recording |
| **macOS** | Core Audio devices; grant microphone permission if prompted |
| **Linux** | Requires PulseAudio or ALSA; install `libasound2` / PulseAudio as needed for your distro |

WAV files are uncompressed (~5 MB/min mono at 44.1 kHz). Use an external tool if you need MP3 later.

## Settings location

| File | Path |
|------|------|
| Settings | `%AppData%/OscarWatch/settings.json` |
| Recordings | `%AppData%/OscarWatch/recordings/` |
| TLE cache | `%AppData%/OscarWatch/tle-cache.txt` |
| Transponder DB (user) | `%AppData%/OscarWatch/satellite_database.json` |
| Logs | `%AppData%/OscarWatch/logs/` (daily rolling `oscarwatch-YYYYMMDD.log`, 14 days retained) |

Open the log folder from **Help → Open logs folder**. Unhandled crashes and rig/rotator/CAT errors are written here (not API keys).

## Operator help (HTML)

Plain-language guides for satellite operators live in the [`help/`](help/) folder (UK English). In the app, open **Help → Operator guide** to read them in your browser.

Topics include quick start, the map and sidebar, frequencies, TLEs, pass planning, radio/rotator setup, settings, and troubleshooting.

## Cross-platform publish

### GitHub Actions

[**Publish**](.github/workflows/publish.yml) runs on a version tag (`v*`) or a manual workflow dispatch. It builds and tests on Linux, then publishes installable packages per platform.

**Artifacts:**

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
