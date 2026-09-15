using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    public class PetDef
    {
        public string id, name, blurb;
        /// <summary>0 Thường, 1 Hiếm, 2 Sử thi, 3 Huyền thoại.</summary>
        public int rarity;
        /// <summary>The produce this pet likes best: a snack of it counts double in the pet book's
        /// flavour line, and it is what the pet reaches for first when two are equally rare.</summary>
        public string favourite;

        public PetDef(string id, string name, int rarity, string favourite, string blurb)
        {
            this.id = id; this.name = name; this.rarity = rarity; this.favourite = favourite; this.blurb = blurb;
        }
    }

    /// <summary>One job on a patrol: water or harvest one plot on one island.</summary>
    public struct PetJob
    {
        public int island, plot;
        public bool water;
    }

    /// <summary>Thú cưng: hatched from eggs, one follows the player and tends the farm.
    ///
    /// Every <see cref="PatrolMs"/> the pet looks over every unlocked island for plots that are
    /// thirsty or ripe, walks to them and does the job with the game's own <see cref="PlotLogic"/>
    /// — so what a pet earns is exactly what the player would have earned tapping. Thirsty plots
    /// come first: a watering window closes, a ripe crop waits.
    ///
    /// Now and then it helps itself to one piece of produce from the warehouse, and it has
    /// expensive taste: a mutated fruit is far more likely to go than a plain one. That is the
    /// pet's whole cost, and it is a charming one rather than a tax.
    ///
    /// No catch-up: a pet works while the game is open, like the player. Offline growth already
    /// never pays out more than being there, and a pet that harvested all night would.
    ///
    /// Pure rules only; the walking lives in <c>PetDirector</c>.</summary>
    public static class PetSys
    {
        public const int UnlockLevel = 5;
        public const long PatrolMs = 3L * 60_000L;
        public const int MaxLevel = 5;
        /// <summary>Eggs in a row without a Sử thi or better before the next one is guaranteed.</summary>
        public const int Pity = 40;
        public const float SnackChance = 0.35f;

        public static readonly PetDef[] All =
        {
            new PetDef("bega",       "Bé Gà",      0, "wheat",      "Ngủ nhiều hơn làm, nhưng chưa bao giờ lỡ một cữ tưới."),
            new PetDef("pinkteriii", "Pinkteriii", 0, "strawberry", "Má phính, mê mọi thứ màu hồng. Dâu tây trong kho coi chừng."),
            new PetDef("shushi",     "Shushi",     1, "watermelon", "Cái vòi dài tưới được cả ô bên cạnh. Ăn cũng nhiều như tưới."),
            new PetDef("yummy",      "Yummy",      1, "grape",      "Chiếc mai hoa hồng thơm phức, đi tới đâu vườn thơm tới đó."),
            new PetDef("tim",        "TiM",        2, "tomato",     "Rồng lửa nhỏ chạy nhanh nhất quần đảo. Thích đồ ăn nóng hổi."),
            new PetDef("mit",        "MiT",        3, "pineapple",  "Linh vật của MATU Farm. Không cây nào chín mà thoát được mắt MiT."),
        };

        public static readonly string[] RarityName = { "Thường", "Hiếm", "Sử thi", "Huyền thoại" };
        public static readonly string[] RarityHex = { "#8FA3AD", "#3E9BD8", "#A05CE0", "#F2A91E" };
        /// <summary>Chance of each rarity per egg. Split evenly between the pets of that rarity.</summary>
        public static readonly float[] RarityOdds = { 0.62f, 0.27f, 0.09f, 0.02f };

        public static PetDef Def(string id)
        {
            foreach (var d in All) if (d.id == id) return d;
            return null;
        }

        public static bool Unlocked(PlayerState s) { return s.lv >= UnlockLevel; }

        public static int LevelOf(PlayerState s, string id) { return s.pets.TryGetValue(id, out int lv) ? lv : 0; }

        public static PetDef Active(PlayerState s)
        {
            return !string.IsNullOrEmpty(s.petActive) && s.pets.ContainsKey(s.petActive) ? Def(s.petActive) : null;
        }

        /// <summary>Plots one patrol handles. A rarer pet covers more ground; each level from a
        /// duplicate egg adds one.</summary>
        public static int JobsPerPatrol(PetDef d, int level)
        {
            if (d == null) return 0;
            int[] baseJobs = { 3, 4, 6, 8 };
            return baseJobs[Mathf.Clamp(d.rarity, 0, 3)] + Mathf.Clamp(level, 1, MaxLevel) - 1;
        }

        // ============================================================
        // eggs
        // ============================================================
        /// <summary>UNIT per egg. 25 let a code-free player hatch ten eggs by day 5 out of loose change; at 35
        /// the second egg still comes on day 2, the tenth in about a week (Kiểm tra hành trình chơi).</summary>
        public const float EggUnits = 35f;

        /// <summary>Coins for <paramref name="count"/> eggs. Quoted in UNIT like the shop; ten at
        /// once is the price of nine. The very first egg is free — the feature has to be seen
        /// working before anyone pays for it.</summary>
        public static long EggPrice(PlayerState s, int count)
        {
            if (s.petFreeEggs >= count) return 0;                  // eggs from a gift code
            if (count == 1 && s.petEggs == 0) return 0;
            float unit = MissionSys.Unit(s) * EggUnits;
            long one = unit < 500f ? Mathf.Max(10, Mathf.RoundToInt(unit / 10f) * 10)
                     : unit < 50_000f ? Mathf.RoundToInt(unit / 100f) * 100
                     : Mathf.RoundToInt(unit / 1000f) * 1000;
            return count >= 10 ? one * 9 : one * count;
        }

        /// <summary>Roll one egg. <paramref name="rarityRoll"/> and <paramref name="pickRoll"/> are
        /// in [0,1). Keeps the pity counter.</summary>
        public static PetDef Roll(PlayerState s, float rarityRoll, float pickRoll)
        {
            int rarity = 0;
            float acc = 0f;
            for (int r = RarityOdds.Length - 1; r >= 0; r--)
            {
                // from the top down, so a roll of 0.99 is not a legendary by accident of ordering
                acc += RarityOdds[r];
                if (rarityRoll < acc) { rarity = r; break; }
            }
            // acc sums to 1 from Legendary down: roll < 0.02 → 3, < 0.11 → 2, < 0.38 → 1, else 0
            if (s.petPity + 1 >= Pity && rarity < 2) rarity = 2;
            s.petPity = rarity >= 2 ? 0 : s.petPity + 1;
            s.petEggs++;

            var pool = new List<PetDef>();
            foreach (var d in All) if (d.rarity == rarity) pool.Add(d);
            return pool[Mathf.Clamp((int)(pickRoll * pool.Count), 0, pool.Count - 1)];
        }

        /// <summary>Add a hatched pet: new, or one level up (capped). The first pet ever becomes
        /// the active one.</summary>
        public static void Grant(PlayerState s, PetDef d, out bool isNew, out int level)
        {
            isNew = !s.pets.ContainsKey(d.id);
            level = isNew ? 1 : Mathf.Min(MaxLevel, s.pets[d.id] + 1);
            s.pets[d.id] = level;
            if (string.IsNullOrEmpty(s.petActive)) s.petActive = d.id;
        }

        /// <summary>Spend coins and hatch. Returns null (and spends nothing) when the player cannot
        /// pay or the feature is locked.</summary>
        public static List<(PetDef pet, bool isNew, int level)> Hatch(PlayerState s, int count, Func<float> rand)
        {
            if (!Unlocked(s)) return null;
            // free eggs are spent only when they cover the whole hatch; otherwise it is paid in coins
            bool free = s.petFreeEggs >= count;
            long price = EggPrice(s, count);
            if (s.coin < price) return null;
            if (free) s.petFreeEggs -= count;
            else s.AddCoin(-price);
            var got = new List<(PetDef, bool, int)>();
            for (int i = 0; i < count; i++)
            {
                var d = Roll(s, rand(), rand());
                Grant(s, d, out bool isNew, out int lv);
                got.Add((d, isNew, lv));
            }
            return got;
        }

        // ============================================================
        // the patrol
        // ============================================================
        /// <summary>The jobs waiting on the farm, thirsty plots first, the island the player is
        /// looking at first within each kind, up to <paramref name="max"/>.</summary>
        public static List<PetJob> FindJobs(PlayerState s, int max, int preferIsland)
        {
            var water = new List<PetJob>();
            var harvest = new List<PetJob>();
            for (int pass = 0; pass < 2; pass++)
                for (int ii = 0; ii < s.islands.Count; ii++)
                {
                    // the preferred island in pass 0, every other island in pass 1
                    if ((pass == 0) != (ii == preferIsland)) continue;
                    var isl = s.islands[ii];
                    if (!isl.unlocked) continue;
                    for (int pi = 0; pi < isl.plots.Count && pi < GS.PlotCount; pi++)
                    {
                        var st = PlotLogic.State(isl.plots[pi]);
                        if (st == PlotState.Thirsty) water.Add(new PetJob { island = ii, plot = pi, water = true });
                        else if (st == PlotState.Ready) harvest.Add(new PetJob { island = ii, plot = pi, water = false });
                    }
                }
            var jobs = new List<PetJob>(max);
            foreach (var j in water) { if (jobs.Count >= max) break; jobs.Add(j); }
            foreach (var j in harvest) { if (jobs.Count >= max) break; jobs.Add(j); }
            return jobs;
        }

        /// <summary>How much a pet wants one piece of produce. A plain crop is 1; each mutation
        /// tier multiplies it steeply (Lôi Điện is 200 times as tempting), a rarer seed a little,
        /// and the pet's favourite crop doubles it.</summary>
        public static float Appetite(PetDef pet, string cropId, int variant)
        {
            var seed = GameData.Get(cropId);
            float[] tier = { 1f, 6f, 20f, 60f, 200f };
            float w = tier[Mathf.Clamp(variant, 0, tier.Length - 1)] * (1f + (seed != null ? seed.r : 0) * 0.8f);
            if (pet != null && pet.favourite == cropId) w *= 2f;
            return w;
        }

        /// <summary>Pick a store key ("crop:variant") for a snack, weighted by <see cref="Appetite"/>;
        /// null when the warehouse is empty.</summary>
        public static string PickSnack(PlayerState s, PetDef pet, float roll)
        {
            float total = 0f;
            foreach (var kv in s.store) if (kv.Value > 0) total += Appetite(pet, Crop(kv.Key), Variant(kv.Key)) * kv.Value;
            if (total <= 0f) return null;
            float r = Mathf.Clamp01(roll) * total;
            string last = null;
            // a stable order, so a given roll always picks the same key
            var keys = new List<string>(s.store.Keys);
            keys.Sort(string.CompareOrdinal);
            foreach (var k in keys)
            {
                int n = s.store[k];
                if (n <= 0) continue;
                last = k;
                r -= Appetite(pet, Crop(k), Variant(k)) * n;
                if (r < 0f) return k;
            }
            return last;
        }

        /// <summary>Take one piece of produce out of the warehouse for the pet.</summary>
        public static bool Eat(PlayerState s, string key)
        {
            if (key == null || !s.store.TryGetValue(key, out int n) || n <= 0) return false;
            if (n <= 1) s.store.Remove(key); else s.store[key] = n - 1;
            s.petSnacks++;
            return true;
        }

        public static string Crop(string key) { int c = key.IndexOf(':'); return c < 0 ? key : key.Substring(0, c); }
        public static int Variant(string key)
        {
            int c = key.IndexOf(':');
            return c >= 0 && int.TryParse(key.Substring(c + 1), out int v) ? v : 0;
        }
    }
}
