# Индекс выбранных методов движка

Проверенная сборка: **1.6.4871 rev590**. Извлечено типов: 40.

Assembly-CSharp SHA-256: `5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A`.

Автоматически извлечены только объявления. Тела методов не публикуются. Строки относятся к локальным `.analysis/engine/<Type>.cs`, созданным ILSpy указанной версии. Наличие сигнатуры не означает подробного аудита всех её ветвей. Отсутствие метода в этом выбранном индексе не доказывает его отсутствие в игре.

## Verse.Game

| Сигнатура | Локальная строка |
|---|---|
| `public void AddMap(Map map)` | 348 |
| `public void ExposeData()` | 393 |
| `public void UpdatePlay()` | 660 |

## Verse.TickManager

| Сигнатура | Локальная строка |
|---|---|
| `public void ExposeData()` | 284 |
| `public void TickManagerUpdate()` | 318 |
| `public void DoSingleTick()` | 357 |

## Verse.Map

| Сигнатура | Локальная строка |
|---|---|
| `public void ExposeData()` | 620 |
| `public void FinalizeInit()` | 801 |
| `public void MapPreTick()` | 933 |
| `public void MapPostTick()` | 966 |
| `public void MapUpdate()` | 1148 |
| `public void Dispose()` | 1370 |

## Verse.MapGenerator

| Сигнатура | Локальная строка |
|---|---|
| `public static Map GenerateMap(IntVec3 mapSize, MapParent parent, MapGeneratorDef mapGenerator, IEnumerable<GenStepWithParams> extraGenStepDefs = null, Action<Map> extraInitBeforeContentGen = null, bool isPocketMap = false, bool stepDebugger = false)` | 79 |
| `public static void MapGeneratorPostInit(IEnumerable<GenStepWithParams> genStepDefs, Map map)` | 271 |

## Verse.MapDeiniter

| Сигнатура | Локальная строка |
|---|---|
| `public static void Deinit(Map map, bool notifyPlayer)` | 14 |

## Verse.MapComponentUtility

| Сигнатура | Локальная строка |
|---|---|
| `public static void MapComponentUpdate(Map map)` | 8 |
| `public static void MapComponentTick(Map map)` | 24 |
| `public static void FinalizeInit(Map map)` | 72 |

## RimWorld.Planet.MapParent

| Сигнатура | Локальная строка |
|---|---|
| `public virtual void PostMapGenerate()` | 51 |
| `public override void ExposeData()` | 180 |

## Verse.MapDrawer

| Сигнатура | Локальная строка |
|---|---|
| `public void MapMeshDirty(IntVec3 loc, ulong dirtyFlags)` | 60 |
| `public void MapMeshDirty(IntVec3 loc, ulong dirtyFlags, bool regenAdjacentCells, bool regenAdjacentSections)` | 66 |
| `public void MapMeshDrawerUpdate_First()` | 102 |
| `public void DrawMapMesh()` | 147 |
| `public Section SectionAt(IntVec3 loc)` | 189 |
| `public void RegenerateEverythingNow()` | 227 |
| `public void WholeMapChanged(ulong change)` | 275 |
| `public void Dispose()` | 287 |

## Verse.MapDrawLayer

| Сигнатура | Локальная строка |
|---|---|
| `public LayerSubMesh GetSubMesh(Material material)` | 39 |
| `public virtual void RefreshSubMeshBounds()` | 67 |
| `protected void FinalizeMesh(MeshParts tags)` | 76 |
| `public virtual void DrawLayer()` | 87 |
| `protected void ClearSubMeshes(MeshParts parts)` | 106 |
| `public void Dispose()` | 114 |

## Verse.Section

| Сигнатура | Локальная строка |
|---|---|
| `public Section(IntVec3 sectCoords, Map map)` | 42 |
| `public void DrawSection()` | 62 |
| `public void DrawDynamicSections(CellRect view)` | 89 |
| `private void RegenerateDirtyLayers()` | 149 |
| `public bool TryUpdate(CellRect view)` | 185 |
| `public void Dispose()` | 243 |

## Verse.SectionLayer

| Сигнатура | Локальная строка |
|---|---|
| `public SectionLayer(Section section) : base(section.map)` | 7 |
| `public override CellRect GetBoundaryRect()` | 13 |

## Verse.LayerSubMesh

| Сигнатура | Локальная строка |
|---|---|
| `public LayerSubMesh(Mesh mesh, Material material, Bounds? bounds = null)` | 32 |
| `public void Clear(MeshParts parts)` | 38 |
| `public void FinalizeMesh(MeshParts parts)` | 67 |

## Verse.SectionLayer_Terrain

| Сигнатура | Локальная строка |
|---|---|
| `public override void Regenerate()` | 38 |

## Verse.SectionLayer_Snow

| Сигнатура | Локальная строка |
|---|---|
| `public override void Regenerate()` | 49 |

## Verse.SectionLayerGeometryMaker_Solid

| Сигнатура | Локальная строка |
|---|---|
| `public static void MakeBaseGeometry(Section section, LayerSubMesh sm, AltitudeLayer altitudeLayer)` | 7 |

## RimWorld.MapMeshFlagDef

| Сигнатура | Локальная строка |
|---|---|
| `public override void PostSetIndices()` | 9 |

## RimWorld.MapMeshFlagDefOf

| Сигнатура | Локальная строка |
|---|---|
| Отдельные выбранные методы не включены; см. локальный тип / свойства / fields. | — |

## Verse.TerrainGrid

| Сигнатура | Локальная строка |
|---|---|
| `public TerrainDef TerrainAt(int ind)` | 59 |
| `public TerrainDef TerrainAtIgnoreTemp(int ind)` | 76 |
| `public TerrainDef TerrainAt(IntVec3 c)` | 89 |
| `public Material GetMaterial(TerrainDef def, bool polluted, ColorDef color)` | 175 |
| `public void SetTerrain(IntVec3 c, TerrainDef newTerr)` | 193 |
| `public void SetTerrainColor(IntVec3 c, ColorDef color)` | 470 |
| `private void DoTerrainChangedEffects(IntVec3 c, TerrainDef oldTerr, TerrainDef newTerr, TerrainDef oldFoundation = null)` | 502 |

## Verse.RoofGrid

| Сигнатура | Локальная строка |
|---|---|
| `public RoofDef RoofAt(int index)` | 75 |
| `public RoofDef RoofAt(IntVec3 c)` | 80 |
| `public RoofDef RoofAt(int x, int z)` | 85 |
| `public void SetRoof(IntVec3 c, RoofDef def)` | 90 |

## Verse.SnowGrid

| Сигнатура | Локальная строка |
|---|---|
| `public void SetDepth(IntVec3 c, float newDepth)` | 100 |
| `public float GetDepth(IntVec3 c)` | 148 |

## Verse.FogGrid

| Сигнатура | Локальная строка |
|---|---|
| `public void Unfog(IntVec3 c)` | 29 |
| `public bool IsFogged(IntVec3 c)` | 71 |
| `public bool IsFogged(int index)` | 80 |

## Verse.DynamicDrawManager

| Сигнатура | Локальная строка |
|---|---|
| `public void DrawDynamicThings()` | 182 |
| `private void PreDrawVisibleThings(NativeArray<ThingCullDetails> details)` | 249 |
| `private void ComputeCulledThings(NativeArray<ThingCullDetails> details)` | 261 |

## Verse.PawnRenderer

| Сигнатура | Локальная строка |
|---|---|
| `public void RenderPawnAt(Vector3 drawLoc, Rot4? rotOverride = null, bool neverAimWeapon = false)` | 221 |
| `public void EnsureGraphicsInitialized()` | 293 |

## Verse.PawnRenderTree

| Сигнатура | Локальная строка |
|---|---|
| `public void EnsureInitialized(PawnRenderFlags defaultRenderFlagsNow)` | 130 |
| `public void Draw(PawnDrawParms parms)` | 170 |
| `private void TrySetupGraphIfNeeded()` | 327 |

## Verse.AI.Pawn_PathFollower

| Сигнатура | Локальная строка |
|---|---|
| `public void StartPath(LocalTargetInfo dest, PathEndMode peMode)` | 154 |
| `public void StopDead()` | 204 |
| `private void TryEnterNextPathCell()` | 586 |

## Verse.PathFinder

| Сигнатура | Локальная строка |
|---|---|
| `public PathRequest CreateRequest(IntVec3 start, LocalTargetInfo target, IntVec3? dest, Pawn pawn, PathFinderCostTuning? tuning, PathEndMode peMode = PathEndMode.OnCell, PathRequest.IPathGridCustomizer customizer = null)` | 505 |
| `public PathRequest CreateRequest(IntVec3 start, LocalTargetInfo target, IntVec3? dest, TraverseParms traverseParms, PathFinderCostTuning? mtuning, PathEndMode peMode = PathEndMode.OnCell, Pawn pawn = null, PathRequest.IPathGridCustomizer customizer = null)` | 512 |
| `public void PushRequest(PathRequest request)` | 519 |

## Verse.PathFinderMapData

| Сигнатура | Локальная строка |
|---|---|
| Отдельные выбранные методы не включены; см. локальный тип / свойства / fields. | — |

## Verse.MapDrawLayer_ExteriorLightingOverlay

| Сигнатура | Локальная строка |
|---|---|
| `public override void Regenerate()` | 30 |

## RimWorld.MapDrawLayer_OrbitalDebris

| Сигнатура | Локальная строка |
|---|---|
| `public override void DrawLayer()` | 78 |
| `public override void Regenerate()` | 96 |

## Verse.Reachability

| Сигнатура | Локальная строка |
|---|---|
| `public bool CanReach(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseMode traverseMode, Danger maxDanger)` | 97 |
| `public bool CanReach(IntVec3 start, LocalTargetInfo dest, PathEndMode peMode, TraverseParms traverseParams)` | 102 |

## Verse.AI.ReservationManager

| Сигнатура | Локальная строка |
|---|---|
| `public bool CanReserve(Pawn claimant, LocalTargetInfo target, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool ignoreOtherReservations = false)` | 157 |
| `public bool Reserve(Pawn claimant, Job job, LocalTargetInfo target, int maxPawns = 1, int stackCount = -1, ReservationLayerDef layer = null, bool errorOnFailed = true, bool ignoreOtherReservations = false, bool canReserversStartJobs = true)` | 300 |
| `public void ReleaseAllForTarget(Thing t)` | 409 |

## Verse.AI.AttackTargetFinder

| Сигнатура | Локальная строка |
|---|---|
| `public static IAttackTarget BestAttackTarget(IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator = null, float minDist = 0f, float maxDist = 9999f, IntVec3 locus = default(IntVec3), float maxTravelRadiusFromLocus = float.MaxValue, bool canBashDoors = false, bool canTakeTargetsCloserThanEffectiveMinRange = true, bool canBashFences = false, bool onlyRanged = false)` | 32 |

## Verse.Projectile

| Сигнатура | Локальная строка |
|---|---|
| `public void Launch(Thing launcher, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null)` | 173 |
| `public virtual void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)` | 178 |
| `protected override void TickInterval(int delta)` | 238 |
| `protected virtual void Impact(Thing hitThing, bool blockedByShield = false)` | 559 |

## Verse.Room

| Сигнатура | Локальная строка |
|---|---|
| `public void Notify_TerrainChanged()` | 666 |

## Verse.RoomTempTracker

| Сигнатура | Локальная строка |
|---|---|
| `public void EqualizeTemperature()` | 177 |
| `private float DeepEqualizationTempChangePerInterval()` | 284 |

## Verse.GenTemperature

| Сигнатура | Локальная строка |
|---|---|
| `public static float GetTemperatureForCell(IntVec3 c, Map map)` | 121 |

## Verse.MapTemperature

| Сигнатура | Локальная строка |
|---|---|
| `public float OutdoorTemp { get; }` | 25 |
| `public float SeasonalTemp { get; }` | 41 |
| `public void MapTemperatureTick()` | 66 |

## RimWorld.PowerNetManager

| Сигнатура | Локальная строка |
|---|---|
| `public void UpdatePowerNetsAndConnections_First()` | 114 |

## Verse.Scribe_References

| Сигнатура | Локальная строка |
|---|---|
| `public static void Look<T>(ref T refee, string label, bool saveDestroyedThings = false) where T : ILoadReferenceable` | 7 |
| `public static void Look<T>(ref WeakReference<T> refee, string label, bool saveDestroyedThings = false) where T : class, ILoadReferenceable` | 37 |

## Verse.Scribe_Collections

| Сигнатура | Локальная строка |
|---|---|
| `public static void Look<T>(ref List<T> list, string label, bool saveDestroyedThings, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs)` | 13 |
| `public static void Look<T>(ref List<T> list, string label, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs)` | 32 |
| `private static void Look<T>(ref List<T> list, bool saveDestroyedThings, string label, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs)` | 112 |
| `public static void Look<K, V>(ref Dictionary<K, V> dict, string label, LookMode keyLookMode = LookMode.Undefined, LookMode valueLookMode = LookMode.Undefined)` | 361 |
| `public static void Look<K, V>(ref Dictionary<K, V> dict, string label, LookMode keyLookMode, LookMode valueLookMode, ref List<K> keysWorkingList, ref List<V> valuesWorkingList, bool logNullErrors = true, bool saveDestroyedKeys = false, bool saveDestroyedValues = false)` | 433 |
| `public static void Look<T>(ref HashSet<T> valueHashSet, string label, LookMode lookMode = LookMode.Undefined)` | 524 |
| `public static void Look<T>(ref HashSet<T> valueHashSet, bool saveDestroyedThings, string label, LookMode lookMode = LookMode.Undefined)` | 529 |
| `public static void Look<T>(ref Stack<T> valueStack, string label, LookMode lookMode = LookMode.Undefined)` | 557 |
| `public static void Look<T>(ref Queue<T> valueQueue, string label, LookMode lookMode = LookMode.Undefined)` | 585 |
