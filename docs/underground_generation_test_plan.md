# Underground Generation Test Plan

## Goal

- Validate the redesigned underground generator before implementation is considered production-ready.
- Cover correctness, performance, stability, and content isolation.

## Current Verification State

- Locally verified in gameplay:
  - underground floors create quickly
  - underground visibility behaves correctly
  - thermal profile behaves correctly when enabled
  - deep resource density is satisfactory
  - lava rivers appear on `-15` and deeper hot floors
- The remaining items below should still be treated as the broader regression checklist.

## Test Levels

- `-1`
- `-2`
- `-3`
- hot-depth threshold floor
- deepest configured floor

## Map Sizes

- small
- medium
- large

Use the same source surface tile for repeated comparisons where possible.

## Functional Tests

### Generation stability

- create each target floor repeatedly from the same source colony
- create each target floor from several different surface biomes
- verify no hang, no error spam, no long freeze that exceeds the expected generation budget

### Sealed-map correctness

- confirm all map edges are solid rock
- confirm underground cavities remain roofed unless intentionally opened by a portal mechanic
- confirm no open-sky void interpretation for normal cavern cells
- confirm no cave exit, road, river, coast, ruin, or surface scatter content appears unless explicitly allowed

### Depth scaling

- `-1/-2`: verify limited caverns exist and remain controlled in count/size
- `-3` and deeper: verify rock dominance increases materially
- hot depths: verify special content appears only in configured bands and only when supported

### Content isolation

- verify no surface-only features appear:
  - roads
  - rivers
  - geysers
  - surface ruins
  - plants not explicitly allowed
  - wild animals
  - scenario-part content
  - DLC special cave content not explicitly requested

### Temperature behavior

- verify underground outdoor/room temperatures follow the intended geological model
- verify roofed caverns and solid rock zones behave consistently
- verify temperatures do not mirror the parent surface map inappropriately

### Fog and visibility

- verify entry area is visible after generation
- verify the whole map is not unintentionally fully fogged
- verify fog rules remain stable after save/load

## Performance Tests

### Instrumentation targets

- total floor creation time
- time inside `MapGenerator.GenerateMap(...)`
- time inside underground layout/materialization gen steps
- spawned thing counts by category:
  - natural rock
  - resource rock
  - hazards
- destroy count during generation
- GC/allocation spikes if measurable in local profiling

### Success criteria

- no mass cleanup pass over pre-generated throwaway content
- destroy count during generation should be near zero in the final design
- generation time should scale predictably with map size and depth band
- no frame-stall pattern equivalent to the previously observed `-1` freeze

## Regression Tests

- save/load after creating multiple floors
- create a floor, switch maps, return, verify state persists
- create consecutive floors up and down from the same site
- verify world object registration and portal registration remain correct
- verify incidents remain suppressed or filtered as intended on underground floors

## Comparative Benchmarks

Run the following side-by-side where practical:

1. current mod generator
2. current mod generator with post-process disabled in a dev branch if available
3. redesigned lightweight underground path
4. reference vanilla underground path such as `AncientStockpile` or `Undercave` for rough baseline timing only

## Exit Criteria For Implementation Phase

- review documents accepted
- target architecture selected
- benchmark plan agreed
- no unresolved blocker on biome/runtime context strategy
- no unresolved blocker on fog-root strategy
