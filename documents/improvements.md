# OscarWatch — improvement opportunities

This is a review by Cursor not a human

A codebase review snapshot (June 2026). Items are grouped by theme and roughly ordered within each section: **quick wins** first, **larger refactors** later. Severity is subjective — adjust to your roadmap.

**Context:** 4 projects (`Core`, `Orbit`, `OscarWatch`, `Tests`), ~480 automated tests, Avalonia desktop app on **.NET 10**, version **0.8.4**. Rig/rotator logic is strong and well-tested; the main gaps are UI orchestration, settings ergonomics, CI, and documentation depth.

---

## 1. Architecture and code organization

### 1.1 Split the god ViewModels (high impact)

Three files carry most of the maintenance burden:

| File | ~Lines | Problem |
|------|--------|---------|
| `OscarWatch/ViewModels/MainViewModel.cs` | 1,680+ | Timers, rig, rotator, recording, speech, Cloudlog, hams.at, updates, standby — one class |
| `OscarWatch/Rig/RigController.cs` | 1,300+ | Pass init, doppler, dial capture, dual-radio, CTCSS, FM companion |
| `OscarWatch/ViewModels/SettingsViewModel.cs` | 1,120+ | Every settings tab, ~78 observable properties, manual save/load mapping |

**Suggestions:**

- **MainViewModel:** Extract focused coordinators, e.g. `PassRecordingCoordinator` (already partially exists), `RigStatusPresenter`, `AppUpdateChecker`, `HamsAtRovesRefreshLoop`. Keep `MainViewModel` as wiring + bindings, not business logic.
- **SettingsViewModel:** One sub-VM per tab (`RigSettingsViewModel`, `RotatorSettingsViewModel`, …) or a single `SettingsDraft` model with two-way bind instead of 100+ manual field copies in `SaveAsync` / `LoadFromDraft`.
- **RigController:** Harder to split, but consider extracting **dial-interaction policy** (8-sample history, 2.5 s Sub cooldown) into a small `RigDialInteractionState` class — would shrink both `RigController.cs` and the 1,695-line `RigControllerTests.cs`.

### ~~1.2 Rig status messages — fix i18n at the source (medium)~~ ✅ Done

**Done (June 2026):** `RigStatusKind` on `RigConnectionStatus` with optional English `StatusPort` / `StatusDetail` for diagnostics. UI uses `RigStatusLocalizer`; logs and the diagnostics bundle use `RigStatusText.ToEnglish()`.

### 1.3 Settings hint property explosion (medium)

`SettingsViewModel` has many near-duplicate visibility flags (`ShowDownlinkIc706MkiiGCivHint`, `ShowUplinkIc706MkiiGCivHint`, …).

**Better approach:** A small helper or computed rule, e.g. `ShowCivHint(RigEndpoint.Downlink, RigType.IcomIc706MkiiG)` driven by a table of `(rigType, hintKey)` pairs. Same for FT-991 / FTX-1 CAT hints.

### 1.4 Hardcoded rig and rotator labels (low)

Dual-radio and rotator type names in `SettingsViewModel` (`"ICOM IC-705"`, `"Yaesu GS-232"`) are English-only while the rest of Settings is localized.

Move labels into `Strings.resx` (or a `RigTypeDisplayNames` map in Core) so all four locales stay in sync.

### 1.5 Overlay ViewModel lifetime (low)

`App.axaml.cs` registers overlay VMs as singletons but `MainViewModel` is transient — intentional, but couples lifetimes. Document the pattern in `documents/` or inject overlays via a narrow `IFrequencyOverlayHost` interface so tests can substitute fakes.

---

## 2. Testing

### 2.1 What is already strong

- Rig codecs and drivers (ICOM CI-V, Yaesu, Kenwood TS-2000)
- `RigController` and `RotatorController` integration tests
- Core math: doppler, Maidenhead, pass prediction, satellite DB merge
- FsCheck property tests for orbit/tracking invariants (~14 properties)

### 2.2 High-value gaps

| Area | Path | Why it matters |
|------|------|----------------|
| **MainViewModel** | `ViewModels/MainViewModel.cs` | Standby/CAT-pause persistence, map-time scrub vs live rig publish — easy to regress |
| **SettingsViewModel** | `ViewModels/SettingsViewModel.cs` | Save/load round-trip, baud defaults per rig type, dual-radio visibility |
| **Settings load/migration** | `Core/Services/SettingsService.cs` | Only atomic save is tested; no tests for first-run defaults, corrupt JSON, `MigrateFt817818ToDualOnly` |
| **MutualPassFinder** | `Core/Services/MutualPassFinder.cs` | Only formatter tested |
| **TrackingOrchestrator** | `Core/Services/TrackingOrchestrator.cs` | Thin coverage beyond `LiveTrackingServiceTests` |
| **IcsPassExporter** | `Core/Export/IcsPassExporter.cs` | User-facing export, zero dedicated tests |
| **CloudlogRadioSyncService** | `Cloudlog/CloudlogRadioSyncService.cs` | Mapper tested; sync path not |
| **EasyComm rotator** | `Rotator/EasyCommRotator.cs` | No codec/driver tests (GS-232/SPID are covered) |
| **IC-9700 / IC-910** | `IcomIc9700Driver.cs`, `IcomIc910Driver.cs` | Thin subclasses; satellite-mode CI-V bytes untested |
| **UI controls** | `WorldMapControl.cs`, `SkyPlotControl.cs` | 800+ lines; only twilight-brush property tests |

### 2.3 Test maintenance cost

`RigControllerTests.cs` mirrors production complexity (1,695 lines). Any RigController refactor should **move tests down** with extracted types so the integration file stays a smoke suite, not a second implementation.

### 2.4 Suggested targets (practical order)

1. `SettingsService` load + migration tests (fast, high confidence for upgrades)
2. `MutualPassFinder` + `IcsPassExporter` unit tests
3. ~~Extract `RigStatusKind` → test localization without MainViewModel~~ ✅ (`RigStatusTextTests`, `RigStatusLocalizer`)
4. `SettingsViewModel` save round-trip with a fake `ISettingsService`
5. MainViewModel: test standby/CAT-pause with injected `IRigController` fake

---

## 3. Settings and persistence

### 3.1 No schema version (medium)

`AppSettings` has no `schemaVersion`. Today you have one migration (`RigSettings.MigrateFt817818ToDualOnly`). As dual-radio and rig types grow, migrations will get brittle.

Add `int SettingsSchemaVersion` and a small migration pipeline on load (version N → N+1 steps).

### 3.2 Settings backup / export (product + TODO)

Listed in `TODO.md`. Users with multiple PCs, reinstalls, or club setups would benefit from export/import of `settings.json` (and optionally station list only).

Implement in `SettingsService` + Settings UI; add round-trip test.

### 3.3 Subtle runtime vs persisted state

`SettingsViewModel.SaveAsync` preserves `CatUpdatesPaused` from disk rather than the draft — intentional but undocumented. Operators may be confused why pause survives a Settings save.

Document in help; consider showing “CAT pause is session-only” in UI or persisting explicitly with a checkbox.

### 3.4 `GetRigSettingsForController()` clone (low)

`MainViewModel` clones full `RigSettings` when overlay pause differs from saved pause. Any new rig field must be copied or behavior drifts.

Prefer passing `CatUpdatesPaused` as an override parameter to `IRigController` instead of cloning the whole settings graph.

---

## 4. Operations, rig, and rotator behaviour

### 4.1 Align track-start elevations (product + TODO)

- Rotator: `TrackStartElevationDeg` default **−3°** (`RotatorSettings`)
- Rig: tracks whenever look angles exist — no equivalent gate

Operators may see the rotator move before CAT updates (or vice versa). Either document clearly, add a rig track-start setting, or unify “start tracking at X°” in Settings.

### 4.2 AOS/LOS rig behaviour (partial)

- **Done:** **Park rotator after pass** — Settings → Rotator (`ParkAfterPass`, default on); `RotatorController.TryPark` respects it for automatic post-pass moves only.
- **TODO:** optional pause CAT at pass end (`RigController` pass lifecycle).

### 4.3 Auto-focus satellite on pass (TODO)

When a pass rises above threshold, focus map overlay automatically — reduces clicks during busy passes.

### 4.4 Remaining hardware roadmap (from `TODO.md`)

| Item | Notes |
|------|--------|
| **ICOM IC-905** | Next single-radio CI-V candidate |
| **SPID LAN/TCP** | Ethernet rotators (port 23); serial SPID exists |
| **Rotator slew lead/lag** | Mechanical lag compensation |
| **Stop on park** | GS-232 `S` before park (partial today via dispose) |

Each follows existing driver docs: `documents/building-radio-drivers.md`, `documents/building-rotator-drivers.md`.

---

## 5. Documentation and help

### 5.1 Help vs UI locale mismatch (medium)

UI supports **en-GB, ja, pt-BR, zh-CN** (~100 keys each). All **11 `help/*.html` pages are British English only**.

Options: translated help sets, or generate help from the same resource keys as the app (heavier).

### 5.2 Operator topics still missing (from `TODO.md`)

Worth writing — they reduce support burden:

- **NOR vs REV** — database tag, doppler sign, offset sign, knob pairing
- **Settings vs database paths** — repo vs `%AppData%` vs bundled vs remote sync
- **Offset storage** — `frequencySelections`, Remember checkbox, `modeOffsets`
- **Pre-pass checklist** — rebuild, offsets, Pause CAT, linear threshold
- **Doppler behaviour matrix** — link from README when written

### 5.3 README still “in development”

For 0.8.4 with 470+ tests and many rigs, consider softening that framing and pointing to a short **supported hardware** table (already partly in README) + link to `help/quick-start.html`.

### 5.4 Accessibility

`docs/ACCESSIBILITY.md` is solid (WCAG-oriented, Okabe–Ito palette). When changing map/sky plot colours, run through Coblis or DevTools simulator — not automated today.

**Improvement:** A single screenshot or UI test that asserts contrast tokens exist in light/dark theme resource dictionaries.

---

## 6. CI, build, and dependencies

### 6.1 No PR / push CI (high)

`.github/workflows/publish.yml` runs build+test only on **version tags** and `workflow_dispatch`. PRs and pushes to `main` are not gated.

**Add** `ci.yml`: `dotnet build` + `dotnet test` on push/PR. Optionally matrix **windows-latest** + ubuntu for serial/speech code paths.

### 6.2 Linux-only validation before release

Publish workflow validates on `ubuntu-latest` only. Windows-specific paths (`System.IO.Ports`, `System.Speech`, PortAudio) are not compiled/tested in CI before tag.

Add a Windows job (even build-only) or document that release candidates need a manual Windows smoke test.

### ~~6.3 .NET 10 and package versions (medium)~~ ✅ Done

**Done (June 2026):** `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `System.Speech`, and `System.IO.Ports` aligned to **10.0.8** on `net10.0`.

### 6.4 Localization parity not enforced in CI

`scripts/apply-settings-localization.py` + `settings-localization-keys.json` exist but CI does not verify all `.resx` files have the same keys.

**Add** a small script in CI: fail if key counts differ across en/ja/pt-BR/zh-CN.

---

## 7. Error handling and observability

### 7.1 Mixed error styles — partially done

- Serilog via static `Log.ForContext<T>()` — consistent but not DI-friendly *(unchanged)*
- ~~`TrackingOrchestrator` swallows some TLE decay exceptions silently~~ ✅ Debug logs via `ITrackingDiagnostics` (once per NORAD per reload)
- ~~Rig errors: English strings → UI string match~~ ✅ `RigStatusKind` + `RigStatusLocalizer` (see 1.2)

**Done (June 2026):**

- Structured rig/rotator error codes on status objects (`RigStatusKind`, `RotatorConnectionKind`)
- Log at Debug when look-angle computation is skipped (NORAD id + exception)
- Help → **Copy diagnostics** (log tail + redacted settings + English rig/rotator status)

### 7.2 Settings save failures

`SettingsService` fires `SaveFailed` and logs Warning — good. Ensure Settings UI surfaces a one-line toast when save fails (verify binding exists).

---

## 8. Product / larger ideas (lower priority)

From `TODO.md` — only if they match your vision:

- WSJT-X-style duplex UI for FT satellites
- Native SSTV decoder
- Built-in packet interface
- IC-9700 keyer-memory dialog

These are multi-week efforts; keep them out of the core tracker refactor path until rig/rotator/docs debt is lower.

---

## 9. Suggested roadmap (if you want a sequence)

### Phase A — Low risk, high leverage (days)

1. Add PR CI (`build` + `test` on ubuntu + windows)
2. Resx key parity check in CI
3. ~~`RigStatusKind` enum + remove string matching in `MainViewModel`~~ ✅
4. `SettingsService` load/migration tests
5. Document track-start elevation mismatch in help

### Phase B — Maintainability (1–2 weeks)

1. Split `SettingsViewModel` or introduce `SettingsDraft` model
2. Extract rig hint visibility table
3. Localize rig/rotator type names
4. Settings export/import
5. `schemaVersion` + migration framework

### Phase C — MainViewModel decomposition (ongoing)

1. Extract update checker, hams.at refresh, recording announcer
2. Test standby/CAT-pause paths
3. Trim `RigControllerTests` as subsystems extract

### Phase D — Product (as needed)

1. IC-905 driver
2. SPID TCP rotator
3. Unified track-start elevation
4. Translated help or key-linked help topics

---

## 10. What not to change (working well)

- **Layering:** Core (models/math) vs Orbit vs UI/drivers — clear boundaries
- **Native rig drivers** instead of HamLib — right call for satellite CAT quirks; documented well
- **Rig driver factory + codec tests** — good template for new radios (recent IC-706, FTX-1 additions fit cleanly)
- **Atomic settings write** with retry — production-ready
- **Help content** for dual radio, Doppler modes, and hardware setup — mostly current with code
- **Accessibility palette** (`PlotColors.cs`, `ACCESSIBILITY.md`) — thoughtful for a mapping app

---

*Generated from static review and test inventory. Re-run `dotnet test` and line-count tools after major refactors to refresh numbers.*
