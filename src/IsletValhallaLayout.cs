using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace IslandsOfValheim
{
    // Valhalla's layout (work in progress, corrected in the game together). North is +z.
    // - the way from where Odin's light sets you down, lined with iron torches and the shields of the fallen, up to
    //   the long hall (open pillars, a shingle roof edged with shields, the feast, a long fire, and at its far
    //   end the throne on a dais, looking down the hall at whoever comes in);
    // - behind the hall, at the north rim, Hlidskjalf, Odin's high seat, looking out over the clouds;
    // - east, the duel ring of stone columns, and by it the einherjar's yard;
    // - west, under golden birches, the Brim Folk's own feast;
    // - south-west, a path of memorial stones from where you arrive to the feast.
    //
    // Everything is a still copy of the game's own pieces (their scripts taken out, so nothing breaks or
    // burns down); where it's lit, its flames are lit first. Each piece is put by its own measured size, so
    // they meet edge to edge whatever the game's pieces measure.
    static partial class IsletBuilder
    {
        internal static readonly Vector3 ValhallaArrival = new Vector3(0f, 0f, -64f);
        const float HallHalfWidth = 8f, HallStart = 4f, HallEnd = 40f, PillarHeight = 6f, FloorRise = 0.6f;
        static readonly Vector2 DuelRing = new Vector2(40f, 6f);
        const float DuelRadius = 10f;
        // his dais: south-east of the ring, by the yard's north-east corner and the oak there, looking at the ring
        internal static readonly Vector2 RingMasterAt = new Vector2(DuelRing.x + 11f, DuelRing.y - 15.5f);
        static readonly Vector2 Feast = new Vector2(-42f, 8f);
        static readonly Vector2 Yard = new Vector2(40f, -18.5f); // south of the ring, in line with it, 6 m off its fires
        // Andhrímnir's kitchen: west of the hall's door, between the hall and the Brim Folk's feast (Eldhrímnir
        // there); its paving 3.5 m clear of the hall guard's round (x -11.5: 2.0.4-2.0.6 had him walk through it),
        // its south edge in line with the hall's front (2.0.7's stood half out in front of the hall: the user)
        internal static readonly Vector2 Kitchen = new Vector2(-20f, 7f);
        internal static float LookoutZ { get { return Mathf.Min(60f, RockEdge(Mathf.PI * 0.5f) - 14f); } }
        // Valhalla's gate: 6 m behind the landing's middle, at least 7 m in from the rim.
        internal static float ValhallaGateZ { get { return -Mathf.Min(-ValhallaArrival.z + 6f, RockEdge(-Mathf.PI * 0.5f) - 7f); } }

        // 1 where the ground is level (the hall, the way, the ring, the yard, the feast, the lookout), fading
        // to 0 over 6 m around them.
        internal static float ValhallaPads(float x, float z)
        {
            float pad = 0f;
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (HallStart + HallEnd) * 0.5f, HallHalfWidth + 3f, (HallEnd - HallStart) * 0.5f + 3f));
            pad = Mathf.Max(pad, RectPad(x, z, 0f, (ValhallaArrival.z + HallStart) * 0.5f - 2f, 5f, (HallStart - ValhallaArrival.z) * 0.5f + 5f));
            pad = Mathf.Max(pad, CirclePad(x, z, DuelRing, DuelRadius + 3f));
            pad = Mathf.Max(pad, CirclePad(x, z, Feast, 11f));
            pad = Mathf.Max(pad, CirclePad(x, z, Yard, 12f));
            pad = Mathf.Max(pad, CirclePad(x, z, Kitchen, 7.5f));
            pad = Mathf.Max(pad, CirclePad(x, z, new Vector2(0f, LookoutZ), 12f));
            return pad;
        }

        static float CirclePad(float x, float z, Vector2 centre, float radius)
        {
            float d = Mathf.Max(0f, new Vector2(x - centre.x, z - centre.y).magnitude - radius);
            return 1f - Mathf.SmoothStep(0f, 1f, d / 6f);
        }

        static float RectPad(float x, float z, float cx, float cz, float hx, float hz)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x - cx) - hx), dz = Mathf.Max(0f, Mathf.Abs(z - cz) - hz);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / 6f);
        }

        static readonly string[] ShieldNames = { "ShieldBanded", "ShieldWood", "ShieldIronSquare", "ShieldSilver", "ShieldGold", "ShieldSerpentscale", "ShieldBlackmetal", "ShieldFlametal" };

        static void BuildValhallaLayout()
        {
            int before = built;
            meals = 0;
            Hall();
            Way();
            Ring();
            Lookout();
            BrimFeast();
            ValhallaKitchen();
            EinherjarYard();
            MemorialPath();
            Meadow();
            Plugin.Debug("Valhalla: layout of " + (built - before) + " pieces, " + missing + " not found");
            LogCatalogue();
            LogChecks();
        }

        // The meadow's height there (0 on the level places).
        static float G(float x, float z) { return SkyGround(x, z); }

        // A plant, tree or stone of the game's set on the ground as the game sets them: by its own origin (its
        // bounds reach below, into the ground, where its roots and far-off looks are).
        static GameObject SkyPlant(string name, float x, float z, float yaw, float sink)
        {
            return SkyPivot(name, new Vector3(x, G(x, z) - sink, z), Quaternion.Euler(0f, yaw, 0f));
        }

        static bool OnRock(float x, float z, float margin)
        {
            return new Vector2(x, z).magnitude < RockEdge(Mathf.Atan2(z, x)) - margin;
        }

        // ---- the hall ----

        static void Hall()
        {
            // A floor of black marble, 4 m tiles; where its front edge really is, measured, for the steps.
            float floorTop = 0f, floorFront = HallStart, floorBack = HallEnd;
            for (float x = -HallHalfWidth + 2f; x < HallHalfWidth; x += 4f)
                for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                {
                    GameObject tile = SkyProp("blackmarble_floor_large", new Vector3(x, 0f, z), Vector3.zero);
                    if (tile == null) continue;
                    // the tiles are thick blocks: most of each sunk, so the floor stands a little over the meadow
                    float thick = SkyBounds(tile).size.y;
                    if (thick > FloorRise) Put(tile, new Vector3(x, FloorRise - thick, z), false);
                    Bounds tb = SkyBounds(tile);
                    floorTop = Mathf.Max(floorTop, tb.max.y);
                    floorFront = Mathf.Min(floorFront, tb.min.z);
                    floorBack = Mathf.Max(floorBack, tb.max.z);
                }
            // Pillars along both sides, every 4 m, a shield hung on the outside of each, facing out; inside, on
            // each but the end ones, a banner high and a hunting trophy below (on item stands, as a player hangs
            // them).
            int n = 0, banners = 0;
            for (float z = HallStart; z <= HallEnd + 0.01f; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject pillar = Pillar(new Vector3(side * HallHalfWidth, floorTop, z), PillarHeight);
                    if (pillar == null) continue;
                    Bounds pb = SkyBounds(pillar);
                    Vector3 at = new Vector3(side * (HallHalfWidth + 0.6f), floorTop + 2.2f, z);
                    PutAgainst(SkyItem(ShieldNames[(n++ * 5 + 3) % ShieldNames.Length], at, new Vector3(side, 0f, 0f), Vector3.up, false), at,
                        new Vector3(-side, 0f, 0f), side > 0f ? pb.max.x : pb.min.x);
                    float inner = side > 0f ? pb.min.x : pb.max.x;
                    int slot = Mathf.RoundToInt((z - (HallStart + 4f)) / 4f);
                    if (slot >= 0 && z < HallEnd - 0.5f) Trophy(slot * 2 + (side > 0f ? 1 : 0), inner, side, floorTop + 2.1f, z);
                    if (z > HallStart + 0.5f && z < HallEnd - 0.5f) Banner(banners++, inner, side, floorTop + PillarHeight - 0.35f, z);
                }
            float wallTop = floorTop + PillarHeight;
            // Beams along the tops of the pillars.
            float beamTop = wallTop + 0.3f, beamOuter = HallHalfWidth + 0.15f;
            for (float z = HallStart + 2f; z < HallEnd; z += 4f)
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = new Vector3(side * HallHalfWidth, wallTop, z);
                    GameObject beam = SkyProp("darkwood_beam4x4", at, Vector3.zero);
                    AlongZ(beam, at);
                    if (beam == null || side < 0f) continue;
                    Bounds bb = SkyBounds(beam);
                    beamTop = bb.max.y;
                    beamOuter = bb.max.x;
                }
            Roof(beamTop, beamOuter);
            // The feast: two long tables, their benches facing them, and down the middle a long fire.
            foreach (float side in new[] { -1f, 1f })
                for (float z = HallStart + 8f; z <= HallEnd - 10f; z += 6f)
                {
                    Vector3 table = new Vector3(side * 4.4f, floorTop, z);
                    GameObject t = SkyProp("piece_table_oak", table, Vector3.zero);
                    AlongZ(t, table);
                    foreach (float off in new[] { -1.4f, 1.4f })
                        SkySeat("piece_bench01", new Vector3(side * 4.4f + off, floorTop, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                    Laid(t, meals++);
                }
            // on each fire an iron spit, its meat roasting; each turned along the hall (across it, the spit's ends
            // reached into the aisles the valkyries walk: the user saw them walk through)
            foreach (float z in new[] { HallStart + 9f, HallStart + 17f, HallStart + 25f })
                Spit(SkyProp("hearth", new Vector3(0f, floorTop, z), new Vector3(0f, 90f, 0f)), floorTop, meals++);
            // Braziers hung on chains from the beams, between the pillars (on the floor if the hanging kind's missing).
            foreach (float x in new[] { -HallHalfWidth, HallHalfWidth })
                for (float z = HallStart + 2f; z <= HallEnd - 6f; z += 8f)
                {
                    if (PrefabManager.Instance.GetPrefab("piece_brazierceiling01") != null) SkyProp("piece_brazierceiling01", new Vector3(x, wallTop, z), Vector3.zero, true);
                    else SkyProp("piece_brazierfloor01", new Vector3(x - Mathf.Sign(x), floorTop, z), Vector3.zero);
                }
            Steps(floorTop, floorFront);
            // and out at the back, across the hall's width between the corner pillars
            Stair(0f, 0f, floorTop, floorBack, 1f, HallHalfWidth * 2f - 1f);
            // The throne at the far end, on a dais, its seat looking south down the hall to the door, a tall
            // torch either side of it.
            float dais = Block(new Vector3(0f, floorTop, HallEnd - 3f), 6f, 0.6f, 4f);
            SkySeat("piece_throne01", new Vector3(0f, dais, HallEnd - 3f), Vector3.back);
            foreach (float x in new[] { -2.3f, 2.3f })
                Lamp("CastleKit_groundtorch", "piece_brazierfloor02", new Vector3(x, dais, HallEnd - 3.4f));
            Mead(floorTop);
        }

        // ---- the hall's trophies, banners and mead ----

        // The einherjar's hunting trophies, in pairs across the hall (left, right), on every pillar but the end
        // ones, from the door end to the throne: the greydwarf brute and the fuling berserker, the drake and the
        // fenring cultist, the ulv and the seeker soldier, the bear and the asksvin; then the hart and the boar,
        // the wolf and the fenring, the troll and the lox, the sea serpent and the abomination. (The bosses'
        // were seven, one side short, and too big for the hall.) The first of each list the game has.
        static readonly string[][] Trophies =
        {
            new[] { "TrophyGreydwarfBrute" }, new[] { "TrophyGoblinBrute" },
            new[] { "TrophyHatchling" }, new[] { "TrophyCultist" },
            new[] { "TrophyUlv" }, new[] { "TrophySeekerBrute", "TrophySeeker" },
            new[] { "TrophyBjorn", "TrophyDraugrElite" }, new[] { "TrophyAsksvin", "TrophyWraith" },
            new[] { "TrophyDeer" }, new[] { "TrophyBoar" },
            new[] { "TrophyWolf" }, new[] { "TrophyFenring", "TrophyWolf" },
            new[] { "TrophyFrostTroll", "TrophyTroll" }, new[] { "TrophyLox" },
            new[] { "TrophySerpent" }, new[] { "TrophyAbomination", "TrophyLox" },
        };
        internal const string TrophyMark = "IoV_Trophy:";

        // A wall item stand on a pillar's inner face (inner: that face's x; side: the pillar's side), its
        // trophy on it the way the game's own stand shows one (the item's "attach" part on the stand's attach
        // point); the trophy's object named for its key.
        static void Trophy(int k, float inner, float side, float y, float z)
        {
            if (k >= Trophies.Length) return;
            Transform look = null;
            string kind = "";
            foreach (string name in Trophies[k])
            {
                GameObject item = PrefabManager.Instance.GetPrefab(name);
                look = item != null ? item.transform.Find("attach") : null;
                kind = name;
                if (look != null) break;
            }
            if (look == null) { Plugin.Debug("Valhalla: no trophy of " + string.Join(", ", Trophies[k])); return; }
            Transform attach = null;
            Vector3 at = new Vector3(inner - side * 0.06f, y, z);
            GameObject stand = SkyProp("itemstand", at, Vector3.zero, false, go =>
            {
                ItemStand s = go.GetComponent<ItemStand>();
                if (s != null) attach = s.m_attachOther;
            });
            if (stand == null) return;
            // flat against the pillar (thin across the hall), its face (the attach point) into the hall
            if (SkyBounds(stand).size.z < SkyBounds(stand).size.x) stand.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * stand.transform.localRotation;
            Put(stand, at, false);
            if (attach != null && (parent.InverseTransformPoint(attach.position).x - SkyBounds(stand).center.x) * -side < 0f)
            {
                stand.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * stand.transform.localRotation;
                Put(stand, at, false);
            }
            Transform holder = attach != null ? attach : stand.transform;
            GameObject trophy = Object.Instantiate(look.gameObject, holder, false);
            trophy.transform.localPosition = attach != null ? look.localPosition : new Vector3(0f, 0f, 0.15f);
            trophy.transform.localRotation = look.localRotation;
            trophy.transform.localScale = look.localScale;
            Strip(trophy);
            trophy.name = TrophyMark + kind;
            built++;
        }

        // A banner of the game's hanging high on a pillar's inner face, flat against it.
        static void Banner(int i, float inner, float side, float top, float z)
        {
            string name = "piece_banner0" + (1 + (i * 3) % 7);
            Vector3 at = new Vector3(inner - side * 0.08f, top, z);
            GameObject banner = SkyProp(name, at, Vector3.zero, true);
            if (banner == null) return;
            if (SkyBounds(banner).size.x > SkyBounds(banner).size.z)
            {
                banner.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * banner.transform.localRotation;
                Put(banner, at, true);
            }
            // (put by its middle: pulled back so its back is at the pillar)
            Bounds b = SkyBounds(banner);
            float back = side > 0f ? b.max.x : b.min.x;
            banner.transform.localPosition += new Vector3(inner - back, 0f, 0f);
        }

        // Mead: a fermenter in each of the hall's back corners, beside the throne's dais (behind the last
        // trophies, clear of them: the Queen's, as wide as she is, sat in one), and barrels by the door.
        static void Mead(float floorTop)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                string fermenter = First("fermenter");
                if (fermenter != null) SkyProp(fermenter, new Vector3(side * 5.3f, floorTop, HallEnd - 1f), new Vector3(0f, side > 0f ? -90f : 90f, 0f));
                string barrel = First("barrell", "piece_barrel", "piece_chest_barrel");
                if (barrel == null) continue;
                SkyProp(barrel, new Vector3(side * 6.6f, floorTop, HallStart + 1.6f), new Vector3(0f, side * 20f, 0f));
                SkyProp(barrel, new Vector3(side * 6.7f, floorTop, HallStart + 2.8f), new Vector3(0f, side * 75f, 0f));
            }
        }

        static string First(params string[] names)
        {
            foreach (string name in names) if (PrefabManager.Instance.GetPrefab(name) != null) return name;
            Plugin.Debug("Valhalla: none of " + string.Join(", ", names));
            return null;
        }

        // Steps of black marble from the ground up to the hall's floor, in front of its measured edge.
        static void Steps(float top, float front)
        {
            Stair(0f, 0f, top, front, -1f, 7f);
        }

        // Steps of black marble up from below (at height from) to a raised edge at z edge (and height to),
        // running out from it the way out (-1: toward the south, +1: toward the north), width wide, middle at x.
        static void Stair(float x, float from, float to, float edge, float way, float width)
        {
            float rise = to - from;
            if (rise < 0.15f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(rise / 0.25f));
            const float depth = 0.45f;
            for (int i = 1; i <= count; i++)
            {
                float height = rise * i / (count + 1f);
                float z = edge + way * (depth * (count - i) + depth * 0.5f);
                Block(new Vector3(x, from, z), width, height, depth);
            }
            Plugin.Debug("Valhalla: " + count + " steps from " + from.ToString("F2") + " up to " + to.ToString("F2") + " m at z " + edge.ToString("F2"));
        }

        // A solid block of black marble, sized to fit (any of the game's blocks, stretched): its foot at
        // at.y, its middle at (at.x, at.z). Gives back its top.
        static float Block(Vector3 at, float width, float height, float depth)
        {
            GameObject go = SkyProp("blackmarble_2x2x1", at, Vector3.zero);
            if (go == null) return at.y + height;
            Bounds b = SkyBounds(go);
            go.transform.localScale = new Vector3(width / Mathf.Max(0.1f, b.size.x), height / Mathf.Max(0.05f, b.size.y), depth / Mathf.Max(0.1f, b.size.z));
            Put(go, at, false);
            return SkyBounds(go).max.y;
        }

        static GameObject Pillar(Vector3 foot, float height)
        {
            GameObject pole = SkyProp("darkwood_pole4", foot, Vector3.zero);
            if (pole == null) return null;
            // as tall as wanted, and thicker with it
            Bounds b = SkyBounds(pole);
            pole.transform.localScale *= height / Mathf.Max(0.1f, b.size.y);
            Put(pole, foot, false);
            return pole;
        }

        // The roof: shingles at 45 degrees from the beams on both sides up to the ridge cap, along the whole
        // hall, put together the way the hammer does it: by the pieces' own snap points (each row's lower
        // edge on the one below's upper edge, the first row's on the beam's top outer edge). Which way a
        // shingle runs downhill is read from its collider's tilt. Without snap points it falls back to
        // stacking by the 2 m grid.
        // (from start to end along z, a metre over at each end; gives back the ridge's top, or NaN)
        // (of other pieces than the hall's darkwood, and over another middle than x 0, if asked: the pavilions')
        static float Roof(float beamTop, float beamOuter, float start = HallStart, float end = HallEnd, string shingle = "darkwood_roof_45", string capName = "darkwood_roof_top_45", float cx = 0f)
        {
            float ridge = float.NaN;
            GameObject prefab = PrefabManager.Instance.GetPrefab(shingle);
            GameObject capPrefab = PrefabManager.Instance.GetPrefab(capName);
            if (prefab == null) return ridge;
            List<Vector3> snaps = SnapPoints(prefab);
            // which way it runs down: its collider's tilt, unless its snap points (low edge, high edge) say
            // otherwise (the thatch at 26°: its tilt read 90° off, and 2.0.22's pieces sloped along the rows)
            Vector3 down = Downhill(prefab), bySnaps = SnapDownhill(snaps);
            if (shingle != "darkwood_roof_45" && bySnaps != Vector3.zero && (down == Vector3.zero || Vector3.Dot(down, bySnaps) < 0.7f)) // (the hall's as it was)
            {
                Plugin.Debug("Valhalla: " + shingle + " runs down " + bySnaps.ToString("F2") + " by its snap points (its collider said " + down.ToString("F2") + ")");
                down = bySnaps;
            }
            float yawL = down != Vector3.zero ? YawFor(down, Vector3.left) : 90f;
            float yawR = down != Vector3.zero ? YawFor(down, Vector3.right) : -90f;
            if (snaps.Count < 4) { Plugin.Log.LogWarning("Valhalla: the shingle has " + snaps.Count + " snap points; roof by the grid"); RoofByGrid(beamTop + 0.3f, yawL, yawR, start, end, beamOuter); return ridge; }
            int rows = 0;
            float slope = 1f, along = 2f, rise = 2f, run = 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                Quaternion turn = Quaternion.Euler(0f, side < 0f ? yawL : yawR, 0f);
                Vector3 lower, upper;
                float width;
                Edges(snaps, turn, out lower, out upper, out width);
                Vector3 step = upper - lower; // one row: in and up
                run = Mathf.Abs(step.x);
                rise = step.y;
                along = width;
                if (run < 0.2f || rise < 0.2f || along < 0.2f) { Plugin.Log.LogWarning("Valhalla: the shingle's snap points don't make a slope"); return ridge; }
                slope = rise / run;
                rows = Mathf.Max(1, Mathf.RoundToInt(beamOuter / run));
                for (int k = 0; k < rows; k++)
                    for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
                    {
                        Vector3 edge = new Vector3(cx + side * beamOuter, beamTop, z) + new Vector3(-side * run, rise, 0f) * k;
                        SkyPivot(shingle, edge - turn * Mid(snaps, true), turn);
                    }
            }
            Plugin.Debug("Valhalla: roof by snap points: " + rows + " rows a side, each " + run.ToString("F2") + " in and " + rise.ToString("F2") + " up, " +
                along.ToString("F2") + " along; from the beams' top outer edge at " + beamOuter.ToString("F2") + ", " + beamTop.ToString("F2"));
            // The ridge cap, its lower edges on the slope either side.
            List<Vector3> capSnaps = capPrefab != null ? SnapPoints(capPrefab) : new List<Vector3>();
            if (capSnaps.Count < 2) return ridge;
            Quaternion capTurn = Quaternion.Euler(0f, yawL, 0f);
            Vector3 capLow = Mid(capSnaps, true);
            float capHalf = 0f;
            foreach (Vector3 s in capSnaps) capHalf = Mathf.Max(capHalf, Mathf.Abs((capTurn * s).x - (capTurn * capLow).x));
            float capY = beamTop + (beamOuter - capHalf) * slope;
            for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
            {
                GameObject cap = SkyPivot(capName, new Vector3(cx, capY, z) - capTurn * capLow, capTurn);
                if (cap != null) ridge = float.IsNaN(ridge) ? SkyBounds(cap).max.y : Mathf.Max(ridge, SkyBounds(cap).max.y);
            }
            return ridge;
        }

        // Which way a sloped piece runs down by its snap points: from the middle of its highest to the middle of
        // its lowest, flat.
        static Vector3 SnapDownhill(List<Vector3> snaps)
        {
            if (snaps.Count < 4) return Vector3.zero;
            Vector3 d = Mid(snaps, true) - Mid(snaps, false);
            d.y = 0f;
            return d.magnitude > 0.3f ? d.normalized : Vector3.zero;
        }

        // A turned piece's lower and upper snap edges (the middles of its lowest and highest snap points)
        // and how far its lower ones spread along z (its width along the hall).
        static void Edges(List<Vector3> snaps, Quaternion turn, out Vector3 lower, out Vector3 upper, out float width)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Vector3 s in snaps) { float y = (turn * s).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            Vector3 l = Vector3.zero, u = Vector3.zero;
            int nl = 0, nu = 0;
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (Vector3 s in snaps)
            {
                Vector3 t = turn * s;
                if (t.y < lo + 0.1f) { l += t; nl++; zMin = Mathf.Min(zMin, t.z); zMax = Mathf.Max(zMax, t.z); }
                if (t.y > hi - 0.1f) { u += t; nu++; }
            }
            lower = nl > 0 ? l / nl : Vector3.zero;
            upper = nu > 0 ? u / nu : Vector3.zero;
            width = zMax - zMin;
        }

        // The middle of a piece's lowest (or highest) snap points, in its own space.
        static Vector3 Mid(List<Vector3> snaps, bool lowest)
        {
            float edge = lowest ? float.MaxValue : float.MinValue;
            foreach (Vector3 s in snaps) edge = lowest ? Mathf.Min(edge, s.y) : Mathf.Max(edge, s.y);
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 s in snaps) if (Mathf.Abs(s.y - edge) < 0.1f) { sum += s; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        // A piece's snap points (where the hammer joins it to others), in its own space.
        static List<Vector3> SnapPoints(GameObject prefab)
        {
            List<Vector3> points = new List<Vector3>();
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (t != prefab.transform && t.CompareTag("snappoint")) points.Add(prefab.transform.InverseTransformPoint(t.position));
            return points;
        }

        // The old way, when a piece has no snap points: stacked on the game's 2 m grid.
        static void RoofByGrid(float eave, float yawL, float yawR, float start, float end, float halfWidth)
        {
            GameObject probe = SkyProp("darkwood_roof_45", Vector3.zero, new Vector3(0f, yawL, 0f));
            if (probe == null) return;
            Bounds pb = SkyBounds(probe);
            Object.DestroyImmediate(probe);
            built--;
            float across = Grid(pb.size.x), up = Grid(pb.size.y), along = Grid(pb.size.z);
            int rows = Mathf.Max(1, Mathf.RoundToInt(halfWidth / across));
            float half = rows * across + 1f;
            for (int k = 0; k < rows; k++)
                for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
                    foreach (float side in new[] { -1f, 1f })
                        SkyProp("darkwood_roof_45", new Vector3(side * (half - across * (k + 0.5f)), eave + up * k, z), new Vector3(0f, side < 0f ? yawL : yawR, 0f));
            for (float z = start - 1f + along * 0.5f; z < end + 1f; z += along)
                SkyProp("darkwood_roof_top_45", new Vector3(0f, eave + up * rows - 0.4f, z), new Vector3(0f, yawL, 0f));
        }

        // A still copy put by its own origin (as the hammer puts a piece), not by its size.
        static GameObject SkyPivot(string name, Vector3 origin, Quaternion turn, System.Action<GameObject> first = null)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; Plugin.Debug("Valhalla: no " + name); return null; }
            usedNames.Add(name);
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = turn;
            go.transform.localPosition = origin;
            if (first != null) first(go);
            LightFires(go);
            Strip(go);
            built++;
            return go;
        }

        // Turned to lie along the hall (z) if it came out lying across it, and put back in place.
        static void AlongZ(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = SkyBounds(go);
            if (b.size.x <= b.size.z) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            Put(go, at, false);
        }

        // ---- the feast: food and drink ----

        static int meals;
        static readonly string[] FeastNames = { "FeastMeadows", "FeastBlackforest", "FeastOceans", "FeastMountains", "FeastPlains", "FeastSwamps", "FeastMistlands", "FeastAshlands" };
        static readonly string[] CookedNames = { "CookedMeat", "CookedDeerMeat", "CookedLoxMeat", "CookedWolfMeat", "CookedMooseMeat", "CookedBjornMeat", "CookedChickenMeat", "CookedHareMeat" };
        static readonly string[] TankardNames = { "Tankard", "TankardAnniversary" };

        // A table laid: one of the game's feasts in the middle (as full as it comes), and drink at both ends
        // within it: three tankards and a horn, a place apart for each sitter across. On its board (its solid
        // top: the studs drawn on it stand a little higher).
        // A tankard stands as the game makes it (its solid box upright: 0.18 across, 0.25 tall); the horn lies on
        // its side as made. Turned only about the upright, each its own way.
        static void Laid(GameObject table, int k)
        {
            if (table == null) return;
            Bounds tb = SkyBounds(table);
            float board = SkySolid(table).max.y;
            Vector3 middle = new Vector3(tb.center.x, board, tb.center.z);
            string feast = FeastNames[k % FeastNames.Length];
            if (PrefabManager.Instance.GetPrefab(feast) == null) feast = FeastNames[0];
            GameObject dish = SkyProp(feast, middle, Vector3.zero, false, FullFeast);
            AlongZ(dish, middle);
            // past the longest feast (3.5 m), short of the legs at the ends
            float along = Mathf.Min(2.2f, tb.size.z * 0.5f - 0.6f), across = Mathf.Min(0.45f, tb.size.x * 0.5f - 0.35f);
            int i = 0;
            foreach (float dz in new[] { -along, along })
                foreach (float dx in new[] { -across, across })
                {
                    string drink = (i + k) % 4 == 3 ? TankardNames[1] : TankardNames[0];
                    SkyProp(drink, new Vector3(tb.center.x + dx, board, tb.center.z + dz), new Vector3(0f, k * 47f + i * 83f, 0f));
                    i++;
                }
        }

        // A feast as it's first set down: every part of it showing that shows when it's full.
        static void FullFeast(GameObject go)
        {
            global::Feast feast = go.GetComponent<global::Feast>();
            if (feast == null) { Plugin.Debug("Valhalla: " + go.name + " has no feast in it"); return; }
            System.Collections.IList parts = HarmonyLib.Traverse.Create(feast).Field("m_feastParts").GetValue() as System.Collections.IList;
            if (parts == null) return;
            foreach (object part in parts)
            {
                HarmonyLib.Traverse t = HarmonyLib.Traverse.Create(part);
                GameObject above = t.Field("m_onAboveEquals").GetValue<GameObject>();
                GameObject below = t.Field("m_onBelow").GetValue<GameObject>();
                if (above != null) above.SetActive(true);
                if (below != null) below.SetActive(false);
            }
        }

        // An iron spit over a hearth, along its length, with meat on every place it has for some, as the game
        // shows meat cooking on one (the cooked meat's own look for it, on each slot).
        static void Spit(GameObject hearth, float floor, int k)
        {
            if (hearth == null) return;
            Bounds hb = SkyBounds(hearth);
            Vector3 at = new Vector3(hb.center.x, floor, hb.center.z);
            GameObject spit = SkyProp("piece_cookingstation_iron", at, Vector3.zero, false, go => Meat(go, k));
            if (spit == null) return;
            Bounds sb = SkyBounds(spit);
            if ((sb.size.x > sb.size.z) != (hb.size.x > hb.size.z))
            {
                spit.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * spit.transform.localRotation;
                Put(spit, at, false);
            }
        }

        static void Meat(GameObject station, int k)
        {
            CookingStation cooking = station.GetComponent<CookingStation>();
            if (cooking == null || cooking.m_slots == null) { Plugin.Debug("Valhalla: " + station.name + " has no places for meat"); return; }
            int shown = 0;
            for (int i = 0; i < cooking.m_slots.Length; i++)
            {
                Transform slot = cooking.m_slots[i];
                GameObject item = PrefabManager.Instance.GetPrefab(CookedNames[(k + i) % CookedNames.Length]);
                if (slot == null || item == null) continue;
                Transform look = item.transform.Find("attach_cook");
                if (look == null) look = item.transform.Find("attach");
                if (look == null) continue;
                GameObject meat = Object.Instantiate(look.gameObject, slot, false);
                meat.transform.localPosition = Vector3.zero;
                meat.transform.localRotation = Quaternion.identity;
                meat.SetActive(true);
                shown++;
            }
            Plugin.Debug("Valhalla: " + shown + " of " + cooking.m_slots.Length + " places on " + station.name + " with meat");
        }

        // ---- the way ----

        static void Way()
        {
            // a paved way two tiles wide
            for (float z = ValhallaArrival.z - 3f; z < HallStart - 2f; z += 2f)
                foreach (float x in new[] { -1f, 1f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // where you arrive: a wide marble landing
            foreach (float x in new[] { -3f, 3f })
                foreach (float z in new[] { ValhallaArrival.z - 2f, ValhallaArrival.z + 2f })
                    SkyProp("blackmarble_tile_floor_2x2", new Vector3(x, 0f, z), Vector3.zero);
            // and behind it, at the south, the gate you came through, looking up the way (kept 7 m in from
            // the rim)
            Gate(new Vector3(0f, G(0f, ValhallaGateZ), ValhallaGateZ), 0f, true);
            // tall iron torches either side, every 8 m (braziers where you arrive and before the hall's steps),
            // and between them a fallen warrior's spear planted upright with his shield leaning on it, facing the way
            int n = 0;
            for (float z = ValhallaArrival.z + 6f; z < HallStart - 4f; z += 8f)
                foreach (float side in new[] { -1f, 1f })
                {
                    bool ends = z == ValhallaArrival.z + 6f || z + 8f >= HallStart - 4f;
                    Lamp(ends ? "piece_brazierfloor01" : "piece_groundtorch", "piece_brazierfloor01", new Vector3(side * 3.2f, 0f, z));
                    if (z + 4f >= HallStart - 4f) continue;
                    Planted(new Vector3(side * 4.6f, 0f, z + 4f), new Vector3(-side, 0f, 0f), n++);
                }
            // and beyond them an avenue of golden birches, one by each torch, their crowns over the way; none by
            // the hall's front (their crowns would reach its roof) or on the memorial path
            // (in pairs: where the path keeps one side bare, the other goes too, or it stands alone)
            int k = 0;
            for (float z = ValhallaArrival.z + 6f; z <= HallStart - 14f; z += 8f)
            {
                if (NearMemorial(-7.5f, z, 3f) || NearMemorial(7.5f, z, 3f)) continue;
                foreach (float side in new[] { -1f, 1f })
                    SkyPlant(k++ % 3 == 1 ? "Birch2_aut" : "Birch1_aut", side * 7.5f, z, k * 71f, 0.1f);
            }
        }

        static readonly string[] SpearNames = { "SpearBronze", "SpearElderbark", "SpearChitin", "SpearCarapace" };

        // A spear stuck upright in the ground and a shield leaning back against it, its face looking along face.
        static void Planted(Vector3 at, Vector3 face, int n)
        {
            // (face is along x here: the way runs north)
            // (on the meadow's own height there: beside the paved way it rolls away from level)
            at.y = G(at.x, at.z);
            Vector3 spearAt = at + new Vector3(0f, -0.5f, 0f) - face * 0.3f;
            spearAt.y = G(spearAt.x, spearAt.z) - 0.5f;
            GameObject spear = SkyItem(SpearNames[n % SpearNames.Length], spearAt, face, Vector3.up, false, true);
            float shaft = spear != null ? SkySolid(spear).center.x : spearAt.x;
            Vector3 leaning = (face + Vector3.up * 0.26f).normalized; // about 15 degrees back
            Vector3 shieldAt = at + new Vector3(0f, -0.05f, 0f);
            // its top resting on the shaft: roughly by their solid boxes first, then exactly by their drawn shapes
            // only shields whose shape can be read: the others are curved, and from their size alone their top
            // can't be told to within the 10-15 cm that shows as a gap
            string shieldName = ShieldNames[(n * 3 + 1) % ShieldNames.Length];
            for (int i = 0; i < ShieldNames.Length && !Readable(shieldName); i++) shieldName = ShieldNames[(n * 3 + 2 + i) % ShieldNames.Length];
            GameObject shield = SkyItem(shieldName, shieldAt, leaning, Vector3.up, false);
            PutAgainst(shield, shieldAt, -face, shaft + face.x * 0.04f);
            if (shield != null && spear != null) RestOn(shield, spear, -face);
            if (shield != null) Settle(shield);
        }

        // Down (or up) until its lowest point is just in the ground under it: a leaning thing's foot. Straight
        // up and down, so whatever it leans on (upright) still holds its top.
        static void Settle(GameObject go)
        {
            List<Vector3> points = SkyPoints(go, null);
            Vector3 foot;
            if (points.Count > 0)
            {
                foot = points[0];
                foreach (Vector3 p in points) if (p.y < foot.y) foot = p;
            }
            else
            {
                Bounds b = SkyBounds(go);
                foot = new Vector3(b.center.x, b.min.y, b.center.z);
            }
            float down = G(foot.x, foot.z) - 0.02f - foot.y;
            go.transform.localPosition += new Vector3(0f, down, 0f);
            if (Mathf.Abs(down) > 0.05f) Plugin.Debug("Valhalla: " + go.name + " set down " + (-down).ToString("F2") + " m onto the ground");
        }

        // Slide a leaning thing (level, so its foot stays on the ground) until the furthest point of its drawn
        // shape toward its support touches the support at that height, and sideways until that point is right
        // on the support's middle there. Boxes can't do this for a round shield on a thin shaft: its corner
        // reaches where the shield doesn't, and a shaft's box is wider than the shaft.
        // The exact way needs the meshes' points, and the game keeps most of them unreadable. Then: the
        // shield's tip is the middle of its top edge on its back (from its own size, the way SkyItem stood it
        // up), and a spear's shaft is the middle of its drawn size (it stands straight); the tip is set on the
        // shaft's middle, so it sinks a little into the shaft rather than ever stopping short of it.
        static void RestOn(GameObject leaning, GameObject support, Vector3 toward)
        {
            toward = new Vector3(toward.x, 0f, toward.z).normalized;
            Vector3 across = Vector3.Cross(Vector3.up, toward);
            string how;
            List<Vector3> points = SkyPoints(leaning, null);
            Vector3 tip;
            if (points.Count > 0)
            {
                float best = float.MinValue;
                foreach (Vector3 p in points) best = Mathf.Max(best, Vector3.Dot(p, toward));
                // the middle of what reaches that far (a flat top edge, or a round top's one point)
                tip = Vector3.zero;
                int n = 0;
                foreach (Vector3 p in points) if (Vector3.Dot(p, toward) > best - 0.01f) { tip += p; n++; }
                tip /= n;
                how = "its shape";
            }
            else
            {
                tip = ItemTip(leaning, toward);
                how = "its size";
            }
            List<Vector3> section = new List<Vector3>();
            SkyPoints(support, section, tip.y);
            float contact;
            Vector3 middle;
            if (section.Count > 0)
            {
                contact = float.MaxValue;
                middle = Vector3.zero;
                foreach (Vector3 p in section) { contact = Mathf.Min(contact, Vector3.Dot(p, toward)); middle += p; }
                middle /= section.Count;
                contact += 0.008f; // just into the surface, never short of it
                how += ", the shaft's shape";
            }
            else
            {
                middle = SkyBounds(support).center;
                contact = Vector3.Dot(middle, toward);
                how += ", the shaft's middle";
            }
            Vector3 shift = toward * (contact - Vector3.Dot(tip, toward)) + across * Vector3.Dot(middle - tip, across);
            if (shift.magnitude > 0.6f) { checks.Add(leaning.name + ": would have to move " + shift.magnitude.ToString("F2") + " m to rest on " + support.name + " (by " + how + "); left by its box"); return; }
            leaning.transform.localPosition += shift;
            Plugin.Debug("Valhalla: " + leaning.name + " rests on " + support.name + " at " + tip.y.ToString("F2") + " m up, by " + how + " (moved " + shift.ToString("F3") + ")");
        }

        // Whether every mesh of a thing can be read (the exact way of resting it needs all its points).
        static readonly Dictionary<string, bool> readable = new Dictionary<string, bool>();
        static bool Readable(string name)
        {
            bool all;
            if (readable.TryGetValue(name, out all)) return all;
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            int count = 0;
            all = prefab != null;
            if (prefab != null)
                foreach (MeshFilter f in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null || !ActiveBelow(f.transform, prefab.transform)) continue;
                    count++;
                    if (!f.sharedMesh.isReadable) all = false;
                }
            all = all && count > 0;
            readable[name] = all;
            return all;
        }

        // An item's far end (as SkyItem stood it up): the middle of its top edge, on the side toward toward.
        static Vector3 ItemTip(GameObject go, Vector3 toward)
        {
            Bounds b = OwnBounds(go);
            int thin, along;
            float sign;
            ItemAxes(b, false, out thin, out along, out sign);
            Vector3 best = Vector3.zero;
            float reach = float.MinValue;
            foreach (float t in new[] { b.min[thin], b.max[thin] })
            {
                Vector3 own = b.center;
                own[along] = sign > 0f ? b.max[along] : b.min[along];
                own[thin] = t;
                Vector3 sky = root.transform.InverseTransformPoint(go.transform.TransformPoint(own));
                sky.y -= SkyHeight;
                if (Vector3.Dot(sky, toward) > reach) { reach = Vector3.Dot(sky, toward); best = sky; }
            }
            return best;
        }

        // An item's thinnest axis (its face), its long one, and which way along that is its far end (the side
        // that reaches further from where it's held; a spear is held near its head: otherEndUp).
        static void ItemAxes(Bounds b, bool otherEndUp, out int thin, out int along, out float sign)
        {
            Vector3 s = b.size;
            thin = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
            along = s[a1] >= s[a2] ? a1 : a2;
            sign = (Mathf.Abs(b.max[along]) >= Mathf.Abs(b.min[along])) != otherEndUp ? 1f : -1f;
        }

        // A thing's drawn shape in the sky floor's space: with no section list, its corners (every vertex); with
        // one, where its surface crosses the height at (each triangle edge across it). Only meshes the game left
        // readable; others are skipped.
        static List<Vector3> SkyPoints(GameObject go, List<Vector3> section, float at = 0f)
        {
            List<Vector3> points = new List<Vector3>();
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = f.sharedMesh;
                if (mesh == null || !mesh.isReadable || !ActiveBelow(f.transform, go.transform)) continue;
                Vector3[] v = mesh.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    v[i] = root.transform.InverseTransformPoint(f.transform.TransformPoint(v[i]));
                    v[i].y -= SkyHeight;
                }
                if (section == null) { points.AddRange(v); continue; }
                int[] t = mesh.triangles;
                for (int i = 0; i + 2 < t.Length; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = v[t[i + e]], b = v[t[i + (e + 1) % 3]];
                        if ((a.y - at) * (b.y - at) > 0f || Mathf.Approximately(a.y, b.y)) continue;
                        section.Add(Vector3.Lerp(a, b, (at - a.y) / (b.y - a.y)));
                    }
            }
            return section ?? points;
        }

        // ---- the duel ring ----

        static void Ring()
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Vector3 at = new Vector3(DuelRing.x + Mathf.Cos(a) * DuelRadius, 0f, DuelRing.y + Mathf.Sin(a) * DuelRadius);
                GameObject column = SkyProp("blackmarble_column_3", at, new Vector3(0f, -a * Mathf.Rad2Deg, 0f));
                // a standing blue brazier on its top, as on the memorial path's pillars
                if (column != null)
                    Lamp("piece_brazierfloor02", "MountainKit_brazier_blue", new Vector3(at.x, SkyBounds(column).max.y, at.z));
            }
            // cold blue fire round the ring, between the columns
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 4 + 1) * Mathf.PI / 10f; // halfway between two columns, every other gap
                Lamp("piece_groundtorch_blue", "MountainKit_brazier", new Vector3(DuelRing.x + Mathf.Cos(a) * (DuelRadius + 1.5f), 0f, DuelRing.y + Mathf.Sin(a) * (DuelRadius + 1.5f)));
            }
            // the ring's master's place: a low marble dais looking over the ring, a fire either side of him
            Vector2 m = RingMasterAt;
            Block(new Vector3(m.x, G(m.x, m.y), m.y), 2.6f, 0.45f, 2.6f);
            Vector2 toRing = (DuelRing - m).normalized, beside = new Vector2(-toRing.y, toRing.x);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 p = m + beside * side * 2.1f + toRing * 0.3f;
                SkyProp("piece_brazierfloor01", new Vector3(p.x, G(p.x, p.y), p.y), Vector3.zero);
            }
            // shields of those who fought here, leaning on the columns, facing in
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 3 + 1) * Mathf.PI * 2f / 10f;
                Vector3 inward = new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a));
                Vector3 at = new Vector3(DuelRing.x, 0f, DuelRing.y) - inward * (DuelRadius - 0.8f);
                SkyItem(ShieldNames[i * 2 % ShieldNames.Length], at, (inward + Vector3.up * 0.26f).normalized, Vector3.up, false);
            }
        }

        // ---- the meadow ----

        static void Meadow()
        {
            // golden birches and a few oaks: round the Brim Folk's feast, the west and the rim
            Vector2[] trees =
            {
                // (-29.5, 16.5: off the kitchen's north-west corner, clear of its roof; it stood at -24, 12, in the
                // oven, then at -26, 13.5, where the roof's corner now is)
                new Vector2(-56f, 22f), new Vector2(-47f, 32f), new Vector2(-30f, 28f), new Vector2(-62f, 4f), new Vector2(-29.5f, 16.5f),
                new Vector2(-58f, -14f), new Vector2(-36f, -22f), new Vector2(26f, 46f), new Vector2(50f, 34f), new Vector2(-22f, 50f),
                new Vector2(70f, 8f),
            };
            string[] kinds = { "Birch1_aut", "Birch2_aut", "Birch1_aut", "Oak1", "Birch2_aut" };
            for (int i = 0; i < trees.Length; i++)
            {
                Vector2 t = trees[i];
                if (!OnRock(t.x, t.y, 12f)) continue;
                SkyPlant(kinds[i % kinds.Length], t.x, t.y, i * 67f, 0.1f);
            }
            // a loose grove round the einherjar's yard, on its open sides (the ring is north of it): each tree
            // a different kind, green against the golden birches, a few metres off the yard's floor
            Vector3[] grove =
            {
                new Vector3(Yard.x + 15f, 0f, Yard.y + 6f),  // east, by its north corner
                new Vector3(Yard.x + 16f, 0f, Yard.y - 5f),  // east
                new Vector3(Yard.x + 7f, 0f, Yard.y - 13f),  // south
                new Vector3(Yard.x - 6f, 0f, Yard.y - 13.5f),// south
                new Vector3(Yard.x - 15f, 0f, Yard.y - 8f),  // west, toward the way
            };
            string[] groveKinds = { "Oak1", "Beech1", "Birch1", "Beech_small1", "Beech_small2" };
            for (int i = 0; i < grove.Length; i++)
            {
                Vector3 g = grove[i];
                if (!OnRock(g.x, g.z, 10f) || NearMemorial(g.x, g.z, 4f)) continue;
                SkyPlant(groveKinds[i], g.x, g.z, i * 113f + 20f, 0.1f);
            }
            // flowers in drifts, west and round the feast
            for (int i = 0; i < 160; i++)
            {
                float a = i * 2.39996f; // golden angle: spread without rows
                float r = 12f + (i % 13) * 4f + 3f * Noise(i * 0.7f, 2f);
                float x = -36f + Mathf.Cos(a) * r * 0.9f, z = 2f + Mathf.Sin(a) * r;
                if (ValhallaPads(x, z) > 0.2f || !OnRock(x, z, 6f)) continue;
                SkyPlant("Pickable_Dandelion", x, z, i * 41f, 0f);
            }
            // over the whole meadow, in drifts, off the level places and the paths: bushes, wild flax,
            // thistles, mushrooms, dandelions and a few half-buried stones
            string[] bushes = { "Bush01", "Bush02_en", "shrub_2" };
            for (float gx = -90f; gx <= 90f; gx += 6f)
                for (float gz = -90f; gz <= 90f; gz += 6f)
                {
                    float x = gx + 3f * Noise(gx * 0.31f, gz * 0.17f), z = gz + 3f * Noise(gz * 0.23f, gx * 0.29f);
                    if (!OnRock(x, z, 5f) || ValhallaPads(x, z) > 0.1f || NearMemorial(x, z, 3f) || NearHartRoute(x, z, 3.5f)) continue;
                    float pick = Mathf.PerlinNoise(x * 0.05f + 300f, z * 0.05f + 77f);
                    float jitter = Mathf.PerlinNoise(x * 0.9f + 11f, z * 0.9f + 3f);
                    if (jitter < 0.35f) continue; // the gaps between drifts
                    string name;
                    float sink = 0f;
                    if (pick < 0.32f) { name = bushes[(int)(jitter * 10f) % bushes.Length]; sink = 0.1f; }
                    else if (pick < 0.5f) name = "Pickable_Flax_Wild";
                    else if (pick < 0.58f) name = "Pickable_Thistle";
                    else if (pick < 0.65f) { name = jitter > 0.6f ? "Rock_4" : "Rock_7_meadows"; sink = 0.15f; }
                    else if (pick < 0.72f) name = "Pickable_Mushroom";
                    else name = "Pickable_Dandelion";
                    SkyPlant(name, x, z, Mathf.Repeat(x * 37f + z * 11f, 360f), sink);
                    // flax and dandelions grow in little clumps
                    if (name == "Pickable_Flax_Wild" || name == "Pickable_Dandelion")
                        for (int k = 0; k < 3; k++)
                        {
                            float cx = x + 1.3f * Mathf.Cos(k * 2.1f + jitter * 6f), cz = z + 1.3f * Mathf.Sin(k * 2.1f + jitter * 6f);
                            if (ValhallaPads(cx, cz) > 0.1f) continue;
                            SkyPlant(name, cx, cz, k * 97f, sink);
                        }
                }
        }

        // Eikþyrnir's way through the meadow east of the hall (ValhallaPeople walks him on it): kept clear of
        // bushes and stones, his antlers being as wide as they are.
        internal static readonly Vector2[] HartRoute = { new Vector2(18f, 36f), new Vector2(21.5f, 26f), new Vector2(18.5f, 15f) };

        static bool NearHartRoute(float x, float z, float within)
        {
            Vector2 p = new Vector2(x, z);
            for (int i = 0; i + 1 < HartRoute.Length; i++)
            {
                Vector2 a = HartRoute[i], ab = HartRoute[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if ((a + ab * t - p).sqrMagnitude < within * within) return true;
            }
            return false;
        }

        static bool NearMemorial(float x, float z, float within)
        {
            for (int i = 0; i <= 40; i++)
                if ((MemorialAt(i / 40f) - new Vector2(x, z)).sqrMagnitude < within * within) return true;
            return false;
        }

        internal static Vector2 MemorialAt(float t)
        {
            return Bezier(new Vector2(-6f, ValhallaArrival.z + 4f), new Vector2(-34f, -46f), new Vector2(Feast.x + 9f, Feast.y - 12f), t);
        }

        // ---- Hliðskjálf, Odin's high seat, behind the hall at the north rim ----

        // A stepped pyramid of black marble with steps up all four sides, and on top a throne looking out over the
        // clouds, fires at its corners; over it all a roof as the hall's on four pillars, with Huginn and Muninn
        // carved on its gables. (The game's raven, Hugin, shows itself only through its workings; the carving's
        // bird can't be had without its board: one mesh. So the carvings go where such boards belong.)
        static void Lookout()
        {
            // A stepped pyramid of black marble, four tiers to 2.4 m (the seat over the whole rock), each tier
            // 1.5 m in from the one under it, with steps up the middle of every side, all four ways.
            float z0 = LookoutZ;
            float[] sizes = { 14f, 11f, 8f, 5f };
            const float rise = 0.6f;
            float[] tops = new float[sizes.Length];
            float under = 0f;
            for (int i = 0; i < sizes.Length; i++)
            {
                tops[i] = Block(new Vector3(0f, under, z0), sizes[i], rise, sizes[i]);
                float half = sizes[i] * 0.5f;
                Stair(0f, under, tops[i], z0 - half, -1f, 3f);
                Stair(0f, under, tops[i], z0 + half, 1f, 3f);
                StairAlongX(z0, under, tops[i], -half, -1f, 3f);
                StairAlongX(z0, under, tops[i], half, 1f, 3f);
                under = tops[i];
            }
            float top = under;
            GameObject throne = SkySeat("piece_blackmarble_throne", new Vector3(0f, top, z0), Vector3.forward);
            // fire at the top tier's four corners
            foreach (float x in new[] { -1.9f, 1.9f })
                foreach (float z in new[] { -1.9f, 1.9f })
                    SkyProp("piece_brazierfloor01", new Vector3(x, top, z0 + z), Vector3.zero);
            if (throne == null) Plugin.Log.LogWarning("Valhalla: no high seat on Hlidskjalf");
            // Over it all, a roofed seat as the hall is built: a pillar at each corner of the lowest tier, beams
            // along their tops, and the hall's shingle roof on them, its ridge running north to south, open on
            // every side. (Pillars 6.1 m out: the beams' outer edge then comes to about 6.25, three rows of 2 m
            // shingles a side meet at the ridge as the hall's four do; 12 m long, 7 shingles, even both ends.)
            const float corner = 6.1f, length = 6f;
            float wallTop = tops[0] + 6f;
            foreach (float x in new[] { -corner, corner })
                foreach (float z in new[] { -corner, corner })
                    Pillar(new Vector3(x, tops[0], z0 + z), 6f);
            float beamTop = wallTop + 0.3f, beamOuter = corner + 0.15f;
            foreach (float z in new[] { -4f, 0f, 4f })
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 at = new Vector3(side * corner, wallTop, z0 + z);
                    GameObject beam = SkyProp("darkwood_beam4x4", at, Vector3.zero);
                    AlongZ(beam, at);
                    if (beam == null || side < 0f) continue;
                    Bounds bb = SkyBounds(beam);
                    beamTop = bb.max.y;
                    beamOuter = bb.max.x;
                }
            float ridge = Roof(beamTop, beamOuter, z0 - length, z0 + length);
            // Huginn and Muninn, carved, on the ridge's two ends as on an old hall's gables: the board along the
            // ridge, the bird out past the eaves, one looking north with Odin, one south to the hall.
            if (float.IsNaN(ridge)) return;
            const float boardUnder = 0.24f, inward = 1.2f; // its board's underside below its mount; how far in it's fixed
            foreach (float way in new[] { 1f, -1f })
                SkyPivot("darkwood_raven", new Vector3(0f, ridge + boardUnder, z0 + way * (length + 1f - inward)), Quaternion.Euler(0f, way > 0f ? 0f : 180f, 0f));
        }

        // Steps up to an edge running along z (the east or west side of something square), out the way way
        // along x; the same as Stair, turned.
        static void StairAlongX(float zMiddle, float from, float to, float edge, float way, float width)
        {
            float risen = to - from;
            if (risen < 0.15f) return;
            int count = Mathf.Max(2, Mathf.CeilToInt(risen / 0.25f));
            const float depth = 0.45f;
            for (int i = 1; i <= count; i++)
                Block(new Vector3(edge + way * (depth * (count - i) + depth * 0.5f), from, zMiddle), depth, risen * i / (count + 1f), width);
        }

        // ---- the Brim Folk's feast, under the trees to the west ----

        static void BrimFeast()
        {
            foreach (float x in new[] { Feast.x - 4.2f, Feast.x + 4.2f })
                for (float z = Feast.y - 6f; z <= Feast.y + 6.01f; z += 6f)
                {
                    Vector3 table = new Vector3(x, 0f, z);
                    GameObject t = SkyProp("piece_table_oak", table, Vector3.zero);
                    AlongZ(t, table);
                    foreach (float off in new[] { -1.4f, 1.4f })
                        SkySeat("piece_bench01", new Vector3(x + off, 0f, z), new Vector3(-Mathf.Sign(off), 0f, 0f));
                    Laid(t, meals++);
                }
            SkyProp("fire_pit_iron", new Vector3(Feast.x, 0f, Feast.y), Vector3.zero);
            // torches round the tables, out past the pavilion's eaves (at 8, 10 they stood under its corners)
            foreach (float dx in new[] { -9.5f, 9.5f })
                foreach (float dz in new[] { -11.5f, 11.5f })
                    SkyProp("piece_groundtorch_wood", new Vector3(Feast.x + dx, 0f, Feast.y + dz), Vector3.zero);
            // and over the tables an open shelter along them, three posts down each side (IsletValhallaPavilion)
            Shelter(Feast, G(Feast.x, Feast.y), 6.8f, new[] { -9f, 0f, 9f }, 4, 3f);
        }

        // ---- Andhrímnir's kitchen, west of the hall's door ----

        // A paved square of black marble (10 x 10 m). In its middle Eldhrímnir, the einherjar's great pot, a
        // tripod over a fire on the ground; north of it Sæhrímnir roasting whole on an iron spit on a stone
        // hearth; west, behind the cook, a stone oven and a preparing table; the wood in the south-west corner;
        // south a laid table and barrels; a chest. Andhrímnir himself (ValhallaPeople) stands west of the pot,
        // looking over it to the hall. (2.0.3's first kitchen was a pot, a spit and a table on the grass: the
        // user found it too plain for Valhalla.)
        static void ValhallaKitchen()
        {
            Vector3 k = new Vector3(Kitchen.x, 0f, Kitchen.y);
            float floor = 0f;
            for (float x = -4f; x <= 4.01f; x += 2f)
                for (float z = -4f; z <= 4.01f; z += 2f)
                {
                    GameObject tile = SkyProp("blackmarble_tile_floor_2x2", k + new Vector3(x, 0f, z), Vector3.zero);
                    if (tile != null) floor = Mathf.Max(floor, SkyBounds(tile).max.y);
                }
            Vector3 f = new Vector3(k.x, floor, k.z);

            // The pot is a tripod that hangs it: a fire on the ground under it, its legs on the paving round
            // it. (2.0.3 stood the tripod on the raised iron fire basket, its feet 0.7 m up in the air on the
            // basket's rim; a raised basket inside the legs doesn't fit either.)
            // (the game's own size: half again as big stood twice the cook's height, 2.0.4-2.0.6)
            GameObject fire = SkyProp(PrefabManager.Instance.GetPrefab("fire_pit") != null ? "fire_pit" : "fire_pit_iron", f, Vector3.zero);
            GameObject pot = SkyProp("piece_cauldron", f, Vector3.zero);
            if (fire != null && pot != null)
                Plugin.Debug("Valhalla: Eldhrímnir's fire " + SkyBounds(fire).size.ToString("F2") + " m, its top at " + SkyBounds(fire).max.y.ToString("F2") + "; the tripod's feet at " + SkyBounds(pot).min.y.ToString("F2"));
            if (pot != null) Plugin.Debug("Valhalla: Eldhrímnir " + SkyBounds(pot).size.ToString("F2") + " m over its fire");

            // the spit on a stone hearth, as the hall's are (2.0.3-2.0.5 had it over the raised iron fire basket)
            Vector3 s = f + new Vector3(0f, 0f, 3.4f);
            GameObject hearth = SkyProp("hearth", s, Vector3.zero);
            List<Transform> slots = new List<Transform>();
            GameObject spit = SkyProp("piece_cookingstation_iron", s, Vector3.zero, false, go =>
            {
                CookingStation c = go.GetComponent<CookingStation>();
                if (c != null && c.m_slots != null) foreach (Transform slot in c.m_slots) if (slot != null) slots.Add(slot);
            });
            if (spit != null && hearth != null)
            {
                Bounds hb = SkyBounds(hearth), sb = SkyBounds(spit);
                if ((sb.size.x > sb.size.z) != (hb.size.x > hb.size.z))
                {
                    spit.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * spit.transform.localRotation;
                    Put(spit, s, false);
                }
            }
            if (spit != null)
            {
                // (its steam, on a spit with nothing cooking, hung over the hooks as still white tufts)
                foreach (ParticleSystem p in spit.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
                RoastingBoar(spit, slots);
            }

            GameObject oven = SkyProp("piece_oven", f + new Vector3(-3.9f, 0f, 2.2f), new Vector3(0f, 90f, 0f));
            if (oven != null)
                // a little less smoke from its chimney (the user: the column was a bit much)
                foreach (ParticleSystem p in oven.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (p.name.IndexOf("smoke", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ParticleSystem.EmissionModule emission = p.emission;
                    emission.rateOverTimeMultiplier *= 0.65f;
                    ParticleSystem.MainModule main = p.main;
                    main.startSizeMultiplier *= 0.85f;
                    Plugin.Debug("Valhalla: the oven's " + p.name + " thinned");
                }
            SkyProp("piece_preptable", f + new Vector3(-3.9f, 0f, -0.6f), new Vector3(0f, 90f, 0f)); // (at -1.4 it ran into the wood)
            // the wood in the south-west corner, all of it on the paving (2.0.4-2.0.6 had it by the oven, out
            // over the paving's edge, a brazier on top of it)
            GameObject wood = SkyProp("wood_stack", f + new Vector3(-3.6f, 0f, -3.9f), Vector3.zero);
            if (wood != null)
            {
                Bounds wb = SkyBounds(wood);
                float outX = Mathf.Max(0f, (k.x - 4.8f) - wb.min.x), outZ = Mathf.Max(0f, (k.z - 4.8f) - wb.min.z);
                if (outX > 0f || outZ > 0f) wood.transform.localPosition += new Vector3(outX, 0f, outZ);
            }
            SkyProp("piece_chest_wood", f + new Vector3(2.8f, 0f, 3.8f), new Vector3(0f, -90f, 0f));
            GameObject table = SkyProp("piece_table", f + new Vector3(0f, 0f, -3.2f), Vector3.zero);
            Laid(table, meals++);
            SkyProp("barrell", f + new Vector3(2.3f, 0f, -3.3f), Vector3.zero);
            SkyProp("barrell", f + new Vector3(2.5f, 0f, -2.3f), new Vector3(0f, 40f, 0f));
            SkyProp("barrell", f + new Vector3(3.3f, 0f, -2.9f), new Vector3(0f, 75f, 0f));
            // (no braziers at its corners: the user had them out; the hearth, the pot's fire and the oven light it)
            // and over it all an open shelter, its ridge north to south, a post at each corner of the paving
            // (IsletValhallaPavilion)
            Shelter(Kitchen, floor, 4.6f, new[] { -5f, 5f }, 3, 3f);
        }

        // Bigger by so much, still standing where it stood (its foot on the same ground, its middle the same).
        static void Grow(GameObject go, float by, Vector3 at)
        {
            if (go == null) return;
            go.transform.localScale *= by;
            Put(go, at, false);
        }

        // Sæhrímnir: a still copy of the game's boar, roasted brown, lying on its side along the spit, the bar
        // through its middle (where the spit's places for meat are), as long as the spit's span.
        static void RoastingBoar(GameObject spit, List<Transform> slots)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab("Boar");
            if (prefab == null) { Plugin.Debug("Valhalla: no boar for the spit"); return; }
            Bounds sb = SkyBounds(spit);
            Vector3 bar;
            if (slots.Count > 0)
            {
                bar = Vector3.zero;
                foreach (Transform slot in slots) bar += root.transform.InverseTransformPoint(slot.position);
                bar /= slots.Count;
                bar.y -= SkyHeight;
            }
            else bar = new Vector3(sb.center.x, sb.min.y + sb.size.y * 0.7f, sb.center.z);

            GameObject holder = new GameObject("IoV_Saehrimnir");
            holder.transform.SetParent(parent, false);
            GameObject boar = Object.Instantiate(prefab, holder.transform, false);
            boar.name = "Saehrimnir";
            Strip(boar);
            foreach (Animator a in boar.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            foreach (Collider c in boar.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (ParticleSystem p in boar.GetComponentsInChildren<ParticleSystem>(true))
                if (p.gameObject != boar) p.gameObject.SetActive(false);
            foreach (Renderer r in boar.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Material[] own = r.sharedMaterials;
                for (int i = 0; i < own.Length; i++)
                {
                    if (own[i] == null) continue;
                    Material m = new Material(own[i]);
                    m.name = own[i].name + " (roasted)";
                    if (m.HasProperty("_Color")) m.SetColor("_Color", Roasted);
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                    own[i] = m;
                }
                r.sharedMaterials = own;
            }
            // as long as the spit is wide, lying on its side along it
            Bounds rest = DrawnBounds(holder.transform);
            float span = Mathf.Max(sb.size.x, sb.size.z);
            float scale = Mathf.Clamp(span * 0.85f / Mathf.Max(0.1f, rest.size.z), 0.6f, 1.6f);
            boar.transform.localScale = prefab.transform.localScale * scale;
            bool alongX = sb.size.x >= sb.size.z;
            boar.transform.localRotation = Quaternion.Euler(0f, alongX ? 90f : 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);
            Bounds b = DrawnBounds(holder.transform);
            boar.transform.localPosition = bar - b.center;
            // (a creature's skinned body is drawn where its bones put it, which the mesh's own measure here
            // needn't match: 2.0.3's boar was built on the spit and wasn't seen there. So it's measured again
            // as drawn once Valhalla stands, and moved onto the bar: RoastOnSpit.)
            holder.AddComponent<RoastOnSpit>().bar = bar;
            built++;
            Plugin.Debug("Valhalla: Sæhrímnir on the spit (" + slots.Count + " places for meat, the bar at " + bar.ToString("F2") + "), " + b.size.ToString("F2") + " m, scale " + scale.ToString("F2"));
        }

        static readonly Color Roasted = new Color(0.85f, 0.56f, 0.32f); // (0.62/0.38/0.2 came out near black on its dark hide)

        // ---- the einherjar's yard, by the duel ring ----

        static void EinherjarYard()
        {
            float floor = Block(new Vector3(Yard.x, 0f, Yard.y), 18f, 0.25f, 14f);
            // a low wall along the south side, shields leaning on it
            float wall = Block(new Vector3(Yard.x, floor, Yard.y - 6.4f), 15f, 1.1f, 0.5f);
            float wallFront = Yard.y - 6.4f + 0.25f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(Yard.x - 6f + i * 2.4f, floor, Yard.y - 5.6f);
                // its top resting on the wall
                PutAgainst(SkyItem(ShieldNames[(i * 3 + 2) % ShieldNames.Length], at, (Vector3.forward + Vector3.up * 0.27f).normalized, Vector3.up, false),
                    at, Vector3.back, wallFront);
            }
            // dummies to fight
            for (int i = 0; i < 4; i++)
                SkyProp(i % 2 == 0 ? "Morkhalla_Trainingdummy1" : "Morkhalla_Trainingdummy2", new Vector3(Yard.x - 6f + i * 4f, floor, Yard.y + 4f), new Vector3(0f, 180f + i * 12f, 0f));
            // armour stands either end
            foreach (float dx in new[] { -8f, 8f })
                SkyProp("ArmorStand_Male", new Vector3(Yard.x + dx, floor, Yard.y), new Vector3(0f, dx < 0f ? 90f : -90f, 0f));
            // a spear rack: a beam on two posts, spears standing against it
            foreach (float dx in new[] { -2.15f, 2.15f })
                SkyProp("darkwood_pole", new Vector3(Yard.x + dx, floor, Yard.y - 1f), Vector3.zero);
            Vector3 beam = new Vector3(Yard.x, floor + 1.4f, Yard.y - 1f);
            GameObject beamGo = SkyProp("darkwood_beam4x4", beam, Vector3.zero);
            AlongX(beamGo, beam);
            float beamFront = beamGo != null ? SkyBounds(beamGo).max.z : beam.z + 0.15f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = new Vector3(Yard.x - 1.5f + i * 1f, floor, Yard.y - 0.4f);
                // head up, leaning back on the beam
                PutAgainst(SkyItem(SpearNames[i % SpearNames.Length], at, Vector3.forward, (Vector3.up + Vector3.back * 0.12f).normalized, false, true),
                    at, Vector3.back, beamFront);
            }
            Plugin.Debug("Valhalla: the yard's wall top at " + wall.ToString("F2"));
        }

        // ---- memorial stones, from where you arrive to the Brim Folk's feast ----

        static readonly Dictionary<string, GameObject> skySources = new Dictionary<string, GameObject>();

        static void MemorialPath()
        {
            Vector2 from = new Vector2(-6f, ValhallaArrival.z + 4f), to = new Vector2(Feast.x + 9f, Feast.y - 12f);
            Vector2 bend = new Vector2(-34f, -46f);
            int n = 0, runes = 0;
            List<GameObject> comingIn = new List<GameObject>(), goingOut = new List<GameObject>();
            for (float t = 0f; t <= 1.001f; t += 0.035f)
            {
                Vector2 p = Bezier(from, bend, to, t);
                // stepping stones
                SkyProp("blackmarble_tile_floor_1x1", new Vector3(p.x, G(p.x, p.y) - 0.05f, p.y), new Vector3(0f, t * 400f, 0f));
                if (n++ % 4 != 2) continue;
                // a stone either side, now a carved pillar, now a rune stone
                Vector2 ahead = (Bezier(from, bend, to, Mathf.Min(1f, t + 0.02f)) - p).normalized;
                Vector2 side = new Vector2(-ahead.y, ahead.x).normalized;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 q = p + side * s * 2.6f;
                    Vector3 foot = new Vector3(q.x, G(q.x, q.y) - 0.2f, q.y);
                    bool pillar = (n / 4 + (s > 0f ? 1 : 0)) % 2 == 0;
                    GameObject stone = pillar
                        ? SkyPart("GDKing", "platforms/StonePillarTall/StonePillar", "", new Vector3(q.x, G(q.x, q.y) - 0.3f, q.y), n * 31f, 0.7f)
                        : SkyPart("Runestone_Meadows", "", "runestone", foot, 0f, 1f);
                    if (stone == null && !pillar) { SkyPart("GDKing", "platforms/StonePillarTall/StonePillar", "", new Vector3(q.x, G(q.x, q.y) - 0.3f, q.y), n * 31f, 0.7f); continue; }
                    if (pillar)
                    {
                        // a standing blue brazier on its top: the path's light at night
                        if (stone != null)
                            Lamp("piece_brazierfloor02", "MountainKit_brazier_blue", new Vector3(q.x, SkyBounds(stone).max.y, q.y));
                        continue;
                    }
                    // Rune stones by turns: one turned to meet you as you come in from the landing, the next to
                    // meet you as you go back out; each a little towards the path. Read, each says its piece.
                    bool inward = runes++ % 2 == 0;
                    Vector2 look = (inward ? -ahead : ahead) - side * s * 0.6f;
                    FaceRune(stone, new Vector3(look.x, 0f, look.y), foot);
                    (inward ? comingIn : goingOut).Add(stone);
                }
            }
            // their words in the order they're met: in from the landing, and out from the far end
            for (int i = 0; i < comingIn.Count; i++)
                Readable(comingIn[i], RunesIn[i % RunesIn.Length]);
            for (int i = 0; i < goingOut.Count; i++)
                Readable(goingOut[goingOut.Count - 1 - i], RunesOut[i % RunesOut.Length]);
            Plugin.Debug("Valhalla: " + comingIn.Count + " rune stones to read coming in, " + goingOut.Count + " going out");
        }

        static readonly string[] RunesIn = { "$iov_rune_in1", "$iov_rune_in2", "$iov_rune_in3", "$iov_rune_in4" };
        static readonly string[] RunesOut = { "$iov_rune_out1", "$iov_rune_out2", "$iov_rune_out3", "$iov_rune_out4" };

        // Which way a rune stone's carved face looks, along its thin side: +1 or -1 (set by what's seen in the
        // game: the carving is on one face only; +1 turned the carvings the wrong way round).
        const float RuneFace = -1f;

        // A rune stone turned so its carved face looks along look, and set back on its foot.
        static void FaceRune(GameObject stone, Vector3 look, Vector3 foot)
        {
            Bounds b = OwnBounds(stone);
            Vector3 normal = (b.size.x <= b.size.z ? Vector3.right : Vector3.forward) * RuneFace;
            Vector3 now = stone.transform.localRotation * normal;
            now.y = 0f;
            float turn = Vector3.SignedAngle(now, look, Vector3.up);
            stone.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * stone.transform.localRotation;
            Put(stone, foot, false);
        }

        static void Readable(GameObject stone, string text)
        {
            ValhallaRune rune = stone.AddComponent<ValhallaRune>();
            rune.text = text;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        // A part of one of the game's structures (by its path in the location, or the first part whose name
        // has that word), copied still, turned and scaled, its foot at at.
        static GameObject SkyPart(string location, string path, string word, Vector3 at, float yaw, float scale)
        {
            GameObject source;
            if (!skySources.TryGetValue(location, out source))
            {
                source = null;
                if (FindLocation(new[] { location }) != null)
                {
                    CustomLocation clone = ZoneManager.Instance.CreateClonedLocation(locationName + "_Part" + (vanillaCount++), location);
                    if (clone != null && clone.Prefab != null)
                    {
                        source = clone.Prefab;
                        ZoneManager.Instance.DestroyCustomLocation(clone.Name);
                    }
                }
                skySources[location] = source;
            }
            if (source == null) { missing++; return null; }
            Transform part = path.Length > 0 ? source.transform.Find(path) : Named(source.transform, word);
            if (part == null) { missing++; Plugin.Log.LogWarning("Valhalla: no " + location + "/" + (path.Length > 0 ? path : "*" + word + "*")); return null; }
            GameObject go = Object.Instantiate(part.gameObject, parent, false);
            go.name = part.name;
            Strip(go);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * (Quaternion.Inverse(source.transform.rotation) * part.rotation);
            go.transform.localScale = part.lossyScale * scale;
            Put(go, at, false);
            built++;
            return go;
        }

        static Transform Named(Transform t, string word)
        {
            foreach (Transform child in t)
            {
                if (child.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0 && child.GetComponentInChildren<MeshFilter>(true) != null) return child;
                Transform deeper = Named(child, word);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // Turned to lie across the hall (x) if it came out lying along it, and put back in place.
        static void AlongX(GameObject go, Vector3 at)
        {
            if (go == null) return;
            Bounds b = SkyBounds(go);
            if (b.size.x >= b.size.z) return;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.localRotation;
            Put(go, at, false);
        }

        // ---- which way the game's things face, read from them instead of guessed ----

        // The way a seat looks (where its sitter faces), flat, in its own space; zero if it has no seat.
        static Vector3 SeatFacing(GameObject prefab)
        {
            Chair chair = prefab != null ? prefab.GetComponentInChildren<Chair>(true) : null;
            if (chair == null || chair.m_attachPoint == null) return Vector3.zero;
            Vector3 f = prefab.transform.InverseTransformDirection(chair.m_attachPoint.forward);
            f.y = 0f;
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.zero;
        }

        // The turn that takes a thing's own flat direction to the one wanted.
        static float YawFor(Vector3 own, Vector3 want)
        {
            return own == Vector3.zero ? 0f : Vector3.SignedAngle(own, want, Vector3.up);
        }

        // A seat (a bench, a throne) turned so its sitter looks along look.
        static GameObject SkySeat(string name, Vector3 at, Vector3 look)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            Vector3 own = SeatFacing(prefab);
            if (own == Vector3.zero) Plugin.Debug("Valhalla: " + name + " has no seat to face by");
            return SkyProp(name, at, new Vector3(0f, YawFor(own, look), 0f));
        }

        // An item (a shield, a spear) turned so its flat face looks along face and its long side, from its grip
        // to its far end, runs along up; its foot (or, byTop, its top) at at.y.
        static GameObject SkyItem(string name, Vector3 at, Vector3 face, Vector3 up, bool byTop, bool otherEndUp = false)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; return null; }
            Bounds b = OwnBounds(prefab);
            int thin, along;
            float sign;
            ItemAxes(b, otherEndUp, out thin, out along, out sign);
            Vector3 thinAxis = Vector3.zero, longAxis = Vector3.zero;
            thinAxis[thin] = 1f;
            longAxis[along] = sign;
            Vector3 u = Vector3.ProjectOnPlane(up, face);
            if (u.sqrMagnitude < 0.001f) u = Vector3.ProjectOnPlane(Vector3.forward, face);
            Quaternion turn = Quaternion.LookRotation(face, u) * Quaternion.Inverse(Quaternion.LookRotation(thinAxis, longAxis));
            return SkyProp(name, at, turn.eulerAngles, byTop);
        }

        // Bounds of a prefab's visible meshes, in its own space.
        static Bounds OwnBounds(GameObject prefab)
        {
            bool any = false;
            Bounds result = new Bounds();
            foreach (MeshFilter f in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !ActiveBelow(f.transform, prefab.transform)) continue;
                Matrix4x4 m = prefab.transform.worldToLocalMatrix * f.transform.localToWorldMatrix;
                Bounds mb = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                    else result.Encapsulate(p);
                }
            }
            return result;
        }

        // Which way a roof piece runs downhill, flat, in its own space: its roof face's tilt, from its largest
        // collider (a flat box: its thinnest side is the face). Zero if it can't be told.
        static Vector3 Downhill(GameObject prefab)
        {
            BoxCollider best = null;
            float bestArea = 0f;
            foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger) continue;
                Vector3 s = Vector3.Scale(box.size, box.transform.lossyScale);
                float area = Mathf.Max(s.x * s.y, Mathf.Max(s.y * s.z, s.x * s.z));
                if (area > bestArea) { best = box; bestArea = area; }
            }
            if (best == null) return Vector3.zero;
            Vector3 bs = Vector3.Scale(best.size, best.transform.lossyScale);
            int thin = bs.x <= bs.y && bs.x <= bs.z ? 0 : bs.y <= bs.z ? 1 : 2;
            Vector3 axis = Vector3.zero;
            axis[thin] = 1f;
            Vector3 n = prefab.transform.InverseTransformDirection(best.transform.TransformDirection(axis));
            if (n.y < 0f) n = -n;
            Vector3 flat = new Vector3(n.x, 0f, n.z);
            // a sloped face's upward normal leans the way it runs down
            return flat.magnitude > 0.2f && n.y > 0.2f ? flat.normalized : Vector3.zero;
        }

        // Put so its side towards toward (one of the flat axes) just touches contact on that axis: something
        // leaning on something else.
        static void PutAgainst(GameObject go, Vector3 at, Vector3 toward, float contact)
        {
            if (go == null) return;
            Put(go, at, false);
            // by its solid shape (its drawn bounds can reach past what's seen)
            Bounds b = SkySolid(go);
            Vector3 p = go.transform.localPosition;
            bool alongX = Mathf.Abs(toward.x) > 0.5f;
            float sign = alongX ? Mathf.Sign(toward.x) : Mathf.Sign(toward.z);
            float reach = alongX ? (sign > 0f ? b.max.x : b.min.x) : (sign > 0f ? b.max.z : b.min.z);
            if (alongX) p.x += contact - reach; else p.z += contact - reach;
            go.transform.localPosition = p;
            Bounds d = SkyBounds(go);
            float drawn = alongX ? (sign > 0f ? d.max.x : d.min.x) : (sign > 0f ? d.max.z : d.min.z);
            float gap = (contact - drawn) * sign;
            if (Mathf.Abs(gap) > 0.08f)
                checks.Add(go.name + " at " + p.ToString("F1") + ": its solid side touches; its drawn side " + (gap > 0f ? "stops " + gap.ToString("F2") + " m short" : "reaches " + (-gap).ToString("F2") + " m past"));
        }

        // A size on the game's 2 m building grid (what's past it overlaps the next piece).
        static float Grid(float size)
        {
            return Mathf.Max(1f, Mathf.Floor(size / 2f + 0.05f) * 2f);
        }

        // ---- measuring and checking (so the next round isn't guessed) ----

        static readonly HashSet<string> usedNames = new HashSet<string>();
        static readonly List<string> checks = new List<string>();

        // Collider bounds (what touches), in the sky floor's space.
        static Bounds SkySolid(GameObject go)
        {
            Bounds b = SolidBounds(go);
            b.center -= new Vector3(0f, SkyHeight, 0f);
            return b;
        }

        // Every kind of piece the layout used, as the game made it: its drawn size and its solid size, where
        // its origin sits in them, which way its seat looks, how it runs downhill, its snap points.
        static void LogCatalogue()
        {
            List<string> names = new List<string>(usedNames);
            names.Sort();
            foreach (string name in names)
            {
                GameObject prefab = PrefabManager.Instance.GetPrefab(name);
                if (prefab == null) continue;
                Bounds drawn = OwnBounds(prefab);
                List<Vector3> snaps = SnapPoints(prefab);
                string snapText = "";
                for (int i = 0; i < snaps.Count && i < 8; i++) snapText += (i > 0 ? " " : "") + snaps[i].ToString("F2");
                List<string> colliders = new List<string>();
                foreach (Collider c in prefab.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger || colliders.Count >= 3) continue;
                    BoxCollider box = c as BoxCollider;
                    colliders.Add(c.GetType().Name + (box != null ? " " + Vector3.Scale(box.size, box.transform.lossyScale).ToString("F2") + " at " +
                        prefab.transform.InverseTransformPoint(box.transform.TransformPoint(box.center)).ToString("F2") + " turned " + box.transform.rotation.eulerAngles.ToString("F0") : ""));
                }
                Vector3 seat = SeatFacing(prefab), down = Downhill(prefab);
                Plugin.Log.LogInfo("Valhalla piece " + name + ": drawn " + drawn.size.ToString("F2") + " from " + drawn.min.ToString("F2") + " to " + drawn.max.ToString("F2") +
                    (colliders.Count > 0 ? "; solid " + string.Join(", ", colliders.ToArray()) : "; nothing solid") +
                    (seat != Vector3.zero ? "; seat looks " + seat.ToString("F2") : "") + (down != Vector3.zero ? "; runs down " + down.ToString("F2") : "") +
                    (snaps.Count > 0 ? "; " + snaps.Count + " snap points " + snapText : ""));
            }
        }

        static void LogChecks()
        {
            if (checks.Count == 0) { Plugin.Log.LogInfo("Valhalla check: everything that leans touches what it leans on"); return; }
            Plugin.Log.LogInfo("Valhalla check: " + checks.Count + " to look at");
            foreach (string line in checks) Plugin.Log.LogInfo("Valhalla check: " + line);
        }

        // ---- putting things ----

        // A still copy of one of the game's things in Valhalla: its flames lit, its scripts out, turned, then put
        // with the middle of its footprint at (at.x, at.z) and its foot (or, byTop, its top) at at.y.
        // (first, before its workings are taken out, whatever it needs set up from them)
        static GameObject SkyProp(string name, Vector3 at, Vector3 euler, bool byTop = false, System.Action<GameObject> first = null)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(name);
            if (prefab == null) { missing++; Plugin.Debug("Valhalla: no " + name); return null; }
            usedNames.Add(name);
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            go.transform.localRotation = Quaternion.Euler(euler);
            if (first != null) first(go);
            LightFires(go);
            Strip(go);
            // An item (a shield) as decoration: without the glint the game puts on things lying to be picked up.
            // And solid: the game keeps things lying about on a layer you walk through.
            if (prefab.GetComponent<ItemDrop>() != null)
                foreach (Collider solid in go.GetComponentsInChildren<Collider>(true))
                    if (!solid.isTrigger) solid.gameObject.layer = LayerMask.NameToLayer("static_solid");
            if (prefab.GetComponent<ItemDrop>() != null)
                foreach (ParticleSystem shine in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (shine.gameObject != go) { shine.gameObject.SetActive(false); continue; }
                    ParticleSystem.MainModule main = shine.main;
                    main.playOnAwake = false;
                    ParticleSystem.EmissionModule emission = shine.emission;
                    emission.enabled = false;
                    ParticleSystemRenderer drawn = shine.GetComponent<ParticleSystemRenderer>();
                    if (drawn != null) drawn.enabled = false;
                }
            Put(go, at, byTop);
            built++;
            return go;
        }

        // A light standing on the ground, of the kind wanted, or another if the game hasn't that one.
        static GameObject Lamp(string name, string otherwise, Vector3 at)
        {
            return SkyProp(PrefabManager.Instance.GetPrefab(name) != null ? name : otherwise, at, Vector3.zero);
        }

        // (Measured where it stands now, so first back to the middle: Move takes the bounds of a thing at 0.)
        static void Put(GameObject go, Vector3 at, bool byTop)
        {
            go.transform.localPosition = Vector3.zero;
            Move(go, SkyBounds(go), at, byTop);
        }

        // LocalBounds is in the location's space; Valhalla's things hang under its sky floor.
        static Bounds SkyBounds(GameObject go)
        {
            Bounds b = LocalBounds(go);
            b.center -= new Vector3(0f, SkyHeight, 0f);
            return b;
        }
    }
}
