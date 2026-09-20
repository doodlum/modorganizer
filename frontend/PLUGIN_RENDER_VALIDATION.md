# Visible plugin rows — 2026-09-20

A desktop-compositor capture confirmed that the Plugins table was genuinely
missing its text. Earlier source-row identity checks passed despite this defect;
the missing text was not merely an offscreen screenshot limitation.

Two shared rendering changes fix it:

- `Mo2DeferredRow` builds the contents of attached virtualized rows synchronously.
  Previously it queued essential contents at Background priority. The failure
  diagnostic found 23 attached empty row wrappers, successful direct construction,
  and a background marker that did not execute within one second.
- `Mo2ListScrollBar.Connect` passes the actual row scroller's viewport to the
  column layout. With hidden headers, the column viewport remained zero while
  the row scroller measured 470 pixels. The star column stayed at its 30-pixel
  minimum, leaving name cells zero pixels wide. Supplying the real viewport made
  the column 470 pixels and the first name cell 210 pixels wide. The shared
  connection follows both Mods and Plugins through layout and source changes.

## Evidence

- Before: [desktop capture](artifacts/native-frame-skyrim-deck.png).
- After: [desktop capture](artifacts/native-frame-skyrim-deck-fixed.png).
  These are Spectacle captures of the identified owned frontend window, inspected
  directly. The maximized client is 1280×750 within the Deck's desktop work area.
- `artifacts/plugin-render-verified-size.log`: `MO2_VERIFY_PLUGIN_RENDER=1`
  verifies readable names and toggles for every realized plugin row, column width,
  1280×750 → 1120×600 → 1280×750, scrolling, filtering and clearing over 150 plugins.
  Background work also completes within the check's one-second observation.
- `artifacts/game-switch-rendered-rows.log`: the existing FNV → Skyrim → FNV
  check now also requires readable rendered plugin cells, alongside native mod,
  filter, plugin-identity and Data comparisons.
- `artifacts/rendered-rows-native-state.json`: original profile, mod/plugin names,
  activation and priority unchanged on FNV (14/11) and Skyrim (151/150). Both
  installed native bridge packages still match source.

The final MockHost Release build passes with eight existing warnings and zero
errors. Diff hygiene passes. No upstream API change is retained for this fix.
This verifies the paired Mods/Plugins view and the stated control interactions;
all-page visual review, physical-input latency and full production acceptance
remain open.
