# Changelog

## 0.7.0

- More variety: every kind of islet has two shapes and sizes, two designs for its main ruin (one is picked for each islet), and its tower, wreck, jetty, plants, pickables and loose stones are there on some islets and not on others.
- New ruins: an abandoned fishing camp (Meadows), the mouth of a sunken crypt (Swamp), a stone ruin taken over by Fulings (Plains), a dvergr outpost (Mistlands) and Ashlands arches with twisted pillars.
- Fixed building pieces collapsing or hanging in the air: pieces next to the floor now stand on the ground instead of just above it, and nothing rests on top of a piece that can't carry it.

## 0.6.0

- Each kind of islet has its own ruins now, built from that biome's pieces: a stone hall and lookout tower (Meadows), a hut on stilts with graves (Swamp), a Fuling camp behind a palisade (Plains), a ring of black marble columns (Mistlands) and a broken grausten fortress (Ashlands).
- Plains, Mistlands and Ashlands islets have a wrecked longship instead of a karve; Ashlands islets have an ashwood jetty.
- Ashlands islets no longer look like a lake of lava.

## 0.5.0

- Five kinds of islets, tougher the further they are from the world centre: Meadows (300-2200 m), Swamp (2200-4000 m), Plains (4000-5800 m), Mistlands (5800-7500 m) and Ashlands (7500 m and further).
- Each kind has its own ground (painted like its biome), shape, guards, plants, chest and loot.
- One setting per kind for how many islets the world gets (replaces IsletCount).
- iov_goto takes a kind: iov_goto swamp. iov_info counts each kind.

## 0.4.1

- Fixed the islet ground being invisible with the terrain look.
- Fixed trees and other plants sinking through the islet towards the sea floor.
- New setting GroundStyle: Terrain (default) or Rock.

## 0.4.0

- The islet ground now uses Valheim's own terrain look (grass on flat ground, sand by the water, rock on steep slopes) instead of a rock texture, which streaked in places.
- Trees and bushes on the islet are the normal vanilla ones: trees can be chopped for wood.

## 0.3.0

- Smoother, more natural ground: no more sharp facets or triangle-shaped moss, and the rock texture no longer stretches on steep faces.
- Fixed a wall-like step around the courtyard on the cliff side.
- The ruins are real building pieces now: like vanilla ruins they can be broken for stone and wood, and they start out worn.
- Bigger ruin: an 8x8 m courtyard with two-storey walls, a tall gate with an arch, standing and fallen pillars.
- A ruined lookout tower on the cliff ridge and an old wooden jetty on the low side to moor a boat.

## 0.2.0

- Bigger islet (about 40 m across) with rough, chiselled rock ground and a high cliff side.
- Rocks and bushes sit on the ground on slopes instead of hanging over the downhill side.
- The chest and the guards were buried inside the 1 m thick floor tiles; they now stand on the floor.
- More on each islet: more shore rocks and outcrops, the remains of a lookout, a wrecked karve (if the game has its parts), raspberry bushes, mushrooms, flint, stones and branches, and two more Greydwarfs.
- New console command `iov_goto` (needs devcommands): teleports to the closest islet that hasn't been generated yet.

## 0.1.0

- First test version: one kind of islet (Meadows / Black Forest level) with a stone ruin, Greydwarf guards and a treasure chest.
