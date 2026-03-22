# 10 Test Plan

## Research stage

- [x] Confirm local version
- [x] Confirm Harmony dependency location
- [x] Confirm key patch signatures by reflection

## Foundation stage current tests

- [x] Compile project
- [ ] Boot game with mod and Harmony only
- [ ] Open colony map, verify navigator appears
- [ ] Create `-1` floor from navigator
- [ ] Jump back and forth with buttons
- [ ] Toggle navigator from bottom-right button
- [ ] Toggle navigator from dedicated keybinding
- [ ] Save/load and verify floor registry persists

## Underground generation validation

- [ ] Create `-1` and verify sealed rock map with safe spawn pocket and some caverns
- [ ] Create `-2` and verify sealed rock map with smaller cavern footprint
- [ ] Create `-3` and verify mostly solid rock with no large open caverns
- [ ] Verify entry pocket is only the aligned stair cell footprint, not a large carved room
- [ ] Verify map edges on underground floors remain rock-sealed
- [ ] Verify underground floors cannot exit via map edge
- [ ] Verify enemy raid incidents do not target underground floors
- [ ] Verify `-4` trends toward the configured cold target instead of mirroring the surface outside temperature
- [ ] If hot-depth floor is created, verify lava river generation when lava terrain defs are available
- [ ] Compare underground floor creation hitch before/after mask-driven generation rewrite on `-1`, `-4`, `-10`
