param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$managedRoot = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
foreach ($dllName in @('UnityEngine.CoreModule.dll', 'Assembly-CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $managedRoot $dllName))
}
$modAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $projectRoot '1.6/Assemblies/DeepRim.Vertical.dll'))

function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

# Exercise the real persistent records without starting Unity or creating a colony.
$site = [DeepRim.Vertical.Persistence.VerticalSiteRecord]::new()
$cell = [Verse.IntVec3]::new(42, 0, 57)
$otherCell = [Verse.IntVec3]::new(43, 0, 57)
foreach ($route in @(@(0, -1, $cell), @(-1, 0, $cell), @(0, -1, $otherCell), @(-1, 0, $otherCell), @(0, 1, $cell), @(1, 0, $cell))) {
    $site.RegisterPortal([DeepRim.Vertical.Persistence.VerticalPortalRecord]::new($route[0], $route[1], $route[2]))
}
$site.RegisterPortal([DeepRim.Vertical.Persistence.VerticalPortalRecord]::new(0, -1, $cell))
Assert-Check ($site.portals.Count -eq 6) 'Repeated registration duplicated a route.'
# Remove from the generated exit end; other stairs and levels must survive.
$site.UnregisterPortalPair(-1, 0, $cell)
Assert-Check ($site.portals.Count -eq 4) 'Removing a pair removed too many or too few routes.'
Assert-Check (@($site.portals | Where-Object { $_.cellX -eq $otherCell.x }).Count -eq 2) 'Another staircase was removed.'
Assert-Check (@($site.portals | Where-Object { $_.sourceLevel -eq 1 -or $_.targetLevel -eq 1 }).Count -eq 2) 'A route to another level was removed.'
$site.UnregisterPortalPair(0, -1, $cell)
Assert-Check ($site.portals.Count -eq 4) 'Repeated removal affected unrelated stairs.'
$site.UnregisterPortalPair(0, -1, $otherCell)
Assert-Check ($site.portals.Count -eq 2) 'Removing the second staircase did not preserve the upstairs pair.'

[xml]$stairXml = Get-Content -LiteralPath (Join-Path $projectRoot '1.6/Defs/ThingDefs_Buildings/DeepRimVertical_Stairs.xml')
$baseDef = $stairXml.Defs.ThingDef | Where-Object Name -eq 'DeepRimVertical_StairBase'
$stairType = $modAssembly.GetType([string]$baseDef.thingClass, $true)
Assert-Check ([Verse.Building].IsAssignableFrom($stairType)) 'Stair XML has an invalid building class.'
Assert-Check (@($baseDef.comps.li | Where-Object Class -eq 'CompProperties_Forbiddable').Count -eq 1) 'Stairs and generated exits lack the native forbid component.'
Assert-Check ([Enum]::IsDefined([Verse.Traversability], [string]$baseDef.passability)) 'Invalid stair passability.'
Assert-Check ([string]$baseDef.graphicData.graphicClass -eq 'Graphic_Appearances') 'Stairs do not use native material appearances.'
Assert-Check ([string]$baseDef.defaultStuff -eq 'Steel') 'Legacy stairs must retain steel as their default.'
foreach ($category in @('Metallic', 'Woody', 'Stony')) {
    Assert-Check (@($baseDef.stuffCategories.li) -contains $category) "Unsupported construction material category: $category"
}
[xml]$appearanceXml = Get-Content -LiteralPath (Join-Path $RimWorldDir 'Data/Core/Defs/Misc/StuffAppearanceDefs/StuffAppearances.xml')
foreach ($def in @($stairXml.Defs.ThingDef | Where-Object { $_.defName })) {
    $extensionType = $modAssembly.GetType([string]$def.modExtensions.li.Class, $true)
    Assert-Check ([Verse.DefModExtension].IsAssignableFrom($extensionType)) 'Invalid stair extension class.'
    Assert-Check ([Math]::Abs([int]$def.modExtensions.li.levelDelta) -eq 1) 'Stairs must connect adjacent levels.'
    $isExit = [string]$def.modExtensions.li.isExit -eq 'true'
    if ($isExit) {
        Assert-Check (-not $def.designationCategory) 'A generated exit is independently buildable.'
        Assert-Check (-not $def.costList) 'Generated exits would refund free resources.'
        Assert-Check ([string]$def.costStuffCount -eq '0') 'Generated exits would refund free construction material.'
    } else {
        Assert-Check ([string]$def.designationCategory -eq 'Structure') 'Buildable stairs are missing from Structure.'
        $placeType = $modAssembly.GetType([string]$def.placeWorkers.li, $true)
        Assert-Check ([Verse.PlaceWorker].IsAssignableFrom($placeType)) 'Invalid placement worker class.'
        Assert-Check (-not $def.costList) 'Stairs still require steel in addition to the chosen material.'
        Assert-Check ([int]$baseDef.costStuffCount -eq 40) 'Buildable stairs should cost 40 of the chosen material.'
        foreach ($icon in $def.uiIconPathsStuff.li) {
            Assert-Check (Test-Path -LiteralPath (Join-Path $projectRoot ('1.6/Textures/' + $icon.iconPath + '.png'))) 'Missing material menu icon.'
        }
    }
    $textureDirectory = Join-Path $projectRoot ('1.6/Textures/' + $def.graphicData.texPath)
    foreach ($appearance in $appearanceXml.Defs.StuffAppearanceDef.defName) {
        $appearanceFiles = @(Get-ChildItem -LiteralPath $textureDirectory -Filter '*.png' | Where-Object { $_.BaseName.EndsWith($appearance) })
        Assert-Check ($appearanceFiles.Count -eq 1) "Missing or ambiguous $appearance stair sprite in $textureDirectory"
    }
}

# Use actual game material categories to validate the native material selector.
$categories = @{}
foreach ($name in @('Metallic', 'Woody', 'Stony', 'Fabric')) {
    $category = [RimWorld.StuffCategoryDef]::new()
    $category.defName = $name
    $categories[$name] = $category
}
$runtimeStairDef = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
$stairCategories = [Collections.Generic.List[RimWorld.StuffCategoryDef]]::new()
foreach ($name in $baseDef.stuffCategories.li) { $stairCategories.Add($categories[$name]) }
$extensions = [Collections.Generic.List[Verse.DefModExtension]]::new()
$extensions.Add([DeepRim.Vertical.VerticalConstruction.VerticalStairExtension]::new())
[Verse.ThingDef].GetField('stuffCategories').SetValue($runtimeStairDef, $stairCategories)
[Verse.ThingDef].GetField('modExtensions').SetValue($runtimeStairDef, $extensions)
[xml]$materialsXml = Get-Content -LiteralPath (Join-Path $RimWorldDir 'Data/Core/Defs/ThingDefs_Items/Items_Resource_Stuff.xml')
foreach ($materialName in @('Steel', 'WoodLog', 'Silver', 'Gold', 'Plasteel', 'Uranium', 'Jade', 'Cloth')) {
    $materialNode = $materialsXml.Defs.ThingDef | Where-Object defName -eq $materialName
    Assert-Check ($null -ne $materialNode) "Native material definition not found: $materialName"
    $properties = [Verse.StuffProperties]::new()
    $properties.categories = [Collections.Generic.List[RimWorld.StuffCategoryDef]]::new()
    foreach ($categoryName in $materialNode.stuffProps.categories.li) { $properties.categories.Add($categories[$categoryName]) }
    $allowed = $properties.CanMake($runtimeStairDef)
    Assert-Check ($allowed -eq ($materialName -ne 'Cloth')) "Incorrect native material eligibility: $materialName"
}

# Old fixed-steel saves lack Stuff on stairs/frames and stuffToUse on blueprints.
$steel = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.ThingDef].GetField('defName').SetValue($steel, 'Steel')
$wood = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.ThingDef].GetField('defName').SetValue($wood, 'WoodLog')
$frameDef = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.ThingDef].GetField('entityDefToBuild').SetValue($frameDef, $runtimeStairDef)
[Verse.ThingDef].GetField('stuffCategories').SetValue($frameDef, $stairCategories)
$blueprintDef = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.ThingDef].GetField('entityDefToBuild').SetValue($blueprintDef, $runtimeStairDef)
$thingDefField = [Verse.Thing].GetField('def')
$stuffProperty = [Verse.Thing].GetProperty('Stuff')
$setStuffMethod = [Verse.Thing].GetMethod('SetStuffDirect')
foreach ($legacyType in @([DeepRim.Vertical.VerticalConstruction.Building_VerticalStair], [RimWorld.Frame], [RimWorld.Blueprint_Build])) {
    $legacy = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($legacyType)
    $definition = if ($legacyType -eq [RimWorld.Frame]) { $frameDef } elseif ($legacyType -eq [RimWorld.Blueprint_Build]) { $blueprintDef } else { $runtimeStairDef }
    $thingDefField.SetValue($legacy, $definition)
    [DeepRim.Vertical.VerticalConstruction.VerticalStairMaterialUtility]::RestoreLegacySteel($legacy, $steel)
    $result = if ($legacyType -eq [RimWorld.Blueprint_Build]) { $legacyType.GetField('stuffToUse').GetValue($legacy) } else { $stuffProperty.GetValue($legacy) }
    Assert-Check ([object]::ReferenceEquals($result, $steel)) "Legacy steel migration failed: $legacyType"
    if ($legacyType -eq [RimWorld.Blueprint_Build]) { $legacyType.GetField('stuffToUse').SetValue($legacy, $wood) }
    else { [void]$setStuffMethod.Invoke($legacy, [object[]]@($wood)) }
    [DeepRim.Vertical.VerticalConstruction.VerticalStairMaterialUtility]::RestoreLegacySteel($legacy, $steel)
    $result = if ($legacyType -eq [RimWorld.Blueprint_Build]) { $legacyType.GetField('stuffToUse').GetValue($legacy) } else { $stuffProperty.GetValue($legacy) }
    Assert-Check ([object]::ReferenceEquals($result, $wood)) "Migration changed a chosen material: $legacyType"
}
$unrelated = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.Building])
$unrelatedDef = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([Verse.ThingDef])
[Verse.ThingDef].GetField('stuffCategories').SetValue($unrelatedDef, $stairCategories)
$thingDefField.SetValue($unrelated, $unrelatedDef)
[DeepRim.Vertical.VerticalConstruction.VerticalStairMaterialUtility]::RestoreLegacySteel($unrelated, $steel)
Assert-Check ($null -eq $stuffProperty.GetValue($unrelated)) 'Stair migration changed an unrelated building.'

$xmlFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot '1.6/Defs'), (Join-Path $projectRoot 'Languages'), (Join-Path $projectRoot 'About') -Filter '*.xml' -File -Recurse)
foreach ($xmlFile in $xmlFiles) {
    $document = New-Object System.Xml.XmlDocument
    $document.Load($xmlFile.FullName)
}
Write-Output "PASS: route pairs, native material eligibility, appearance sprites/icons, costs, legacy stairs/frames/blueprints, chosen materials, $($xmlFiles.Count) XML files."
