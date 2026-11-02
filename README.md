# IslandsOfValheim

A Valheim mod that adds small islands to the ocean, with stone ruins, guards and treasure. See [package/README.md](package/README.md) for what it does.

## Building

`build.ps1` compiles `src/*.cs` with the .NET Framework C# compiler (C# 5) against the game's assemblies, BepInEx and Jötunn, then packs `package/` and the DLL into `dist/IslandsOfValheim-<version>.zip` for Thunderstore. Adjust the paths at the top of the script to your install.

## How the islands are made

No asset bundle: the islet is built when the game starts. The ground is a generated rock mesh using a vanilla rock material, and the location snaps to the water surface, so it works in deep water. Rocks and plants are vanilla prefabs copied as plain meshes and colliders (scripts removed). The ruins, tower and jetty are real vanilla building pieces, damaged at random on spawn (`Location.m_applyRandomDamage`). The guards are one-time `CreatureSpawner`s and the chest is a copy of a vanilla treasure chest with its own loot table, both registered as their own prefabs.
