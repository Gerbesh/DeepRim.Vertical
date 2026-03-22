# Underground Generation Options

## Evaluation Criteria

- true underground semantics, not surface-plus-postfix
- sealed map correctness
- no unwanted surface or DLC special content
- stable generation time on `-1` and deeper floors
- compatibility with same-tile vertical architecture
- extensibility by depth band
- low dependence on broad Harmony patches

## Status

- Chosen option: `Option C`
- Current state: implemented and locally verified in game

## Option A: Keep current design and tune it

### Description

- Keep `DeepRimVertical_Underground`.
- Keep `MapGenerator.GenerateMap(...)`.
- Keep post-process wipe and respawn.
- Try to tune gen steps, masks, and temperature patches.

### Pros

- Lowest implementation cost.
- Minimal disruption to save model and current code layout.

### Cons

- Core architecture remains wrong for the requirement.
- `pocketMapProperties` still do not apply.
- surface tile mutators still leak unless separately patched
- destroy/respawn churn remains the dominant performance cost
- fog logic remains awkward on sealed maps
- future depth-specific content remains bolted on after generation rather than authored by generation

### Verdict

- Reject.
- Suitable only as a temporary stabilization stopgap, not as the requested redesign.

## Option B: Reuse vanilla underground pocket generators as a base

### Description

- Base underground floors on `Undercave`, `InsectLair`, or `AncientStockpile`.
- Remove or suppress unwanted steps with `preventGenSteps` or follow-up cleanup.

### Pros

- Closer to true underground semantics than surface generation.
- Can inherit pocket biome and temperature behavior if the map is actually created as a pocket map.
- `AncientStockpile` is a useful lightweight example.

### Cons

- `Undercave` and `InsectLair` are packed with DLC-specific cave content that is inappropriate for generic colony floors.
- `PlaceCaveExit`, `Plants`, `RockChunks`, ruins, and special structures are wrong defaults for sealed vertical floors.
- Still encourages cleanup/post-filtering rather than clean generation.
- Strong DLC coupling.

### Verdict

- Reject as the production architecture.
- Keep as reference material only.

## Option C: Custom lightweight `MapGeneratorDef` plus custom underground builder gen step

### Description

- Keep `MapGenerator.GenerateMap(...)` for map lifecycle.
- Replace the current underground step list with a custom minimal path.
- Use one bespoke builder step, or a small set of bespoke steps, to write the final underground state directly.
- Avoid generating content that will later be destroyed.

### Candidate shape

- `DeepRimVertical_Underground`
  - `isUnderground=true`
  - bespoke underground context handling
  - gen steps:
    - `DeepRimVertical_UndergroundLayout`
    - `DeepRimVertical_UndergroundContent`
    - `DeepRimVertical_UndergroundFog`

### Pros

- Best fit for the stated goal.
- Keeps vanilla map lifecycle and save integration.
- Eliminates most disposable generation work.
- Cleanly supports depth bands:
  - `-1/-2`: limited caverns
  - `-3` and deeper: mostly solid rock
  - hot depths: optional curated volcanic overlays
- Easy to reason about safety because every content source is explicit.
- Best platform for precomputed masks and caches.

### Cons

- Requires real rewrite of the underground generation service into gen-step form.
- Needs explicit handling for fog roots, biome/weather context, and geology defaults.
- Still must spawn many rock things on deep floors because that is how RimWorld represents solid rock.

### Verdict

- Recommended base architecture.

## Option D: Fully custom underground builder outside `MapGenerator`

### Description

- Bypass vanilla `MapGenerator.GenerateMap(...)`.
- Construct the map and all contents through a private builder pipeline.

### Pros

- Maximum control.
- Could theoretically remove all unwanted vanilla generation overhead.

### Cons

- Very high risk.
- Re-implements lifecycle that vanilla already stabilizes:
  - component construction
  - weather init
  - map finalization
  - post-map hooks
- Harder to keep compatible across RimWorld updates.
- Not justified by the local evidence.

### Verdict

- Reject.

## Option E: Convert underground floors to actual pocket maps

### Description

- Create underground floors using `GenerateMap(..., isPocketMap: true, ...)`.
- Use pocket biome/temp directly from `pocketMapProperties`.

### Pros

- Instantly solves part of the biome/temperature context problem.
- Prevents the surface world tile from being the active biome source.
- Matches how vanilla underground generators are authored.

### Cons

- Pocket maps in vanilla are usually temporary/special-purpose, not persistent colony floors.
- The current vertical architecture uses custom `MapParent`, not `PocketMapParent`.
- Some systems may assume pocket-map lifecycle semantics that are not appropriate for a permanent colony floor.
- Compatibility risk is materially higher than Option C.

### Verdict

- Do not choose as the first-line redesign.
- Use as a comparison baseline and fallback experiment if synthetic underground tile context proves too invasive.

## Recommended Option

- Choose Option C.
- Keep `MapGenerator.GenerateMap(...)`.
- Replace the current mixed vanilla-plus-postprocess path with a custom lightweight underground builder.
- Treat pocket-style context handling as a secondary design input, not as the primary map model.

## Option Comparison Table

| Option | Correct underground semantics | Performance | Risk | Extensibility | Recommendation |
| --- | --- | --- | --- | --- | --- |
| A. Tune current path | Low | Low | Medium | Low | Reject |
| B. Reuse vanilla underground generators | Medium | Medium-low | Medium | Low-medium | Reject |
| C. Lightweight custom generator path | High | High | Medium | High | Recommend |
| D. Fully custom outside `MapGenerator` | High | Medium-high | Very high | High | Reject |
| E. Full pocket-map conversion | Medium-high | Medium-high | High | Medium | Not primary choice |
