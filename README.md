![](logo.png)
# KindredPortals for V Rising
KindredPortals is a fast travel mod for V Rising. It allows you to create portals to travel between locations.
   - **Note:** Until BepInEx is updated for 1.1, please do not use the thunderstore version. Get the correct testing version https://wiki.vrisingmods.com/user/game_update.html.


- Portal Travel: Functions like caves but not attached to anything unless you make them! Assign mapicons to them or don't! Use [KindredSchematics](https://thunderstore.io/c/v-rising/p/odjit/KindredSchematics/) to place tilemodels at their location for extra pizzazz.
- Waygates: Create new waygates on the map! Accessible by all players via the map, but must be "discovered" first by getting close to them. (Unless unlock all waygates is on)
- MapIcons: Some require attachment to an object to be seen (like a portal), others are fine on their own. [Helpful Sheet](https://docs.google.com/spreadsheets/d/1FcbO8aMtH2FtSx-ntoMXjoyXhfGQkjnjzj1nkeR2Tk4/edit?usp=sharing)


Notes: 
- Portals are limited to 8 per chunk. Waygates are limited to 1 per chunk. Chunks are 160x160 coordinate points / 32x32 tiles. (5 coordinates = 1 tile) [Map](https://i.imgur.com/2H0TMoS.jpeg)
- Waygates cannot be seen in the "inky black" of the map. It covers up the mapicons.
- Portals are best for travel into the "inky black" as they do not involve the map for use. (Like Dev Island! `teleporttochunk 7,2`)
- If you destroy a waygate, it will be removed from the map for all players immediately
- If you destroy a portal, it will be removed from use, but the text will remain to players until they relog.

---
Thanks to the V Rising modding and server communities for ideas and requests!
Feel free to reach out to me on discord (odjit) if you have any questions or need help with the mod.

[V Rising Modding Discord](https://vrisingmods.com/discord)

## Commands

### Portal Commands
- `.portal start (Icon)` 
  - Starts creating a portal at the player's location.  Needs a second location for the other end. Add an icon to assign a map icon to this side of the portal.
- `.portal end (Icon)`
  - Connects the location started creating a portal. Add an icon to assign a map icon to this side of the portal.
- `.portal destroy`
  - Destroys the spawned portal connection you are standing near. (Both sides will go)
- `.portal teleporttoclosest`
  - Teleports you to the closest spawned portal.


### Waygate Commands
- `.waygate create (waygateprefab)`
  - Creates a waygate at the player's location.
- `.waygate destroy`
  - Destroys the spawned waygate you are standing on.
- `.waygate teleporttoclosest`
  - Teleports you to the closest spawned waygate.


### MapIcon Commands
- `.mapicon create (Icon)`
  - Creates a map icon at the player's location.
- `.mapicon destroy`
  - Destroys the map icon you are standing near.
- `.mapicon list`
  - Lists all map icons prefabs and their text.

	
## Eventual To-Do/Possible features
- Come find out in the V Rising Modding Discord!

This mod is licensed under the AGPL-3.0 license.