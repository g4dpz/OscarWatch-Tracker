# Credits

## Earth imagery

The default `OscarWatch/Assets/Maps/world_map.jpg` is an equirectangular Blue Marble–style texture suitable for map overlay rendering. Replace with NASA Marble `world_map.jpg` from KDE Marble if you prefer that asset locally.

- NASA Blue Marble / Visible Earth imagery — public domain ([NASA Earth Observatory](https://earthobservatory.nasa.gov/))
- KDE Marble project — map tiling and `world_map.jpg` convention ([Marble](https://marble.kde.org/))

## Audio

- [PortAudio](https://www.portaudio.com/) via PortAudioSharp2 — cross-platform capture for pass recording
- Optional [ffmpeg](https://ffmpeg.org/) on PATH — converts finished pass recordings to MP3 with libmp3lame when File format is MP3 (not bundled)

## Orbit propagation

- [OrbitTools](http://www.zeptomoby.com/satellites/) by Michael F. Henry — NORAD SGP4/SDP4 (Public Edition via NuGet for non-commercial use)

## TLE data

- Amateur satellite TLEs from [tle.oscarwatch.org](https://tle.oscarwatch.org/)

## UI framework

- [Avalonia UI](https://avaloniaui.net/)

## Localisation

- **Igor Monteiro (PU4ELT)** — Brazilian Portuguese (`pt-BR`) user interface localisation
- **Carlos (EA3HAH)** — Spanish (`es`) user interface localisation (newer strings completed with AI assistance)

## Hardware testing

- **Abdel (M0NPT)** and **Joe (KE9AJ)** — Yaesu FT-847 CAT driver
