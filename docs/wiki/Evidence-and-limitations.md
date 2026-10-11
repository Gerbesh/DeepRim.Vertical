# Доказательства и границы исследования

## Исследованный снимок

- Дата: 11 октября 2026, часовой пояс пользователя Europe/Moscow.
- Исходная версия мода: `62e4dd763e5ae53029776882cdafcbbd75ef833c`.
- Текущая установленная игра: `1.6.4871 rev590`, прочитано из Version.txt.
- Unity: `2022.3.35f1 (011206c7a712)`, строка инициализации в Player.log. Это информация локального запуска, не вывод из TargetFramework мода.
- SHA-256 Assembly-CSharp.dll: `5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A`.
- ILSpy CLI: 9.1.0.7988; извлечено 40 типов из `tools/analysis/engine-types.txt`.
- Build: Release, .NET SDK 8.0.425, net472, установленные RimWorld/Harmony refs.

Историческое `docs/00_sources.md` описывает `1.6.4633 rev1260`; это другой снимок. Все заключения о нынешнем DrawMapMesh относятся именно к DLL с указанным hash. Повторный `inspect-engine.ps1` записывает новый manifest, а обновление вики требует нового аудита.

## Как перепроверить важные утверждения

| Утверждение | Собственный код мода | Метод игры, который нужно открыть локально |
|---|---|---|
| Пропущены global layers/gravship | VerticalSectionRenderPatches.RenderMap | MapDrawer.DrawMapMesh |
| Динамические секции вызываются не как в ванили | RenderMap | Section.DrawSection, DrawDynamicSections |
| Overload coverage неполное | MapDrawer_MapMeshDirty_VerticalSectionRender_Patch | MapDrawer.MapMeshDirty обоих overloads; TerrainGrid.DoTerrainChangedEffects; SnowGrid.SetDepth |
| ClearSubMeshes не освобождает Mesh | SectionLayer_LowerLevel.Regenerate/GetColorSubMesh | MapDrawLayer.ClearSubMeshes/GetSubMesh/Dispose |
| Слой автоматически обнаруживается | SectionLayer_LowerLevel, LowerLevelSectionWorker | Section constructor |
| Ghost не применяет tint/fog policy | VerticalGhostRenderer.DrawPawnGhost; VerticalPawnVisibilityService.CanSeePawn | PawnRenderer.RenderPawnAt; DynamicDrawManager.DrawDynamicThings |
| Все карты продолжают update/ticks | VerticalMapComponent | Game.UpdatePlay; TickManager.DoSingleTick |
| Пути/reservations принадлежат карте | VerticalRoutingPatches; VerticalTransferService | Reachability.CanReach; Pawn_PathFollower.StartPath; ReservationManager.CanReserve |
| Save ownership идёт через MapParent | VerticalFloorRecord.ExposeData | Game.ExposeData; Scribe_References.Look |

Raw декомпиляция хранится только локально. Публикуются выводы, ссылки на открытые исходники мода, сигнатуры и собственные диаграммы; игровые DLL и полные исходники игры не включаются в репозиторий или Wiki.

## Первичные внешние источники

- [Unity Graphics.DrawMesh 2022.3](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Graphics.DrawMesh.html): deferred submission и отдельные свойства draw через PropertyBlock.
- [Harmony 2 Prefix](https://harmony.pardeike.net/v2/articles/patching-prefix.html): последствия return false и совместимость patches.
- [CodeGraph upstream](https://github.com/colbymchenry/codegraph): дистрибутив и команды инструмента.
- [CodeSlicer upstream](https://github.com/artemnoor/CodeSlicer): CLI/evidence graph; установлена закреплённая версия.
- [Codex MCP](https://developers.openai.com/codex/mcp): project-scoped server configuration.

Главный источник сигнатур игры — установленная DLL, а не найденный на GitHub декомпил. Assembly version и успешный compile не подтверждают совпадение всех строковых Harmony targets: нужны runtime patch report и сценарии игры.

## Что осталось неизвестным

Нет игрового trace, сохранения пользователя с воспроизводимым симптомом, измеренных кадров или heap snapshots. Поэтому нельзя утверждать, какой пункт объясняет большую часть прежних лагов, сколько FPS даст фикс и сколько памяти реально утекало. Не проверены все gen steps/DLC scenarios, все Unity shaders и все внутренние методы 40 типов; индекс методов шире списка реально подробно исследованных тел.

CodeSlicer без Roslyn не даёт compiler-exact slicing. Call graphs не видят все Harmony edges, XML activation и reflection. Ручная проверка использовала эти контракты дополнительно. Два удалённых класса были обычными утилитами, а не engine-discovered types.

Предыдущие GitHub Actions runs уже были красными до исследования. Запрос старых логов вернул HTTP 410; причина старых сбоев не установлена и не объявлена исправленной. Для новой итерации нужно смотреть новый запуск CI.
