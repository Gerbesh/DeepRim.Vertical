# 08 UI UX

## Implemented now (2026-10-18)

- Compact native HUD in the lower right: ▲ Floor +N ▼, three Verse.Widgets buttons, no persistent navigator window.
- Placement after the 2026-10-18 HUD overlap fix: anchor the control 285 UI pixels from the right edge and 40 UI pixels above the bottom menu, keeping it clear of the vanilla lower-right status/action icons.
- Up/down arrows are disabled if the adjacent floor does not exist; camera zoom and focus are preserved.
- Click the central floor label for RimWorld's native FloatMenu: known floors, optional floor creation, upper overlay actions, profiler, logistics help.
- Existing hotkeys, save data, settings and bilingual support remain compatible. HUD may be hidden without disabling floor hotkeys.
- Overlay development diagnostics are available through the label tooltip when enabled.
- Net10 / Waylith compilation and five rendered visual smoke captures passed with zero reported GUI exceptions.
- An isolated native-menu game probe also passed: the center floor control creates a real RimWorld FloatMenu in WindowStack; reproducible test source is under tools/validation/native-menu/.

## Further interactive acceptance

- [ ] Click arrows and the floor dropdown; verify disabled directions and camera preservation.
- [ ] Create floors and select distant levels through the menu.
- [ ] Verify profiler/help and overlay actions from the menu.
- [ ] Verify HUD toggle key and 100/125/150% UI scales.

## Missing for later stages

- click-on-portal jump affordance
- portal overlays on adjacent floors
- player-facing non-debug construction workflow for stairs/shafts
- above-ground support overlay stack for `+` floors

## Accepted v1 direction for above-ground floors

- On floor `+N`, render a ghost support overlay for every lower non-negative floor down to `0`.
- Negative floors are not ghosted upward.
- Closer floors visually dominate deeper ones:
  - `+1` sees `0`
  - `+2` sees `+1` and `0`
  - `+3` sees `+2`, `+1`, `0`
- Active floor remains fully interactive.
- Lower overlaid floors are read-only visual support layers used for:
  - structural context
  - stair and shaft alignment
  - upper-floor build validation
