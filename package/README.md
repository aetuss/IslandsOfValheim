# IslandsOfValheim

Small islands out at sea with old ruins, guards and treasure, tougher the further you sail from the centre.

## What changes?

Valheim's ocean is mostly empty water between continents. This mod puts small islands in it, worth sailing to:

- **Islets** in the open sea, away from the coasts, with ruins on top, often a tower or graves on the cliff, sometimes an old jetty or a wrecked ship on the shore, plus berries, mushrooms, flint and branches to pick up.
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

Each kind has its own ground, painted like its biome, its own plants and its own ruins, two designs each: a stone hall or a fishing camp (Meadows), a hut on stilts or a sunken crypt's entrance (Swamp), a Fuling camp or a ruin Fulings took over (Plains), a ring of black marble columns or a dvergr outpost (Mistlands), a grausten fortress or broken arches (Ashlands). Every kind comes in four shapes (round, long, with a bay, with two humps), the ruin is turned differently from islet to islet, and towers, wrecks, jetties and plants are there on some islets and not on others, so no two islets look quite the same.

About one islet in four has a **creature nest** instead of a ruin (a Greydwarf nest, a Swamp bone pile, Seeker eggs, Charred twitcher nests): break it before it sends more. About one in eight hides a **hoard**: a second, richer chest tucked between rocks on the slope (it isn't buried, look around). Some have a runestone.

The islands are built from the game's own rocks and building pieces, so they look like they belong. Like vanilla ruins, their walls can be broken for stone and wood.

Needs [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).

## New worlds and existing worlds

Valheim decides where locations go when a world is created, and only builds them when someone first comes near.

- **New world:** islets are there from the start.
- **Existing world:** run `genloc` once in the console (needs `devcommands`, as the host or in single player). Islets then appear in ocean areas nobody has visited yet. Ocean you've already sailed through doesn't get any.

## Bigger worlds

The distances above are for a vanilla-sized world. With [Expand World Size](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Size/) the rings grow with the world radius (that mod moves every location's distances itself), so a Meadows islet is still near the centre and Ashlands islets are still far out.

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
