# DeepRim Vertical

DeepRim Vertical is a RimWorld 1.6 mod workspace for a production-oriented vertical colony framework. The current milestone establishes the save-safe world and map persistence layer, floor creation flow, floor navigation UI, bilingual settings, and the documentation baseline for continuing implementation.

## Status

This repository is an active development snapshot, not a release build. It currently includes:

- verified RimWorld `1.6.4633 rev1260` research notes
- vertical site registry and floor persistence records
- custom vertical `MapParent` and floor creation services
- floor navigation UI and hotkeys
- initial depth temperature utilities
- English and Russian localization

Planned but not yet implemented:

- cross-floor pathfinding and reservations
- hauling, bills, and utility graph integration
- vertical combat and line-of-sight resolution
- finalized construction UX for stairs and shafts

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

- The `.csproj` references RimWorld and Harmony from local Windows paths. Adjust them if your installation lives elsewhere.
- `docs/` contains the engineering record, current assumptions, and known limitations for the project.

## License

This repository is published under the MIT License. See `LICENSE`.
