# 00 Sources

## Local source of truth

- RimWorld install: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
- Game version: `1.6.4633 rev1260`
  - verified from `Version.txt`
- Managed assemblies inspected:
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll`
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll`
- Local source snippets available:
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Source`
  - note: local `Source/` is partial only (`43` `.cs` files), so it is not authoritative for patch signatures.
- Local official docs used:
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\ModUpdating.txt`
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data\Core\About\About.xml`
  - base game XML defs under `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data`

## DLC detected locally

- Core
- Royalty
- Ideology
- Biotech
- Anomaly
- Odyssey

## Harmony reference

- Workshop package id: `brrainz.harmony`
- Confirmed local About: `C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\About\About.xml`
- Confirmed local assembly used for compile reference only:
  - `C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll`
- Mod packaging rule: do not ship `0Harmony.dll` inside this mod.

## Web sources

- RimWorld Wiki Hello World / folder structure:
  - https://rimworldwiki.com/wiki/Modding_Tutorials/Hello_World
- RimWorld modding wiki tutorial index:
  - https://rimworldwiki.com/wiki/Modding_Tutorials

## Asset production note

- Asset generation is allowed in the project workflow.
- Current agreed workflow: generate art/assets manually via the web version when needed.
- Codex-side `imagegen` skill is present in the environment, but it is not treated as a verified local production dependency for this workspace because live API prerequisites are not configured here.

## Practical source priority

1. Local `Assembly-CSharp.dll` reflection for actual signatures.
2. Local game XML defs for def names, inheritance and schema examples.
3. Local shipped docs (`ModUpdating.txt`) and partial `Source/`.
4. RimWorld wiki only for mod packaging conventions and general structure guidance.

## Confirmed reflection targets sampled during stage 0

- `Verse.MapGenerator.GenerateMap(Verse.IntVec3, RimWorld.Planet.MapParent, Verse.MapGeneratorDef, IEnumerable<Verse.GenStepWithParams>, Action<Verse.Map>, Boolean, Boolean)`
- `RimWorld.Planet.MapParent.PostMapGenerate()`
- `Verse.AI.Pawn_PathFollower.StartPath(...)`
- `Verse.AI.Pawn_PathFollower.TryEnterNextPathCell()`
- `Verse.Reachability.CanReach(...)`
- `Verse.AI.AttackTargetFinder.BestAttackTarget(...)`
- `Verse.Projectile.Launch(...)`
- `RimWorld.PowerNetManager.UpdatePowerNetsAndConnections_First()`
- `Verse.GenTemperature.GetTemperatureForCell(...)`
- `Verse.RoomTempTracker.EqualizeTemperature()`

## Additional local defs confirmed during implementation

- Core map generation defs under:
  - `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Data\Core\Defs\MapGeneration`
- DLC underground generator examples used as schema references:
  - `Data\Anomaly\Defs\MapGeneration\UndercaveMapGenerator.xml`
  - `Data\Anomaly\Defs\MapGeneration\MetalHellMapGenerator.xml`
