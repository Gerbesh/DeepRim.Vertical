# DeepRim Vertical: RimWorld 1.7 / Waylith compatibility audit (2026-10-18)

## Technical baseline

- Audited the project against Steam unstable RimWorld **1.7.4981 rev136**, .NET 10, Vulkan/Waylith, and the installed Harmony 3.0.0.0 assembly.
- Starting point: `origin/main` commit `bad59e2`. Keep a separate net472 / RimWorld 1.6 project and assets.
- Approximately **82 source files**, **65 declarative HarmonyPatch attributes**, and 34 optional profiler instrumentation hooks.
- Official technical references:
  - <https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub>
  - <https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates>
- The game deliberately preserves its Unity compatibility layer. Avoid a blanket rewrite of graphics against Waylith APIs. Prepatcher is incompatible, but DeepRim does not depend on it. There are no custom shader bundles or GLSL assets to migrate.

## Changes implemented in this audit

1. **Cached map and section accessors.** `VerticalSectionRenderPatches` uses Harmony 3 typed `FieldRefAccess` delegates instead of creating `Traverse` reflection wrappers in the map drawing and mesh-dirty paths. The map/section fields were verified on installed 1.7 assemblies.
2. **Cached thermal fields.** `VerticalTemperaturePatches` uses typed cached delegates for `MapTemperature.map`, `RoomTempTracker.room`, and `RoomTempTracker.thickRoofCoverage` instead of repeated `FieldInfo.GetValue`. These are high-frequency calls in climate simulation.
3. **Missing 1.7 snow/sand assets.** The old `Other/SnowPolluted` and `Other/WindsweptSandPolluted` resources are absent from 1.7 `Assets.zip`; an actual rendered smoke test logged an error. Under `DEEPRIM_WAYLITH_17`, use existing `Other/Snow` and `Other/Sand` resources. Keep original behavior under 1.6. Per-cell pollution color information remains encoded in the existing mesh colors.
4. **Future update guard.** `tools/validation/waylith-api` independently checks Harmony 3 field delegates against the installed game and the actual `Assets.zip` resource keys. This test fails if a later 1.7 unstable patch changes the field layouts or removes an expected texture.

## Tests and evidence

- `dotnet10 build DeepRim.Vertical.1.7.csproj -c Release`: **PASS** (zero warnings, zero errors).
- `python tools/validation/verify-waylith-package.py`: **PASS** (metadata, load folders, resources, DLL).
- `dotnet10 run --project tools/validation/waylith-api/Probe.csproj -c Release`: **PASS** (5 critical private fields, 2 packed texture assets and the installed Dubs 1.7 reflection contract).
- Actual `RimWorld.exe -Headless -quicktest -mod=...` with an isolated `native-smoke-colony` test: **PASS**. Four maps, autonomous work, physical hauling, construction, global production bills, multi-floor combat, three real save/load cycles, and per-floor profiler installation/removal (34 hooks). Evidence: `obj/waylith17-audit-20261018/native-smoke-colony/`.
- First graphics test: **FAIL**, one missing texture `Other/SnowPolluted`; caught and corrected.
- Retest using `-NoWindow` (graphics initialized, hidden window): **PASS**, five captures, ten screenshots, zero logged errors or captured GUI exceptions. FPS in these small scenarios: ~143.8-143.9, near a frame cap. This is **not** evidence of a speedup relative to the 1.6 version. Evidence: `obj/waylith17-audit-textures-fixed/native-performance/`.
- Two game tests were run with disposable profiles and `-mod`, not with the user's own game settings or valuable saves. The headless log can complain about absent display resources or resolution without implying a failed colony test; final graphics retest contains no errors.

## Outstanding risks: prioritized

**P1 - Cross-floor thermal safety:** Native pawn temperature escape logic usually evaluates the current map. DeepRim models underground floors as separate maps; freezing/overheating pawns can remain idle without selecting a warm floor. A cross-floor safe-temperature job with valid portal routes, cooldowns, and drafted/forced job priority rules is a gameplay feature still missing, not something solved by .NET 10. Add a dedicated cold/hot native test before implementing it.

**P1 - User saves and other mods:** Reproduce known problem saves after backing them up, test RimWorld 1.6 to 1.7 migration, and validate Dubs Bad Hygiene, modded building/race AI and DLC interactions. The small Core/Harmony/DeepRim test does not prove broad modpack compatibility.

**P1 - Long-running memory and rendering:** Stress hundreds of pawns across 5+ floors, fast speed, repeated map swaps and zoom, construction/roof changes, and save/load for hours. Compare Waylith's native allocation warnings against a pure vanilla baseline; they are not attributed to DeepRim.

**P2 - Visual placement preview:** The earlier ghost tests covered spawned blueprints and lower-level geometry, not a live cursor preview for an unplaced building, each UI scale, or every opacity mode. Preserve a separate manual/UI test.

**P2 - Native Waylith frame profiling:** RimWorld 1.7 includes a built-in developer **Frame Profiler**. In Dev Mode -> Actions/tools -> General -> Frame Profiler, enable Instrumentation for the `DeepRim.Vertical` assembly and/or its patches, inspect CPU, allocations, GC and per-frame stalls. Avoid blanket all-assemblies instrumentation for performance comparisons; it adds overhead. The current DeepRim profiler's 34 temporary hooks remain useful for cross-floor attribution.

**P3 - Future 1.7 updates:** Re-run the net10 build, ABI/assets probe, mod package validation and separate in-game tests whenever unstable updates. The game internals are not a frozen API and Harmony 3 rejects mismatched injection parameters.

## Installation and rollback

Installed the new 1.7 DLL in `RimWorld/Mods/DeepRimVertical/1.7/Assemblies` with hash verification. The previous installed version is backed up at `obj/install-backups/20261018-waylith-api-audit-before/DeepRimVertical`. No 1.6 DLL or user saves were overwritten.
