using UnityEngine;

namespace LQFarm
{
    /// <summary>What a plot IS, as opposed to how it is drawn.
    ///
    /// These were static methods on the view, which worked while there was exactly one view of
    /// exactly one field. They are pure functions of a <see cref="Plot"/>, so they belong beside
    /// the data: missions, the HUD and — shortly — a pooled view that may not exist for the
    /// island being asked about all need them without a view in hand.</summary>
    public static class PlotLogic
    {
        public static PlotState State(Plot p)
        {
            if (p == null || p.locked) return PlotState.Locked;
            if (string.IsNullOrEmpty(p.crop)) return PlotState.Empty;
            if (Elapsed(p) >= p.dur) return PlotState.Ready;
            return WaterSys.WindowOpen(p, out _) ? PlotState.Thirsty : PlotState.Growing;
        }

        /// <summary>Grow progress in seconds: wall-clock since planting plus everything watering
        /// has taken off. Note that <see cref="WaterSys.Nominal"/> is the same subtraction WITHOUT
        /// the cut — window timing must not move when the player waters.</summary>
        public static float Elapsed(Plot p) { return WaterSys.Nominal(p) + p.cut; }

        public static int Remain(Plot p) { return Mathf.Max(0, Mathf.CeilToInt(p.dur - Elapsed(p))); }

        // ============================================================
        // the economics of a plot — shared by the island view and the journey simulation
        // ============================================================
        /// <summary>Plant: take the seed from the actor, stamp everything situational on the plot.
        /// False (and nothing spent) when the plot is not an empty open bed or the seed is not in
        /// the bag. The view adds the drawing; <c>JourneyTest</c> plays thousands of these without
        /// one, which is only honest while both run exactly this code.</summary>
        /// <summary>A big crop needs a big plot and a small crop a small one.</summary>
        public static bool Fits(Seed seed, Plot p) { return seed != null && p != null && !p.none && seed.big == p.big; }

        public static bool Plant(PlayerState owner, PlayerState actor, Plot p, string seedId)
        {
            if (p == null || p.locked || !string.IsNullOrEmpty(p.crop)) return false;
            var seed = GameData.Get(seedId);
            // trees in big plots, everything else in small ones
            if (seed == null || !Fits(seed, p)) return false;
            // the seed leaves the actor's bag; everything else is a property of the land
            if (!actor.TakeSeed(seedId, 1)) return false;

            // Everything situational is resolved HERE and stamped on the plot. After this the
            // crop's value is fixed: the weather may turn and the tag set may rotate, and neither
            // can reach back and change what this planting is worth.
            var w = WeatherSys.Now(owner);

            // A greenhouse charge is spent only when the weather would actually hurt, and it is
            // spent HERE — before anything is stamped. The hour itself is kept: shelter floors
            // each axis in the player's favour rather than swapping the weather for Nắng, so a
            // sheltered planting during a storm still gets the storm's +60% mutation odds.
            bool sheltered = ShopSys.UseGreenhouse(owner, w);

            // Order matters: the tier is rolled first, because it stretches the duration, and the
            // windows are spaced across the duration.
            int variant = owner.RollVariantFor(seedId, w, sheltered);

            p.crop = seedId;
            p.plantedAt = GS.Now;
            p.variant = variant;
            p.dur = owner.GrowTimeIn(seed, w, variant, sheltered);
            p.cut = 0;
            p.waterMask = 0;
            p.friendMask = 0;
            p.windowCount = (byte)WaterSys.WindowsFor(seed);
            p.waterSec = seed.waterCut;
            p.plantWeather = (byte)w;
            owner.ResolveSituational(seedId, w, out p.sellMul, out p.xpMul, sheltered);

            // Rain waters the field for you: the first window is granted at planting. It saves
            // sixteen taps and is the one weather that rewards a player for simply being there.
            if (w == Weather.Rain)
            {
                int k = WaterSys.NextUnusedWindow(p);
                if (k > 0) WaterSys.Consume(p, k, byVisitor: false);
            }

            actor.TrackCrop("plant", seedId, 1);
            owner.AddEnergy(owner.EnergyGain(1));
            return true;
        }

        /// <summary>Water the open window. Returns the seconds it took off the grow time (0 is possible
        /// right at the floor), or -1 when there was nothing to water.</summary>
        public static int Water(PlayerState owner, PlayerState actor, Plot p, bool byVisitor)
        {
            if (p == null || State(p) != PlotState.Thirsty) return -1;
            if (!WaterSys.WindowOpen(p, out int window)) return -1;
            return Credit(owner, actor, WaterSys.Consume(p, window, byVisitor));
        }

        /// <summary>Water the next unused window without waiting for it to open — what the golden
        /// watering can does to every plot. It still spends a real window, so the floor is untouched:
        /// the item buys convenience, never extra growth. Seconds taken off, or -1.</summary>
        public static int WaterAhead(PlayerState owner, PlayerState actor, Plot p, bool byVisitor)
        {
            if (p == null || p.locked || string.IsNullOrEmpty(p.crop) || State(p) == PlotState.Ready) return -1;
            int k = WaterSys.NextUnusedWindow(p);
            if (k == 0) return -1;
            return Credit(owner, actor, WaterSys.Consume(p, k, byVisitor));
        }

        static int Credit(PlayerState owner, PlayerState actor, float seconds)
        {
            actor.Track("water", 1);
            owner.AddEnergy(owner.EnergyGain(2));
            return Mathf.RoundToInt(seconds);
        }

        public struct HarvestOutcome
        {
            public Seed seed;
            public int variant, xp, fruits, bonusCoins;
            public float sellMul;
            public Weather weather;
            public CropTag tag;
        }

        /// <summary>Harvest a ripe plot: produce, XP, energy and the on-the-spot bonus to the owner,
        /// mission credit to the actor, and the plot cleared.</summary>
        public static bool Harvest(PlayerState owner, PlayerState actor, Plot p, out HarvestOutcome r)
        {
            r = default;
            if (p == null || State(p) != PlotState.Ready) return false;
            var seed = GameData.Get(p.crop);
            if (seed == null) return false;
            int v = p.variant;

            // harvesting is owner-only (a visitor gets Steal, not Harvest), so the goods and the
            // xp go to the owner; only the mission credit follows the actor
            int xp = Mathf.RoundToInt(owner.XpFor(seed, v) * Mathf.Max(0.01f, p.xpMul));
            if (owner.buffXpUntil > GS.Now) xp *= 2;          // Bùa kinh nghiệm, read at harvest
            int fruits = owner.YieldOf(seed, v);
            owner.AddProduce(p.crop, v, fruits);
            owner.AddXp(xp);
            owner.AddEnergy(owner.EnergyFor(seed, v));
            actor.TrackCrop("harvest", p.crop, 1);
            if (v > 0) actor.Track("mutate", 1);

            int bonus = Mathf.RoundToInt(owner.HarvestValue(p.crop, v) * (Mathf.Max(1f, p.sellMul) - 1f));
            if (bonus > 0) owner.AddCoin(bonus);

            r = new HarvestOutcome
            {
                seed = seed, variant = v, xp = xp, fruits = fruits, bonusCoins = bonus, sellMul = p.sellMul,
                weather = (Weather)Mathf.Clamp(p.plantWeather, 0, WeatherSys.All.Length - 1),
                tag = TagSys.TagOf(owner, p.crop, p.plantedAt),
            };

            p.crop = null; p.plantedAt = 0; p.variant = 0;
            p.cut = 0; p.waterMask = 0; p.friendMask = 0; p.windowCount = 0; p.waterSec = 0;
            p.plantWeather = 0; p.sellMul = 1f; p.xpMul = 1f;
            return true;
        }

        public static int StageOf(Plot p)
        {
            float t = Elapsed(p) / Mathf.Max(0.001f, p.dur);
            if (t >= 1f)    return 3;
            if (t >= 0.62f) return 2;
            if (t >= 0.28f) return 1;
            return 0;
        }
    }
}
