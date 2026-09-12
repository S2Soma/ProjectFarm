using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Everything one farm owner owns, and every rule that reads or writes it.
    ///
    /// This exists as an instance rather than a static because of the planned online layer:
    /// "water my friend's tomato" needs THEIR plot, THEIR level modifiers and MY mission credit
    /// at the same time. With static state the only way to express that is to duplicate every
    /// economy function, and duplicated economy code drifts apart within a sprint.
    ///
    /// The local player is one instance (<see cref="GS.Local"/>); a visited friend is another.
    /// Nothing here touches the filesystem or the scene — see <see cref="GS"/> for both.</summary>
    public sealed class PlayerState
    {
        // ---------------- identity ----------------
        /// <summary>"" for the local player. A server uid once friends go online.</summary>
        public string ownerId = "";
        public string displayName = "Nông dân";

        /// <summary>Drives weather and crop-tag rotation. Both are pure functions of
        /// (seed, time), so no schedule is ever stored and every client agrees.</summary>
        public long worldSeed;

        // ---------------- currency ----------------
        public int lv = 1, coin = 5000;
        public long xp, energy;

        // ---------------- land ----------------
        public readonly List<Plot> plots = new List<Plot>();

        // ---------------- inventory ----------------
        public readonly Dictionary<string, int> seeds = new Dictionary<string, int>();
        public readonly Dictionary<string, int> store = new Dictionary<string, int>();
        public int[] chests = new int[4];

        // ---------------- progress ----------------
        public readonly Dictionary<string, TaskRec> missions = new Dictionary<string, TaskRec>();
        public readonly Dictionary<string, TaskRec> daily = new Dictionary<string, TaskRec>();
        public readonly HashSet<string> collected = new HashSet<string>();
        public readonly HashSet<string> claimedSets = new HashSet<string>();
        public readonly HashSet<int> claimedMs = new HashSet<int>();
        public readonly HashSet<string> shopBought = new HashSet<string>();
        public readonly HashSet<string> visited = new HashSet<string>();
        public Stats stats = new Stats();
        public int stealLeft = 20;
        public long day;
        public double buffMutateUntil;

        // ================================================================
        // derived
        // ================================================================
        public LevelInfo Info => GameData.Level(lv);
        public int XpNeed => Info.xpNeed;
        public int MaxPlots => Info.plots;

        public int SellPrice(string cropId, int variant)
        {
            var s = GameData.Get(cropId);
            if (s == null) return 0;
            return Mathf.RoundToInt(s.sell * (1f + Info.priceUp) * Art.Elem(variant).sell);
        }

        public int XpFor(Seed s, int variant)     { return Mathf.RoundToInt(s.xp * Art.Elem(variant).xp); }
        public int EnergyFor(Seed s, int variant) { return Mathf.RoundToInt(EnergyGain(s.en) * Art.Elem(variant).energy); }
        public float GrowTime(Seed s)             { return Mathf.Max(6f, Mathf.Round(s.grow * (1f - Info.growCut))); }
        public int EnergyGain(int baseAmount)     { return Mathf.RoundToInt(baseAmount * (1f + Info.energyUp)); }
        public float MutateChance                 => Info.mutate + (buffMutateUntil > GS.Now ? 0.3f : 0f);

        /// <summary>One roll decides whether the crop mutates; a second picks the element.</summary>
        public int RollVariant()
        {
            if (UnityEngine.Random.value >= MutateChance) return 0;
            float r = UnityEngine.Random.value, acc = 0f;
            for (int v = 1; v < Art.Elements.Length; v++)
            {
                acc += Art.Elements[v].chance;
                if (r < acc) return v;
            }
            return 1;
        }

        // ================================================================
        // currency
        // ================================================================
        public void AddCoin(int n) { coin = Mathf.Max(0, coin + n); }

        /// <summary>XP accumulates but never levels the farm on its own — that is what the
        /// upgrade button does.</summary>
        public void AddXp(int n) { xp += n; }

        public bool LevelUp()
        {
            var a = Info;
            if (xp < a.xpNeed || coin < a.cost) return false;
            xp -= a.xpNeed;
            coin -= a.cost;
            lv++;
            SyncPlots();
            Track("level", 0);
            return true;
        }

        public void AddEnergy(int n)
        {
            energy += n;
            int tier = ChestTier();
            while (energy >= GameData.Chests[tier].need)
            {
                energy -= GameData.Chests[tier].need;
                chests[tier]++;
            }
            if (energy > 99999) energy = 99999;
        }

        /// <summary>Which chest the energy bar is currently filling.</summary>
        public int ChestTier()
        {
            if (lv >= 20) return 3;
            if (lv >= 12) return 2;
            if (lv >= 6)  return 1;
            return 0;
        }
        public int EnergyGoal => GameData.Chests[ChestTier()].need;

        // ================================================================
        // inventory
        // ================================================================
        public void AddSeed(string id, int n)
        {
            seeds.TryGetValue(id, out int have);
            seeds[id] = have + n;
        }

        public bool TakeSeed(string id, int n)
        {
            if (!seeds.TryGetValue(id, out int have) || have < n) return false;
            have -= n;
            if (have <= 0) seeds.Remove(id); else seeds[id] = have;
            return true;
        }

        public void AddProduce(string cropId, int variant, int n = 1)
        {
            string k = cropId + ":" + variant;
            store.TryGetValue(k, out int have);
            store[k] = have + n;
            if (collected.Add(k)) Track("collect", 1);
        }

        public List<StoreItem> StoreList()
        {
            var list = new List<StoreItem>();
            foreach (var kv in store)
            {
                var parts = kv.Key.Split(':');
                int v = parts.Length > 1 ? int.Parse(parts[1]) : 0;
                list.Add(new StoreItem
                {
                    key = kv.Key, crop = parts[0], v = v, n = kv.Value,
                    price = SellPrice(parts[0], v)
                });
            }
            list.Sort((a, b) => (b.price * b.n).CompareTo(a.price * a.n));
            return list;
        }

        public int CollectedCount => collected.Count;

        // ================================================================
        // plots
        // ================================================================
        /// <summary>Plots open outward from the middle so the farm always reads as a block.</summary>
        static readonly int[] UnlockOrder = { 5, 6, 9, 10, 4, 7, 1, 2, 13, 14, 8, 11, 0, 3, 12, 15 };

        public void SyncPlots()
        {
            while (plots.Count < GS.PlotCount) plots.Add(new Plot());
            var open = new HashSet<int>(UnlockOrder.Take(MaxPlots));
            for (int i = 0; i < plots.Count; i++)
                if (open.Contains(i)) plots[i].locked = false;
        }

        /// <summary><paramref name="want"/> unlocks that exact tile — the player pays for the
        /// one they tapped.</summary>
        public bool UnlockExtraPlot(int want = -1)
        {
            if (want >= 0 && want < plots.Count && plots[want].locked)
            {
                plots[want].locked = false;
                return true;
            }
            foreach (int i in UnlockOrder)
                if (plots[i].locked) { plots[i].locked = false; return true; }
            return false;
        }

        // ================================================================
        // mission tracking
        // ================================================================
        static void Bump(Dictionary<string, TaskRec> bag, Task t, int amount)
        {
            if (!bag.TryGetValue(t.id, out var rec)) bag[t.id] = rec = new TaskRec();
            if (rec.claimed) return;
            rec.p = Mathf.Min(t.need, rec.p + amount);
        }

        public void Track(string type, int amount)
        {
            switch (type)
            {
                case "harvest": stats.harvest += amount; break;
                case "plant":   stats.plant   += amount; break;
                case "water":   stats.water   += amount; break;
                case "sell":    stats.sell    += amount; break;
                case "chest":   stats.chest   += amount; break;
                case "visit":   stats.visit   += amount; break;
                case "mutate":  stats.mutate  += amount; break;
            }

            foreach (var ch in GameData.Chapters)
                foreach (var t in ch.tasks)
                {
                    if (t.type != type || t.crop != null) continue;
                    if (type == "level" || type == "collect") continue;  // derived at read time
                    Bump(missions, t, amount);
                }

            foreach (var t in GameData.Daily)
                if (t.type == type) Bump(daily, t, amount);
        }

        public void TrackCrop(string type, string cropId, int amount)
        {
            Track(type, amount);
            foreach (var ch in GameData.Chapters)
                foreach (var t in ch.tasks)
                    if (t.type == type && t.crop == cropId) Bump(missions, t, amount);
        }

        public TaskRec Progress(Task t, bool isDaily)
        {
            var bag = isDaily ? daily : missions;
            bag.TryGetValue(t.id, out var rec);
            rec = rec ?? new TaskRec();
            if (t.type == "level")   return new TaskRec { p = Mathf.Min(t.need, lv), claimed = rec.claimed };
            if (t.type == "collect") return new TaskRec { p = Mathf.Min(t.need, CollectedCount), claimed = rec.claimed };
            return new TaskRec { p = Mathf.Min(t.need, rec.p), claimed = rec.claimed };
        }

        public bool ClaimTask(Task t, bool isDaily)
        {
            var bag = isDaily ? daily : missions;
            var pr = Progress(t, isDaily);
            if (pr.claimed || pr.p < t.need) return false;
            if (!bag.TryGetValue(t.id, out var rec)) bag[t.id] = rec = new TaskRec();
            rec.claimed = true;
            rec.p = t.need;
            AddCoin(t.coin);
            AddXp(t.xp);
            return true;
        }

        public Task ActiveMission(out TaskRec progress)
        {
            foreach (var ch in GameData.Chapters)
                foreach (var t in ch.tasks)
                {
                    var pr = Progress(t, false);
                    if (!pr.claimed) { progress = pr; return t; }
                }
            progress = null;
            return null;
        }

        // ================================================================
        // daily reset
        // ================================================================
        public void CheckDay()
        {
            long d = (long)(GS.Now / 86400000.0);
            if (day != d)
            {
                day = d;
                daily.Clear();
                visited.Clear();
                stealLeft = 20;
            }
        }

        // ================================================================
        // lifecycle
        // ================================================================
        /// <summary>A save from an older crop roster can hold ids that no longer exist.</summary>
        public void PruneUnknown()
        {
            foreach (var id in seeds.Keys.ToList())    if (GameData.Get(id) == null) seeds.Remove(id);
            foreach (var k in store.Keys.ToList())     if (GameData.Get(k.Split(':')[0]) == null) store.Remove(k);
            foreach (var k in collected.ToList())      if (GameData.Get(k.Split(':')[0]) == null) collected.Remove(k);
            foreach (var p in plots)
                if (p.crop != null && GameData.Get(p.crop) == null) { p.crop = null; p.variant = 0; }
        }

        public void SeedStarter()
        {
            seeds["carrot"] = 6; seeds["wheat"] = 4; seeds["tomato"] = 2;
        }

        public void NewGame()
        {
            lv = 1; xp = 0; coin = 5000; energy = 0;
            worldSeed = NewWorldSeed();
            plots.Clear(); seeds.Clear(); store.Clear(); missions.Clear(); daily.Clear();
            collected.Clear(); claimedSets.Clear(); claimedMs.Clear(); shopBought.Clear(); visited.Clear();
            chests = new int[4]; stats = new Stats(); stealLeft = 20; buffMutateUntil = 0;
            SeedStarter();
            SyncPlots();
        }

        /// <summary>Non-zero so a save that predates the field is distinguishable from a real one.</summary>
        static long NewWorldSeed()
        {
            long hi = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            long lo = (uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            long s = (hi << 32) ^ lo;
            return s == 0 ? 0x5DEECE66DL : s;
        }
    }

    public struct StoreItem { public string key, crop; public int v, n, price; }
}
