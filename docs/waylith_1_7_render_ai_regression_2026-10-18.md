# RimWorld 1.7 / Waylith regression fixes, 2026-10-18

## Verified failure from user Player.log

The repeated 1.7 `Verse.AI.ThinkNode_PrioritySorter.TryIssueJobPackage` `NullReferenceException` traces to `VerticalWorkSearch.FindFrame` at the Construction skill lookup (line 97), reached through `VerticalWorkPackagePatch.Postfix`. Null Construction skill records and incomplete frame build definitions are now skipped safely. This avoids a full think-tree exception; **reproducing the original failing pawn in its existing user save remains untested**.

## Renderer repair

- `LowerLevelCellSampler` does not treat naturally occurring rock roofs as opaque painted ceiling quads; constructed roofs remain opaque.
- Under covered lower floors, `Blueprint` and `Frame` objects get an explicit Print path.
- `VerticalSectionLayerUtility.ShouldPrintThing` no longer excludes Blueprint/Frame merely because their drawer type is `None`, and they bypass ordinary storage/snow filtering.
- These fixes preserve the 1.6 project and operate in the net10 Waylith assembly.

## Evidence

- Compiler: `dotnet10 build DeepRim.Vertical.1.7.csproj -c Release` passed, zero errors and warnings.
- Static package verifier: PASS.
- Real native rendered smoke: PASS, five captures, zero reported exceptions (profile `obj/waylith17-reverified/native-performance`).
- Focused ghost/rock probe in actual Waylith: `ROCK_OK` and `COVERED_BLUEPRINT_OK`; rendered smoke PASS, five captures, zero exceptions (profile `obj/waylith17-ghost-test/native-performance`).
- Test screenshots show the upper overlay and covered blueprint. The test places a physical blueprint; **it does not automate a live mouse-hover construction placement preview**.
- A four-floor native colony rerun did not produce an acceptance result due to lengthy quicktest world initialization and test process shutdown. It must not be counted as PASS. No workgiver errors were logged before shutdown.
- Waylith persistent native allocation warnings at shutdown remain unattributed.

## Installation

The compiled 1.7 DLL was copied to `RimWorld/Mods/DeepRimVertical/1.7/Assemblies` with hash verification after the fixes. An installed-copy backup is in `obj/install-backups/20261018-render-ai-before/DeepRimVertical`. Temporary probe mods were removed from the game's Mods directory. User saves and normal mod selection were not modified.

## Outstanding

Run a user-save smoke on a copy of the problem save to verify workgiver errors are gone, inspect genuine placement ghost while a building designator is selected, test overlay with rock plus constructed roofs at different zoom levels, and verify DLC/third-party interactions.