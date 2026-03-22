# 13 Prompt Memory

- Build a production-quality RimWorld 1.6 vertical multi-floor framework, not a cosmetic map switcher.
- Use local current RimWorld install as source of truth.
- Do not invent APIs or Harmony targets.
- Keep documentation updated throughout development.
- Do not ship Harmony inside the mod.
- Avoid Prepatcher unless proven necessary and documented.
- If art/assets are needed, current workflow is manual generation via web version, not workspace-verified live imagegen.
- Current iteration target:
  - keep foundation moving with real code and docs
  - underground floors should generate as sealed rock maps
  - `-1/-2` may contain limited caverns
  - deeper floors should be mostly solid rock plus resources
  - hot deep floors should generate lava rivers if defs make that possible
  - underground map edges should stay rock-sealed
  - no normal edge-arrival raids or map-edge exits on underground floors
  - `-4` should feel geologically cold instead of mirroring surface outdoor temperature
  - future stair-based underground entry should carve only the aligned stair cell footprint
  - `+` floors should show layered ghost overlays of all lower non-negative floors down to `0`
