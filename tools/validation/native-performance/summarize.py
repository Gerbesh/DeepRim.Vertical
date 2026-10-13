"""Summarize accepted normal-window probe captures using only the Python stdlib."""
import csv
import re
import sys
from collections import defaultdict
from pathlib import Path

root = Path(sys.argv[1])
with (root / "summary.csv").open(encoding="utf-8-sig", newline="") as handle:
    captures = list(csv.DictReader(handle))
groups = defaultdict(list)
for capture in captures:
    groups[re.sub(r"^r\d+-", "", capture["scenario"])].append(capture)

print("| Scenario | Captures | FPS range | TPS range | Target TPS at end | Mean tick ms range |")
print("|---|---:|---:|---:|---:|---:|")
for scenario, rows in groups.items():
    def limits(key, digits=1):
        values = [float(row[key]) for row in rows]
        return f"{min(values):.{digits}f}–{max(values):.{digits}f}"
    print(f"| {scenario} | {len(rows)} | {limits('fps')} | {limits('tps')} | {limits('target_tps', 0)} | {limits('tick_mean_ms', 3)} |")

if "--table-only" in sys.argv:
    raise SystemExit(0)

metrics = {"FrameCpu", "GuiCpu", "MeshDraw", "MeshUpdate", "UnderlayRebuild", "WorkerScan", "GhostDraw",
           "FullInvalidation", "StateRebuild", "SupportRebuild", "TerrainRebuild", "MaskRebuild", "NeutralBaseRebuild",
           "WorkPlanning", "RoutePlanning", "TaskMaintenance", "UtilityMaintenance", "PowerGraph", "PlumbingRefresh"}
print("\nInclusive scope attribution; different scopes can overlap. Values are measured per capture.")
for capture in captures:
    scenario = capture["scenario"]
    with (root / "Measurements" / f"{scenario}.csv").open(encoding="utf-8-sig", newline="") as handle:
        report = list(csv.DictReader(handle))
    totals = defaultdict(lambda: [0, 0.0])
    for row in report:
        if row["kind"] != "timing" or row["metric"] not in metrics:
            continue
        totals[row["metric"]][0] += int(row["calls"])
        totals[row["metric"]][1] += float(row["total_ms"])
    print("\n" + scenario)
    for metric, (calls, total_ms) in sorted(totals.items(), key=lambda item: -item[1][1]):
        print(f"  {metric}: {calls} calls, {total_ms / float(capture['frames']):.4f} ms/frame, {total_ms / max(1, calls):.4f} ms/call")
if any(int(row["exceptions"]) for row in captures):
    raise SystemExit("Capture exceptions present; inspect raw evidence before acceptance")
