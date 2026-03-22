# 08 UI UX

## Implemented now

- toggleable draggable floor navigator window drawn from the active map
- dedicated navigator toggle binding path
- bottom-right button that shows or hides the navigator
- optional floor creation buttons for development/foundation testing
- current-cell portal summary

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
