# PortalSelector

PortalSelector adds personal portal names and destinations without changing shared tags or links. Install it on the client.

## Controls

- **E:** Rename a portal. Names must be unique and can contain 50 characters.
- **Shift + E:** Choose a known destination, grouped by biome and sorted by distance from the map center. Press **OK** to save.

Only named, active portals appear in the destination picker. Like vanilla, a saved destination marks the source as connected and lights its portal. When the player approaches, the portal requests the destination object if needed and allows travel only after receiving it. An unanswered portal stays in the character catalog with `active=false`.

At the first spawn of each connection, saved portals are checked with native object requests spaced 250 ms apart. A portal that does not respond within 10 seconds of its request is marked inactive. When a player changes a personal destination, the previous destination is checked once; it is marked inactive if it does not respond within 3 seconds.

Names start from shared tags. Unconnected portals have no suffix. In a connected pair, the endpoint nearest another unlinked portal within 100 m gets `_2`; otherwise, one endpoint gets `_2`. Personal data is stored with your character and written on Valheim's next normal save.

When a portal is discovered, the mod assigns it a GUID in the portal's ZDO if it has none. The character's per-world catalog also saves that GUID. The GUID catalog starts empty and does not import earlier portal lists, personal names, or routes. A portal received without a GUID is added only after its GUID becomes available.
Each catalog record keeps position and biome together as display metadata. Portal identity, personal names, and target confirmation use the GUID instead of position.

A rebuilt portal can reuse a personal name if the old portal's object does not answer a 3-second request, regardless of distance. When the new portal takes an inactive portal's name, it inherits that catalog record and GUID with its new ZDOID and updated metadata.
Personal routes store a source GUID and a destination GUID within each world. Renaming does not change a route. A rebuilt portal inherits its previous routes when it reuses the inactive record's GUID.

The classic wooden portal costs **30 fine wood, 15 greydwarf eyes, and 3 surtling cores**. Install PortalSelector and BepInExPack Valheim on your client.

## Diagnostics

PortalSelector writes `[Debug]` entries to BepInEx `LogOutput.log` for catalog loading, GUID assignment, validation, ZDO requests, routes, and teleportation. Set `Diagnostics.EnableDebugLogs` to `false` in the mod config to stop these entries.

## Contact

Report bugs through [GitHub Issues](https://github.com/landoria-gaming/Landoria.PortalSelector/issues).
