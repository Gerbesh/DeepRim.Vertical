# 10 Test Plan

## Research stage

- [x] Confirm local version
- [x] Confirm Harmony dependency location
- [x] Confirm key patch signatures by reflection

## Foundation stage current tests

- [x] Compile project
- [ ] Boot game with mod and Harmony only
- [ ] Open colony map, verify compact floor controls, not a persistent window
- [ ] Create -1 floor from the native floor dropdown
- [ ] Jump back and forth with buttons
- [ ] Open native floor dropdown, verify the floor list and camera preservation
- [ ] Toggle navigator from dedicated keybinding
- [ ] Save/load and verify floor registry persists

## Underground generation validation

- [ ] Create `-1` and verify sealed, initially fogged rock map with some natural caverns and no automatic entrance
- [ ] Create `-2` and verify sealed rock map with smaller cavern footprint
- [ ] Create `-3` and verify mostly solid rock with no large open caverns
- [ ] Build stairs and verify only the aligned landing cell is excavated; see [stairs checks](stairs_test_plan.md)
- [ ] Verify map edges on underground floors remain rock-sealed
- [ ] Verify underground floors cannot exit via map edge
- [ ] Verify enemy raid incidents do not target underground floors
- [ ] Verify `-4` trends toward the configured cold target instead of mirroring the surface outside temperature
- [ ] If hot-depth floor is created, verify lava river generation when lava terrain defs are available
- [ ] Compare underground floor creation hitch before/after mask-driven generation rewrite on `-1`, `-4`, `-10`
