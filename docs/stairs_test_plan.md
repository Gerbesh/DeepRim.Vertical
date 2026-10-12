# Stairs and floor creation regression checks

Use a separate test colony with Harmony and DeepRim Vertical. Restart RimWorld after installing the new DLL.

## Independent floor creation

1. Create a level above/below from the navigator with the cursor in different positions.
2. Verify that no staircase, portal or excavated pocket appears at the cursor or map center.
3. Underground maps are initially fogged. Natural caverns remain part of the geology.
4. Repeating the button for an existing level switches to that map without creating a duplicate.

## Construction

1. Open Architect -> Structure. Both stairs up and stairs down should appear without research, at 40 of the selected material. Steel, wood, stone blocks and other native metallic/stony building materials are available.
2. Place a downstairs blueprint on the surface. Before a colonist finishes construction, no passage or lower landing exists.
3. Complete construction normally. The adjacent level is created if missing; an existing level is reused.
4. Verify that only the aligned landing cell is excavated, with no free room. Only the landing and adjacent naturally connected spaces are revealed.
5. Create upstairs stairs. The upper landing has a walkable deck rather than void. The camera stays on the construction map.
6. Select a pawn, right-click the stairs and order the vertical transition. Verify travel in both directions.
7. Cancel a transition before arrival. The pawn should not teleport later when visiting the same cell.
8. A second staircase may not overlap a staircase or its plan. Placement near map edges or outside configured floor limits must be rejected.
9. Block the target cell on an existing floor with a building or blueprint. Stair placement must be rejected without deleting the obstruction.

## Materials and sprites

1. Build steel, wooden and stone-block stairs in both directions. Only 40 of the selected material should be consumed, with no extra steel requirement.
2. Wood uses planks/grain, stone blocks use masonry joints, metals use riveted treads, and jade uses a smooth polished surface. Each material uses its vanilla Stuff color, including different stone-block colors.
3. Check both the construction preview/menu icon and the completed structure. The paired exit must use the same material and sprite family as the paid end.
4. Compare health, construction time, beauty and flammability: vanilla Stuff factors apply; wooden stairs must not be fireproof.
5. Save/load existing fixed-steel stairs, unfinished frames and blueprints from the previous build. They should migrate to steel without missing-Stuff errors. Newly selected materials must remain unchanged.
6. Deconstruct each material at either end. Refund the selected material once; the free exit must not yield additional resources.

## Persistence and removal

1. Save/load with multiple stairs; both ends and their routes remain usable.
2. Deconstruct either end; its paired end and both route records disappear. The paid end refunds materials once.
3. Remove stairs while a pawn is approaching them; no stale transfer should occur.
4. Removing one staircase leaves other staircases between the same floors working.
5. Old saves retain earlier development portals. This change does not remove their existing excavated pockets.

The first implementation uses vanilla Construction work for both excavation and assembly. A dedicated Mining job, automatic hauling/pathfinding across stairs, and stair chains sharing one cell are not implemented.
