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

- **World map** — equirectangular Earth texture with satellite subpoint, ground track, and footprint overlays
- **TLE catalog** — fetched from `https://tle.oscarwatch.org/`, cached under `%AppData%/OscarWatch/`
- **Satellite picker** — choose which spacecraft to track
- **Ground station settings** — latitude, longitude, Maidenhead grid square, altitude ASL
- **Appearance** — light, dark, or system theme (sky plot adapts; world map image stays light)
- **Pass predictions** — upcoming passes with TCA (time of closest approach / max elevation), min-elevation and min-duration filters
- **Pass planner** — multi-station profiles (home / portable), pass quality filters, and `.ics` calendar export for contest or field-day planning
- **Live telemetry** — azimuth, elevation, range updated every second (UTC)

## Settings location

| File | Path |
|------|------|
| Settings | `%AppData%/OscarWatch/settings.json` |
| TLE cache | `%AppData%/OscarWatch/tle-cache.txt` |

## Cross-platform publish

```bash
# Windows
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r win-x64 --self-contained

# macOS (Apple Silicon)
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r osx-arm64 --self-contained

# Linux x64
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r linux-x64 --self-contained

# Raspberry Pi (64-bit OS)
dotnet publish OscarWatch/OscarWatch.csproj -c Release -r linux-arm64 --self-contained
```

Publish profiles are also available under `OscarWatch/Properties/PublishProfiles/`.

## Orbit propagation

Uses [OrbitTools](http://www.zeptomoby.com/satellites/) Public Edition via [Zeptomoby.OrbitTools.Orbit](https://www.nuget.org/packages/Zeptomoby.OrbitTools.Orbit) (non-commercial). For commercial use or the Track Library (pass engine, iso-elevation footprints), obtain a [Professional license](http://www.zeptomoby.com/satellites/editionInfo.htm).

## Project structure

| Project | Role |
|---------|------|
| `OscarWatch.Core` | Models, TLE parser, settings, Maidenhead grid, orbit interfaces |
| `OscarWatch.Orbit` | OrbitTools adapters, pass predictor, ground geometry |
| `OscarWatch` | Avalonia UI |

## Accessibility

UI work should follow [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) (contrast, color-blind-safe tracking colors, keyboard, no color-only meaning).

## Credits

See [CREDITS.md](CREDITS.md).
