# Upper Floor Test Plan

## Creation

1. Create `+1`
   - no clutter, plants, rocks, ruins, geysers, animals or deposits
   - no surface biome generation
   - default slab floor exists above roofed cells on `0`
   - stair / shaft anchor gives an obvious starting foothold
   - floor `0` ghost data visible
2. Create `+2`
   - default slab exists only where `+1` has valid covered/support-backed structure below
   - `+1` and `0` visible
   - `+1` stronger than `0`
3. Create `+3`
   - stack is `+2`, `+1`, `0`
   - active floor remains readable

## Support validation

1. Building outside supported area is rejected
2. Building over valid support is accepted
3. Connected supported upper-floor structures extend the buildable region
4. Roofed rooms below create obvious starting build area on the new upper floor
5. Stair / shaft cores remain valid bootstrap points even over small rooms

## Input

1. Ghost layers never intercept selection or commands
2. `Full` pawn markers remain visual-only

## Pawn usability

1. On a fresh `+1`, pawns have a practical place to stand and start work
2. Open unroofed spaces below do not read as free full-floor foundations above

## Performance

1. Dense base on `0/+1/+2`
2. Repeated build and deconstruct operations
3. Overlay cache refresh stays correct without per-frame full rebuild
