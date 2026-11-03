# IslandsOfValheim

Small islands out at sea with old ruins, dvergr outposts, guards and treasure, tougher the further you sail from the centre.

## What changes?

Valheim's ocean is mostly empty water between continents. This mod puts small islands in it, worth sailing to:

- **Islets** in the open sea, away from the coasts, with ruins on top, often a tower or graves on the cliff, sometimes an old jetty or a wrecked ship on the shore, plus berries, mushrooms, flint and branches to pick up.
- **Guards** that show up when you come close, once: killed guards don't come back.
- **Treasure.** One chest per islet. Sometimes it holds a **sea vegvisir shard**: use it (right-click in your inventory) and it marks the closest islet nobody has visited yet on your map, one of the kind of the waters you're in or a tougher one, so one islet leads you to the next. If none is left, the shard isn't used up.
- **Signs from the sea.** Gulls circle over every islet by day, and a fire burns on its cliff at night, so you can spot one from your boat.

The further from the world centre, the tougher the islet:

| Islet | Distance from the centre | Guards | Loot |
|---|---|---|---|
| Meadows | 300-2200 m | Greydwarfs, a Greydwarf Shaman | coins, amber, amber pearls, rubies, fire arrows |
| Swamp | 2200-4000 m | Draugr, a Draugr archer, a Draugr Elite | coins, iron scrap, chain, withered bones, iron arrows |
| Plains | 4000-5800 m | Fulings, a Fuling archer and shaman | coins, black metal scrap, silver necklaces, needle and obsidian arrows |
| Mistlands | 5800-7500 m | Seekers, a Seeker Brute | coins, soft tissue, black marble, black cores, carapace arrows |
| Dvergr outpost | 5800-7500 m, about one Mistlands-ring islet in four | dvergr rogues and a fire, an ice and a support mage, neutral until provoked | coins, black cores, dvergr extractors, lanterns, soft tissue, iron, copper, yggdrasil wood |
| Ashlands | 7500 m and further, southern half of the world | Charred warriors, an archer and a mage | coins, flametal ore, grausten, gemstones, molten cores, charred arrows |
| Deep North | 7500 m and further, northern half of the world | a Deep North skeleton, Ulvs, a Cultist, a Fenring | coins, frostwood, ice, frost cores, crystal, silver, freeze glands, frost arrows |

Each kind has its own ground, painted like its biome, its own plants and its own ruins, two designs each: a stone hall or a fishing camp (Meadows), a hut on stilts or a sunken crypt's entrance (Swamp), a Fuling camp or a ruin Fulings took over (Plains), a ring of black marble columns or an abandoned dvergr post (Mistlands), a dvergr camp or a dvergr dig site (Dvergr outposts), a grausten fortress or broken arches (Ashlands), a frozen stone keep or a hunters' camp the frost took (Deep North, with ice washed up on the shore). Every kind comes in four shapes (round, long, with a bay, with two humps), the ruin is turned differently from islet to islet, and towers, wrecks, jetties and plants are there on some islets and not on others, so no two islets look quite the same.

About one islet in four has a **creature nest** instead of a ruin (a Greydwarf nest, a Swamp bone pile, Seeker eggs, Charred twitcher nests): break it before it sends more. About one in eight hides a **hoard**: a second, richer chest tucked between rocks on the slope (it isn't buried, look around). Some have a runestone.

The islands are built from the game's own rocks and building pieces, so they look like they belong. Like vanilla ruins, their walls can be broken for stone and wood.

## Dvergr outposts

Some islets in the Mistlands ring aren't ruins: dvergr live there. Like the dvergr camps on land, they leave you alone as long as you leave them alone, so you can land, look around and sail on in peace. Or raid them: hitting a dvergr, breaking anything of theirs (walls, crates, barrels, furniture), taking the stein or lantern off their table or the black core from its stand, or opening their chest turns the whole outpost against you. Their chest is worth it, and so is the black core. You can't build on their islet, the same as in their camps on land.

Now and then you arrive in the middle of a fight: Seekers have come ashore and the dvergr are fighting them.

Needs [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).

## New worlds and existing worlds

Valheim decides where locations go when a world is created, and only builds them when someone first comes near.

- **New world:** islets are there from the start.
- **Existing world:** run `genloc` once in the console (needs `devcommands`, as the host or in single player). Islets then appear in ocean areas nobody has visited yet. Ocean you've already sailed through doesn't get any.

## Bigger worlds

The distances above are for a vanilla-sized world. With [Expand World Size](https://thunderstore.io/c/valheim/p/JereKuusela/Expand_World_Size/) the rings grow with the world radius (that mod moves every location's distances itself), so a Meadows islet is still near the centre and Ashlands islets are still far out. The outer ring is split by a line running east-west through the world centre, the same way the game divides the Deep North from the Ashlands, so that stays true in any world size.

## Configuration

- `MeadowsIslets`, `SwampIslets`, `PlainsIslets`, `MistlandsIslets`, `DvergrIslets`, `AshlandsIslets`, `DeepNorthIslets`: how many islets of each kind the game tries to place (0 turns a kind off). Only used when locations are generated (new world or `genloc`).
- `GroundStyle`: how the islet ground looks: `Terrain` (like the game's own land) or `Rock` (bare stone). Needs a restart.
- `DebugLog`: writes how the islands were built and where they are to the BepInEx log.

## Console commands

- `iov_info`: how many islets of each kind this world has and how far the closest ones are (host or single player).
- `iov_goto [kind]` (needs `devcommands`): teleports you to the closest islet nobody has been to yet, for example `iov_goto swamp`, `iov_goto dvergr` or `iov_goto deepnorth`.

## Multiplayer

**Note:** Multiplayer and dedicated servers haven't been tested yet. It's built to work there, but if something seems off, please report it on GitHub Issues.

Install it on the server and on every player's game: the islands are part of the world, and everyone needs the mod to see them. Jötunn checks that everyone has the same version.

## Feedback and bug reports

Found a bug or have an idea? Open an issue on [GitHub](https://github.com/aetuss/IslandsOfValheim/issues). Turn on `DebugLog` and include the matching lines from `BepInEx/LogOutput.log`.
