# Карты и симуляция: что получает каждый этаж

## Карта — отдельная симуляция, а не новый Z в Unity

У каждого `Map` собственные terrain/thing/fog grids, regions, reachability, reservations, power networks, комнаты, weather и tick-компоненты. `IntVec3.y` в координатах клеток не превращает ванильное pathfinding в трёхмерное. Высоты AltitudeLayer относятся к визуальному порядку.

`Game.UpdatePlay` вызывает `MapUpdate` всех загруженных карт. `TickManager.DoSingleTick` проходит `MapPreTick`, tick lists и `MapPostTick`; дополнительная карта добавляет нагрузку на симуляцию, даже если камера её не показывает. Поэтому FPS на паузе и TPS при 3× измеряются отдельно. Оптимизация overlay не устранит стоимость всех этажей.

## Генерация и world ownership

Проверенная сигнатура `MapGenerator.GenerateMap`: mapSize, MapParent, MapGeneratorDef, extraGenStepDefs, `Action<Map> extraInitBeforeContentGen`, isPocketMap, stepDebugger. Игра создаёт и регистрирует карту, выполняет callback до content generation, gen steps, FinalizeInit и `MapParent.PostMapGenerate`.

`VerticalMapCreationService` создаёт отдельный MapParent, регистрирует этаж до генерации, использует размер исходной карты и разные генераторы для underground/upper. Это согласуется с движком; world object нужен для сохранения и ownership. Но операция состоит из нескольких изменений состояния: при исключении генератора нужно откатить ещё не завершённый этаж и MapParent, иначе останется частичная запись.

Несколько MapParent на одном PlanetTile не дают автоматически единый объект поселения. Отдельно проверяются caravan, trader, storyteller target tags, raids, quests, wealth и DLC. Блокировка `CanEverExit`, tags и RaidEnemy не доказывает запрет всех способов входа/выхода.

Генерация подземелья и sealed borders требуют проверки playable cells, доступной области входа, mineables, GenStep порядка и восстанавливаемого RNG. Нельзя изменять дефы Core глобально, чтобы получить синтетический biome только для одной карты.

## Пути, задачи и reservations

`Reachability` хранит конкретную карту и отвергает target или pawn другой карты. `Pawn_PathFollower.StartPath(LocalTargetInfo, PathEndMode)` и следующая клетка относятся к карте пешки. `ReservationManager.CanReserve` также требует `claimant.Map == map`.

В этой сборке реализация поиска — `Verse.PathFinder`: path follower создаёт `PathRequest` через `CreateRequest` и отправляет его `PushRequest`. Поэтому старые примеры с другим namespace или синхронным `FindPath` нельзя переносить без проверки. `PathFinderMapData` — отдельный источник данных поиска.

Мод сейчас добавляет команды переноса и защиту от ходьбы в void. Проверка назначения/следующей клетки предотвращает отдельные переходы, но не делает pathfinder осведомлённым обо всех запрещённых void cells. Возможны найденный путь через void и повторные StopDead. Нужны согласованные path costs/regions/walkability и тесты альтернативных маршрутов.

Целевой путь между этажами состоит из маршрута к порталу, reservation обеих сторон, перехода и продолжения задачи на целевой карте. Поиск WorkGiver, hauling/bill targets и выбор ресурсов должен понимать межэтажный маршрут **до** выдачи задачи. Телепортация пешки через DeSpawn/Spawn сама по себе не переносит корректно reservations, job targets, carry state и всех внешних наблюдателей.

Подход: graph порталов над локальным pathfinding каждой карты; отдельная цена пути и совместимая модель глобальной задачи. Не объединять region grids карт механически.

## Бой и line of sight

`AttackTargetFinder` использует вещи, достижимость, дистанции и LOS в контексте карты атакующего. Projectile хранит origin/destination, при TickInterval обращается к своему Map, thing grid, interceptors и крышам. Показать нижнего врага в overlay недостаточно для прицеливания и попадания.

Нужны явные правила шахт/отверстий, межэтажная проекция LOS, перенос снаряда или отдельная модель попадания, friendly fire, cover и roofs. Это новая игровая логика; текущий render sampler нельзя использовать как доказательство физической видимости.

## Температура, комнаты и коммуникации

`MapTemperature.OutdoorTemp/SeasonalTemp` — внешние условия карты. `RoomTempTracker` хранит температуру комнаты и выполняет equalization; `GenTemperature.GetTemperatureForCell` читает клетки/комнаты. Патч возвращаемого значения может заставить читателей видеть одну температуру, пока внутреннее состояние комнаты остаётся другим.

Мод изменяет наружную/сезонную температуру, чтение температуры клетки и deep equalization; верхняя карта синхронизируется с ground. Проверить нужно закрытые комнаты, воздух снаружи, heater/cooler, thick roof, fire и межэтажные отверстия. Межэтажный обмен — отдельные edges с тепловой проводимостью; он не появляется от одинакового OutdoorTemp.

`PowerNetManager.UpdatePowerNetsAndConnections_First` обслуживает сеть своей карты. Для передачи энергии нужен bridge с определённой бухгалтерией потребления/генерации. Water/utilities других модов требуют adapters; рендер может только отображать их.

## Сохранение и загрузка

`Game.ExposeData` сохраняет maps через `LookMode.Deep`. Registry мода сохраняет sites/floors/portals глубоко; FloorRecord ссылается на MapParent через `Scribe_References`. Это правильное направление: reference вместо самостоятельного дублирования Map. После PostLoadInit/FinalizeInit восстанавливаются индексы.

Derived render data нельзя считать сохранённой истиной. После загрузки нужно сбросить caches, восстановить зависимости source→consumer и лениво построить видимые секции. ID карт пригоден только вместе с жизненным циклом текущей игры.

`MapDeiniter.Deinit` и `MapDrawer.Dispose` — точки освобождения ресурсов. Внешние worker layers должны быть освобождены отдельно. Нужны тесты load A→load B в одном процессе, удаления этажа, смены game, сохранения без загруженной карты этажа и удалённого MapParent.

## Порядок развития систем

Сначала стабилизировать владение картами, lifecycle и render correctness. Затем portal graph и walkability, reservations и задачи; после этого hauling/bills, utilities, combat/LOS. Такая последовательность позволяет проверять ошибки по слоям, а не смешивать визуальный overlay с симуляцией.
