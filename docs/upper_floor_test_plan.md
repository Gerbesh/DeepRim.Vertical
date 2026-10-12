# Upper Floor Test Plan

## Creation

1. Create `+1`
   - no clutter, plants, rocks, ruins, geysers, animals or deposits
   - no surface biome generation
   - default slab floor exists above roofed cells on `0`
   - creating a level alone does not add a stair anchor or passage
   - completing upstairs construction gives a walkable landing
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

1. After building upstairs stairs, pawns have a walkable landing on `+1`
2. Open unroofed spaces below do not read as free full-floor foundations above

## Performance

1. Dense base on `0/+1/+2`
2. Repeated build and deconstruct operations
3. Overlay cache refresh stays correct without per-frame full rebuild

## Physical ceilings from upper construction

- Build a DeepRimVertical_UpperDeck tile at +1, +2 or +3. On the corresponding cell of the next lower loaded map, a constructed roof should appear. The room beneath should become roofed and subject to normal RimWorld room/temperature rules.
- Remove the upper floor foundation and any replacement top-layer flooring. If there is no remaining full structural building block, only the mod-owned constructed roof below should disappear.
- Place/remove a full wall/block upstairs over void. The matching lower roof should appear/disappear, but decorative furniture and blueprints alone must not create ceilings.
- Test an already roofed lower cell (normal player roof, thin/thick natural roof). Upper construction must not claim or remove those roofs.
- Save/load with constructed upper floors; verify ownership and roof state survive. Re-check a floor built or loaded after its lower neighbor.
- Ensure an upper deck blocks cross-floor firing, whereas an open void with a truly unroofed lower cell allows it. Stairs do not create firing holes.

VerticalCeilingComponent stores ownership on the lower map, queues updates after source terrain/building changes, and reconciles the grid after loading. Ownership is not inferred from a roof's visual appearance to avoid deleting vanilla roofs.
