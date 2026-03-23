# Upper Floor Rendering Constraints

## Confirmed local rendering classes

- `Verse.MapDrawer`
- `Verse.Section`
- `Verse.SectionLayer`
- `Verse.DynamicDrawManager`
- `Verse.GenDraw`
- `Verse.CellRenderer`
- `Verse.MeshPool`
- `Verse.SolidColorMaterials`

## v1 constraints

- No full composite renderer across all maps
- No multi-floor input
- No per-frame rebuild of all floor data
- No item-spam rendering

## Performance policy

Use:

- cached cell lists
- view-rect culling
- whitelist-based rendering
- event-driven invalidation

Worst case is a dense base across `0/+1/+2/+3` with `Full` mode active; default mode stays `Supports` to keep the normal path lighter.
