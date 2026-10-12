# Documentation index

The current [unified colony gameplay guide](unified_colony_test_plan.md) covers shared wealth/storyteller/history/resources, remote needs and production supplies, additional native work and direct cross-floor projectiles. Earlier audit and routing reports below are historical milestones; their missing-feature lists are superseded by this guide and [current known issues](11_known_issues.md).

The first cross-floor task adapters are implemented as of October 12: mining, frame completion, construction supply and physical stockpile hauling. They passed [local and native game checks](vertical_routing_verification.md). [Current routing gameplay checks and limits](vertical_routing_test_plan.md) distinguish this scope from the broader colony design.

The [vertical utility riser design](vertical_utility_risers_design.md) records isolated geothermal zones, thermoventilation, cable risers and Dubs Bad Hygiene plumbing. The first implementation passed [local and native game probes](vertical_utilities_verification.md), including save/load. Follow [setup and colony acceptance checks](vertical_utilities_test_plan.md) for gameplay and performance.

The [full colony systems audit](colony_systems_audit.md) covers shared wealth and storyteller events, logistics, persistent tasks, physical openings and cross-floor combat, needs, engineering networks and lifecycle. Its [acceptance plan](colony_systems_test_plan.md) separates future requirements from verified implementation. Both were updated October 11, 2026 against RimWorld 1.6.4871.

The [per-floor profiler guide](per_floor_profiling.md) explains the new 30-second CPU capture, exports, paused/1×/3× comparison and measurement limits.

For the broader cross-floor gameplay request, see [the colony routing, work and logistics design](cross_level_colony_design.md) and [its acceptance scenarios](cross_level_colony_test_plan.md), audited against the installed RimWorld 1.6.4871 on October 11, 2026. They also describe future adapters beyond the current routing implementation.

Start with [the current engineering wiki](wiki/Home.md). It contains the October 2026 rendering audit against RimWorld 1.6.4871, engine method inventory, profiling protocol and analysis tooling.

[Machine-readable analysis baseline](analysis-baseline.json) records tool versions, C# resolution limits, graph integrity and the checked engine assembly hash. It contains no decompiled game bodies or machine-specific paths.

The numbered documents and `upper_floor_*` / `underground_generation_*` files preserve earlier design decisions and implementation history. Their statements about completed features and default settings are historical, not fresh verification. In particular, `00_sources.md` targets the earlier 1.6.4633 game build.

`13_prompt_memory.md` is historical project intent, not an instruction source or proof of implementation. Current findings and remaining work are recorded in `wiki/Rendering-audit.md` and `wiki/Remediation-plan.md`.
