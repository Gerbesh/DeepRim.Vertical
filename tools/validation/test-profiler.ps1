param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$HarmonyAssembly = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll',
    [switch]$ClrProbe
)
$ErrorActionPreference = 'Stop'
if ($ClrProbe -and $PSVersionTable.PSEdition -eq 'Core') {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RimWorldDir $RimWorldDir -HarmonyAssembly $HarmonyAssembly -ClrProbe
    if ($LASTEXITCODE -ne 0) { throw 'Profiler validation failed in Windows PowerShell.' }
    return
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$managedRoot = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
foreach ($dllName in @('UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.dll', 'Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managedRoot $dllName))
}
[void][Reflection.Assembly]::LoadFrom($HarmonyAssembly)
[void][Reflection.Assembly]::LoadFrom((Join-Path $projectRoot '1.6/Assemblies/DeepRim.Vertical.dll'))
function Assert-Check([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }

# Outliers, fixed-size percentile bounds and independent snapshots.
$stats = [DeepRim.Vertical.VerticalProfiling.VerticalProfileStats]::new()
Assert-Check ($stats.PercentileUpperBound(.95) -eq 0) 'Empty percentile should be zero.'
for ($i=0; $i -lt 95; $i++) { $stats.Add(1, $false) }
for ($i=0; $i -lt 5; $i++) { $stats.Add(100, $true) }
$stats.Add([double]::NaN, $false); $stats.Add([double]::PositiveInfinity, $false); $stats.Add(-1, $false)
Assert-Check ($stats.Calls -eq 100 -and $stats.Errors -eq 5) 'Invalid samples or escaping exceptions were counted incorrectly.'
Assert-Check ([Math]::Abs($stats.MeanMs - 5.95) -lt .00001) 'Timing mean is incorrect.'
Assert-Check ($stats.PercentileUpperBound(.95) -eq 1 -and $stats.MaxMs -eq 100) 'p95 lost the distribution or the maximum.'
$copy=$stats.Copy(); $stats.Add(10000, $false)
Assert-Check ($copy.Calls -eq 100 -and $copy.MaxMs -eq 100) 'Snapshot was mutated by a later sample.'
Assert-Check ($stats.PercentileUpperBound(1) -eq 10000) 'Overflow bucket should preserve the observed maximum.'

function Add-TestRow($snapshot, [int]$mapId, $metric, [double]$milliseconds, [string]$label, [int]$objectKey=0) {
    $row=[DeepRim.Vertical.VerticalProfiling.VerticalProfileRow]::new()
    $row.Key=[DeepRim.Vertical.VerticalProfiling.VerticalProfileKey]::new($mapId, $metric, $objectKey)
    $row.Label=$label; $row.Stats=[DeepRim.Vertical.VerticalProfiling.VerticalProfileStats]::new()
    $row.Stats.Add($milliseconds, $false); $snapshot.Rows.Add($row)
}
$snapshot=[DeepRim.Vertical.VerticalProfiling.VerticalProfileSnapshot]::new()
$snapshot.Seconds=2; $snapshot.StartedUtc=[datetime]::UtcNow
Add-TestRow $snapshot -1 GameTick 50 'GameTick'
Add-TestRow $snapshot 42 MapPreTick 10 'MapPreTick'
Add-TestRow $snapshot 42 MapPostTick 5 'MapPostTick'
Add-TestRow $snapshot 42 ThingTick 20 'ThingTick'
Add-TestRow $snapshot 42 JobSearch 12 'JobSearch'
$specialLabel="Pawn, `"Test`"`nnext line"
Add-TestRow $snapshot 42 ObjectTick 15 $specialLabel -73
Add-TestRow $snapshot 43 ThingTick 99 'Other floor'
Assert-Check ($snapshot.SimulationMs(42) -eq 35) 'Floor totals double-count details or another floor.'
Assert-Check ($snapshot.Tps -eq .5) 'TPS must use completed tick calls and real capture duration.'
$originalCulture=[Threading.Thread]::CurrentThread.CurrentCulture
try {
    [Threading.Thread]::CurrentThread.CurrentCulture=[Globalization.CultureInfo]::GetCultureInfo('ru-RU')
    $csvRows=@($snapshot.ToCsv() | ConvertFrom-Csv)
    $pawnRow=@($csvRows | Where-Object { $_.metric -eq 'ObjectTick' })[0]
    Assert-Check ($pawnRow.label -eq $specialLabel) 'CSV escaping lost quotes, commas or line breaks.'
    Assert-Check (($csvRows | Where-Object { $_.label -eq 'actual_tps' }).metric -eq '0.5') 'CSV depends on decimal comma locale.'
} finally { [Threading.Thread]::CurrentThread.CurrentCulture=$originalCulture }

# Validate the actual game's hook contracts without JIT of Unity game bodies outside Mono.
# Full runtime installation belongs to the in-game test: the bundled Harmony does not support
# desktop .NET Core, and .NET Framework cannot JIT all current game interface implementations.
if (-not $ClrProbe) {
$hooks=[DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::GetHooks()
Assert-Check ($hooks.Count -eq 23) 'Expected 23 hook contracts.'
foreach($hook in $hooks) {
    $prefixState=@($hook.Prefix.GetParameters() | Where-Object { $_.Name -eq '__state' })[0]
    $finalState=@($hook.Finalizer.GetParameters() | Where-Object { $_.Name -eq '__state' })[0]
    Assert-Check ($prefixState.ParameterType.GetElementType() -eq $finalState.ParameterType) "State mismatch: $($hook.Method)"
    Assert-Check ($hook.Finalizer.ReturnType -eq [void]) 'A timing finalizer must not suppress engine exceptions.'
    foreach($parameter in $hook.Prefix.GetParameters()) {
        if($parameter.Name -like '___*') {
            $field=[HarmonyLib.AccessTools]::Field($hook.Method.DeclaringType, $parameter.Name.Substring(3))
            Assert-Check ($null -ne $field -and $field.FieldType -eq $parameter.ParameterType) "Invalid injected field: $($hook.Method) $($parameter.Name)"
        }
        if($parameter.Name -eq '__0') {
            Assert-Check ($hook.Method.GetParameters()[0].ParameterType -eq $parameter.ParameterType) "Invalid first map argument: $($hook.Method)"
        }
        if($parameter.Name -eq 'pawn') {
            Assert-Check (@($hook.Method.GetParameters() | Where-Object { $_.Name -eq 'pawn' -and $_.ParameterType -eq [Verse.Pawn] }).Count -eq 1) "Missing pawn argument: $($hook.Method)"
        }
    }
}

}

# Run the real prefix/finalizer and owner cleanup against a CLR probe method.
if ($ClrProbe) {
Add-Type 'public static class DeepRimProfilerForeignProbe { public static int Calls; public static void Prefix() { Calls++; } public static void Work() { throw new System.InvalidOperationException("probe"); } }'
$foreign=[HarmonyLib.Harmony]::new('deeprim.validation.foreign')
$preTick=[DeepRimProfilerForeignProbe].GetMethod('Work')
$foreignPrefix=[HarmonyLib.HarmonyMethod]::new([DeepRimProfilerForeignProbe].GetMethod('Prefix'))
[void]$foreign.Patch($preTick, $foreignPrefix, $null, $null, $null)
$patchType=[DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]
$patchFlags=[Reflection.BindingFlags]'NonPublic,Static'
$metrics=$patchType.GetField('Metrics', $patchFlags).GetValue($null)
$profileHarmony=$patchType.GetField('ProfilerHarmony', $patchFlags).GetValue($null)
$probePrefix=[HarmonyLib.HarmonyMethod]::new($patchType.GetMethod('GlobalPrefix'))
$probeFinalizer=[HarmonyLib.HarmonyMethod]::new($patchType.GetMethod('Finalizer'))
try {
    $metrics.Add($preTick, [DeepRim.Vertical.VerticalProfiling.VerticalProfileMetric]::GameTick)
    [void]$profileHarmony.Patch($preTick, $probePrefix, $null, $null, $probeFinalizer)
    $thrown=$null
    try { [void]$preTick.Invoke($null, @()) } catch { $thrown=$_.Exception.GetBaseException() }
    Assert-Check ($thrown -is [InvalidOperationException] -and $thrown.Message -eq 'probe') 'Profiling must preserve the original method exception.'
    Assert-Check ([DeepRimProfilerForeignProbe]::Calls -eq 1) 'Foreign patch was bypassed.'
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::Uninstall()
    Assert-Check ([HarmonyLib.Harmony]::GetPatchInfo($preTick).Owners.Contains('deeprim.validation.foreign')) 'Stopping the profiler removed another owner.'
    Assert-Check (-not [HarmonyLib.Harmony]::GetPatchInfo($preTick).Owners.Contains('gerbe.deeprim.vertical.profiler')) 'Profiler hooks remain while stopped.'
    $metrics.Add($preTick, [DeepRim.Vertical.VerticalProfiling.VerticalProfileMetric]::GameTick)
    [void]$profileHarmony.Patch($preTick, $probePrefix, $null, $null, $probeFinalizer)
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::Uninstall()
    Assert-Check ([DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::InstalledCount -eq 0) 'Reinstall leaked hooks.'
} finally {
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::Uninstall()
    $foreign.Unpatch($preTick, [HarmonyLib.HarmonyPatchType]::All, 'deeprim.validation.foreign')
}
}

# No Unity gameplay calls: give the recorder a private test epoch/thread, then test nested holders.
if (-not $ClrProbe) {
$profilerType=[DeepRim.Vertical.VerticalProfiling.VerticalProfiler]
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$runningField=$profilerType.GetField('<Running>k__BackingField', $flags)
$epochField=$profilerType.GetField('sessionId', $flags)
$profilerType.GetField('mainThreadId', $flags).SetValue($null, [Threading.Thread]::CurrentThread.ManagedThreadId)
$epochField.SetValue($null, 100)
$recorderRows=$profilerType.GetField('Rows', $flags).GetValue($null)
$recorderRows.Clear()
$thing=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Thing])
$thingDef=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.Def].GetField('shortHash').SetValue($thingDef, [uint16]37)
[Verse.Def].GetField('defName').SetValue($thingDef, 'TestObject')
[Verse.Thing].GetField('def').SetValue($thing, $thingDef)
[Verse.Thing].GetField('mapIndexOrState', [Reflection.BindingFlags]'NonPublic,Instance').SetValue($thing, [sbyte]-1)
$runningField.SetValue($null, $true)
try {
    $outer=[DeepRim.Vertical.VerticalProfiling.VerticalThingProfileScope]::new()
    $nested=[DeepRim.Vertical.VerticalProfiling.VerticalThingProfileScope]::new()
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::ThingPrefix($thing, [ref]$outer)
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::ThingPrefix($thing, [ref]$nested)
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::ThingFinalizer($thing, $nested, $null)
    [DeepRim.Vertical.VerticalProfiling.VerticalProfilerPatches]::ThingFinalizer($thing, $outer, [InvalidOperationException]::new('probe'))
    $floorKey=[DeepRim.Vertical.VerticalProfiling.VerticalProfileKey]::new(-2, [DeepRim.Vertical.VerticalProfiling.VerticalProfileMetric]::ThingTick)
    Assert-Check ($recorderRows[$floorKey].Stats.Calls -eq 1 -and $recorderRows[$floorKey].Stats.Errors -eq 1) 'Nested held ticks were counted twice or the exception was lost.'
    $oldScope=[DeepRim.Vertical.VerticalProfiling.VerticalProfiler]::Begin(42, [DeepRim.Vertical.VerticalProfiling.VerticalProfileMetric]::MapPreTick)
    $epochField.SetValue($null, 101)
    [DeepRim.Vertical.VerticalProfiling.VerticalProfiler]::End($oldScope, $false)
    Assert-Check ($recorderRows.Count -eq 2) 'A stale epoch wrote into the next capture.'
} finally { $runningField.SetValue($null, $false); $recorderRows.Clear() }
}

# Every displayed metric and action has matching localizations.
$en=[xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Languages/English/Keyed/DeepRimVertical.xml') -Raw)
$ru=[xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Languages/Russian/Keyed/DeepRimVertical.xml') -Raw)
$enKeys=@($en.LanguageData.ChildNodes | Where-Object { $_.Name -like 'DeepRimVertical.Profiler.*' } | ForEach-Object { $_.Name })
$ruKeys=@($ru.LanguageData.ChildNodes | Where-Object { $_.Name -like 'DeepRimVertical.Profiler.*' } | ForEach-Object { $_.Name })
Assert-Check ((Compare-Object $enKeys $ruKeys).Count -eq 0) 'Profiler localization keys differ.'
foreach ($metric in [Enum]::GetNames([DeepRim.Vertical.VerticalProfiling.VerticalProfileMetric])) {
    Assert-Check ($enKeys -contains "DeepRimVertical.Profiler.Metric.$metric") "Missing metric localization: $metric"
}
if ($ClrProbe) {
    Write-Output 'PASS: CLR probe exception preservation and owner isolation.'
} else {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RimWorldDir $RimWorldDir -HarmonyAssembly $HarmonyAssembly -ClrProbe
    if ($LASTEXITCODE -ne 0) { throw 'CLR profiling probe failed.' }
    Write-Output 'PASS: timing statistics/outliers, floor partition, TPS, CSV/locales, 23 engine hook contracts, nested holders, capture epochs, RU/EN labels. Full Unity/Mono capture requires in-game validation.'
}
