# IslandsOfValheim

Small islands out at sea with old ruins, guards and treasure, tougher the further you sail from the centre.

## What changes?

Valheim's ocean is mostly empty water between continents. This mod puts small islands in it, worth sailing to:

- **Rocky islets** in the open sea, away from the coasts. Each one has the ruins of a stone hall on top, a crumbling lookout tower on its cliff, an old jetty and a wrecked ship on its shore, plus berries, mushrooms, flint and branches to pick up.
- **Guards** that show up when you come close, once: killed guards don't come back.
- **Treasure.** One chest per islet.

The further from the world centre, the tougher the islet:

| Islet | Distance from the centre | Guards | Loot |
|---|---|---|---|
| Meadows | 300-2200 m | Greydwarfs, a Greydwarf Shaman | coins, amber, amber pearls, rubies, fire arrows |
| Swamp | 2200-4000 m | Draugr, a Draugr archer, a Draugr Elite | coins, iron scrap, chain, withered bones, iron arrows |
| Plains | 4000-5800 m | Fulings, a Fuling archer and shaman | coins, black metal scrap, silver necklaces, needle and obsidian arrows |
| Mistlands | 5800-7500 m | Seekers, a Seeker Brute | coins, soft tissue, black marble, black cores, carapace arrows |
| Ashlands | 7500 m and further | Charred warriors, an archer and a mage | coins, flametal ore, grausten, gemstones, molten cores, charred arrows |

Each kind has its own ground, painted like its biome, and its own plants and berries.

The islands are built from the game's own rocks and building pieces, so they look like they belong. Like vanilla ruins, their walls can be broken for stone and wood.

## New worlds and existing worlds

Valheim decides where locations go when a world is created, and only builds them when someone first comes near.

- **New world:** islets are there from the start.
- **Existing world:** run `genloc` once in the console (needs `devcommands`, as the host or in single player). Islets then appear in ocean areas nobody has visited yet. Ocean you've already sailed through doesn't get any.

## Configuration

- `MeadowsIslets`, `SwampIslets`, `PlainsIslets`, `MistlandsIslets`, `AshlandsIslets`: how many islets of each kind the game tries to place (0 turns a kind off). Only used when locations are generated (new world or `genloc`).
- `GroundStyle`: how the islet ground looks: `Terrain` (like the game's own land) or `Rock` (bare stone). Needs a restart.
- `DebugLog`: writes how the islands were built and where they are to the BepInEx log.

## Console commands

- `iov_info`: how many islets of each kind this world has and how far the closest ones are (host or single player).
- `iov_goto [kind]` (needs `devcommands`): teleports you to the closest islet nobody has been to yet, for example `iov_goto swamp`.

## Multiplayer

Install it on the server and on every player's game: the islands are part of the world, and everyone needs the mod to see them. Jötunn checks that everyone has the same version.

## Feedback and bug reports

Found a bug or have an idea? Open an issue on [GitHub](https://github.com/aetuss/IslandsOfValheim/issues). Turn on `DebugLog` and include the matching lines from `BepInEx/LogOutput.log`.
