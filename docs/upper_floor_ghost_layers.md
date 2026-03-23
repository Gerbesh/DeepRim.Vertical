# Upper Floor Ghost Layers

## Player-facing stack

On active `+N`, the player sees:

- the active floor as the only interactive layer
- lower non-negative floors only
- nearer floors stronger than deeper floors
- lower-floor structure and pawns as muted contextual haze, not as replacement by a flat debug layer

Stack rule:

- `+1` shows `0`
- `+2` shows `+1`, then `0`
- `+3` shows `+2`, `+1`, `0`
- negative floors are excluded

## Modes

- `Supports`
  - normal lower-floor ghost haze
  - support cells from lower floors
  - active-floor buildable mask as a light hint only
- `Structure`
  - walls
  - doors
  - large buildings
  - key structural footprints
- `Full`
  - structure mode plus pawn markers

Default mode: `Full`

## Rendering point

Confirmed local hook:

- `Verse.MapDrawer.DrawMapMesh()`

This keeps active-floor dynamic things visually dominant while lower-floor ghosts remain non-interactive context.

## Current implementation note

The current `v1` renderer is still a lightweight ghost-footprint renderer, not a full live lower-map renderer.

- It already shows lower-floor structure and pawn context in haze.
- It does not yet reproduce the exact vanilla visual richness of the lower map.
- The intended next refinement is to move closer to "see the base below through haze" while keeping the cache-driven design.
