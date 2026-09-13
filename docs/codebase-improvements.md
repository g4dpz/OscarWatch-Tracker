# OscarWatch codebase improvements

Living engineering backlog for the desktop tracker. Pick items when you have time; this is not a commitment order and nothing here has to land before the next operator feature.

Product ideas (new rigs, SSTV, packet, pass-radar extras) stay in [`TODO.md`](../TODO.md). Live-loop performance work is tracked in [`optimization-roadmap.md`](optimization-roadmap.md). This file is about **making the current codebase easier to change, test, and ship without regressions**.

**Snapshot:** September 2026, app version **1.0.3**, .NET 10, Avalonia 11.3. About 800 C# files / 111k lines, ~1,540 automated tests (xUnit facts, theories, and FsCheck properties).

The June 2026 review in [`documents/improvements.md`](../documents/improvements.md) is a historical snapshot. Several items there are already done (PR CI, settings export/import, MutualPassFinder tests, `IcsPassExporter` tests, `SettingsService` tests). Use this file instead.

---

## How to use this list

Each item is meant to be a single PR-sized slice. When you start one, keep the change focused: extract or test that area, do not “while we are here” the neighbouring god class.

Suggested default order if you want a sequence: **settings mapping**, then **MainViewModel coordinators**, then **RigController policy types**. Those three reduce the blast radius of almost every later change.

---

## Already in good shape (do not redo)

- **Layering:** `OscarWatch.Core` (models, settings, logbook, orbit interfaces) / `OscarWatch.Orbit` (SGP4 adapter, pass predictor) / `OscarWatch` (Avalonia UI and hardware I/O). Native `IRigDriver` / `IRotatorDriver` instead of HamLib is the right call for satellite CAT.
- **Hardware tests:** CI-V, Yaesu, Kenwood TS-2000, Flex SmartSDR, dual-radio, and rotator planners are densely covered. `RigControllerTests` is large because the behaviour is large, not because tests are missing.
- **Orbit and tracking maths:** pass prediction, horizon mask, ground tracks, Doppler physics, `GetLiveGeometry` equivalence tests.
- **Live-loop CPU work:** combined live geometry, pass-predictor coarse sampling, map/timeline paint throttles, ground-track rebuild priority. Do **not** revive snapshot pooling or `SatelliteTrackState` object pools (rejected PRs #112 / #118). See [`optimization-branches.md`](optimization-branches.md).
- **Operator diagnostics:** Help → Copy diagnostics redacts Cloudlog / hams.at / satellite-status secrets (`DiagnosticsBundleBuilder`).
- **Settings durability:** atomic save, debounce, timestamped backups, import/export (`AppDataFileCommands`), first-run and corrupt-file handling in `SettingsService`.
- **Accessibility palette:** Okabe–Ito in `PlotColors.cs` plus [`docs/ACCESSIBILITY.md`](ACCESSIBILITY.md). Keep that path when touching map or sky-plot colour.
- **Locale key parity:** `ResxKeyParityTests` fails CI if `es` / `ja` / `pt-BR` / `zh-CN` omit or add keys versus British English `Strings.resx`.

---

## P0: ship-quality gates (days, high leverage)

### ~~P0.1 Enforce `.resx` key parity in CI~~ Done

`OscarWatch.Tests/ResxKeyParityTests.cs` copies `OscarWatch/Resources/Strings*.resx` into the test output and asserts every locale has the same `data name=` set as `Strings.resx`. `dotnet test` in `.github/workflows/ci.yml` runs it on every CI job.

When you add a UI string: put it in `Strings.resx` first, then the same key in all four locale files in the same change.

### ~~P0.2 Widen CI beyond “Ubuntu, PRs only”~~ Done

`.github/workflows/ci.yml` runs `dotnet build` and `dotnet test` on **Ubuntu** and **Windows** for pull requests to `main` and for pushes to `main`. That catches Windows-only compile paths (`System.IO.Ports`, `System.Speech`) before a release tag. Publish still packs six RIDs on tags.

Optional later: a macOS build-only CI job so PortAudio / `say` packaging does not wait until `publish.yml`.

### P0.3 Align NuGet versions and watch them

**Why:** `Microsoft.Data.Sqlite` is **10.0.9** while `Microsoft.Extensions.DependencyInjection`, `System.Speech`, and `System.IO.Ports` are **10.0.8**. There is no Dependabot (or equivalent) workflow. Drift is how you miss both security patches and “works on my machine” SDK mismatches.

**Do:** Align the Microsoft 10.0.x packages in one PR. Add Dependabot for `nuget` (weekly is enough) or a scheduled `dotnet list package --vulnerable` job.

**Done when:** the 10.0.x family matches, and outdated/vulnerable packages show up without someone remembering to check.

---

## P1: shrink the classes everyone has to touch (1–3 weeks each theme)

These three files are the maintenance bottleneck. Line counts have grown a lot since mid-2026.

| File | Lines (Sept 2026) | Role |
|------|-------------------|------|
| `OscarWatch/ViewModels/MainViewModel.cs` | ~3,700 | Startup, timers, map time, passes, rig/rotator UI, recording, speech, Cloudlog, Satellite Link, hams.at, updates, community status, window openers |
| `OscarWatch/ViewModels/SettingsViewModel.cs` | ~2,700 | Every Settings tab, huge Load/Save field copy, hint flags, hardware choice lists |
| `OscarWatch/Rig/RigController.cs` | ~2,650 | Pass init, Doppler, dial capture, dual-radio, CTCSS, FM companion |

`FlexSmartSdrClient.cs` (~2,340) and `WorldMapControl.cs` (~1,430) are next, but they are more contained. Split the three above first.

### P1.1 Split `MainViewModel` into coordinators

**Why:** Almost every feature adds a timer, a status string, or a `DispatcherTimer` here (seven timers today). There are **no** `MainViewModel` tests. Standby, CAT pause, map-time vs live CAT, GPS persist, and recording identity are easy to regress.

**Keep in `MainViewModel`:** bindings, commands the shell must expose, and wiring.

**Extract first (highest coupling, clearest seams):**

1. **App update checker** (`RunStartupAppUpdateCheckAsync`, `_appUpdateCheckTimer`).
2. **Community satellite status** (`ApplyCommunityStatus`, `_satelliteStatusRefreshTimer`).
3. **hams.at refresh loop** (`RefreshHamsAtRovesAsync`, `_hamsAtRefreshTimer`).
4. **Pass recording presenter** (identity sync around `PassRecordingCoordinator`, which already exists).
5. **Window/dialog host** so `OpenSettingsAsync` / `OpenPassPlanningAsync` / logbook do not call `App.Services` from the VM.

Then, if it still hurts: GPS station persist, scheduled-pass alerts, live telemetry formatting.

**Done when:** `MainViewModel` is mostly wiring; at least standby / CAT-pause / map-time vs live publish have tests against fakes (`IRigController`, `ISettingsService`).

### P1.2 Stop hand-copying every settings field

**Why:** `SettingsViewModel` Load/Save is a long explicit mapping (`LoadFromDraft` / `SaveAsync` from ~line 1350 onward). New settings fields are easy to bind in the UI and forget to persist, or persist and forget to load. `SettingsWindow.axaml` is ~1,570 lines on one view model.

**Do one of:**

- A **`SettingsDraft`** (or bind `AppSettings` clones per tab) with two-way mapping and a round-trip test.
- **One sub-VM per tab** (`StationSettingsViewModel`, `RigSettingsViewModel`, …) sharing `ISettingsService`.

While there, collapse the IC-706 / FT-991 / FTX-1 **hint visibility** flags into a table keyed by `(endpoint, rigType)` instead of six near-duplicate `ShowDownlinkIc706*` properties.

**Done when:** adding a setting is “property on the model + XAML”, not a four-place copy; a fake `ISettingsService` round-trip test covers a representative tab.

### P1.3 Extract RigController policy objects

**Why:** Dial history, Sub cooldown, passband capture, and Doppler lead already have types in places (`InteractiveDialResumePolicy`, `DopplerCatLead`). A lot of session state still lives as private fields on `RigController`. `RigControllerTests.cs` is ~2,940 lines and will keep growing.

**Do:** Pull **dial-interaction state** (sample history, settle windows, RIT vs Main; see also open issue [#99](https://github.com/magicbug/OscarWatch-Tracker/issues/99) on IC-9700 RIT) into a small class with its own tests. Move the matching tests *out* of `RigControllerTests` so that file stays an integration suite.

**Do not** split the serial loop across files just to lower the line count.

**Done when:** passband / RIT / settle policy can be unit-tested without a full `RigController`; `RigControllerTests` shrinks or at least stops absorbing new policy cases.

### P1.4 Localise rig and rotator type names

**Why:** Settings still builds English-only labels (`"ICOM IC-705"`, `"Yaesu GS-232"`) while the rest of the window is localised. README already admits this.

**Do:** `Strings.resx` keys (or a `RigTypeDisplayNames` map in Core used by every locale). Same keys in all five `.resx` files.

**Done when:** Settings → Radio / Rotator lists follow UI language.

---

## P2: correctness and test gaps that still bite

### P2.1 Settings schema version

**Why:** `AppSettings` has no `schemaVersion`. Migrations are ad hoc (`MigrateFt817818ToDualOnly`, `MigrateDisplayTimesInUtc`, `MigrateMissingEnabledSatelliteNoradIds`). The next dual-radio or station-profile change will be another one-off branch in `SettingsService.TryParse`.

**Do:** `int SettingsSchemaVersion` plus ordered N → N+1 steps on load. Keep unknown-future versions loadable with a clear log line.

**Done when:** a new migration is one class/method, covered by a load test, not another `if` in `TryParse`.

### P2.2 Tests that are still thin relative to risk

| Area | Notes |
|------|--------|
| **MainViewModel** | No dedicated tests. Highest UI regression risk. Unblocks after P1.1. |
| **SettingsViewModel save/load** | Only recording-device tests today. Pair with P1.2. |
| **EasyComm rotator** | Parser tests only (`EasyCommPositionParserTests`). GS-232 / SPID have codec coverage; EasyComm driver send/parse loop does not. |
| **IC-9700 SAT CI-V** | Thin subclass of `IcomCivDriverBase`. IC-910 is exercised in `IcomCivDriverTests`; 9700 SAT on/off (`0x16 0x5A`) and 9700 mode bytes are weakly covered. Relevant to issue #99. |
| **Cloudlog radio sync path** | Mapper and policy tests exist; `CloudlogRadioSyncService` HTTP/sync loop is not. |
| **World map / sky plot** | Throttle and geometry helpers are tested. Several “property” files **reimplement** private cache eviction instead of calling production code (`FootprintGeometryCachePropertyTests`, `GroundTrackSplitCachePropertyTests`). Prefer `internal` helpers + `InternalsVisibleTo` over duplicated algorithms. |

Practical order: EasyComm driver tests, IC-9700 SAT/mode bytes, then VM round-trips once those classes are injectable.

### P2.3 Service locator (`App.Services`)

**Why:** `MainViewModel`, `PassPlanningViewModel`, `MutualPassViewModel`, logbook and timeline windows resolve extras from the static `App.Services` provider. `MainViewModel` is a **singleton** (as are overlay VMs). That is workable at runtime and painful in tests.

**Do:** Inject `IServiceScopeFactory` or a narrow `IDialogHost` / `IWindowFactory`. Overlay VMs can stay singletons; document that in `documents/` so nobody “fixes” lifetime by making `MainViewModel` transient again.

**Done when:** view models used in tests do not touch `App.Services`.

### P2.4 Analyzers and warnings

**Why:** No `.editorconfig`, no `EnableNETAnalyzers` / `AnalysisLevel`, no `TreatWarningsAsErrors`. Nullable is on, which is good, but unused usings, CA/IDE nits, and Avalonia compiled-binding misses are easy to accumulate across 111k lines.

**Do:** Turn on SDK analyzers at a modest level (start with `AnalysisLevel` latest, warnings only). Add `.editorconfig` for indent, UTF-8, and `dotnet_style` defaults that match the repo. Do **not** enable TreatWarningsAsErrors until the first analyzer pass is cleaned up.

**Done when:** `dotnet build` on a clean tree has a known warning baseline, and new PRs do not add a pile of unused-code noise.

---

## P3: docs, help, and smaller cleanups

### P3.1 Mark `optimization-branches.md` remaining items as shipped

The “Remaining / in progress” section still lists world-map throttle, timeline invalidation, `GetLiveGeometry`, coarse pass samples, and ground-track priority. [`optimization-roadmap.md`](optimization-roadmap.md) already marks those done. Move them under **Shipped** so the two files agree.

### P3.2 Operator help is English-only

UI: en-GB, es, ja, pt-BR, zh-CN. Bundled `help/*.html` (28 pages) is British English only. Translating help is a large, ongoing effort; do not block app-locale work on it.

**Near-term:** keep the README / in-app note honest. **Later:** either locale subfolders or generate short topics from the same `.resx` keys as the Settings tabs operators already struggle with (Radio Doppler, dual radio, offsets).

Still worth writing in English when someone has a spare hour (these cut support mail):

- NOR vs REV (database tag, Doppler sign, offset sign, knob pairing)
- Where files live (repo vs `%AppData%` vs bundled vs remote sync)
- Offset storage (`frequencySelections`, Remember, `modeOffsets`)
- Pre-pass checklist (TLE rebuild, offsets, Pause CAT, linear threshold)

### P3.3 Duplicate transponder database files

Repo root `satellite_database.json` and `OscarWatch/Assets/satellite_database.json` must stay in sync ([`documents/satellite-database.md`](../documents/satellite-database.md)). A CI check that they are identical (or a single source copied on build) would stop silent drift.

### P3.4 Package version comments in csproj

`OscarWatch.Core` already pins `SQLitePCLRaw.bundle_e_sqlite3` for a CVE. When bumping Microsoft.Data.Sqlite, re-read that comment so the override is not dropped.

### P3.5 Static Serilog vs DI

`Log.ForContext<T>()` is consistent and fine for a desktop app. Do not introduce a logging abstraction unless you are already extracting a class for another reason. Diagnostics already have `ITrackingDiagnostics` where tests needed it.

---

## Open product bugs that are also code-structure work

These are in GitHub, not invented here. They sit on the same large types as P1.

| Issue | Why it belongs on this list |
|-------|-----------------------------|
| [#99 IC-9700 RIT treated as Main dial](https://github.com/magicbug/OscarWatch-Tracker/issues/99) | Needs RIT-aware frequency reads or a documented “RIT off while CAT” policy. Fits P1.3 / P2.2 IC-9700 tests. |
| [#78 Full-duplex rigs as dual-radio legs](https://github.com/magicbug/OscarWatch-Tracker/issues/78) | Settings and `RigType` capability matrix, not a one-line flag. Easier after P1.2. |
| [#61 IC-821H Doppler sluggish](https://github.com/magicbug/OscarWatch-Tracker/issues/61) | Pending feedback; keep hardware tests around CAT delay / settle if you change the 821H path. |
| [#34 Multiple TLE URLs](https://github.com/magicbug/OscarWatch-Tracker/issues/34) | `TleSourceSettings` is a single source today. Schema-version (P2.1) first if the JSON shape grows. |

Hardware roadmap that is **not** a codebase-health item (keep in `TODO.md`): rotator slew lead, Smart450 early Extended commit, pass-radar gallery extras, auto-focus on AOS, unified track-start elevation, pause CAT at LOS.

---

## Suggested pickup order

### This month (if doing any engineering work)

1. P3.1 fix stale optimization-branches status
2. P0.3 align Microsoft package versions

### Next time someone is already in Settings

4. P1.2 `SettingsDraft` or per-tab VMs + hint table
5. P2.1 `schemaVersion`
6. P1.4 localised rig/rotator names

### Next time someone is already in the main window or CAT

7. P1.1 extract 2–3 coordinators from `MainViewModel` and add standby/CAT-pause tests
8. P1.3 dial/RIT policy type + issue #99
9. P2.2 EasyComm driver tests, IC-9700 SAT bytes

### Leave until the classes above are smaller

- Full help translation
- WSJT-X / SSTV / packet (see `TODO.md`)
- Analyzer TreatWarningsAsErrors
- Logging DI

---

## What not to spend time on

- Snapshot array pooling, `SatelliteTrackState` pools, StringBuilder pools for CAT, precomputed trig tables (measured or rejected).
- Rewriting the OrbitTools adapter or moving to HamLib.
- A second settings format (keep JSON).
- Splitting `FlexSmartSdrClient` before `RigController` policy extraction; Flex already has a large dedicated test suite (`FlexRadioDriverTests`, codec tests, stub server).
- Sweeping the repo for UK/US spelling or em dashes outside files you are already editing.

---

*Refresh line counts and test totals after a large extract. `ResxKeyParityTests` already checks locale key counts.*
