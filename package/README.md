# IslandsOfValheim

Small rocky islands out at sea, with old stone ruins, creatures guarding them and a treasure chest.

## What changes?

Valheim's ocean is mostly empty water between continents. This mod puts small islands in it, worth sailing to:

- **Rocky islets** in the open sea, away from the coasts. Each one has the ruins of a stone hall on top, a crumbling lookout tower on its cliff, an old jetty and a wrecked ship on its shore, plus berries, mushrooms, flint and branches to pick up.
- **Guards.** Greydwarfs and a Greydwarf Shaman live on each islet. They show up when you come close, once: killed guards don't come back.
- **Treasure.** One chest per islet with Meadows / Black Forest loot: coins, amber, amber pearls, sometimes rubies, fire arrows, resin, feathers.

This first version has one kind of islet, at Meadows / Black Forest level, within about 4.5 km of the world centre. Islands further out, with tougher guards and better loot, are planned.

The islands are built from the game's own rocks and building pieces, so they look like they belong. Like vanilla ruins, their walls can be broken for stone and wood.

## New worlds and existing worlds

Valheim decides where locations go when a world is created, and only builds them when someone first comes near.

- **New world:** islets are there from the start.
- **Existing world:** run `genloc` once in the console (needs `devcommands`, as the host or in single player). Islets then appear in ocean areas nobody has visited yet. Ocean you've already sailed through doesn't get any.

## Configuration

- `IsletCount`: how many islets the game tries to place in a new world (default 30). Only used when locations are generated (new world or `genloc`).
- `GroundStyle`: how the islet ground looks: `Terrain` (like the game's own land) or `Rock` (bare stone). Needs a restart.
- `DebugLog`: writes how the islands were built and where they are to the BepInEx log.

## Console commands

- `iov_info`: how many islets this world has and how far the closest ones are (host or single player).
- `iov_goto` (needs `devcommands`): teleports you to the closest islet nobody has been to yet.

## Multiplayer

Install it on the server and on every player's game: the islands are part of the world, and everyone needs the mod to see them. Jötunn checks that everyone has the same version.

## Feedback and bug reports

Found a bug or have an idea? Open an issue on [GitHub](https://github.com/aetuss/IslandsOfValheim/issues). Turn on `DebugLog` and include the matching lines from `BepInEx/LogOutput.log`.
