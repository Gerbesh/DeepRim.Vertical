# 09 Compatibility Matrix

## Current audit baseline

- Installed engine inspected October 11, 2026: **RimWorld 1.6.4871 rev590**.
- Current build and local contract checks use installed assemblies. Full gameplay compatibility, DLC combinations and third-party mods have not been verified by those checks.
- See [the systems audit](colony_systems_audit.md) for missing adapters and [the acceptance plan](colony_systems_test_plan.md) for required checks.

## Historical research baseline

- RimWorld `1.6.4633 rev1260`
- Historical runtime logs later reported local executable revision `rev1261`.
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
