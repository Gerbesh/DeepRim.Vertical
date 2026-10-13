# Профилирование и проверки

Первоначальная исследовательская итерация 11 октября не содержала игровых замеров. 13 октября выполнен [живой DEV-прогон пяти этажей](Live-profiling-2026-10-13.md): 22 записи без ошибок, FPS/TPS, включение/выключение подложки и повтор в обратном порядке. Ниже сохранён более широкий протокол; его дополнительные поля не измерялись.

В моде есть [встроенный профилировщик этажей](../per_floor_profiling.md): CPU-время карт, TPS, тик/кадр, объекты/системы, пересборки и CSV/TXT. Запись и окно проверены в обычной живой игре. GPU, фоновые path jobs, GC allocations и native retained resources требуют отдельных средств, поэтому таблица расширенного протокола ниже не заполнена вместо них средним FPS.

## Фиксировать условия

Указать build игры/Unity, commit мода, DLC, список mods/load order, размеры карт, количество этажей/пешек и settings. Сохранить одинаковый save и положение камеры. Прогреть shaders и секции перед steady-state замером. Debug verbose выключить во всех сравниваемых вариантах.

Сделать три одинаковых прогона каждого сценария, отдельно сохраняя первый кадр и прогретый участок. Сравнивать median и p95 **frame time в ms**, GC allocations/frame, число rebuilds и retained Mesh. Средний FPS сглаживает короткие подвисания.

## Сравнения режимов

| Вариант | Что измеряет |
|---|---|
| Мод отключён, обычная поверхностная карта | Стоимость игры без его patches, при сопоставимом наполнении |
| Мод включён, surface 0 | Стоимость общих hooks и нескольких карт |
| Активный +1, checkbox overlay выключен | Без основной подложки; invalidation hooks всё ещё работают |
| Активный +1, checkbox включён, Off | Подложка работает, ghosts/support mask выключены |
| Supports / Full | Дополнительный mask опор / динамические пешки |
| +3, несколько пустых UpperVoid этажей | Рекурсивный sampler и множество source maps |

На паузе изолировать визуальный путь, на 1×/3× дополнительно измерять TPS и длительность tick. Если без overlay FPS исправился, но TPS низок, исследовать simulation cost карт отдельно.

## Минимальные счётчики

Нужны временные counters и timing scopes в: `TryGetContext/GetLevel`, mask rebuild, terrain cache rebuild, RebuildWorkers, new worker, Regenerate, neutral DrawMesh, pawn ghost, invalidation received/routed/deduplicated, Dispose. Не логировать каждую клетку: агрегировать один раз в секунду или выгрузить снимок.

Для native памяти считать отдельно **созданные минус уничтоженные собственные Mesh/Material**, а также inspect Unity retained objects. Managed GC heap не доказывает отсутствие native leak. Если Unity Profiler нельзя подключить к release player, начать с собственного Stopwatch/counters и доступных Unity runtime counters; отдельно отметить предел измерения.

## Проверки корректности

| Сценарий | Ожидаемый результат |
|---|---|
| Scroll вправо-влево, zoom, 10 одинаковых циклов | Прогретые workers переиспользуются, retained mesh не растёт без причины |
| Terrain/цвет/roof/snow/pollution/fog на нижней карте | Картинка верхнего consumer меняется без переключения карты |
| Изменение клетки x/z=16/17, край карты | Все затронутые transitions/соседние секции обновлены; нет выхода за bounds |
| Поставить и убрать multi-cell building, blueprint/frame | Occupancy footprint и reveal совпадают; остатки перекрывающихся объектов учитываются |
| +3 → UpperVoid +2/+1 → surface | Источник и fade соответствуют реально видимому этажу |
| Constructed / thin natural / thick natural roof | Согласованная политика непрозрачности, без случайных исключений |
| Пешка/terrain в fog нижней карты | Не раскрывается через overlay |
| Скрытый origin, видимая часть большого объекта | Предсказуемое clipping-правило, без protrusion сквозь верхний пол |
| Настройки overlay on/off/Off/Supports/Full | Семантика соответствует UI; изменение settings обновляет relevant meshes |
| Load A → меню → load B, повторить | Нет старых Map refs/resources и совпадений кеша по ID |
| Odyssey gravship, global layers и сторонний renderer | Ванильный проход сохранён; нет потерянных или двойных слоёв |
| Transfer во время carry/job/reservation | Нет потерянного предмета, зависшей задачи или чужой reservation |
| Underground void path, вход/выход, sealed edge | Пешка не застревает в повторном планировании невозможного пути |
| Закрытая комната, отопление, thick roof | Читаемая и внутренняя температура согласованы |

## Таблица результата

| Save / режим | Median ms | p95 ms | GC/frame | Rebuild/s | Workers created/disposed | Retained native resources | TPS |
|---|---|---|---|---|---|---|---|
| Не измерено | — | — | — | — | — | — | — |

Для принятия исправления важен измеримый эффект при той же картинке. Для paused idle дополнительный rebuild не должен происходить без изменения source/settings/topology. Для камеры по уже известным секциям не должно быть повторного создания ресурсов. Целевой frame budget зависит от компьютера; его нельзя обещать из статического аудита.

## Four-floor gameplay acceptance (2026-10-12)

The isolated test-only colony probe builds a vertical site with four maps, performs three real save/reload cycles, and exercises shared wealth, native incident scheduling, mine/haul/build/food/sleep/recipe/refuel/work, real downward and upward projectiles, original-map queued orders, drafted travel, blocked routes and cargo conservation. The same probe validates owned lower ceiling creation/removal from upper decks and roof persistence across saves, as well as 0/1/2/3+ height-combat modifiers. Local smoke logs are not published as source artifacts: reproduce via tools/validation/native-colony/README.md. The probe is not a real-colony performance benchmark or a guarantee for Combat Extended, modded abilities or every DLC event.
