param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$HarmonyAssembly = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'
)
$ErrorActionPreference = 'Stop'
# The game's bundled Harmony uses the classic .NET reflection runtime, like RimWorld/Mono.
# Run its IL reader under Windows PowerShell rather than the newer PowerShell Core runtime.
if ($PSVersionTable.PSEdition -eq 'Core') {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -RimWorldDir $RimWorldDir -HarmonyAssembly $HarmonyAssembly
    if ($LASTEXITCODE -ne 0) { throw 'Colonist-bar validation failed in Windows PowerShell.' }
    return
}
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$managedRoot = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
foreach ($dllName in @('UnityEngine.CoreModule.dll', 'Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managedRoot $dllName))
}
[void][Reflection.Assembly]::LoadFrom($HarmonyAssembly)
[void][Reflection.Assembly]::LoadFrom((Join-Path $projectRoot '1.6/Assemblies/DeepRim.Vertical.dll'))

function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

$layoutTarget = [DeepRim.Vertical.VerticalMaps.ColonistBarDrawLocsFinder_CalculateDrawLocs_VerticalSitePatch]::TargetMethod()
Assert-Check ($null -ne $layoutTarget -and $layoutTarget.IsPublic -and $layoutTarget.GetParameters().Length -eq 3 -and $layoutTarget.GetParameters()[1].ParameterType.IsByRef) 'Colonist-bar Harmony target must resolve the public three-argument overload.'

function New-TestPawn([int]$order) {
    $pawn = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Pawn])
    $settingsField = [Verse.Pawn].GetField('playerSettings')
    $settingsType = $settingsField.FieldType
    $settings = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($settingsType)
    $settingsType.GetField('displayOrder').SetValue($settings, $order)
    $settingsField.SetValue($pawn, $settings)
    return $pawn
}

# Only identity and display order are read; these test objects do not initialize Unity maps.
$surface = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Map])
$upper = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Map])
$emptyFloor = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Map])
$otherColony = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Map])
$surfacePawn = New-TestPawn 30
$upperPawn = New-TestPawn 10
$caravanPawn = New-TestPawn 20
$otherCaravanPawn = New-TestPawn 40
$entries = [Collections.Generic.List[RimWorld.ColonistBar+Entry]]::new()
$entries.Add([RimWorld.ColonistBar+Entry]::new($surfacePawn, $surface, 0))
$entries.Add([RimWorld.ColonistBar+Entry]::new($upperPawn, $upper, 1))
$entries.Add([RimWorld.ColonistBar+Entry]::new($null, $emptyFloor, 2))
$entries.Add([RimWorld.ColonistBar+Entry]::new($null, $otherColony, 3))
$entries.Add([RimWorld.ColonistBar+Entry]::new($caravanPawn, $null, 4))
$entries.Add([RimWorld.ColonistBar+Entry]::new($otherCaravanPawn, $null, 5))
$locationKey = [Func[Verse.Map, object]]{
    param($map)
    if ([object]::ReferenceEquals($map, $otherColony)) { return 'other-colony' }
    return 'vertical-colony'
}
$groupCount = [DeepRim.Vertical.VerticalMaps.VerticalColonistBarUtility]::GroupEntries($entries, $locationKey)
Assert-Check ($groupCount -eq 4) 'Floors must share a group; other colonies and caravans must remain separate.'
Assert-Check ($entries.Count -eq 5) 'Empty floors added slots or a colonist was lost.'
Assert-Check ([object]::ReferenceEquals($entries[0].pawn, $upperPawn)) 'Combined portrait order does not honor displayOrder.'
Assert-Check ([object]::ReferenceEquals($entries[0].map, $upper)) 'A pawn lost its real floor map.'
Assert-Check ([object]::ReferenceEquals($entries[1].map, $surface)) 'The surface pawn map was replaced.'
Assert-Check ($entries[0].group -eq 0 -and $entries[1].group -eq 0) 'Floor colonists are in different groups.'
Assert-Check ($null -eq $entries[2].pawn -and [object]::ReferenceEquals($entries[2].map, $otherColony)) 'An empty independent colony disappeared.'
Assert-Check ($entries[3].group -eq 2 -and $entries[4].group -eq 3) 'Independent caravans were merged.'
foreach ($entry in $entries) {
    foreach ($callback in @($entry.reorderAction, $entry.extraDraggedItemOnGUI)) {
        $capturedGroup = $callback.Target.GetType().GetField('group').GetValue($callback.Target)
        Assert-Check ($capturedGroup -eq $entry.group) 'Portrait dragging still targets the old floor group.'
    }
}

$emptyEntries = [Collections.Generic.List[RimWorld.ColonistBar+Entry]]::new()
$emptyEntries.Add([RimWorld.ColonistBar+Entry]::new($null, $surface, 0))
$emptyEntries.Add([RimWorld.ColonistBar+Entry]::new($null, $upper, 1))
$emptyEntries.Add([RimWorld.ColonistBar+Entry]::new($null, $emptyFloor, 2))
$groupCount = [DeepRim.Vertical.VerticalMaps.VerticalColonistBarUtility]::GroupEntries($emptyEntries, $locationKey)
Assert-Check ($groupCount -eq 1 -and $emptyEntries.Count -eq 1) 'An empty vertical colony must retain exactly one location slot.'

# Check the actual installed engine IL, not a synthetic copy of its methods.
$instructionReader = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
    $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
}
foreach ($methodName in @('DrawGroupFrame', 'HandleGroupFrameClicks')) {
    $method = [HarmonyLib.AccessTools]::Method([RimWorld.ColonistBarColonistDrawer], $methodName)
    $original = $instructionReader.Invoke($null, [object[]]@($method, $null))
    $rewritten = @([DeepRim.Vertical.VerticalMaps.ColonistBarColonistDrawer_DrawGroupFrame_VerticalSitePatch]::ResolveGroupMap($original))
    $resolverCalls = @($rewritten | Where-Object { $_.operand -is [Reflection.MethodInfo] -and $_.operand.Name -eq 'ResolveFrameMap' })
    Assert-Check ($resolverCalls.Count -eq 1) "Engine IL is incompatible with the group-frame patch: $methodName"
}
Assert-Check ($null -ne [HarmonyLib.AccessTools]::Method([RimWorld.ColonistBarColonistDrawer], 'ApplyEntryInAnotherMapAlphaFactor')) 'Portrait alpha patch target is missing.'
Write-Output 'PASS: unified floor groups, no empty floor slots, actual pawn maps, ordering/drag callbacks, independent colonies/caravans, installed engine IL targets.'
