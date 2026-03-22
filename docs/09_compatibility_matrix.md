# 09 Compatibility Matrix

## Verified baseline

- RimWorld `1.6.4633 rev1260`
- Runtime logs later reported local executable revision `rev1261`; the current workspace code is being validated against that live revision.
- DLC present during research:
  - Royalty
  - Ideology
  - Biotech
  - Anomaly
  - Odyssey

## Dependency policy

- hard dependency:
  - `brrainz.harmony`
- no hard dependency:
  - Odyssey
  - Royalty
  - Ideology
  - Biotech
  - Anomaly
  - third-party utility mods

## Asset pipeline

- No runtime dependency on any image-generation tool.
- Optional art/texture generation may be done manually outside the workspace via the web version.

## Underground generation compat notes

- Underground floors do not require Odyssey.
- Lava river generation is soft-compat only:
  - if a loaded terrain def named `LavaDeep` exists, deep hot floors may use it
  - otherwise no lava river content is generated
- Underground floor creation now uses mod-owned defs:
  - `DeepRimVertical_Underground`
  - `DeepRimVertical_UndergroundBiome`
- This is intentionally independent of Anomaly underground generators and avoids inheriting special DLC content such as undercave-specific or anomaly-specific map features.
