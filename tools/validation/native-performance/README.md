# Rendered DEV performance probe

Opt-in test mod, excluded from the production DLL/package. Use a new isolated save-data folder ending in `native-performance`. It creates/maps floors, drafts the starting colonists, changes speed/overlay and quits after measurement. Never enable it in a real colony.

Build `Probe.csproj` after production. Install only its `DeepRim.Performance.NativeProbe.dll` in a temporary mod with package ID `gerbe.deeprim.performanceprobe` and a dependency on `gerbe.deeprim.vertical`. Append the probe ID only to the isolated profile's ModsConfig.xml. Enable DEV mode and run-in-background in that profile's Prefs.xml.

Launch `RimWorldWin64.exe -quicktest -savedatafolder=<absolute isolated profile> -logFile <absolute log> -screen-fullscreen 0 -screen-width 1600 -screen-height 900` in a visible, non-minimized normal window. **Do not use batch mode or manual ticking for this benchmark.** Leave the window/scene/speed alone during the run.

Protocol: 6-second warmup + 20-second capture. Two initial captures on the surface before adding floors; then create real levels −1/+1/+2/+3 and paired wooden stairs, retain level 0, and save `DEV-performance-five-floors`. Ten surface/underground/upper scenarios run twice, reversing order on the second pass. The game advances through its real TickManager. Starting colonists are drafted; animals, weather, needs and simulation continue, so this is a small live colony comparison, not an identical-state replay or a large-colony stress test.

Each capture requires at least 100 real frames and GUI repaint events; non-paused cases must actually tick. Summary includes successful TPS and the native target TPS (the third speed button can target 360 or 720 depending on what is happening). Frame intervals include waits; they are not CPU or GPU utilization. CPU timing details overlap parent scopes and must not be summed blindly. Captures incur profiler overhead.

Outputs: `summary.csv`, per-case CSV/TXT and Unity screenshots in `Measurements/`, saved baseline in `Saves/`, log, status and `result.txt`. Inspect screenshots for the intended floor/overlay and check timing exceptions before accepting results. The initial October 13 debug-log-window trial and the subsequent error-producing trial are discarded; accept only a run with the debug log closed and no engine/timing errors. Preserve raw evidence outside the production package, then remove the temporary test mod.

`target_tps` in the summary is the target at the end, not an average across the interval. New captures also contain `observed_target_tps`. An optional `visual-only.txt` marker in the isolated profile runs five short five-second UI smoke captures, including the profiler window on +3 and a deliberately fogged patch of the lower surface. Repeat with UI scale 1.5 to check bounds/buttons. These are visual/metadata checks, not a replacement for the full performance protocol.
