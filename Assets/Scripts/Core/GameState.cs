using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LQFarm
{
    [Serializable]
    public class Plot
    {
        public bool locked = true;
        public string crop;
        public double plantedAt;
        public float dur;
        public bool watered;
        public int variant;
        public float bonus;
    }

    [Serializable] public class TaskRec { public int p; public bool claimed; }

    [Serializable]
    public class Stats
    {
        public int harvest, plant, water, sell, chest, visit, mutate;
    }

    /// <summary>Flat, JsonUtility-friendly mirror of the live state.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int lv = 1, coin = 5000;
        public long xp, energy;
        public List<Plot> plots = new List<Plot>();
        public int[] chests = new int[4];
        public Stats stats = new Stats();
        public int stealLeft = 20;
        public long day;
        public double buffMutateUntil;

        // dictionaries flattened to parallel lists
        public List<string> seedK = new List<string>(); public List<int> seedV = new List<int>();
        public List<string> storeK = new List<string>(); public List<int> storeV = new List<int>();
        public List<string> misK = new List<string>();  public List<TaskRec> misV = new List<TaskRec>();
        public List<string> dayK = new List<string>();  public List<TaskRec> dayV = new List<TaskRec>();
        public List<string> collected = new List<string>();
        public List<string> claimedSets = new List<string>();
        public List<int> claimedMs = new List<int>();
        public List<string> shopBought = new List<string>();
        public List<string> visited = new List<string>();
    }

    /// <summary>Game state, persistence and every economy rule.</summary>
    public static class GS
    {
        public const int PlotCount = 16;
        static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "lqfarm.save.json");

        public static int lv = 1, coin = 5000;
        public static long xp, energy;
        public static readonly List<Plot> plots = new List<Plot>();
        public static readonly Dictionary<string, int> seeds = new Dictionary<string, int>();
        public static readonly Dictionary<string, int> store = new Dictionary<string, int>();
        public static int[] chests = new int[4];
        public static readonly Dictionary<string, TaskRec> missions = new Dictionary<string, TaskRec>();
        public static readonly Dictionary<string, TaskRec> daily = new Dictionary<string, TaskRec>();
        public static readonly HashSet<string> collected = new HashSet<string>();
        public static readonly HashSet<string> claimedSets = new HashSet<string>();
        public static readonly HashSet<int> claimedMs = new HashSet<int>();
        public static readonly HashSet<string> shopBought = new HashSet<string>();
        public static readonly HashSet<string> visited = new HashSet<string>();
        public static Stats stats = new Stats();
        public static int stealLeft = 20;
        public static long day;
        public static double buffMutateUntil;

        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        // ---------------- derived ----------------
        public static LevelInfo Info => GameData.Level(lv);
        public static int XpNeed => Info.xpNeed;
        public static int MaxPlots => Info.plots;

        public static int SellPrice(string cropId, int variant)
        {
            var s = GameData.Get(cropId);
            if (s == null) return 0;
            return Mathf.RoundToInt(s.sell * (1f + Info.priceUp) * Art.Elem(variant).sell);
        }
        public static int XpFor(Seed s, int variant)     { return Mathf.RoundToInt(s.xp * Art.Elem(variant).xp); }
        public static int EnergyFor(Seed s, int variant) { return Mathf.RoundToInt(EnergyGain(s.en) * Art.Elem(variant).energy); }
        public static float GrowTime(Seed s)             { return Mathf.Max(6f, Mathf.Round(s.grow * (1f - Info.growCut))); }
        public static int EnergyGain(int baseAmount)     { return Mathf.RoundToInt(baseAmount * (1f + Info.energyUp)); }
        public static float MutateChance                 => Info.mutate + (buffMutateUntil > Now ? 0.3f : 0f);

        /// <summary>One roll decides whether the crop mutates; a second picks the element.</summary>
        public static int RollVariant()
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

        // ---------------- currency ----------------
        public static void AddCoin(int n) { coin = Mathf.Max(0, coin + n); }

        /// <summary>XP accumulates but never levels the farm on its own — that is what the upgrade button does.</summary>
        public static void AddXp(int n) { xp += n; }

        public static bool LevelUp()
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

        public static void AddEnergy(int n)
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
        public static int ChestTier()
        {
            if (lv >= 20) return 3;
            if (lv >= 12) return 2;
            if (lv >= 6)  return 1;
            return 0;
        }
        public static int EnergyGoal => GameData.Chests[ChestTier()].need;

        // ---------------- inventory ----------------
        public static void AddSeed(string id, int n)
        {
            seeds.TryGetValue(id, out int have);
            seeds[id] = have + n;
        }

        public static bool TakeSeed(string id, int n)
        {
            if (!seeds.TryGetValue(id, out int have) || have < n) return false;
            have -= n;
            if (have <= 0) seeds.Remove(id); else seeds[id] = have;
            return true;
        }

        public static void AddProduce(string cropId, int variant, int n = 1)
        {
            string k = cropId + ":" + variant;
            store.TryGetValue(k, out int have);
            store[k] = have + n;
            if (collected.Add(k)) Track("collect", 1);
        }

        public struct StoreItem { public string key, crop; public int v, n, price; }

        public static List<StoreItem> StoreList()
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

        public static int CollectedCount => collected.Count;

        // ---------------- plots ----------------
        /// <summary>Plots open outward from the middle so the farm always reads as a block.</summary>
        static readonly int[] UnlockOrder = { 5, 6, 9, 10, 4, 7, 1, 2, 13, 14, 8, 11, 0, 3, 12, 15 };

        public static void SyncPlots()
        {
            while (plots.Count < PlotCount) plots.Add(new Plot());
            var open = new HashSet<int>(UnlockOrder.Take(MaxPlots));
            for (int i = 0; i < plots.Count; i++)
                if (open.Contains(i)) plots[i].locked = false;
        }

        /// <summary><paramref name="want"/> unlocks that exact tile — the player pays for the one they tapped.</summary>
        public static bool UnlockExtraPlot(int want = -1)
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

        // ---------------- mission tracking ----------------
        static void Bump(Dictionary<string, TaskRec> bag, Task t, int amount)
        {
            if (!bag.TryGetValue(t.id, out var rec)) bag[t.id] = rec = new TaskRec();
            if (rec.claimed) return;
            rec.p = Mathf.Min(t.need, rec.p + amount);
        }

        public static void Track(string type, int amount)
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

        public static void TrackCrop(string type, string cropId, int amount)
        {
            Track(type, amount);
            foreach (var ch in GameData.Chapters)
                foreach (var t in ch.tasks)
                    if (t.type == type && t.crop == cropId) Bump(missions, t, amount);
        }

        public static TaskRec Progress(Task t, bool isDaily)
        {
            var bag = isDaily ? daily : missions;
            bag.TryGetValue(t.id, out var rec);
            rec = rec ?? new TaskRec();
            if (t.type == "level")   return new TaskRec { p = Mathf.Min(t.need, lv), claimed = rec.claimed };
            if (t.type == "collect") return new TaskRec { p = Mathf.Min(t.need, CollectedCount), claimed = rec.claimed };
            return new TaskRec { p = Mathf.Min(t.need, rec.p), claimed = rec.claimed };
        }

        public static bool ClaimTask(Task t, bool isDaily)
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

        public static Task ActiveMission(out TaskRec progress)
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

        // ---------------- daily reset ----------------
        public static void CheckDay()
        {
            long d = (long)(Now / 86400000.0);
            if (day != d)
            {
                day = d;
                daily.Clear();
                visited.Clear();
                stealLeft = 20;
            }
        }

        // ---------------- persistence ----------------
        public static void Save()
        {
            var d = new SaveData
            {
                lv = lv, coin = coin, xp = xp, energy = energy,
                plots = plots, chests = chests, stats = stats,
                stealLeft = stealLeft, day = day, buffMutateUntil = buffMutateUntil,
                collected = collected.ToList(),
                claimedSets = claimedSets.ToList(),
                claimedMs = claimedMs.ToList(),
                shopBought = shopBought.ToList(),
                visited = visited.ToList(),
            };
            foreach (var kv in seeds)    { d.seedK.Add(kv.Key);  d.seedV.Add(kv.Value); }
            foreach (var kv in store)    { d.storeK.Add(kv.Key); d.storeV.Add(kv.Value); }
            foreach (var kv in missions) { d.misK.Add(kv.Key);   d.misV.Add(kv.Value); }
            foreach (var kv in daily)    { d.dayK.Add(kv.Key);   d.dayV.Add(kv.Value); }

            try { System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(d)); }
            catch (Exception e) { Debug.LogWarning("Không lưu được tiến trình: " + e.Message); }
        }

        public static void Load()
        {
            string raw = null;
            try { if (System.IO.File.Exists(SavePath)) raw = System.IO.File.ReadAllText(SavePath); }
            catch (Exception) { /* fall through to a new game */ }

            if (string.IsNullOrEmpty(raw)) { NewGame(); }
            else
            {
                SaveData d = null;
                try { d = JsonUtility.FromJson<SaveData>(raw); } catch (Exception) { }
                if (d == null) { NewGame(); }
                else Apply(d);
            }

            PruneUnknown();
            SyncPlots();
            CheckDay();
            if (seeds.Count == 0 && stats.plant == 0) SeedStarter();
        }

        static void Apply(SaveData d)
        {
            lv = Mathf.Max(1, d.lv); coin = d.coin; xp = d.xp; energy = d.energy;
            plots.Clear(); if (d.plots != null) plots.AddRange(d.plots);
            chests = (d.chests != null && d.chests.Length == 4) ? d.chests : new int[4];
            stats = d.stats ?? new Stats();
            stealLeft = d.stealLeft; day = d.day; buffMutateUntil = d.buffMutateUntil;

            seeds.Clear();    for (int i = 0; i < d.seedK.Count && i < d.seedV.Count; i++) seeds[d.seedK[i]] = d.seedV[i];
            store.Clear();    for (int i = 0; i < d.storeK.Count && i < d.storeV.Count; i++) store[d.storeK[i]] = d.storeV[i];
            missions.Clear(); for (int i = 0; i < d.misK.Count && i < d.misV.Count; i++) missions[d.misK[i]] = d.misV[i];
            daily.Clear();    for (int i = 0; i < d.dayK.Count && i < d.dayV.Count; i++) daily[d.dayK[i]] = d.dayV[i];

            collected.Clear();   foreach (var k in d.collected)   collected.Add(k);
            claimedSets.Clear(); foreach (var k in d.claimedSets) claimedSets.Add(k);
            claimedMs.Clear();   foreach (var k in d.claimedMs)   claimedMs.Add(k);
            shopBought.Clear();  foreach (var k in d.shopBought)  shopBought.Add(k);
            visited.Clear();     foreach (var k in d.visited)     visited.Add(k);
        }

        /// <summary>A save from an older crop roster can hold ids that no longer exist.</summary>
        static void PruneUnknown()
        {
            foreach (var id in seeds.Keys.ToList())    if (GameData.Get(id) == null) seeds.Remove(id);
            foreach (var k in store.Keys.ToList())     if (GameData.Get(k.Split(':')[0]) == null) store.Remove(k);
            foreach (var k in collected.ToList())      if (GameData.Get(k.Split(':')[0]) == null) collected.Remove(k);
            foreach (var p in plots)
                if (p.crop != null && GameData.Get(p.crop) == null) { p.crop = null; p.variant = 0; }
        }

        static void SeedStarter()
        {
            seeds["carrot"] = 6; seeds["wheat"] = 4; seeds["tomato"] = 2;
        }

        public static void NewGame()
        {
            lv = 1; xp = 0; coin = 5000; energy = 0;
            plots.Clear(); seeds.Clear(); store.Clear(); missions.Clear(); daily.Clear();
            collected.Clear(); claimedSets.Clear(); claimedMs.Clear(); shopBought.Clear(); visited.Clear();
            chests = new int[4]; stats = new Stats(); stealLeft = 20; buffMutateUntil = 0;
            SeedStarter();
            SyncPlots();
        }

        public static void Reset()
        {
            try { if (System.IO.File.Exists(SavePath)) System.IO.File.Delete(SavePath); } catch (Exception) { }
            NewGame();
        }
    }
}
