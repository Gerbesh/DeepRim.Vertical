# 11 Known Issues

## P0 / blocking for release

- Cross-level pathing is not implemented.
- Cross-level hauling, bills and reservations are not implemented.
- Cross-level combat and LOS are not implemented.
- Utility transfer graphs are not implemented.

## P1 / important

- Floor creation currently uses navigator development buttons rather than final buildable stairs/shafts.
- `+` floors are still foundation-style separate maps and do not yet have the planned layered ghost support overlay down to floor `0`.
- Multi-map same-tile world interactions such as caravan/trader aggregation are not yet integrated.
- No verified in-workspace image-generation pipeline is configured; any art assets are currently expected to be produced manually via the web workflow if needed.
- Underground incident suppression is currently focused on storyteller targeting tags and enemy raid rejection; other surface-style arrivals still need explicit audit.
- Underground floors still use a post-process sealing pass on top of the mod-owned underground generator, so generation is not yet a fully custom bare-metal map builder.
