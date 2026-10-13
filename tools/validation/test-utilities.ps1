param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$HygieneDir = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\836308268'
)
$ErrorActionPreference='Stop'
$utilityRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$utilityManaged=Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
foreach($dll in @('UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.dll','Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $utilityManaged $dll))
}
[void][Reflection.Assembly]::LoadFrom('C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\1.6\Assemblies\0Harmony.dll')
$utilityAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $utilityRoot '1.6/Assemblies/DeepRim.Vertical.dll'))
function Assert-Utility([bool]$pass,[string]$message) { if(-not $pass){throw $message} }
function Near-Utility([double]$a,[double]$b,[double]$epsilon=.0001) { [Math]::Abs($a-$b) -le $epsilon }

# Actual production source selection: continuous column, valve, explicit source and nearest.
$column=[Collections.Generic.Dictionary[int,DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]]::new()
$column[-1]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalDuct',$true)
$column[-2]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalIntake',$true)
$column[-3]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalDuct',$true)
$column[-4]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalIntake',$true)
$lookup=[Func[int,System.Nullable[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]]]{param($level)
    if($column.ContainsKey($level)){return $column[$level]}; return $null
}
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::FindIntakeLevel(0,$null,$lookup) -eq -2) 'Nearest intake was not selected.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::FindIntakeLevel(0,-4,$lookup) -eq -4) 'Explicit source was not respected.'
$column[-3]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalDuct',$false)
Assert-Utility ($null -eq [DeepRim.Vertical.VerticalUtilities.UtilityMath]::FindIntakeLevel(0,-4,$lookup)) 'Closed duct did not break the column.'
$column[-3]=[DeepRim.Vertical.VerticalUtilities.UtilityMath+ColumnNode]::new('ThermalDuct',$true)
[void]$column.Remove(-1)
Assert-Utility ($null -eq [DeepRim.Vertical.VerticalUtilities.UtilityMath]::FindIntakeLevel(0,$null,$lookup)) 'Source search skipped a missing floor section.'

# Heat is conserved, reaches equilibrium without crossing it, reverses for cold intake.
$heat=[DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(60,0,10,20,240)
Assert-Utility (Near-Utility $heat 240) 'Thermal power budget exceeded.'
$from=60-$heat/10; $to=0+$heat/20
Assert-Utility (Near-Utility ($from*10+$to*20) 600) 'Heat transfer created energy.'
$equilibrium=[DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(60,0,10,20,10000)
Assert-Utility (Near-Utility (60-$equilibrium/10) ($equilibrium/20)) 'Transfer overshot equilibrium.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(-20,20,10,20,240) -eq -240) 'Cold intake did not reverse heat flow.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(20,20,10,20,240) -eq 0) 'Equal temperatures transferred heat.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(60,0,0,20,240) -eq 0) 'Invalid room size accepted.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer([float]::NaN,0,10,20,240) -eq 0) 'NaN temperature accepted.'
Assert-Utility ([DeepRim.Vertical.VerticalUtilities.UtilityMath]::HeatTransfer(60,0,10,20,[float]::PositiveInfinity) -eq 0) 'Invalid budget accepted.'

# Production network grouping: parallel links and cycles preserve unique membership; disconnect splits.
$edges=[Collections.Generic.List[Tuple[int,int]]]::new()
foreach($pair in @(@(1,2),@(2,3),@(3,1),@(1,2))){$edges.Add([Tuple[int,int]]::new($pair[0],$pair[1]))}
$groupsMethod=$utilityAssembly.GetType('DeepRim.Vertical.VerticalUtilities.UtilityMath').GetMethod('Groups').MakeGenericMethod([int])
$groups=$groupsMethod.Invoke($null,@([int[]]@(1,2,3,4),$edges))
Assert-Utility ($groups.Count -eq 2 -and $groups[0].Count -eq 3 -and $groups[1].Count -eq 1) 'Cyclic graph duplicated or lost networks.'
$edges.Clear(); $edges.Add([Tuple[int,int]]::new(1,2))
$split=$groupsMethod.Invoke($null,@([int[]]@(1,2,3,4),$edges))
Assert-Utility ($split.Count -eq 3) 'Disconnect did not split the circuit.'

$ledger=[DeepRim.Vertical.VerticalUtilities.UtilityMath+TickLedger[string]]::new()
Assert-Utility ($ledger.Claim(10,'battery')) 'First energy accounting failed.'
Assert-Utility (-not $ledger.Claim(10,'battery')) 'Parallel/rebuilt link ticked the same battery twice.'
Assert-Utility ($ledger.Claim(10,'generator')) 'Distinct generator was lost.'
Assert-Utility ($ledger.Claim(11,'battery')) 'Ledger blocked next-tick charging.'

# Build the real temporary PowerNet view without touching Unity, and verify no reparenting.
$n1=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([RimWorld.PowerNet])
$n2=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([RimWorld.PowerNet])
$trader=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([RimWorld.CompPowerTrader])
$battery=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([RimWorld.CompPowerBattery])
$transNetField=[RimWorld.CompPower].GetField('transNet')
$transNetField.SetValue($trader,$n1); $transNetField.SetValue($battery,$n2)
foreach($net in @($n1,$n2)){
    $net.powerComps=[Collections.Generic.List[RimWorld.CompPowerTrader]]::new(); $net.powerComps.Add($trader)
    $net.batteryComps=[Collections.Generic.List[RimWorld.CompPowerBattery]]::new(); $net.batteryComps.Add($battery)
    $net.transmitters=[Collections.Generic.List[RimWorld.CompPower]]::new()
}
$netList=[Collections.Generic.List[RimWorld.PowerNet]]::new(); $netList.Add($n1); $netList.Add($n2)
$powerGroup=[DeepRim.Vertical.VerticalUtilities.GameComponent_VerticalUtilities+PowerGroup]::new($netList)
$view=$powerGroup.BuildView()
Assert-Utility ($view.powerComps.Count -eq 1 -and $view.batteryComps.Count -eq 1) 'Temporary view duplicated shared components.'
Assert-Utility ([object]::ReferenceEquals($transNetField.GetValue($trader),$n1) -and [object]::ReferenceEquals($transNetField.GetValue($battery),$n2)) 'View changed native component ownership.'
Assert-Utility ($n1.powerComps.Count -eq 1 -and $n2.batteryComps.Count -eq 1) 'View mutated real network membership.'

# Toggle must exit before any live-game lookup, even for a non-null dummy underground map.
$oldSettings=[DeepRim.Vertical.VerticalRuntime]::Settings
try {
    $settings=[DeepRim.Vertical.Settings.VerticalModSettings]::new()
    [DeepRim.Vertical.VerticalRuntime]::Settings=$settings
    Assert-Utility ([DeepRim.Vertical.VerticalTemperature.DepthTemperatureUtility]::EvaluateGeologicalTarget(-4) -eq -20) 'Cold geothermal band changed.'
    Assert-Utility ([DeepRim.Vertical.VerticalTemperature.DepthTemperatureUtility]::EvaluateGeologicalTarget(-15) -eq 60) 'Hot geothermal band changed.'
    $settings.coldDepthLevel=0; $settings.hotDepthLevel=0
    Assert-Utility ([DeepRim.Vertical.VerticalTemperature.DepthTemperatureUtility]::EvaluateGeologicalTarget(-1) -eq -20) 'Invalid cold depth divides by zero.'
    Assert-Utility ([DeepRim.Vertical.VerticalTemperature.DepthTemperatureUtility]::EvaluateGeologicalTarget(-2) -eq 60) 'Hot depth must remain deeper than the cold band.'
    $settings.enableDepthThermalProfile=$false
    $dummyMap=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Map])
    $level=0; $target=[float]0
    Assert-Utility (-not [DeepRim.Vertical.VerticalTemperature.DepthTemperatureUtility]::TryGetGeologicalTarget($dummyMap,[ref]$level,[ref]$target)) 'Disabled profile still supplies a target.'
} finally { [DeepRim.Vertical.VerticalRuntime]::Settings=$oldSettings }

# Exact optional mod ID and runtime binding contract; do not JIT native gameplay bodies in CoreCLR.
[xml]$hygieneMeta=Get-Content -Raw (Join-Path $HygieneDir 'About/About.xml')
Assert-Utility ($hygieneMeta.ModMetaData.packageId.ToLowerInvariant() -eq 'dubwise.dubsbadhygiene') 'Wrong hygiene mod.'
Assert-Utility ('1.6' -in @($hygieneMeta.ModMetaData.supportedVersions.li)) 'No DBH 1.6 support.'
$hygieneAssembly=[Reflection.Assembly]::LoadFrom((Join-Path $HygieneDir '1.6/Assemblies/BadHygiene.dll'))
$binding=@{
    'DubsBadHygiene.CompPipe'=@('pipeNetRef')
    'DubsBadHygiene.PlumbingNet'=@('PipedThings','MapComp','NetType','slave','IP','cells','HeatingCap','CoolingCap','BoilerCapacitySum','HeatStoreCapacitySum','OutdoorUnitCapacitySum','IndoorUnitCapacitySum','WaterCap','GroundWaterCapacitySum','PumpingCapacitySum','WaterPumpedPerTick')
    'DubsBadHygiene.HygienePipeMapComp'=@('MasterInternetID','DBHComps')
}
$bindingFlags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
foreach($typeName in $binding.Keys){$type=$hygieneAssembly.GetType($typeName,$true); foreach($field in $binding[$typeName]) {
    Assert-Utility ($null -ne $type.GetField($field,$bindingFlags)) "Missing DBH field: $typeName.$field"
}}
foreach($entry in @(@('DubsBadHygiene.PlumbingNet','AllTileNets'),@('DubsBadHygiene.PlumbingNet','InitNet'),@('DubsBadHygiene.PlumbingNet','Tick'),@('DubsBadHygiene.HygienePipeMapComp','RefreshInternetsOnTile'),@('DubsBadHygiene.HygienePipeMapComp','MapRemoved'),@('DubsBadHygiene.HygienePipeMapComp','RegenPipeGrids'))){
    Assert-Utility ($null -ne $hygieneAssembly.GetType($entry[0]).GetMethod($entry[1],$bindingFlags)) "Missing DBH method: $entry"
}
Assert-Utility ($null -ne [RimWorld.PowerNet].GetMethod('PowerNetTick')) 'Power tick contract changed.'
Assert-Utility ($null -ne [RimWorld.PowerNetManager].GetField('delayedActions',$bindingFlags)) 'Power topology contract changed.'
Assert-Utility (-not ($utilityAssembly.GetReferencedAssemblies().Name -contains 'BadHygiene')) 'Optional mod became a hard DLL dependency.'

[xml]$defs=Get-Content -Raw (Join-Path $utilityRoot '1.6/Defs/ThingDefs_Buildings/DeepRimVertical_Utilities.xml')
$buildables=@($defs.Defs.ThingDef | Where-Object defName)
Assert-Utility ($buildables.Count -eq 5) 'Wrong utility building count.'
foreach($def in $buildables){
    $kind=[Enum]::Parse([DeepRim.Vertical.VerticalUtilities.UtilityKind],[string]$def.modExtensions.li.kind)
    Assert-Utility ($def.ParentName -eq 'DeepRimVertical_UtilityBase') 'Utility lacks buildable base.'
    if($kind -eq 'Plumbing'){Assert-Utility ($def.MayRequire -eq 'dubwise.dubsbadhygiene') 'DBH def lacks dependency guard.'}
}
foreach($language in @('English','Russian')){
    [xml]$translations=Get-Content -Raw (Join-Path $utilityRoot "Languages/$language/Keyed/DeepRimVertical.xml")
    foreach($key in @('NoConnection','Connected','Valve','ValveDescription','Closed','SelectSource','NearestSource','SourceFloor','NoPower','SourceOffline','EnclosedRoom','Operating','NoAdapter','Insulated','IntakeReading','ThermalReading')){
        Assert-Utility ($null -ne $translations.LanguageData.SelectSingleNode("DeepRimVertical.Utility.$key")) "Missing $language utility key $key"
    }
}
Write-Output 'PASS: source selection, continuous/closed column, heat conservation/equilibrium, cyclic/disconnected networks, once-per-tick ledger, real PowerNet union ownership, geothermal toggle, DBH contracts and optional ID, five building defs, RU/EN keys.'
Write-Output 'This script runs local CLR checks; the separate native probe and remaining colony acceptance are documented in docs/vertical_utilities_verification.md.'
