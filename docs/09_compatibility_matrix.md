# 09 Compatibility Matrix

## RimWorld 1.7 / Waylith preview (October 18, 2026)

- Installed game revision observed: **1.7.4981 rev136**. Waylith uses a customized **.NET 10** runtime.
- Build project: `DeepRim.Vertical.1.7.csproj` (local SDK: `.tools/dotnet10/dotnet.exe`).
- References: current game's `Assembly-CSharp.dll`, `RimWorld.dll`, `UnityStubs.dll`, `Waylith.dll`, `Waylith.UnityCompat.dll` and the Harmony 1.7 assembly.
- The navigator switched from the removed `UnityEngine.GUI.Window` overload to a `Verse.Window` implementation.
- Dedicated `1.7/Assemblies`, `1.7/Defs`, `1.7/Textures` are selected by `LoadFolders.xml`. The 1.6 binary/assets are left unchanged. Shared assets are copied for 1.7 and must be kept in sync when edited.
- Static packaging and net10 compilation succeeded. **Real 1.7 native gameplay and rendered five-floor smoke tests passed**, including multi-floor jobs, combat, three save/reload cycles, profiler, navigator and overlay screenshots. [Observed results and limitations](waylith_1_7_validation_2026-10-18.md). DLC, third-party compatibility and long-play stability remain unverified.
- Recommended first verification: a separate throwaway 1.7 save with Harmony and DeepRim only; test loading, opening/closing/toggling floor navigator, generating floors above/below, stair routing, hauling, production, roof/terrain rendering, combat and save/reload. Do not overwrite a valuable 1.6 save before a backup.
- On the current machine RimWorld 1.6 managed reference assemblies are no longer installed, so recompiling the 1.6 project requires restoring the 1.6 assemblies; the previous 1.6 build is preserved.

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
