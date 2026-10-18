# Compact floor HUD / native menu probe (RimWorld 1.7)

Test-only code, not included in the production DeepRim DLL. Compile with:

    ./.tools/dotnet10/dotnet.exe build tools/validation/native-menu/Probe.csproj -c Release

Create a disposable mod with package ID 'gerbe.deeprim.menuprobe', whose Assemblies folder contains the resulting DeepRim.CompactFloorMenuProbe.dll. Make a separate game profile whose path ends in 'native-floor-menu', enabling Core, Harmony, DeepRim and the temporary probe. Launch RimWorld.exe with '-batchmode -quicktest -savedatafolder=<that profile>'. The probe waits for the game and map, opens the compact HUD's native FloatMenu through its private action method, asserts it appears in the actual game's WindowStack, writes result.txt, and quits. Delete the temporary probe mod after the run. Do not enable in a real save.

This tests actual menu creation but does not simulate a mouse click, verify the resulting jump, or inspect screenshot/UI scale. Those remain separate manual checks.

Observed pass on RimWorld 1.7.4981 rev136 (Waylith) on October 18, 2026.
