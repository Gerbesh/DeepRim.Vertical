# Native routing probe

Test-only mod, excluded from the production assembly and package. Run against an isolated RimWorld save-data folder whose last directory name is exactly `native-smoke-routing`. Never enable this mod in a real colony: it deliberately replaces test resources, colonist work settings and terrain.

Build production first, then `dotnet build tools/validation/native-routing/Probe.csproj -c Release`. The probe DLL appears under `obj/native-routing-probe/Assemblies/`.

To execute, create a temporary RimWorld mod with that DLL under `Assemblies/` and an `About/About.xml` using package ID `gerbe.deeprim.routingprobe`. Copy the user's ModsConfig.xml into the isolated profile's `Config/` directory and append that package ID **only there**. Install the production mod, then launch RimWorld with `-batchmode -quicktest -savedatafolder=<absolute isolated profile> -logFile <absolute log path>`.

The probe creates three floors and uses real native pawns, job drivers, storage and save/load. Stair pairs invoke the production connection service directly: batch mode cannot display the synchronous loading window awaited by queued stair construction. It waits for **all** long events before manually ticking, so partially loaded maps are never advanced.

Coverage: two-hop routes with different intermediate stair cells; actual autonomous mining and walking; saved intent and carried cargo; rotating work search on a generated map with many haulable items; competing local resource reservations; physical stockpile delivery and global remote priorities; native blueprint material deposit and construction cost; original-map queued cell orders; drafted manual travel; blocked/forbidden/missing stairs and safe cargo cancellation; native profiler hooks, hidden-map rows, export and cleanup.

The game writes `result.txt` in the isolated profile and quits after PASS/FAIL. Preserve the log and native saves for diagnosis. Remove the temporary test-mod files afterward; do not append its ID to the real user profile. This controlled probe is not an accelerated-game benchmark or a substitute for colony-scale compatibility testing.
