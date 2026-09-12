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

    /// <summary>Flat, JsonUtility-friendly mirror of one <see cref="PlayerState"/>.
    ///
    /// Replaced wholesale in the next step by a Newtonsoft schema — JsonUtility silently drops
    /// JSON keys it has no field for, which is fatal once a save has to survive a newer client
    /// or a server. Kept as-is here so this step changes behaviour in no way at all.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int lv = 1, coin = 5000;
        public long xp, energy;
        public long worldSeed;
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

    /// <summary>The game's entry point into state: who is playing, whose farm is on screen,
    /// what time it is, and persistence.
    ///
    /// Everything that used to be a static field here now lives on <see cref="PlayerState"/>.
    /// The two statics that remain are deliberate and are the only ones allowed: read
    /// <see cref="Local"/> for anything that belongs to the player (wallet, level, inventory,
    /// missions, collections) and <see cref="Viewing"/> for the farm currently drawn. They are
    /// the same object until a friend's farm can be visited, at which point the distinction is
    /// what stops "their crops, my coins" from needing a second copy of the economy.</summary>
    public static class GS
    {
        public const int PlotCount = 16;
        static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "lqfarm.save.json");

        /// <summary>The player holding the phone. Wallet, level, inventory, progress.</summary>
        public static PlayerState Local = new PlayerState();

        static PlayerState _viewing;

        /// <summary>The farm being drawn. Equals <see cref="Local"/> except while visiting.
        ///
        /// Deliberately a property that falls back to <see cref="Local"/> rather than a field
        /// seeded with it: a field would capture whatever <c>Local</c> happened to be at static
        /// init and then silently go stale if <c>Local</c> were ever replaced wholesale — the
        /// kind of bug that shows up as a friend's farm still on screen after leaving.
        /// Assign a host to enter a farm; assign null to go home.</summary>
        public static PlayerState Viewing
        {
            get { return _viewing ?? Local; }
            set { _viewing = ReferenceEquals(value, Local) ? null : value; }
        }

        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        // ---------------- persistence ----------------
        public static void Save()
        {
            var s = Local;
            var d = new SaveData
            {
                lv = s.lv, coin = s.coin, xp = s.xp, energy = s.energy, worldSeed = s.worldSeed,
                plots = s.plots, chests = s.chests, stats = s.stats,
                stealLeft = s.stealLeft, day = s.day, buffMutateUntil = s.buffMutateUntil,
                collected = s.collected.ToList(),
                claimedSets = s.claimedSets.ToList(),
                claimedMs = s.claimedMs.ToList(),
                shopBought = s.shopBought.ToList(),
                visited = s.visited.ToList(),
            };
            foreach (var kv in s.seeds)    { d.seedK.Add(kv.Key);  d.seedV.Add(kv.Value); }
            foreach (var kv in s.store)    { d.storeK.Add(kv.Key); d.storeV.Add(kv.Value); }
            foreach (var kv in s.missions) { d.misK.Add(kv.Key);   d.misV.Add(kv.Value); }
            foreach (var kv in s.daily)    { d.dayK.Add(kv.Key);   d.dayV.Add(kv.Value); }

            try { System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(d)); }
            catch (Exception e) { Debug.LogWarning("Không lưu được tiến trình: " + e.Message); }
        }

        public static void Load()
        {
            var s = Local;
            string raw = null;
            try { if (System.IO.File.Exists(SavePath)) raw = System.IO.File.ReadAllText(SavePath); }
            catch (Exception) { /* fall through to a new game */ }

            if (string.IsNullOrEmpty(raw)) { s.NewGame(); }
            else
            {
                SaveData d = null;
                try { d = JsonUtility.FromJson<SaveData>(raw); } catch (Exception) { }
                if (d == null) { s.NewGame(); }
                else Apply(s, d);
            }

            s.PruneUnknown();
            s.SyncPlots();
            s.CheckDay();
            if (s.seeds.Count == 0 && s.stats.plant == 0) s.SeedStarter();
        }

        static void Apply(PlayerState s, SaveData d)
        {
            s.lv = Mathf.Max(1, d.lv); s.coin = d.coin; s.xp = d.xp; s.energy = d.energy;
            s.worldSeed = d.worldSeed;
            s.plots.Clear(); if (d.plots != null) s.plots.AddRange(d.plots);
            s.chests = (d.chests != null && d.chests.Length == 4) ? d.chests : new int[4];
            s.stats = d.stats ?? new Stats();
            s.stealLeft = d.stealLeft; s.day = d.day; s.buffMutateUntil = d.buffMutateUntil;

            s.seeds.Clear();    for (int i = 0; i < d.seedK.Count && i < d.seedV.Count; i++) s.seeds[d.seedK[i]] = d.seedV[i];
            s.store.Clear();    for (int i = 0; i < d.storeK.Count && i < d.storeV.Count; i++) s.store[d.storeK[i]] = d.storeV[i];
            s.missions.Clear(); for (int i = 0; i < d.misK.Count && i < d.misV.Count; i++) s.missions[d.misK[i]] = d.misV[i];
            s.daily.Clear();    for (int i = 0; i < d.dayK.Count && i < d.dayV.Count; i++) s.daily[d.dayK[i]] = d.dayV[i];

            s.collected.Clear();   foreach (var k in d.collected)   s.collected.Add(k);
            s.claimedSets.Clear(); foreach (var k in d.claimedSets) s.claimedSets.Add(k);
            s.claimedMs.Clear();   foreach (var k in d.claimedMs)   s.claimedMs.Add(k);
            s.shopBought.Clear();  foreach (var k in d.shopBought)  s.shopBought.Add(k);
            s.visited.Clear();     foreach (var k in d.visited)     s.visited.Add(k);
        }

        public static void Reset()
        {
            try { if (System.IO.File.Exists(SavePath)) System.IO.File.Delete(SavePath); } catch (Exception) { }
            Local.NewGame();
        }
    }
}
