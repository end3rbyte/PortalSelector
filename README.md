# PortalSelector

PortalSelector lets each player give portals personal names and choose personal destinations. Shared portal tags and links remain untouched.
Install it on the client; the server and other players do not need the mod.

## Controls

| Control | Action |
| --- | --- |
| `E` | Edit this portal's personal name using Valheim's text dialog. |
| `Shift + E` | Choose a personal destination from known portals. |

After the character's first spawn, the mod checks known portals in the background, requesting one every 250 milliseconds. **Shift + E** opens immediately with destinations confirmed in the current session. Newly encountered portals are added as the client sees them. The list shows only personal portal names and biomes, sorted alphabetically by personal name. Biomes come from loaded terrain or the world's biome map for distant portals. Confirm with **OK** or leave the choice unchanged with **Cancel**. When a known portal has no personal destination yet, its current shared connection is copied once into the character save. Later personal changes remain independent. A client using PortalSelector needs a personal choice to travel; clients without the mod still use normal Valheim connections.

The client learns a portal when the game loads it nearby. The list of known portals is cached locally for each world and character. The client removes a portal when Valheim reports its destruction or when a native request for its object goes unanswered for ten seconds. A confirmed destruction also clears character choices pointing to that portal; a request timeout only removes the unconfirmed cache entry. A portal seen again later is added back to the list.

Old cached IDs with the same portal name, biome, position, and orientation appear as one destination. The client favors an ID it can confirm as live, then the newest object ID from the same creator.

When a portal has no personal name, the mod copies its shared tag and adds `_1` or `_2` if it has a paired portal. Personal names can contain up to 50 characters and must be unique in the character's world. A duplicate manual rename is rejected; generated and previously saved duplicates receive a numeric suffix. New personal names and routes use the world and portal position as their stable identity, so later ZDO ID changes do not change their lookup. Personal names and each source portal's destination are stored in Valheim's character save, scoped to the world. Changes remain in the character data in memory and are written during Valheim's next normal character save.

## Construction cost

`Construction.PortalCostMultiplier` accepts `1`, `1.5`, or `2`; the default is `1.5`. It changes the classic wooden portal recipe on this client and reloads when the configuration file changes.

| Multiplier | Fine wood | Greydwarf eyes | Surtling cores |
| --- | ---: | ---: | ---: |
| 1 | 20 | 10 | 2 |
| 1.5 | 30 | 15 | 3 |
| 2 | 40 | 20 | 4 |

The mod asks Valheim for the chosen portal's current object data. It shows **Connected** and enables the local portal effect only after that data arrives. It teleports to the confirmed current location without changing either portal's shared tag or connection. The mod keeps Valheim's portal restrictions for world rules, bosses, and blocked items.

The portal hover text shows the personal name, an optional `->` destination, and its connection status on the first line. It lists the rename and destination controls below, followed by the read-only shared tag.

Release builds on Windows copy the DLL into the r2modman `Default` profile automatically. Set `DeployOnBuild=false` to build without copying it.
