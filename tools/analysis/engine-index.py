"""Publish selected declaration signatures, never decompiled method bodies."""
import json
from pathlib import Path
import re
from tree_sitter_language_pack import get_parser

ROOT = Path(__file__).resolve().parents[2]
SELECTED = {
    'Verse.Game': {'UpdatePlay', 'ExposeData', 'AddMap', 'RemoveMap'},
    'Verse.TickManager': {'DoSingleTick', 'TickManagerUpdate', 'ExposeData'},
    'Verse.Map': {'MapUpdate', 'MapPreTick', 'MapPostTick', 'FinalizeInit', 'ExposeData', 'Dispose'},
    'Verse.MapGenerator': {'GenerateMap', 'MapGeneratorPostInit'},
    'Verse.MapDeiniter': {'Deinit'},
    'Verse.MapComponentUtility': {'MapComponentUpdate', 'MapComponentTick', 'FinalizeInit'},
    'RimWorld.Planet.MapParent': {'PostMapGenerate', 'ExposeData'},
    'Verse.MapDrawer': {'MapMeshDirty', 'DrawMapMesh', 'MapMeshDrawerUpdate_First', 'SectionAt', 'WholeMapChanged', 'RegenerateEverythingNow', 'Dispose'},
    'Verse.MapDrawLayer': {'GetSubMesh', 'ClearSubMeshes', 'FinalizeMesh', 'DrawLayer', 'RefreshSubMeshBounds', 'Dispose'},
    'Verse.Section': {'Section', 'DrawSection', 'DrawDynamicSections', 'TryUpdate', 'RegenerateDirtyLayers', 'Dispose'},
    'Verse.SectionLayer': {'SectionLayer', 'GetBoundaryRect'},
    'Verse.LayerSubMesh': {'LayerSubMesh', 'Clear', 'FinalizeMesh'},
    'Verse.SectionLayer_Terrain': {'Regenerate'},
    'Verse.SectionLayer_Snow': {'Regenerate'},
    'Verse.SectionLayerGeometryMaker_Solid': {'MakeBaseGeometry'},
    'RimWorld.MapMeshFlagDef': {'PostSetIndices'},
    'RimWorld.MapMeshFlagDefOf': set(),
    'Verse.TerrainGrid': {'SetTerrain', 'SetTerrainColor', 'GetMaterial', 'DoTerrainChangedEffects', 'TerrainAt', 'TerrainAtIgnoreTemp'},
    'Verse.RoofGrid': {'SetRoof', 'RoofAt'},
    'Verse.SnowGrid': {'SetDepth', 'GetDepth'},
    'Verse.FogGrid': {'Unfog', 'IsFogged'},
    'Verse.DynamicDrawManager': {'DrawDynamicThings', 'PreDrawVisibleThings', 'ComputeCulledThings'},
    'Verse.PawnRenderer': {'RenderPawnAt', 'ParallelPreDraw', 'EnsureGraphicsInitialized'},
    'Verse.PawnRenderTree': {'Draw', 'EnsureInitialized', 'TrySetupGraphIfNeeded'},
    'Verse.AI.Pawn_PathFollower': {'StartPath', 'TryEnterNextPathCell', 'StopDead', 'GenerateNewPath'},
    'Verse.PathFinder': {'CreateRequest', 'PushRequest', 'FindPath', 'PathFinderUpdate'},
    'Verse.PathFinderMapData': {'RebuildAll', 'Notify_CellChanged'},
    'Verse.MapDrawLayer_ExteriorLightingOverlay': {'Regenerate', 'DrawLayer'},
    'RimWorld.MapDrawLayer_OrbitalDebris': {'Regenerate', 'DrawLayer'},
    'Verse.Reachability': {'CanReach'},
    'Verse.AI.ReservationManager': {'CanReserve', 'Reserve', 'ReleaseAllForTarget'},
    'Verse.AI.AttackTargetFinder': {'BestAttackTarget'},
    'Verse.Projectile': {'Launch', 'TickInterval', 'Impact'},
    'Verse.Room': {'ExposeData', 'Notify_TerrainChanged'},
    'Verse.RoomTempTracker': {'EqualizeTemperature', 'DeepEqualizationTempChangePerInterval'},
    'Verse.GenTemperature': {'GetTemperatureForCell'},
    'Verse.MapTemperature': {'MapTemperatureTick', 'MapTemperatureUpdate', 'OutdoorTemp', 'SeasonalTemp'},
    'RimWorld.PowerNetManager': {'UpdatePowerNetsAndConnections_First'},
    'Verse.Scribe_References': {'Look'},
    'Verse.Scribe_Collections': {'Look'},
}


def walk(node):
    yield node
    for child in node.named_children:
        yield from walk(child)


def main():
    directory = ROOT / '.analysis/engine'
    manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8-sig'))
    parser = get_parser('csharp')
    pages = [
        '# Индекс выбранных методов движка', '',
        f"Проверенная сборка: **{manifest['gameVersion']}**. Извлечено типов: {len(manifest['types'])}.", '',
        f"Assembly-CSharp SHA-256: `{manifest['assemblySha256']}`.", '',
        'Автоматически извлечены только объявления. Тела методов не публикуются. '
        'Строки относятся к локальным `.analysis/engine/<Type>.cs`, созданным ILSpy указанной версии. '
        'Наличие сигнатуры не означает подробного аудита всех её ветвей. Отсутствие метода в этом выбранном индексе не доказывает его отсутствие в игре.', '',
    ]
    count = 0
    for typename in manifest['types']:
        if typename not in SELECTED:
            continue
        data = (directory / f'{typename}.cs').read_bytes()
        tree = parser.parse(data)
        if tree.root_node.has_error:
            raise RuntimeError(f'C# parsing error: {typename}')
        rows = []
        for node in walk(tree.root_node):
            if node.type not in {'method_declaration', 'constructor_declaration', 'property_declaration'}:
                continue
            name = node.child_by_field_name('name')
            if name is None or data[name.start_byte:name.end_byte].decode() not in SELECTED[typename]:
                continue
            body = node.child_by_field_name('body')
            if node.type == 'property_declaration':
                body = next((child for child in node.named_children if child.type == 'accessor_list'), body)
            if body is None:
                body = next((child for child in node.named_children if child.type == 'arrow_expression_clause'), None)
            end = body.start_byte if body else node.end_byte
            signature = re.sub(r'\s+', ' ', data[node.start_byte:end].decode('utf-8-sig')).strip().rstrip(';')
            if node.type == 'property_declaration':
                signature += ' { get; }'
            signature = signature.replace('|', '\\|')
            rows.append(f'| `{signature}` | {node.start_point.row + 1} |')
        pages.extend([f'## {typename}', '', '| Сигнатура | Локальная строка |', '|---|---|'])
        pages.extend(rows or ['| Отдельные выбранные методы не включены; см. локальный тип / свойства / fields. | — |'])
        pages.append('')
        count += len(rows)
    (ROOT / 'docs/wiki/Engine-method-index.md').write_text('\n'.join(pages), encoding='utf-8')
    print(f'Engine method index: {count} signatures from {len(manifest["types"])} types')


if __name__ == '__main__':
    main()
