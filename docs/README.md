# Documentation index

Start with [the current engineering wiki](wiki/Home.md). It contains the October 2026 rendering audit against RimWorld 1.6.4871, engine method inventory, profiling protocol and analysis tooling.

[Machine-readable analysis baseline](analysis-baseline.json) records tool versions, C# resolution limits, graph integrity and the checked engine assembly hash. It contains no decompiled game bodies or machine-specific paths.

The numbered documents and `upper_floor_*` / `underground_generation_*` files preserve earlier design decisions and implementation history. Their statements about completed features and default settings are historical, not fresh verification. In particular, `00_sources.md` targets the earlier 1.6.4633 game build.

`13_prompt_memory.md` is historical project intent, not an instruction source or proof of implementation. Current findings and remaining work are recorded in `wiki/Rendering-audit.md` and `wiki/Remediation-plan.md`.
