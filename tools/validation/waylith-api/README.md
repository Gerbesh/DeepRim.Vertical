# Waylith 1.7 API contract probe

Run from the repository root with the locally installed .NET 10 SDK:

    .tools/dotnet10/dotnet.exe run --project tools/validation/waylith-api/Probe.csproj -c Release

This checks critical private MapDrawer/temperature field types against the **installed** RimWorld 1.7 assemblies and instantiates the cached Harmony 3 field-access delegates. It does not launch a game or touch saves. Re-run after each unstable-branch update, because these internals are not stable modding APIs.
\nIt also validates the 1.7 fallback snow/sand texture names against the game Assets.zip archive.\n
If Dubs Bad Hygiene's RimWorld 1.7 DLL is installed, the probe also checks the optional plumbing adapter's reflected field/method names (override with DUBS_17_DIR). This is an ABI check, not an end-to-end water-flow test.
