# DeepRim Vertical

DeepRim Vertical is an experimental RimWorld 1.6 vertical colony framework. It includes separate maps for floors, persistence records, floor navigation, underground generation and an upper-floor rendering prototype.

The October 2026 [rendering audit](docs/wiki/Rendering-audit.md) checks the code against installed RimWorld `1.6.4871 rev590`. It identifies missing vanilla draw passes, incomplete invalidation coverage, mesh ownership problems and expensive frame-time work. These rendering issues are documented, not yet fixed. See the [engineering wiki](https://github.com/Gerbesh/DeepRim.Vertical/wiki) and [local documentation index](docs/README.md).

## Status

This repository is an active development snapshot, not a final release build. It currently includes:

- historical RimWorld `1.6.4633 rev1260` notes and a new `1.6.4871 rev590` engine audit
- vertical site registry and floor persistence records
- custom vertical `MapParent` and floor creation services
- floor navigation UI and hotkeys
- working underground floor generation redesign
- upper-floor creation flow with `UpperVoid` baseline terrain
- upper-floor camera and climate sync to level `0`
- working selective upper-floor lower-level reveal pipeline
- stacked lower-floor visibility across positive levels
- lower-floor live pawn/animal/mech ghost rendering through visible void channels
- upper-floor debug logging and navigator diagnostics
- depth-based underground resource scaling
- hot-depth lava river generation
- depth temperature utilities
- English and Russian localization

Planned but not yet implemented:

- cross-floor pathfinding and reservations
- hauling, bills, and utility graph integration
- vertical combat and line-of-sight resolution
- finalized construction UX for stairs and shafts
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
