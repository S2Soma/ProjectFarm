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

        /// <summary>When this farm was created, unix ms. Weather's grace period reads it: a new
        /// player meeting storm or drought before they have met good weather has no frame of
        /// reference for it.</summary>
        public long createdAt;

        // ---------------- currency ----------------
        public int lv = 1, coin = 5000;
        public long xp, energy;

        // ---------------- land ----------------
        public readonly List<Island> islands = new List<Island> { new Island(0, true) };

        /// <summary>The home island's plots. Everything outside the world layer still thinks in
        /// one field, and will keep doing so until the multi-island step; this property is what
        /// lets the save schema be island-shaped today without that rippling into the UI.</summary>
        public List<Plot> plots => islands[0].plots;

        // ---------------- time ----------------
        public readonly Clock clock = new Clock();

        /// <summary>The save document this state was last read from. Held ONLY so that JSON
        /// keys written by a newer build — or by the server, later — survive a load-then-save
        /// cycle here. Never read for gameplay; see <see cref="SaveIO"/>.</summary>
        internal SaveDto loadedEcho;

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
        public long buffMutateUntil;
        /// <summary>Weather is visible this far ahead — see <see cref="ShopSys.ForecastMs"/>.</summary>
        public long forecastUntil;
        /// <summary>Plantings still shielded from bad weather.</summary>
        public int greenhouse;

        /// <summary>Plantings since the last legendary. A jackpot nobody ever meets is not a
        /// jackpot, so after enough misses the next mutation is forced to the top tier.</summary>
        public int sinceLegendary;

        // ---------------- contracts ----------------
        /// <summary>The short-term mission board. One entry per slot; an entry with no type is an
        /// empty slot waiting on its refill timer.</summary>
        public readonly List<MissionRec> contracts = new List<MissionRec>();

        /// <summary>Contracts completed back to back without letting one expire. This is the
        /// engagement mechanic in one integer — a player who clears their board sees the good
        /// grades roughly twice as often, and "chuỗi 7" is legible in a way a hidden luck
        /// modifier never is.</summary>
        public int streak;

        /// <summary>Contracts seen since the last diamond, for the pity rule.</summary>
        public int sinceDiamond;

        /// <summary>Contracts completed today, against <see cref="MissionSys.DailyCap"/>.</summary>
        public int contractsToday;

        // ================================================================
        // derived
        // ================================================================
        public LevelInfo Info => GameData.Level(lv);
        public int XpNeed => Info.xpNeed;
        /// <summary>Plots actually open, across every island. Levelling no longer hands these
        /// out — it makes the next one buyable — so this counts what the player bought rather
        /// than what the level table promises.</summary>
        public int OpenPlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < islands.Count; i++)
                    if (i == 0 || islands[i].unlocked) n += IslandSys.OpenCount(islands[i]);
                return n;
            }
        }

        /// <summary>What ONE fruit sells for.
        ///
        /// Derived by DIVIDING the harvest value by the fruits that harvest actually produced —
        /// not by scaling a fixed per-fruit price. Those look equivalent and are not, because a
        /// good mutation adds fruits: pricing each of a legendary avocado's five fruits at the
        /// two-fruit rate paid 70,135 instead of 28,054, which is the 25x-instead-of-10x leak
        /// the no-double-dip rule exists to prevent. A test caught it; nothing in play would have.
        ///
        /// Since yield is a pure function of (crop, tier), the divisor is known for any
        /// warehouse stack without having to remember which plot it came from.</summary>
        public int SellPrice(string cropId, int variant)
        {
            var s = GameData.Get(cropId);
            if (s == null) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(HarvestValue(cropId, variant) / (float)YieldOf(s, variant)));
        }

        /// <summary>What one harvest from this plot is worth, using the multipliers stamped on
        /// it at plant rather than whatever the weather happens to be now.</summary>
        public int SellPriceOf(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return 0;
            return Mathf.RoundToInt(SellPrice(p.crop, p.variant) * Mathf.Max(0.01f, p.sellMul));
        }

        public int XpFor(Seed s, int variant)     { return Mathf.RoundToInt(s.xp * Art.Elem(variant).xp); }
        public int EnergyFor(Seed s, int variant) { return Mathf.RoundToInt(EnergyGain(s.en) * Art.Elem(variant).energy); }
        public float GrowTime(Seed s)             { return Mathf.Max(6f, Mathf.Round(s.grow * (1f - Info.growCut))); }

        /// <summary>Grow time as it will actually be: weather at plant, then the mutation tier.
        ///
        /// A better mutation takes longer, and since the tier is rolled AT PLANT the player can
        /// see it from the first growth stage. That turns mutation from a slot machine pulled at
        /// harvest into anticipation — and it is the only way the extra time can exist at all,
        /// because a duration cannot be changed after the fact without rewriting the plot.</summary>
        public float GrowTimeIn(Seed s, Weather w, int variant = 0, bool sheltered = false)
        {
            var wd = sheltered ? WeatherSys.Sheltered(w) : WeatherSys.Def(w);
            float baseSec = Mathf.Max(6f, Mathf.Round(GrowTime(s) * wd.grow));
            var el = Art.Elem(variant);
            if (el.grow <= 1f) return baseSec;

            float add = Mathf.Min(baseSec * (el.grow - 1f), Art.MaxMutationGrowAddSeconds);
            return Mathf.Round(baseSec + add);
        }

        /// <summary>Fruits this plot will produce. Frozen at plant so the popup can promise it.</summary>
        public int YieldOf(Seed s, int variant)
        {
            return Mathf.Max(1, s.fruits + Art.Elem(variant).yieldAdd);
        }

        /// <summary>Coin value of one whole harvest.
        ///
        /// **No double dip.** Value is base x level x tier, full stop — the extra fruits a good
        /// mutation produces do NOT multiply on top. Without that rule a legendary avocado pays
        /// 845 x 5 fruits x 1.66 x 10 = 70,135, which is 25x a normal harvest and breaks every
        /// coin sink in the game. With it: 28,050, exactly the 10x the tier advertises.
        ///
        /// The extra fruits still matter — they count toward island tribute and the collection
        /// book — which is a real, felt reward that costs the economy nothing.</summary>
        public int HarvestValue(string cropId, int variant)
        {
            var s = GameData.Get(cropId);
            if (s == null) return 0;
            return Mathf.RoundToInt(s.sell * (1f + Info.priceUp) * Art.Elem(variant).sell);
        }
        public int EnergyGain(int baseAmount)     { return Mathf.RoundToInt(baseAmount * (1f + Info.energyUp)); }
        public float MutateChance                 => Info.mutate + (buffMutateUntil > GS.Now ? 0.3f : 0f);

        /// <summary>Mutation chance for one planting, after weather and the crop's tag.
        ///
        /// Hard-capped at 0.75, and the cap's job is not balance — it is to stop mutation from
        /// becoming a CERTAINTY. The slot-machine feel is the entire reason the mutation system
        /// exists; a guaranteed mutation is just a bigger number.</summary>
        public float MutateChanceFor(string cropId, Weather w, bool sheltered = false)
        {
            float m = MutateChance * (sheltered ? WeatherSys.Sheltered(w) : WeatherSys.Def(w)).mutate;
            var tag = TagSys.TagOf(this, cropId, GS.Now);
            m *= TagSys.Def(tag).mutate;
            return Mathf.Min(0.75f, m);
        }

        /// <summary>Resolve the situational multipliers for one planting.
        ///
        /// Called once, at plant, and the result is stamped on the plot. The chain is
        /// multiplicative because the codebase already is — inserting an additive layer would
        /// make "x1.35" mean something different at level 3 than at level 30, and the readout
        /// being learnable is the whole reason the system works.
        ///
        /// Capped at 3.0 per axis. Nothing in the current tables reaches it (snow 1.30 x season
        /// 2.20 = 2.86 is the worst case); it is a guardrail for tags added later.</summary>
        public void ResolveSituational(string cropId, Weather w, out float sellMul, out float xpMul,
                                       bool sheltered = false)
        {
            var wd = sheltered ? WeatherSys.Sheltered(w) : WeatherSys.Def(w);
            var tag = TagSys.TagOf(this, cropId, GS.Now);
            var td = TagSys.Def(tag);

            float tagSell = 1f, tagXp = 1f;
            if (tag == CropTag.Season)
            {
                // the bonus applies only in the named weather — and costs nothing outside it
                if (TagSys.SeasonWeatherOf(this, cropId, GS.Now) == w) { tagSell = td.sell; tagXp = td.xp; }
            }
            else { tagSell = td.sell; tagXp = td.xp; }

            sellMul = Mathf.Min(3f, wd.sell * tagSell);
            xpMul = Mathf.Min(3f, wd.xp * tagXp);
        }

        /// <summary>Plantings without a legendary before the next mutation is forced to be one.
        /// At level 30 the natural rate is about 1 in 238, so this bites only for a player who
        /// has been unlucky well past the point of noticing.</summary>
        public const int LegendaryPity = 400;

        /// <summary>Mutation roll for one planting: does it mutate, and at what tier.</summary>
        public int RollVariantFor(string cropId, Weather w, bool sheltered = false)
        {
            sinceLegendary++;

            if (UnityEngine.Random.value >= MutateChanceFor(cropId, w, sheltered)) return 0;

            int top = Art.Elements.Length - 1;
            if (sinceLegendary >= LegendaryPity) { sinceLegendary = 0; return top; }

            float r = UnityEngine.Random.value, acc = 0f;
            for (int v = 1; v < Art.Elements.Length; v++)
            {
                acc += Art.Elements[v].chance;
                if (r < acc)
                {
                    if (v == top) sinceLegendary = 0;
                    return v;
                }
            }
            return 1;
        }

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

        /// <summary>Fruits of a crop on hand, across every mutation tier.
        ///
        /// Tribute counts FRUITS, not harvests, which is what makes a good mutation matter to a
        /// player who is mid-way through unlocking an island: a legendary cherry hands over nine
        /// toward the quota instead of six.</summary>
        public int StockOf(string cropId)
        {
            int n = 0;
            foreach (var kv in store)
            {
                int c = kv.Key.IndexOf(':');
                if (c > 0 && string.CompareOrdinal(kv.Key, 0, cropId, 0, c) == 0 && c == cropId.Length)
                    n += kv.Value;
            }
            return n;
        }

        /// <summary>Spend fruits, cheapest tier first.
        ///
        /// Direction is load-bearing. Taking the best tier first would quietly feed a player's
        /// legendaries into a quota that counts them the same as a plain one — the single worst
        /// trade in the game, made silently on their behalf.</summary>
        public int TakeStock(string cropId, int qty)
        {
            int taken = 0;
            for (int v = 0; v < Art.Elements.Length && taken < qty; v++)
            {
                string k = cropId + ":" + v;
                if (!store.TryGetValue(k, out int have) || have <= 0) continue;
                int n = Mathf.Min(have, qty - taken);
                have -= n; taken += n;
                if (have <= 0) store.Remove(k); else store[k] = have;
            }
            return taken;
        }

        // ================================================================
        // islands and plots
        // ================================================================
        /// <summary>Make sure the state has a record for this island, locked, so tribute can be
        /// paid into it long before it opens.</summary>
        public Island EnsureIsland(int index)
        {
            while (islands.Count <= index) islands.Add(new Island(islands.Count, false));
            return islands[index];
        }

        /// <summary>Bring every island's plot list up to sixteen and open the free ones.
        ///
        /// The rule changed in the redesign and this method is where it lives: level no longer
        /// OPENS a plot, it makes the next one BUYABLE. The old behaviour handed out plots on
        /// level-up, which meant the coin sink the whole economy is balanced around did not
        /// exist, and levelling arrived with nothing to decide.</summary>
        public void SyncPlots()
        {
            for (int i = 0; i < IslandSys.Max; i++)
            {
                // Every island gets its sixteen plot records whether or not it is unlocked. A
                // locked island is drawn on the same canvas as the rest — that is the whole point
                // of the shared map — so it needs tiles to draw, and giving it an empty list just
                // means every render path has to special-case it.
                var isl = EnsureIsland(i);
                while (isl.plots.Count < IslandSys.PlotsPerIsland) isl.plots.Add(new Plot());
                if (i > 0 && !isl.unlocked) continue;
                for (int k = 0; k < isl.plots.Count; k++)
                    if (IslandSys.StartsUnlocked(i, k)) isl.plots[k].locked = false;
            }
        }

        public int PlotPriceNow(int island)
        {
            return IslandSys.PlotPrice(island, IslandSys.OpenCount(EnsureIsland(island)));
        }

        public int PlotLevelNow(int island)
        {
            return IslandSys.PlotLevel(island, IslandSys.OpenCount(EnsureIsland(island)));
        }

        public bool PlotBuyable(int island, out int price, out int needLv)
        {
            var isl = EnsureIsland(island);
            int open = IslandSys.OpenCount(isl);
            price = IslandSys.PlotPrice(island, open);
            needLv = IslandSys.PlotLevel(island, open);
            return open < IslandSys.PlotsPerIsland && lv >= needLv && coin >= price;
        }

        /// <summary>Buy the plot the player tapped, at the price the ladder is currently at.</summary>
        public bool BuyPlot(int island, int slot)
        {
            var isl = EnsureIsland(island);
            if (slot < 0 || slot >= isl.plots.Count || !isl.plots[slot].locked) return false;
            if (!PlotBuyable(island, out int price, out _)) return false;
            AddCoin(-price);
            isl.plots[slot].locked = false;
            return true;
        }

        /// <summary>Kept for the debug menu and the old free-plot rewards, which hand over a plot
        /// without charging for it.</summary>
        public bool UnlockExtraPlot(int want = -1)
        {
            var home = islands[0];
            if (want >= 0 && want < home.plots.Count && home.plots[want].locked)
            {
                home.plots[want].locked = false;
                return true;
            }
            foreach (int i in IslandSys.FreeOrder)
                if (i < home.plots.Count && home.plots[i].locked) { home.plots[i].locked = false; return true; }
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

            BumpContracts(type, null, amount);
        }

        public void TrackCrop(string type, string cropId, int amount)
        {
            Track(type, amount);
            foreach (var ch in GameData.Chapters)
                foreach (var t in ch.tasks)
                    if (t.type == type && t.crop == cropId) Bump(missions, t, amount);

            // Track() already credited the crop-agnostic contracts; this adds the named ones.
            for (int i = 0; i < contracts.Count; i++)
            {
                var m = contracts[i];
                if (m.Empty || m.claimed || m.type != type || m.cropId != cropId) continue;
                m.p = Mathf.Min(m.need, m.p + amount);
            }
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

            // Chapter rewards are quoted in UNIT and therefore recomputed; dailies keep their
            // authored numbers, because they are meant to stay a small, steady trickle.
            if (isDaily) { AddCoin(t.coin); AddXp(t.xp); }
            else
            {
                int idx = IndexInChapter(t);
                AddCoin(MissionSys.ChapterCoin(this, idx));
                AddXp(MissionSys.ChapterXp(this, idx));
            }
            return true;
        }

        static int IndexInChapter(Task t)
        {
            foreach (var ch in GameData.Chapters)
                for (int i = 0; i < ch.tasks.Length; i++)
                    if (ch.tasks[i] == t) return i;
            return 0;
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
        // contracts
        // ================================================================
        /// <summary>Bring the board up to date: retire anything that expired, fill anything that
        /// is due, and never do more than one round of either.
        ///
        /// The cap is the point. A player away for thirty days must come back to a full board and
        /// one set of rewards, not thirty cycles of back-pay — offline time is not supposed to be
        /// a way to farm contracts. Empty slots do refill instantly on open, because a board that
        /// is empty when you arrive gives you nothing to do with the session you just started.</summary>
        public void SyncContracts()
        {
            int want = MissionSys.SlotsFor(lv);
            while (contracts.Count < want) contracts.Add(new MissionRec { slot = contracts.Count });
            while (contracts.Count > want) contracts.RemoveAt(contracts.Count - 1);

            long now = GS.Now;
            for (int i = 0; i < contracts.Count; i++)
            {
                var m = contracts[i];
                m.slot = i;

                // expired unclaimed: the streak is the price of letting one lapse
                if (!m.Empty && !m.claimed && now >= m.expiresAt)
                {
                    streak = 0;
                    Clear(m, now);
                }

                if (m.Empty && now >= m.refillAt && contractsToday < MissionSys.DailyCap)
                {
                    var fresh = MissionSys.Generate(this, i, MissionSys.CycleOf(now));
                    contracts[i] = fresh;
                    sinceDiamond = fresh.grade == Grade.Diamond ? 0 : sinceDiamond + 1;
                }
            }
        }

        static void Clear(MissionRec m, long now)
        {
            m.type = null; m.cropId = null; m.grade = Grade.None;
            m.need = 0; m.p = 0; m.claimed = false; m.expiresAt = 0;
            m.refillAt = now + MissionSys.RefillMs;
        }

        /// <summary>Credit progress toward every live contract that matches.</summary>
        void BumpContracts(string type, string cropId, int amount)
        {
            for (int i = 0; i < contracts.Count; i++)
            {
                var m = contracts[i];
                if (m.Empty || m.claimed || m.type != type) continue;
                if (m.cropId != null && m.cropId != cropId) continue;
                m.p = Mathf.Min(m.need, m.p + amount);
            }
        }

        public bool ClaimContract(int slot)
        {
            if (slot < 0 || slot >= contracts.Count) return false;
            var m = contracts[slot];
            if (m.Empty || m.claimed || !m.Done) return false;

            AddCoin(MissionSys.CoinReward(this, m));
            AddXp(MissionSys.XpReward(this, m));
            AddEnergy(MissionSys.EnergyReward(m));

            m.claimed = true;
            streak = Mathf.Min(MissionSys.StreakCap * 10, streak + 1);
            contractsToday++;
            Clear(m, GS.Now);
            return true;
        }

        // ================================================================
        // daily reset
        // ================================================================
        public void CheckDay()
        {
            long d = (GS.Now + clock.resetOffsetMinutes * 60000L) / 86400000L;
            if (day != d)
            {
                day = d;
                daily.Clear();
                visited.Clear();
                stealLeft = 20;
                contractsToday = 0;
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
            createdAt = GS.Now;
            islands.Clear(); islands.Add(new Island(0, true));
            seeds.Clear(); store.Clear(); missions.Clear(); daily.Clear();
            collected.Clear(); claimedSets.Clear(); claimedMs.Clear(); shopBought.Clear(); visited.Clear();
            chests = new int[4]; stats = new Stats(); stealLeft = 20; buffMutateUntil = 0;
            forecastUntil = 0; greenhouse = 0;
            contracts.Clear(); streak = 0; sinceDiamond = 0; contractsToday = 0;
            sinceLegendary = 0;
            SeedStarter();
            SyncPlots();
        }

        /// <summary>Give this farm a world seed if it has none — a save written before the field
        /// existed, or a hand-edited one. Zero is reserved as "unset", so weather and crop tags
        /// can never silently fall back to a shared schedule that every player would see.</summary>
        public void EnsureWorldSeed()
        {
            if (worldSeed == 0) worldSeed = NewWorldSeed();
            if (createdAt == 0) createdAt = GS.Now;
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
