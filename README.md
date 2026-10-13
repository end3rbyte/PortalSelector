# PortalSelector

PortalSelector adds personal portal names and destinations without changing shared tags or links. Install it on the client.

## Controls

- **E:** Rename a portal. Names must be unique and can contain 50 characters.
- **Shift + E:** Choose a known destination, grouped by biome and sorted by distance from the map center. Press **OK** to save.

Only named, active portals appear in the destination picker. A saved route to a known portal marks the source as connected and lights it even while the destination is `active=false`. When the player approaches, the portal requests the destination object if needed and allows travel only after receiving the matching object. An unanswered portal becomes `active=false` in the character catalog.

When Valheim reports that a portal was destroyed, its catalog record becomes inactive immediately, including when the portal was not loaded locally. At the first spawn of each connection, saved portals are checked with native object requests spaced 250 ms apart. A portal that does not respond within 10 seconds of its request is marked inactive. When a player changes a personal destination, the previous destination is checked once; it is marked inactive if it does not respond within 3 seconds.

After the first-spawn check finishes, an inactive portal is removed completely from the character catalog if no other cataloged portal has a personal route to or from it. Its name, route, and one-time import marker are removed too. Inactive portals with a route in either direction remain available for rebuilding under their old name.

Names start from shared tags. Unconnected portals have no suffix. In a connected pair, the endpoint nearest another cataloged portal within 100 m gets `_2`; otherwise, one endpoint gets `_2`. Personal data is stored with your character and written on Valheim's next normal save.

On the first scan without an import marker, the mod imports the shared tag and the exact vanilla linked partner together. This also covers portals known before this version. It waits for the partner's ZDO and GUID if needed. A per-portal GUID marker completes the import once; later scans do not re-import changes to the vanilla tag or link. Existing personal names and destinations are preserved.

When a portal is discovered, the mod assigns it a GUID in the portal's ZDO if it has none. The character's per-world catalog also saves that GUID. The GUID catalog does not import portal lists, personal names, or routes from before GUIDs were used; the previous GUID catalog format remains readable. A portal received without a GUID is added only after its GUID becomes available.
Each catalog record keeps position and biome together as display metadata. Portal identity, personal names, and target confirmation use the GUID instead of position.

A rebuilt portal can reuse a personal name if the old portal's object does not answer a 3-second request, regardless of distance. When the new portal takes an inactive portal's name, it inherits that catalog record and GUID with its new ZDOID and updated metadata.
Personal routes store a source GUID and a destination GUID within each world. Renaming does not change a route. A rebuilt portal inherits its previous routes when it reuses the inactive record's GUID.

The classic wooden portal costs **30 fine wood, 15 greydwarf eyes, and 3 surtling cores**. Install PortalSelector and BepInExPack Valheim on your client.

## Diagnostics

PortalSelector writes `[Debug]` entries to BepInEx `LogOutput.log` for catalog loading, GUID assignment, validation, ZDO requests, routes, and teleportation. Set `Diagnostics.EnableDebugLogs` to `false` in the mod config to stop these entries.

## Contact

Report bugs through [GitHub Issues](https://github.com/landoria-gaming/Landoria.PortalSelector/issues).
