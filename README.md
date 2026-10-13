# PortalSelector

PortalSelector adds personal portal names and destinations without changing shared tags or links. Install it on the client.

## Controls

- **E:** Rename a portal. Names must be unique and can contain 50 characters.
- **Shift + E:** Choose a confirmed destination, listed by personal name and biome. Press **OK** to save.

Hover text shows the name, destination, connection status, and shared tag. Known destinations are checked every 250 ms; destroyed portals are removed.

Names start from shared tags. Unconnected portals have no suffix. In a connected pair, the endpoint nearest another unlinked portal within 100 m gets `_2`; otherwise, one endpoint gets `_2`. Personal data is stored with your character and written on Valheim's next normal save.

## Clear personal data

Enter `portalselector_clear confirm` in the console to clear this world's names, destinations, and known portals. Rediscovery pauses until you rejoin; your next normal character save records the removal. Shared portals are unaffected.

The classic wooden portal costs **40 fine wood, 20 greydwarf eyes, and 4 surtling cores**. Install PortalSelector and BepInExPack Valheim on your client.

## Contact

Report bugs through [GitHub Issues](https://github.com/landoria-gaming/Landoria.PortalSelector/issues).
