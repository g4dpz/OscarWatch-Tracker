# OscarWatch

Desktop satellite tracking for amateur radio operators. OscarWatch shows where AMSAT spacecraft are, predicts passes over your station, works out Doppler-corrected uplink and downlink frequencies, and can drive your rotator and radio during a pass — all from one map-centred window.

TLEs and the transponder frequency database are published from [tle.oscarwatch.org](https://tle.oscarwatch.org/) ([TLEs](https://tle.oscarwatch.org/), [transponder database](https://tle.oscarwatch.org/satellite_database.json)).

## Who is this for?

Licensed amateurs working **VHF/UHF satellites**: FM cubesats (SO-50, ISS, …), linear transponders (RS-44, FO-29, …), and similar modes. You should already be comfortable with pass times, azimuth, elevation, and Doppler; OscarWatch handles the maths and optional hardware control so you can focus on the contact.

You do **not** need to be a programmer to use published builds.

## What OscarWatch does

- **Map and sky plot** — subpoint, ground track, footprint, and a polar view from your QTH
- **Pass list** — upcoming passes with max elevation and time-to-AOS; sidebar scrolls on smaller windows
- **Frequency panel** — transponder modes from a built-in database, live uplink/downlink with Doppler, RX offsets (separate for Voice and CW on linear SSB), and CTCSS (access/arm tones). Keyboard shortcuts: [help/keyboard-shortcuts.html](help/keyboard-shortcuts.html) (**Ctrl+W**, numpad **+** / **−** for RX offset, map arrows, etc.)
- **Optional automation** — serial **rotator** tracking and **radio CAT** (Doppler, satellite/split layout, tones) during a pass
- **Optional extras** — voice “satellite rising” alerts, pass **WAV recording**, **Cloudlog** frequency sync

OscarWatch does **not** decode telemetry or replace your logging software; it is a pass-tracking and station-assist tool for the shack or field.

## Getting started

### Download

Pre-built packages for Windows, macOS, and Linux are on the [**Releases**](https://github.com/magicbug/OscarWatch-Tracker/releases) page (see [Cross-platform publish](#cross-platform-publish) for platform names). Extract the archive for your OS and run `OscarWatch`.

**macOS (first install):** release builds are not code-signed or notarized. macOS may block the app or bundled native libraries on first use:

1. **OscarWatch** — if Finder says the app “cannot be opened”, right-click `OscarWatch` → **Open** once, or use **System Settings → Privacy & Security → Open Anyway**.
2. **Pass recording** — if you use automatic WAV capture, macOS may also ask you to allow **`libportaudio.dylib`** (in `runtimes/osx-*/native/` inside the app folder). Approve it the same way when prompted.
3. **Microphone** — allow microphone access when you first enable recording in Settings.

These prompts are usually one-time per install. Tracking, passes, and radio/rotator control work without pass recording if you skip step 2.

To build from source instead, see [Build and run](#build-and-run) below.

### First-time setup

1. Open **Settings → Station** and enter your latitude, longitude, and grid square.
2. **Satellites → Select satellites** — enable the spacecraft you plan to work.
3. **Satellites → Refresh TLEs** — refresh at least once per operating day (or enable auto-update under **Settings → Tracking**).
4. If you use a rig or rotator: **Settings → Radio** and **Settings → Rotator** — set COM ports (rig and rotator must use **different** ports).
5. Click a satellite on the map or in the pass list to **focus** it — live az/el and frequencies apply to the focused pass.

### During a pass

1. Confirm the correct **transponder mode** in the frequency panel on the map (e.g. FM voice, Mode B USB/LSB). On linear SSB modes, use **Voice** / **CW** in the panel title bar (or **Ctrl+W**) — see [frequencies help](help/frequencies.html).
2. Watch **azimuth** and **elevation** in the sidebar — point your antenna there (or let the rotator track if enabled).
3. Set your radio from the **Radio** / **Sat** columns in the overlay, or enable CAT so OscarWatch updates frequencies for Doppler.
4. On FM satellites, pick the correct **CTCSS** tone when access and arm are both listed.

### Standby (browsing only)

Press **Standby** in the sidebar when you are only planning or browsing: the rotator parks, CAT pauses, and accidental tracking stops. Press **Resume** before a real pass. While in Standby, menu **Rotator** opens manual az/el control for a quick contact between passes.

### Operator guide

Plain-language help ships with the app: **Help → Operator guide** (also in the [`help/`](help/) folder). Topics include quick start, frequencies, TLEs, pass planning, radio/rotator setup, recording, and troubleshooting.

---

## Features

- **World map** — equirectangular Earth texture with satellite subpoint, ground track, footprint overlays (optional motion arrows), and correct rendering near the poles
- **Sky plot** — polar view of satellite azimuth/elevation relative to your station; click to focus; expand/collapse state is remembered
- **TLE catalog** — fetched from `https://tle.oscarwatch.org/`, cached under `%AppData%/OscarWatch/`
- **TLE auto-update** — manual refresh, on startup (if stale), or every 6 hours while running (Settings → Tracking)
- **Satellite picker** — choose which spacecraft to track
- **Pass predictions** — upcoming passes with TCA (time of closest approach / max elevation), min-elevation and min-duration filters
- **Pass planner** — multi-station profiles (home / portable), pass quality filters, and `.ics` calendar export for contest or field-day planning
- **Mutual pass finder** — find passes visible from two stations at once (Passes → Mutual pass finder)
- **Live telemetry** — azimuth, elevation, range, and altitude updated every second (UTC)
- **Voice announcements** — optional spoken “rising” alerts when a satellite crosses a configurable elevation while ascending (e.g. “Alpha Oscar Zero Seven is rising”); Settings → Voice
- **Pass recording** — optional automatic WAV capture from a line-in or USB audio device while the **focused** satellite is above configurable elevation thresholds; Settings → Recording. Files save to `%AppData%/OscarWatch/recordings/` by default as `{sat-name}-{yy}-{MM}-{dd}-{HH}-{mm}.wav` (UTC). A red **REC** badge appears on the pass row while recording.
- **Doppler frequencies** — draggable overlay on the world map with transponder modes from the satellite database, live radio/sat uplink & downlink, RX offsets (separate stored values for Voice and CW on linear SSB), CTCSS (access/arm), and **Voice/CW** toggle for linear SSB (header buttons + **Ctrl+W**; CAT/Cloudlog follow **Settings → Radio → Linear CW: keep receive in USB/LSB**)
- **Transponder database editor** — Satellites → Manage transponder database… (add satellites from your **TLE catalog** or a custom name, **Import/Export JSON**, edit modes); **Satellites → Update transponder database…** merges published modes from [tle.oscarwatch.org/satellite_database.json](https://tle.oscarwatch.org/satellite_database.json) (new entries added with your consent; local edits kept on conflicts unless you accept remote). See [documents/satellite-database.md](documents/satellite-database.md)
- **Radio CAT** — doppler tracking, satellite/split setup, Main/Sub VFOs, uplink CTCSS where supported; Settings → Radio (see [Supported hardware](#supported-hardware))
- **Rotator control** — serial pass tracking, manual park, and **manual rotator** (az/el dialog in Standby for quick contacts between passes); Settings → Rotator (see [Supported hardware](#supported-hardware))
- **Cloudlog** — optional Radio API v2 uplink/downlink when tracking (Settings → Cloudlog)
- **Appearance** — light, dark, or system theme (sky plot adapts; world map image stays light); optional footprint motion arrows on the map

## Supported hardware

OscarWatch talks to rigs and rotators over **serial CAT** (COM port on Windows, device path on Linux/macOS). Rig and rotator must use **different** ports.

### Radios

| Radio | Protocol | Notes |
|-------|----------|--------|
| **ICOM IC-910** | CI-V | Cross-band: satellite mode, Main/Sub, Sub uplink CTCSS. Receive-only (uplink 0): SAT off, downlink on Main |
| **ICOM IC-9100** | CI-V | Same as IC-9700; default CI-V address `7C` |
| **ICOM IC-9700** | CI-V | Same layout as IC-910 |
| **Yaesu FT-847** | Yaesu CAT | Satellite mode, SAT RX/TX VFOs, doppler, uplink CTCSS |
| **Kenwood TS-2000** | Kenwood ASCII CAT | **Beta** — cross-band SATL; enable SATL and turn TRACE off on the radio |
| **Dummy rig** | — | No serial I/O; for UI and doppler testing without a radio |

All tracked rigs: linear NOR/REV doppler, interactive Main VFO tuning, TX/RX offset spinners, configurable CAT thresholds and pause.

More rigs: see [TODO.md](TODO.md) and [building radio drivers](documents/building-radio-drivers.md).

### Rotators

| Controller | Protocol | Notes |
|------------|----------|--------|
| **Yaesu GS-232** | GS-232 | Yaesu rotators and many GS-232 clones |
| **EasyComm** | EasyComm II | SPID, M2, and other EasyComm-compatible controllers |

Pass tracking when elevation is above the track-start threshold; manual **Park** in the sidebar; **manual rotator** in Standby (menu **Rotator…** — set az/el, Rotate, Stop, Park for a quick contact without resuming pass tracking). Azimuth range **360°** or **450°** (e.g. G-5500). On **450°** rotators, optional **smart azimuth** chooses 361–450° commands for the shortest path across north (Settings → Rotator). Optional **calibration offsets** correct pass tracking and manual moves; park uses your configured park az/el exactly.

More controllers: [building rotator drivers](documents/building-rotator-drivers.md).

## Settings

Open **Settings** from the menu. Tabs:

| Tab | Purpose |
|-----|---------|
| **Station** | Display name, latitude/longitude, Maidenhead grid square, altitude ASL |
| **Tracking** | Minimum pass elevation, prediction window, TLE auto-update, transponder database check on startup |
| **Appearance** | Light / dark / system theme; footprint motion arrows on/off |
| **Voice** | Enable announcements, trigger elevation (default −3°), voice selection, test button |
| **Recording** | Automatic pass WAV capture, input device, start/stop elevation, output folder, test clip |
| **Rotator** | Type (GS-232 / EasyComm), COM port, 360°/450° azimuth, smart 450°, park, track-start elevation, calibration offsets |
| **Radio** | Rig type, COM port, region (Icom), CI-V address, linear CW receive mode (USB/LSB vs CW on both VFOs), Doppler CAT thresholds (FM default 350 Hz, SSB/CW default 50 Hz — see [help](help/radio-rotator.html#doppler-cat-thresholds)), pause CAT |
| **Cloudlog** | Base URL, API key, radio name, test connection; posts SAT uplink/downlink when tracking |

Settings are stored in `%AppData%/OscarWatch/settings.json`.

Planned work is listed in [TODO.md](TODO.md). Transponder database merge/sync is documented in [documents/satellite-database.md](documents/satellite-database.md).

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
| **macOS** | Core Audio devices; see [macOS (first install)](#download) if `libportaudio.dylib` is blocked; grant microphone permission when prompted |
| **Linux** | Requires PulseAudio or ALSA; install `libasound2` / PulseAudio as needed for your distro |

WAV files are uncompressed (~5 MB/min mono at 44.1 kHz). Use an external tool if you need MP3 later.

## Settings location

| File | Path |
|------|------|
| Settings | `%AppData%/OscarWatch/settings.json` |
| Recordings | `%AppData%/OscarWatch/recordings/` |
| TLE cache | `%AppData%/OscarWatch/tle-cache.txt` |
| Transponder DB (user) | `%AppData%/OscarWatch/satellite_database.json` |
| Transponder DB (remote) | [tle.oscarwatch.org/satellite_database.json](https://tle.oscarwatch.org/satellite_database.json) |
| Logs | `%AppData%/OscarWatch/logs/` (daily rolling `oscarwatch-YYYYMMDD.log`, 14 days retained) |

Open the recordings or log folder from **Help → Open recordings folder** or **Help → Open logs folder**. Unhandled crashes and rig/rotator/CAT errors are written to logs (not API keys).

## Contributing

Pull requests are welcome when they add something valuable to the **core** of OscarWatch — bug fixes, rig or rotator drivers, tracking behaviour, operator-facing features, and **transponder database** entries (new satellites/modes or corrections with a credible source). For larger changes, open an issue first so direction can be agreed before you invest time in a big diff.

Transponder data is also published at [tle.oscarwatch.org/satellite_database.json](https://tle.oscarwatch.org/satellite_database.json); see [documents/satellite-database.md](documents/satellite-database.md).

## Support

OscarWatch is **free** and open source. If you find it useful, you can support ongoing development via [GitHub Sponsors](https://github.com/sponsors/magicbug) or [PayPal](https://www.paypal.com/paypalme/PGoodhall). If you donate, please let Peter know your **callsign** so you can be thanked.

---

## For developers

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build and run

```bash
dotnet build OscarWatch.slnx
dotnet run --project OscarWatch/OscarWatch.csproj
```

For lower memory use, prefer **Release** (`-c Release`). Debug builds include Avalonia diagnostics overhead.

```bash
dotnet run -c Release --project OscarWatch/OscarWatch.csproj
```

### Cross-platform publish

#### GitHub Actions

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

### Project structure

| Project | Role |
|---------|------|
| `OscarWatch.Core` | Models, TLE parser, settings, Maidenhead grid, orbit interfaces, voice/phonetics |
| `OscarWatch.Orbit` | OrbitTools adapters, pass predictor, ground geometry |
| `OscarWatch` | Avalonia UI |

### Developer documentation

- [documents/](documents/) — how to add **radio** and **rotator** drivers (`IRigDriver`, `IRotatorDriver`); [satellite transponder database](documents/satellite-database.md) (remote updates, merge policy, schema)
- [help/](help/) — operator HTML help (bundled with the app)
- [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) — UI contrast, colour-blind-safe tracking colours, keyboard

### Orbit propagation

Uses [OrbitTools](http://www.zeptomoby.com/satellites/) Public Edition via [Zeptomoby.OrbitTools.Orbit](https://www.nuget.org/packages/Zeptomoby.OrbitTools.Orbit) (non-commercial). For commercial use or the Track Library (pass engine, iso-elevation footprints), obtain a [Professional license](http://www.zeptomoby.com/satellites/editionInfo.htm).

---

## License

Copyright © 2026 **Peter Goodhall (MM9SQL)**.

OscarWatch is free software: you can redistribute it and/or modify it under the terms of the [GNU Affero General Public License v3.0 or later](https://www.gnu.org/licenses/agpl-3.0.html) (AGPL-3.0-or-later). See [LICENSE.md](LICENSE.md) for the full license text.

If you run a modified version as a network service, AGPL requires making the corresponding source available to users who interact with it over the network.

Third-party components (OrbitTools, Avalonia, TLE sources, map imagery, etc.) have their own licenses — see [CREDITS.md](CREDITS.md) and the Orbit propagation section above.

## Credits

See [CREDITS.md](CREDITS.md).

## Supporters

Donors who have helped fund development are listed in [supporters.md](supporters.md).
