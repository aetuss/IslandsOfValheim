# Changelog

## 1.2.0

- Dvergr outposts: a new kind of islet in the Mistlands ring (about one in four there), where dvergr live in a camp or a dig site. They're neutral like their camps on land; breaking their things, taking their stein, lantern or black core, or opening their chest turns them hostile. Richer loot: black cores, dvergr extractors, lanterns. Sometimes Seekers are attacking them as you arrive.
- Deep North islets: the northern half of the outer ring now has Deep North islets (snowy ground, ice on the shore, a frozen stone keep or a frozen hunters' camp, Deep North skeletons, Ulvs, Cultists and Fenrings, frost loot). Ashlands islets keep to the southern half (their default count is now 15, as is the Deep North's).
- New settings `DvergrIslets` and `DeepNorthIslets`; `iov_info` and `iov_goto` know the new kinds.
- Each kind's islet count is now shared exactly between its four shapes (before, a few were lost to rounding).
- Existing worlds keep the islets they have; the new kinds appear in a new world, or after `genloc` in ocean nobody has visited yet.

## 1.1.1

- README: notes that multiplayer and dedicated servers haven't been tested yet. No code changes.

## 1.1.0

- Sea vegvisir shards: island chests sometimes have one. Use it (right-click) and it marks the closest islet nobody has visited yet on your map, one of these waters' kind or a tougher one. Works in multiplayer.
- Gulls circle over islets by day, and a fire burns on their cliff at night, so you can spot one from your boat.
- Fixed some ruin pieces and jetty posts breaking by themselves: they sat a few centimetres off the ground or entirely under it, and upper wall rows hung a little above the lower ones.
- With Expand World Size the islet rings follow the world radius (that mod already moves every location; the shard uses the same radius).

## 1.0.0

- First release: small islands in the open ocean, in five kinds by distance from the world centre (Meadows, Swamp, Plains, Mistlands, Ashlands), each with its own ground, plants, ruins, guards and loot.
- Four shapes per kind, two ruin designs turned different ways, creature nests on some islets, rare hoards, runestones, optional jetties, wrecks and towers.
- Console commands `iov_info` and `iov_goto [kind]`.
