using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace IslandsOfValheim
{
    // Bragi's saga of you (asked with the alt use key on him, once Sævar has welcomed you): an opening, four of
    // your most telling deeds from the game's own record of you (PlayerProfile stats: days alive, foes, bosses,
    // the sea, how you've died, your work) and from Sævar's tale, and a close, sung line by line over his head.
    // The whole saga goes into the compendium as "Your saga" and is written anew each time he sings it.
    static class BragiSaga
    {
        internal const string Page = "$iov_saga_page";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        // Until when he's still singing (a time, not a flag: if he's gone mid-song, as you leave Valhalla, a
        // flag would never be cleared and he'd never sing again).
        static float singingUntil;
        internal static bool Singing { get { return Time.time < singingUntil; } }
        static readonly System.Reflection.FieldInfo KnownTexts = HarmonyLib.AccessTools.Field(typeof(Player), "m_knownTexts");

        // One line he could sing: how telling it is (higher is likelier to be picked) and its text.
        class Deed
        {
            internal float weight;
            internal string text;
            internal bool tale;
        }

        static float Stat(PlayerProfile profile, PlayerStatType stat)
        {
            return profile.GetStat(stat, DifficultyRequirement.RawStats);
        }

        static string L(string token, params object[] args)
        {
            string text = Localization.instance.Localize(token);
            try { return args.Length > 0 ? string.Format(Inv, text, args) : text; }
            catch (System.FormatException) { return text; }
        }

        static string N(float value) { return Mathf.RoundToInt(value).ToString(Inv); }

        // Everything the saga could tell of this player now.
        static List<Deed> Deeds(Player player, PlayerProfile profile, out List<Deed> rare)
        {
            List<Deed> deeds = new List<Deed>();
            rare = new List<Deed>();

            // Sævar's tale, as far as it has gone (the world's)
            if (Story.Key(Story.FinalDone)) deeds.Add(new Deed { weight = 3f, tale = true, text = L("$iov_saga_tale1") });
            if (Story.Key(ValhallaDuel.WonKey)) deeds.Add(new Deed { weight = 3f, tale = true, text = L("$iov_saga_tale2") });
            if (TaleCaptains.AllFreed) deeds.Add(new Deed { weight = 3f, tale = true, text = L("$iov_saga_tale3") });
            if (RanNet.Done) deeds.Add(new Deed { weight = 3.5f, tale = true, text = L("$iov_saga_tale4") });
            if (SpiritsIslet.Stage >= 2) deeds.Add(new Deed { weight = 3f, tale = true, text = L("$iov_saga_tale5") });
            if (Story.Key(ValhallaDuel.HafdisWonKey)) deeds.Add(new Deed { weight = 4f, tale = true, text = L("$iov_saga_tale6") });

            // days alive in a row (the longest)
            float days = Stat(profile, PlayerStatType.ConsecutiveDaysSurvivedMax);
            if (days >= 50f) deeds.Add(new Deed { weight = 2.5f, text = L("$iov_saga_days2", N(days)) });
            else if (days >= 10f) deeds.Add(new Deed { weight = 1.5f, text = L("$iov_saga_days1", N(days)) });

            // foes
            float kills = Stat(profile, PlayerStatType.EnemyKills);
            if (kills >= 2000f) deeds.Add(new Deed { weight = 3f, text = L("$iov_saga_kills3", N(kills)) });
            else if (kills >= 500f) deeds.Add(new Deed { weight = 2.5f, text = L("$iov_saga_kills2", N(kills)) });
            else if (kills >= 50f) deeds.Add(new Deed { weight = 1.5f, text = L("$iov_saga_kills1", N(kills)) });

            // the world's great foes (as the world remembers them beaten), in the game's order
            int beaten = 0;
            string last = "";
            foreach (string[] boss in Bosses)
            {
                if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(boss[0])) continue;
                beaten++;
                last = BossName(boss[1]);
            }
            if (beaten >= 2) deeds.Add(new Deed { weight = 1.5f + beaten * 0.4f, text = L("$iov_saga_bosses", beaten, last) });
            else if (beaten == 1 && ZoneSystem.instance.GetGlobalKey("defeated_eikthyr")) deeds.Add(new Deed { weight = 1.2f, text = L("$iov_saga_eikthyr") });

            // the sea
            float sailed = Stat(profile, PlayerStatType.DistanceSail) / 1000f;
            if (sailed >= 5f) deeds.Add(new Deed { weight = 1.5f + Mathf.Min(1.5f, sailed / 100f), text = L("$iov_saga_sail", N(sailed)) });
            float fish = Stat(profile, PlayerStatType.FishCaught);
            if (fish >= 10f) deeds.Add(new Deed { weight = 1.3f, text = L("$iov_saga_fish", N(fish)) });

            // how death has gone with you
            float deaths = Stat(profile, PlayerStatType.Deaths);
            if (deaths >= 5f) deeds.Add(new Deed { weight = 1.8f, text = L("$iov_saga_deaths", N(deaths), L(Cause(profile))) });
            else if (deaths < 1f && days >= 10f) deeds.Add(new Deed { weight = 2.5f, text = L("$iov_saga_nodeath") });

            // work
            float trees = Stat(profile, PlayerStatType.Tree);
            if (trees >= 100f) deeds.Add(new Deed { weight = 1.2f, text = L("$iov_saga_trees", N(trees)) });
            float rocks = Stat(profile, PlayerStatType.Mines);
            if (rocks >= 100f) deeds.Add(new Deed { weight = 1.2f, text = L("$iov_saga_rocks", N(rocks)) });
            float built = Stat(profile, PlayerStatType.Builds);
            if (built >= 200f) deeds.Add(new Deed { weight = 1.3f, text = L("$iov_saga_builds", N(built)) });
            float tamed = Stat(profile, PlayerStatType.CreatureTamed);
            if (tamed >= 3f) deeds.Add(new Deed { weight = 1.3f, text = L("$iov_saga_tamed", N(tamed)) });

            // the odd ones, now and then
            float byTree = Stat(profile, PlayerStatType.DeathByTree);
            if (byTree >= 3f) rare.Add(new Deed { weight = 1f, text = L("$iov_saga_rare_tree", N(byTree)) });
            float byFall = Stat(profile, PlayerStatType.DeathByFall);
            if (byFall >= 5f) rare.Add(new Deed { weight = 1f, text = L("$iov_saga_rare_fall", N(byFall)) });
            float jumps = Stat(profile, PlayerStatType.Jumps);
            if (jumps >= 10000f) rare.Add(new Deed { weight = 1f, text = L("$iov_saga_rare_jump", N(jumps)) });
            float meals = Stat(profile, PlayerStatType.FoodEaten);
            if (meals >= 1000f) rare.Add(new Deed { weight = 1f, text = L("$iov_saga_rare_food", N(meals)) });
            return deeds;
        }

        // The world's keys of the great foes, and their prefabs (whose names the game knows in every language).
        static readonly string[][] Bosses =
        {
            new[] { "defeated_eikthyr", "Eikthyr" }, new[] { "defeated_gdking", "gd_king" }, new[] { "defeated_bonemass", "Bonemass" },
            new[] { "defeated_dragon", "Dragon" }, new[] { "defeated_goblinking", "GoblinKing" }, new[] { "defeated_queen", "SeekerQueen" },
            new[] { "defeated_fader", "Fader" },
        };

        static string BossName(string prefab)
        {
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            Character c = go != null ? go.GetComponent<Character>() : null;
            return c != null ? Localization.instance.Localize(c.m_name) : prefab;
        }

        // What has killed you most often, as the saga names it.
        static string Cause(PlayerProfile profile)
        {
            string best = "$iov_saga_cause_enemy";
            float most = -1f;
            Check(profile, ref best, ref most, "$iov_saga_cause_enemy", PlayerStatType.DeathByEnemyHit, PlayerStatType.DeathByPlayerHit);
            Check(profile, ref best, ref most, "$iov_saga_cause_fall", PlayerStatType.DeathByFall, PlayerStatType.DeathByImpact);
            Check(profile, ref best, ref most, "$iov_saga_cause_sea", PlayerStatType.DeathByDrowning, PlayerStatType.DeathByWater, PlayerStatType.DeathByAshlandsOcean, PlayerStatType.DeathByBoat);
            Check(profile, ref best, ref most, "$iov_saga_cause_fire", PlayerStatType.DeathByBurning, PlayerStatType.DeathByCinderFire, PlayerStatType.DeathBySmoke);
            Check(profile, ref best, ref most, "$iov_saga_cause_frost", PlayerStatType.DeathByFreezing);
            Check(profile, ref best, ref most, "$iov_saga_cause_poison", PlayerStatType.DeathByPoisoned);
            Check(profile, ref best, ref most, "$iov_saga_cause_tree", PlayerStatType.DeathByTree);
            Check(profile, ref best, ref most, "$iov_saga_cause_self", PlayerStatType.DeathBySelf);
            Check(profile, ref best, ref most, "$iov_saga_cause_lava", PlayerStatType.DeathByAshlandsLava);
            return best;
        }

        static void Check(PlayerProfile profile, ref string best, ref float most, string token, params PlayerStatType[] stats)
        {
            float sum = 0f;
            foreach (PlayerStatType s in stats) sum += Stat(profile, s);
            if (sum > most) { most = sum; best = token; }
        }

        // Up to four deeds, the more telling likelier, at most two of the tale, maybe one of the odd ones.
        static List<string> Pick(List<Deed> deeds, List<Deed> rare)
        {
            List<string> picked = new List<string>();
            List<Deed> pool = new List<Deed>(deeds);
            int tale = 0;
            if (rare.Count > 0 && Random.value < 0.35f) picked.Add(rare[Random.Range(0, rare.Count)].text);
            while (picked.Count < 4 && pool.Count > 0)
            {
                float total = 0f;
                foreach (Deed d in pool) total += d.weight;
                float roll = Random.value * total;
                Deed chosen = pool[pool.Count - 1];
                foreach (Deed d in pool)
                {
                    roll -= d.weight;
                    if (roll <= 0f) { chosen = d; break; }
                }
                pool.Remove(chosen);
                if (chosen.tale && tale >= 2) continue;
                if (chosen.tale) tale++;
                picked.Insert(Mathf.Min(picked.Count, Random.Range(0, picked.Count + 1)), chosen.text);
            }
            return picked;
        }

        internal static void Sing(ValhallaSpeaker bragi, Player player)
        {
            if (Time.time < singingUntil || bragi == null || player == null || Game.instance == null) return;
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            if (profile == null) return;
            List<Deed> rare;
            List<Deed> deeds = Deeds(player, profile, out rare);
            if (deeds.Count + rare.Count < 2)
            {
                bragi.Say("$iov_saga_none");
                return;
            }
            string name = player.GetPlayerName();
            List<string> verses = new List<string>();
            verses.Add(L(Random.value < 0.5f ? "$iov_saga_open1" : "$iov_saga_open2", name));
            verses.AddRange(Pick(deeds, rare));
            verses.Add(L(Random.value < 0.5f ? "$iov_saga_close1" : "$iov_saga_close2"));
            Write(player, verses);
            singingUntil = Time.time + verses.Count * Pause;
            bragi.StartCoroutine(Recite(bragi, verses));
        }

        const float Pause = 5.5f; // between his lines (each shown a little longer, so they never blink out)

        static IEnumerator Recite(ValhallaSpeaker bragi, List<string> verses)
        {
            foreach (string verse in verses)
            {
                if (bragi == null) yield break;
                bragi.Say(verse, Pause + 1f);
                yield return new WaitForSeconds(Pause);
            }
        }

        // The compendium's page: written anew each time (the game itself only adds a page that isn't there).
        static void Write(Player player, List<string> verses)
        {
            string text = L("$iov_saga_page_intro") + "\n\n" + string.Join("\n\n", verses.ToArray());
            Dictionary<string, string> known = KnownTexts != null ? KnownTexts.GetValue(player) as Dictionary<string, string> : null;
            if (known != null && known.ContainsKey(Page)) known[Page] = text;
            else
            {
                player.AddKnownText(Page, text);
                player.Message(MessageHud.MessageType.TopLeft, "$iov_saga_written");
            }
        }
    }
}
