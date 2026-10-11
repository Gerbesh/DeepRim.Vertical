# CodeGraph и CodeSlicer

## Что установлено

| Инструмент | Закреплённая версия | Назначение | Проверка |
|---|---|---|---|
| [CodeGraph](https://github.com/colbymchenry/codegraph) | npm `@colbymchenry/codegraph@1.6.2` | Структура C#, callers/callees, impact, локальный MCP | Индекс, CLI-запросы, MCP initialize/list/call |
| [CodeSlicer](https://github.com/artemnoor/CodeSlicer) | `0.5.3`, commit `510aca022de1b59316de20a734954e1427f07e3b` | Evidence graph и ограниченный срез влияния | Анализ Source, graph quality, exact-target impact, MCP initialize/list/call |
| .NET SDK | `8.0.425` | Сборка net472, runtime декомпилятора | Release build |
| ILSpy command line | `9.1.0.7988` | Проверка установленной Assembly-CSharp | 40 выбранных типов |

Исполняемые инструменты, virtualenv и runtime находятся в `.tools/`, индексы — в `.codegraph/` и `.analysis/`. Они исключены из Git. Версии, npm lock и Python dependency lock находятся в `tools/`. Имена совпадают с найденными GitHub-проектами; выбор не означает гарантию их качества.

## Установка и подключение к проекту

Windows PowerShell 7, Git, Python 3.10+ и npm нужны для bootstrap. Обычная установка не требует игровых файлов. Выполнять из корня репозитория:

```powershell
./tools/analysis/setup.ps1 -Engine -ConfigureCodex
```

`-Engine` добавляет локальный .NET/ILSpy; `-ConfigureCodex` создаёт локальную `.codex/config.toml`, если её ещё нет. Существующая конфигурация не перезаписывается. Файл содержит абсолютные пути текущего checkout и не коммитится. Без этих switches ставятся только два анализатора и индексы.

MCP project configuration работает в trusted projects; серверы нужно загрузить в новом сеансе/после перезапуска клиента. Наличие файла не означает, что инструменты уже появились в текущем наборе tools. [Официальная документация Codex MCP](https://developers.openai.com/codex/mcp).

Шаблон для ручного добавления, где `<ROOT>` — полный путь checkout, а `<PWSH>` — PowerShell 7:

```toml
[mcp_servers.deep_rim_codegraph]
command = "<PWSH>"
args = ["-NoProfile", "-File", "<ROOT>/tools/analysis/mcp.ps1", "-Tool", "codegraph"]
cwd = "<ROOT>"
startup_timeout_sec = 60

[mcp_servers.deep_rim_codeslicer]
command = "<PWSH>"
args = ["-NoProfile", "-File", "<ROOT>/tools/analysis/mcp.ps1", "-Tool", "codeslicer"]
cwd = "<ROOT>"
startup_timeout_sec = 60
```

## Практические команды

```powershell
./tools/analysis/run.ps1 -Tool codegraph sync .
./tools/analysis/run.ps1 -Tool codegraph callers DirtyRenderingMapCells
./tools/analysis/run.ps1 -Tool codegraph callees RenderMap
./tools/analysis/run.ps1 -Tool codegraph explore "lower level regeneration"
./tools/analysis/run.ps1 -Tool codeslicer --json analyze . --scope Source --no-daemon --no-research-requests --out .analysis/codeslicer-graph.json
./tools/analysis/run.ps1 -Tool codeslicer --json graph-quality .analysis/codeslicer-graph.json
./tools/analysis/run.ps1 -Tool codeslicer --json impact .analysis/codeslicer-graph.json --target 'method:DeepRim.Vertical.VerticalRendering.VerticalSectionRenderPatches.DirtyRenderingMapCells' --direction both --depth 3
./.tools/python/Scripts/python.exe tools/analysis/verify-mcp.py
```

Проверенный exact-target срез содержит 1 matched node, 109 affected nodes и 150 affected edges на снимке после удаления двух утилит. Это traversal графа, не полноценный data/control-flow slice. Идентичные method names и overloads требуют проверки в исходниках; fuzzy `--symbol` может выбрать также CALL_EXPR.

## Критическое ограничение C#

На проверенном снимке CodeSlicer сообщает `roslyn=unavailable`, `local_semantic` provider. Coverage accounting: 410 call sites, 2 external terminals, **408 actionable unresolved**, 0 resolved exact/inferred. Graph quality при этом сообщает 0 orphan/dangling/duplicate nodes. Эти результаты не противоречат друг другу: структурная целостность графа не доказывает полноту call resolution.

CodeGraph на исходном снимке построил 883 nodes / 1571 edges; после двух удалений до добавления новых tooling files — 866 / 1551. Статистика всего репозитория меняется при добавлении scripts/workflows. CodeGraph доказал полезность на ручных запросах RenderMap и DirtyRenderingMapCells, но Harmony/reflective auto-discovery, внешние DLL и полнота overload resolution также требуют независимой проверки.

Ни один из графов не является основанием автоматически удалять SectionLayer, MapComponent, HarmonyPatch или типы из XML. Диагноз рендера проверялся вручную по исходникам и установленной игре. Следующий шаг для более сильного C# анализа — compiler-backed evidence/адаптер, с тестом разрешения overloads и внешних Verse symbols.

## Локальное исследование игры

```powershell
./tools/analysis/inspect-engine.ps1
./.tools/python/Scripts/python.exe tools/analysis/engine-index.py
./.tools/dotnet/dotnet.exe build DeepRim.Vertical.csproj -c Release
```

Пути игры/Harmony можно переопределить MSBuild properties `RimWorldDir` и `HarmonyAssembly`. Декомпиляция остаётся в `.analysis/engine`; публичный индекс содержит только выбранные сигнатуры и собственные пояснения.

## Публикация Wiki

```powershell
./tools/analysis/publish-wiki.ps1
./tools/analysis/publish-wiki.ps1 -Push
```

Первая команда подготавливает diff в `.tools/wiki`, вторая коммитит и делает обычный push. Wiki должна быть включена, и первую страницу GitHub требует создать через сайт. Репозиторий wiki отдельный; изменение docs/wiki в основном Git автоматически её не обновляет.
