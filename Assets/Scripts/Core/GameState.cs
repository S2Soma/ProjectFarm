using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    public enum PlotState : byte
    {
        Locked,
        Empty,
        Growing,
        /// <summary>Growing, and a watering window is open right now.</summary>
        Thirsty,
        Ready,
    }

    public class Plot
    {
        public bool locked = true;

        /// <summary>From the island's layout (<see cref="IslandSys.KindOf"/>), set on every load:
        /// a big plot takes only big crops; a slot that is not land at all (the river on Đảo Nước,
        /// the cells a big plot covers) stays locked and is never drawn, bought or planted.</summary>
        public bool big, none;
        public string crop;

        /// <summary>Unix ms UTC. Absolute, so growth needs no offline catch-up pass — elapsed
        /// time is a subtraction, not an integral over the hours the app was closed.</summary>
        public long plantedAt;

        /// <summary>Nominal grow seconds, frozen at plant.</summary>
        public float dur;

        /// <summary>Seconds removed by watering. Replaces the old one-shot <c>bonus</c>.</summary>
        public float cut;

        /// <summary>One bit per watering window already consumed (bit 0 = first window).
        /// Consumed windows can never be reclaimed, which is what stops an offline player from
        /// banking them — see <see cref="WaterSys"/>.</summary>
        public byte waterMask;

        /// <summary>Which of those windows a visitor used. Keeps a friend's help from being
        /// credited twice, and is what "Hà đã tưới 3 cây của bạn" will read.</summary>
        public byte friendMask;

        /// <summary>How many windows this crop was planted with. Frozen so a balance change
        /// never rewrites a crop that is already in the ground.</summary>
        public byte windowCount;

        /// <summary>Seconds one watering takes off this crop (the seed's <c>waterCut</c>), frozen at
        /// plant for the same reason, before drought doubles it. 0 on a plot planted before watering
        /// became per-crop (2026-09-15): that crop finishes under the old rule, 20% of its duration
        /// split across its windows — see <see cref="WaterSys.BaseCut"/>.</summary>
        public float waterSec;

        public int variant;

        // ---- snapshot, frozen at plant, never revalued ----
        /// <summary>The weather this crop was planted under. Stored rather than recomputed so a
        /// player who was offline can never have value taken away, and so elapsed growth stays a
        /// subtraction instead of an integral over the hours the app was closed.</summary>
        public byte plantWeather;

        /// <summary>Sell multiplier from weather x tag, resolved at plant. One number instead of
        /// re-deriving the chain at harvest — the chain's inputs have rotated by then.</summary>
        public float sellMul = 1f;
        public float xpMul = 1f;
    }

    /// <summary>One island: sixteen plots and the state that belongs to the land itself.
    ///
    /// Exists already, holding exactly one instance, so the save schema is final before the
    /// multi-island work starts — otherwise that step would need a second migration for data
    /// that never had to change shape.</summary>
    public class Island
    {
        public int id;
        public bool unlocked;
        public long unlockedAt;
        public readonly List<Plot> plots = new List<Plot>();

        /// <summary>Crops handed over toward this island's tribute, by crop id. Lives on the
        /// LOCKED island rather than in a global ledger so the sign painted on it can read
        /// straight off the thing the player is looking at.</summary>
        public Dictionary<string, int> tribute;

        public Island(int id, bool unlocked) { this.id = id; this.unlocked = unlocked; }
        public Island() { }
    }

    public class TaskRec { public int p; public bool claimed; }

    /// <summary>One short-term contract occupying a board slot.
    ///
    /// Definition and state are deliberately in the same object: the definition is reproducible
    /// from (worldSeed, slot, genCycle), but storing it flat means a save round-trip cannot
    /// disagree with what the player was looking at — which is the kind of mismatch that shows up
    /// as a mission changing its own requirements between sessions.</summary>
    public class MissionRec
    {
        public int slot;
        public long genCycle;

        public string type;
        public string cropId;      // null = any crop
        public Grade grade;
        public int need;

        public int p;
        public bool claimed;

        /// <summary>Unix ms. Passing it without claiming breaks the streak.</summary>
        public long expiresAt;

        /// <summary>When an empty slot becomes eligible for a new contract.</summary>
        public long refillAt;

        public bool Empty => string.IsNullOrEmpty(type);
        public bool Done => p >= need;
    }

    public class Stats
    {
        public int harvest, plant, water, sell, chest, visit, mutate;
        /// <summary>Plots watered or harvested by a pet.</summary>
        public int petJobs;
    }

    /// <summary>Wall-clock state that has to survive a restart.
    ///
    /// <see cref="lastSeenUtc"/> is the important one: it is a monotonic floor under
    /// <see cref="GS.Now"/>, so winding the device clock backwards is a no-op rather than a way
    /// to re-claim the daily reset. This is not anti-cheat — a determined player edits the save
    /// file — it exists so an honest player crossing a timezone or receiving an NTP correction
    /// does not lose crops, and so the trivial exploit stops being trivial.</summary>
    public class Clock
    {
        public long lastSeenUtc;
        public long savedAtUtc;
        public double savedAtMono;
        public int resetOffsetMinutes;
        public bool clockSuspect;
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

        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Unix ms UTC, clamped so it can never go backwards within a save.
        ///
        /// Every rule in the game reads time through here. A player who winds the device clock
        /// back therefore freezes time instead of rewinding it: crops do not un-grow, expired
        /// things do not un-expire, and — the one that actually mattered — the daily reset in
        /// <see cref="PlayerState.CheckDay"/> cannot be triggered a second time.</summary>
        public static long Now
        {
            get
            {
                long raw = (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
                var c = Local?.clock;
                if (c == null) return raw;
                if (raw < c.lastSeenUtc) { c.clockSuspect = true; return c.lastSeenUtc; }
                c.lastSeenUtc = raw;
                return raw;
            }
        }

        // ---------------- persistence ----------------
        public static void Save() { SaveIO.Save(Local); }

        public static void Load()
        {
            var s = Local;
            SaveIO.Load(s);
            // Whatever Load decided — the file, a new game, or a refused file set aside — this is
            // now the state the player is meant to have, so it may be written.
            s.loaded = true;

            s.EnsureWorldSeed();
            s.PruneUnknown();
            s.SyncPlots();
            ApplyOffline(s);
            if (s.seeds.Count == 0 && s.stats.plant == 0) s.SeedStarter();
        }

        /// <summary>Everything time-driven that a closed app could have missed.
        ///
        /// The list is deliberately two items long. Growth, watering windows, weather and crop
        /// tags are all pure functions of stored timestamps, so none of them needs a catch-up
        /// pass — that is the payoff for freezing their inputs at plant time. What is left is
        /// genuinely stateful, and each is capped at ONE cycle no matter how long the player was
        /// away: thirty days offline must not pay out thirty daily resets.</summary>
        static void ApplyOffline(PlayerState s)
        {
            s.CheckDay();
            s.SyncContracts();
            // the rain kept watering while the app was closed
            foreach (var isl in s.islands)
                foreach (var p in isl.plots) WaterSys.RainWater(s, p, catchUp: true);
        }

        public static void Reset()
        {
            SaveIO.Delete();
            Local.NewGame();
            Local.loaded = true;
        }
    }
}
