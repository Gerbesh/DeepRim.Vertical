param([string]$RimWorldDir='C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$routeRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$routeManaged=Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
foreach($dll in @('UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.dll','Assembly-CSharp.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $routeManaged $dll))}
[void][Reflection.Assembly]::LoadFrom('C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll')
[void][Reflection.Assembly]::LoadFrom((Join-Path $routeRoot '1.6/Assemblies/DeepRim.Vertical.dll'))
function Assert-Route([bool]$pass,[string]$message){if(-not $pass){throw $message}}
$edges=[Collections.Generic.Dictionary[string,single]]::new()
$edges['0:2']=8; $edges['0:3']=1; $edges['3:4']=1; $edges['4:1']=1; $edges['2:1']=1
$lookup=[Func[int,int,System.Nullable[single]]]{param($a,$b)if($edges.ContainsKey("${a}:$b")){return $edges["${a}:$b"]};return $null}
$route=[DeepRim.Vertical.VerticalRouting.RouteMath]::ShortestPath(5,0,1,$lookup)
Assert-Route (($route -join ',') -eq '0,3,4,1') 'Route did not select cheaper directed endpoint path.'
[void]$edges.Remove('3:4'); [void]$edges.Remove('0:2')
Assert-Route ($null -eq [DeepRim.Vertical.VerticalRouting.RouteMath]::ShortestPath(5,0,1,$lookup)) 'Route crossed an unreachable intermediate local leg.'
$edges['3:4']=-1
Assert-Route ($null -eq [DeepRim.Vertical.VerticalRouting.RouteMath]::ShortestPath(5,0,1,$lookup)) 'Negative traversal cost was accepted.'
$edges['3:4']=[single]::NaN
Assert-Route ($null -eq [DeepRim.Vertical.VerticalRouting.RouteMath]::ShortestPath(5,0,1,$lookup)) 'NaN traversal cost was accepted.'
$edges['3:4']=1
Assert-Route ($null -eq [DeepRim.Vertical.VerticalRouting.RouteMath]::ShortestPath(5,1,0,$lookup)) 'Directed route became bidirectional implicitly.'
$record=[DeepRim.Vertical.VerticalRouting.VerticalTaskRecord]::new()
Assert-Route ($record.expectedJobId -eq -1) 'Fresh task owns an unrelated job.'
[xml]$defs=Get-Content (Join-Path $routeRoot '1.6/Defs/JobDefs/DeepRimVertical_Routing.xml') -Raw
Assert-Route (@($defs.Defs.JobDef).Count -eq 5) 'Routing job definitions missing.'
foreach($job in $defs.Defs.JobDef){Assert-Route ($job.dropThingBeforeJob -eq 'false') 'Routing phase may drop cargo at its start.'}
foreach($name in @('DeepRimVertical_Travel','DeepRimVertical_Pickup')){Assert-Route ($defs.Defs.JobDef.Where({$_.defName -eq $name}).carryThingAfterJob -eq 'true') 'Transit phase may drop cargo during a hop.'}
$targets=@(
 [HarmonyLib.AccessTools]::Method([RimWorld.JobGiver_Work],'TryIssueJobPackage'),
 [HarmonyLib.AccessTools]::Method([Verse.AI.Pawn_JobTracker],'CleanupCurrentJob'),
 [HarmonyLib.AccessTools]::Method([Verse.AI.ThinkNode_QueuedJob],'TryIssueJobPackage'),
 [HarmonyLib.AccessTools]::Method([Verse.AI.ReservationManager],'CanReserve'),
 [HarmonyLib.AccessTools]::Method([Verse.AI.ReservationManager],'CanReserveStack')
)
Assert-Route (@($targets.Where({$null -eq $_})).Count -eq 0) 'Native routing/claim target changed.'
Write-Output 'PASS: directed weighted endpoint routes, blocked intermediate legs, invalid costs, fresh intent ownership, carry-safe job defs and native routing/claim targets. Native pawn walking, cargo and save/load require the separate game probe.'
