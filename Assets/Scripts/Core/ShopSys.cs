using UnityEngine;

namespace LQFarm
{
    /// <summary>Prices for the goods shelf, and the two pieces of state its new items need.
    ///
    /// **Every price is quoted in UNIT** — the coin margin of the best crop the player can grow —
    /// rather than in absolute coins. The old shelf was a flat table: 2.600 for a watering can is
    /// four hours of work at level 3 and ninety seconds of work at level 25, so the shop was
    /// unaffordable, then briefly interesting, then irrelevant, and it was irrelevant for most of
    /// the game. Pricing in UNIT means "this costs about twelve harvests" stays true at every
    /// level, which is the only way a consumable shelf keeps mattering.
    ///
    /// It also means the tables never need rebalancing when a crop's numbers change — UNIT moves
    /// with them.</summary>
    public static class ShopSys
    {
        /// <summary>How many UNITs each item costs. These are the only balance numbers on the
        /// shelf; everything else is derived.</summary>
        public static float UnitCost(string effect)
        {
            switch (effect)
            {
                case "water3":   return 6f;    // saves taps, not time — cheap on purpose
                case "seedbag":  return 5f;
                case "energy":   return 12f;
                case "instant":  return 10f;   // one crop's worth of time, priced above one crop
                case "mutate":   return 18f;
                case "forecast": return 14f;
                case "reroll":   return 8f;
                case "green":    return 30f;   // the most expensive thing on the shelf
                default:         return 10f;
            }
        }

        public static int PriceOf(PlayerState s, ShopItem it)
        {
            if (it.effect == null) return it.price;           // cosmetics keep their fixed tag
            return Round(MissionSys.Unit(s) * UnitCost(it.effect));
        }

        /// <summary>Prices a player can hold in their head. UNIT x 18 gives numbers like 21.474.</summary>
        static int Round(float v)
        {
            if (v < 500f) return Mathf.Max(10, Mathf.RoundToInt(v / 10f) * 10);
            if (v < 5000f) return Mathf.RoundToInt(v / 50f) * 50;
            if (v < 50_000f) return Mathf.RoundToInt(v / 500f) * 500;
            return Mathf.RoundToInt(v / 5000f) * 5000;
        }

        // ============================================================
        // Dự Báo Thời Tiết
        // ============================================================
        /// <summary>Twelve hours of forecast.
        ///
        /// Weather normally reveals only in the last ten minutes of the hour, which makes it
        /// something to react to. A forecast turns it into something to PLAN around — hold the
        /// legendary-odds planting until the Snow hour, dump the cheap crop into the storm — and
        /// that is a real decision rather than a stat boost, which is why it is on the shelf.</summary>
        public const long ForecastMs = 12L * 3_600_000L;

        public static bool ForecastActive(PlayerState s) { return s.forecastUntil > GS.Now; }

        /// <summary>Hours ahead the player can currently see. One normally (and only near the
        /// turn of the hour), twelve while a forecast is running.</summary>
        public static int ForecastHours(PlayerState s)
        {
            if (ForecastActive(s)) return (int)((s.forecastUntil - GS.Now) / 3_600_000L) + 1;
            return WeatherSys.NextRevealed(GS.Now) ? 1 : 0;
        }

        // ============================================================
        // Nhà Kính
        // ============================================================
        /// <summary>Plantings protected from bad weather.
        ///
        /// Six, not sixteen, and counted in PLANTINGS rather than in plots or in minutes. Six is
        /// large enough to cover the plots a player actually cares about during one bad hour and
        /// small enough that they have to choose WHICH — a duration would have protected whatever
        /// happened to be in the ground, which is not a decision.</summary>
        public const int GreenhouseCharges = 6;

        /// <summary>Spend a charge if the weather would otherwise hurt this planting. Returns
        /// true when the crop goes in protected.
        ///
        /// Deliberately does not fire in good weather: spending a charge to be shielded from Sun
        /// would be the game quietly wasting something the player paid for.</summary>
        public static bool UseGreenhouse(PlayerState s, Weather w)
        {
            if (s.greenhouse <= 0) return false;
            if (!WeatherSys.IsHarmful(w)) return false;
            s.greenhouse--;
            return true;
        }
    }
}
