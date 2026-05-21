# OscarWatch accessibility guidelines

OscarWatch is a desktop satellite tracker (Avalonia). These guidelines keep the app usable for people with low vision, color-vision deficiency, and those who rely on keyboard or screen readers.

**Target:** [WCAG 2.1](https://www.w3.org/TR/WCAG21/) Level AA where applicable to a desktop mapping app.

## Principles

1. **Perceivable** — Text and lines have enough contrast; information is not hue-only.
2. **Operable** — Keyboard and focus work for tracking and dialogs.
3. **Understandable** — Satellite identity and pass timing are clear in words and numbers.
4. **Robust** — Theme-aware resources; automation names on custom controls.

## Color blindness and the map

Many users cannot distinguish red vs green or certain cyan/yellow pairs. The tracker shows multiple satellites at once on the world map and sky plot.

| Do | Don't |
|----|--------|
| Always show **satellite names** on the map near subpoints | Rely on dot color alone to tell ISS from SO-50 |
| Use a **fixed, documented palette** (`PlotColors.cs`) tuned for common CVD types | Add random bright colors per build |
| Pair **“AOS in …”** text with green highlight for imminent passes | Use only green text for “soon” |
| Mark stale TLEs with **“stale”** text (and color) | Orange dot only |

Recommended palette family: [Okabe–Ito](https://jfly.uni-koeln.de/color/) (8 colors, color-blind safe). When changing colors, check with a simulator (e.g. Chrome DevTools vision deficiency, or [Coblis](https://www.color-blindness.com/coblis-color-blindness-simulator/)).

## Contrast (light and dark theme)

The app supports **System / Light / Dark** (`AppThemeManager`). Any new UI must be verified in **both** themes.

| Element | Minimum contrast ratio |
|---------|-------------------------|
| Body text (sidebar, settings, pass list) | **4.5:1** |
| Large / bold headings (≥14pt bold) | **3:1** |
| Sky plot grid, horizon ring, map track strokes | **3:1** vs local background |
| Non-text focus ring | Visible in both themes |

**Implemented in code:**

- Okabe–Ito palette in `PlotColors.cs`
- Theme-aware `PassHighlightBrush` and `StaleTleForegroundBrush` (`AccessibilityThemeResources`)
- Map/sky markers: dark + white outline; dashed outline below minimum elevation on sky plot
- Ground station: theme-aware blue marker with dark halo
- Automation names and keyboard selection on map/sky plot

Tools: [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/), or APCA for very small text.

## Typography

| Use | Size |
|-----|------|
| Live telemetry, pass times, settings fields | **≥12px** |
| Hints (“Click a satellite…”) | 10–11px OK if not sole instruction |
| Map labels | **≥11px**, high-contrast fill behind text (`MapLabelBackground`) |

Respect OS **text scaling**; avoid clipping in fixed-height sidebars when possible.

## Keyboard and focus

- Tab through menu, map, sky plot, pass list.
- Focused satellite should match click selection (`FocusedNoradId`).
- Settings / pass planner / picker: standard dialog keys (Escape, default button).

## Screen readers

Custom controls should expose:

- **Name:** e.g. “World map, 3 satellites”
- **Focus:** selected satellite name and live az/el when available

Implementation: Avalonia `AutomationProperties` on `WorldMapControl` and `SkyPlotControl` (incremental).

## What we are not (yet)

Planned improvements (not required for every PR):

- **High contrast** theme variant
- **UI scale** setting (125% / 150%)
- **Shape or pattern** per satellite in addition to color
- Full WCAG audit of every dialog

## Manual test checklist

Before release or after UI changes:

- [ ] Light theme: read sidebar and pass planner without strain
- [ ] Dark theme: same
- [ ] Simulate deuteranopia: still tell satellites apart on map + sky plot
- [ ] Keyboard-only: select satellite, open Settings, close with Escape
- [ ] 125% OS display scaling: no clipped pass rows

## References

- [WCAG 2.1 Quick Reference](https://www.w3.org/WAI/WCAG21/quickref/)
- [W3C Understanding use of color](https://www.w3.org/WAI/WCAG21/Understanding/use-of-color.html)
- [IBM Accessible color palette](https://davidmathers.github.io/ibm-a11y-colors/)

Developer rule for Cursor: `.cursor/rules/accessibility.mdc`.
