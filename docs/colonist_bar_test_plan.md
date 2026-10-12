# One colony across vertical floors

1. Load an existing save with surface and several empty upper/underground floors. The top colonist bar has one colony group without empty slots for floors.
2. Move colonists onto different floors. All portraits remain in that colony group; switching floors does not dim its portraits as a remote colony.
3. Double-click a portrait on another floor. The camera switches to that pawn's real map and position.
4. Drag portraits to reorder them; the order is retained after switching floors and after save/load.
5. With another real colony or caravan, those locations retain separate groups. Clicking the vertical colony frame stays on its current floor, or returns to its surface when coming from another location.
6. Inspect the world map: floors have no separate settlement icon, selection target or caravan commands.
7. Create another floor and save/load again; no additional location group appears.

Automated checks: `tools/validation/test-colonist-bar.ps1` exercises the actual compiled grouping helper and checks the frame patches against installed RimWorld method IL. Live layout, mouse handling and world rendering still need in-game verification.

Floors remain separate internal RimWorld maps for simulation and persistence. Colony-wide jobs, wealth/storyteller aggregation and caravan gathering across floors require further integration.
