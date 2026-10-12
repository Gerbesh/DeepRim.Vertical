# DeepRim Vertical

DeepRim Vertical is an experimental RimWorld 1.6 vertical colony framework. It includes separate maps for floors, persistence records, floor navigation, underground generation and an upper-floor rendering prototype.

The October 2026 [rendering audit](docs/wiki/Rendering-audit.md) checks the code against installed RimWorld `1.6.4871 rev590`. It identifies missing vanilla draw passes, incomplete invalidation coverage, mesh ownership problems and expensive frame-time work. These rendering issues are documented, not yet fixed. See the [engineering wiki](https://github.com/Gerbesh/DeepRim.Vertical/wiki) and [local documentation index](docs/README.md).

## Status

This repository is an active development snapshot, not a final release build. It currently includes:

- historical RimWorld `1.6.4633 rev1260` notes and a new `1.6.4871 rev590` engine audit
- vertical site registry and floor persistence records
- custom vertical `MapParent` and floor creation services
- floor navigation UI and hotkeys
- one colonist-bar colony group across floors, without extra floor locations or world settlement icons
- independent floor creation and buildable upstairs/downstairs passages under Architect -> Structure
- steel, wooden and stone stairs with native material selection, appearance patterns and colors
- working underground floor generation redesign
- upper-floor creation flow with `UpperVoid` baseline terrain
- upper-floor camera and climate sync to level `0`
- working selective upper-floor lower-level reveal pipeline
- stacked lower-floor visibility across positive levels
- lower-floor live pawn/animal/mech ghost rendering through visible void channels
- upper-floor debug logging and navigator diagnostics
- on-demand per-floor CPU profiler with 30-second captures, hidden-floor simulation costs, work/path/render detail and CSV/TXT export; native three-floor capture/export checked, accelerated colony benchmarks still pending
- depth-based underground resource scaling
- hot-depth lava river generation
- depth temperature utilities
- thermoventilation intake/insulated duct/outlet, cable risers and optional Dubs Bad Hygiene water/sewage risers; see [setup and gameplay checks](docs/vertical_utilities_test_plan.md)
- multi-hop stair routes, saved travel intents and original-map queued orders; automatic mining, frame completion, physical stockpile hauling and construction supply for ordinary colonists; [gameplay checks and limits](docs/vertical_routing_test_plan.md)
- shared colony/world threat wealth and population, coordinated wealth history, one storyteller scheduling target per site and a scoped combined resource readout
- remote food/beds, physical recipe ingredient and fuel delivery, shared production target counts, repair/cleaning/tending/research adapters and directed projectile flight across open floors; [colony acceptance checks](docs/unified_colony_test_plan.md)
- upper-floor decks and full wall blocks produce owned, saved constructed ceilings on the map below without removing native roofs; see [construction checks](docs/upper_floor_test_plan.md)
- downward height advantage for supported projectile weapons (+30/+60/+100% range and cover penalty reduction for +1/+2/+3 or more floors)
- English and Russian localization

Planned but not yet implemented:

- broader work-giver, utility, transport, raid-group and third-party combat adapters; larger colony compatibility acceptance
- vertical falling/gas/fire spread and complete support/shaft construction lifecycle
- dedicated Mining work for stair excavation and finalized shaft construction UX
- finalized parity-quality upper-floor rendering comparable to the reference mod

## Repository layout

- `Source/`: C# implementation
- `1.6/Defs/`: RimWorld XML defs
- `About/`: mod metadata
- `Languages/`: localization files
- `docs/`: architecture, research, schema, and test notes

Build artifacts are intentionally not tracked in GitHub. Compile locally before installing or packaging the mod.

## Build

Requirements:

1. RimWorld installed locally at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld`
2. Harmony installed locally from Steam Workshop id `2009463077`
3. .NET SDK with MSBuild support for `net472`

Build command:

```powershell
dotnet build .\DeepRim.Vertical.csproj -c Release
```

The compiled assembly is written to `1.6\Assemblies\DeepRim.Vertical.dll`.

## Local install

After a successful build, the helper script can copy the mod into a RimWorld mods directory:

```powershell
.\install_mod.ps1
```

The script first tries the game installation mods folder and falls back to the user mods folder.

## Notes

- Override MSBuild properties `RimWorldDir` and `HarmonyAssembly` if your installation lives elsewhere; the `.csproj` keeps the original Windows defaults.
- `docs/` contains the engineering record, current assumptions, and known limitations for the project.
- Upper-floor rendering remains experimental. A successful build does not establish in-game performance or compatibility.

## Code analysis

Pinned [CodeGraph and CodeSlicer setup](docs/wiki/Analysis-tools.md), CLI wrappers, MCP launchers and private engine inspection are available under `tools/analysis/`:

```powershell
./tools/analysis/setup.ps1 -Engine -ConfigureCodex
./tools/analysis/run.ps1 -Tool codegraph explore "lower level regeneration"
```

Local runtimes and game decompilation are ignored by Git. CodeSlicer's C# graph currently lacks Roslyn-backed call resolution; read its limitations before using impact results to change or delete code.

## License

This repository is published under the MIT License. See `LICENSE`.
