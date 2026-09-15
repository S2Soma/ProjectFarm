using UnityEngine;

namespace LQFarm
{
    /// <summary>Timed, repeatable watering — a fixed number of waterings per crop, each worth a fixed time.
    ///
    /// **The rule as the player sees it (2026-09-15):** every crop gets thirsty a set number of times
    /// while it grows (<see cref="Seed.waters"/>: a carrot once, a pumpkin three times, an avocado four),
    /// and each watering makes it ripen a set time sooner (<see cref="Seed.waterCut"/>: "chín sớm 16p").
    /// Not watering is allowed — the crop still grows, just slower — so nobody is ever stuck, and
    /// being away costs nothing but the bonus. It replaced "20% split across the windows", which
    /// read as a percentage nobody could turn into minutes and paid a long crop a whole hour per tap.
    ///
    /// **Windows open at evenly spaced wall-clock points**, dur/(N+1) apart, and a window, once open,
    /// stays open until the next one opens (the last until the crop is ripe). So at most ONE window is
    /// ever open and there is no backlog. A window is open only during its own slice of WALL-CLOCK time
    /// since planting — <see cref="Nominal"/> deliberately ignores <see cref="Plot.cut"/> — so closing
    /// the app for six hours simply lets every unconsumed window pass: there is no catch-up pass to
    /// write, because there is nothing to catch up. (Rain is the one exception, and it is by design:
    /// <see cref="RainWater"/>.)
    ///
    /// **The floor.** A watering can never make the crop ripen before the next window has opened (and
    /// stayed open a little) — otherwise that window would be dead for exactly the attentive player
    /// who earned it — and all the waterings together never take off more than the crop's own N × time
    /// (doubled in drought) or half its duration. Both limits are enforced where the number is
    /// written (<see cref="Consume"/>), so no visitor, pet, rain or golden can can get past them.</summary>
    public static class WaterSys
    {
        /// <summary>A plot can never finish faster than half its duration, however it is watered.</summary>
        public const float MaxShare = 0.5f;

        /// <summary>The share of duration a plot planted under the OLD rule (<c>Plot.waterSec</c> = 0)
        /// still gets from all its windows together.</summary>
        public const float LegacyShare = 0.20f;

        /// <summary>Most waterings a crop can have. The masks are bytes, so eight would fit; four is
        /// what a 24-hour crop needs to be worth checking on through a day.</summary>
        public const int MaxWindows = 4;

        /// <summary>How much of a period the next window is guaranteed to stay open after the watering
        /// before it, at the very least.</summary>
        const float OpenMargin = 0.05f;

        public static int WindowsFor(Seed s) { return s == null ? 1 : Mathf.Clamp(s.waters, 1, MaxWindows); }

        /// <summary>Windows for a plot planted before per-crop watering and before windows were
        /// stored at all (windowCount 0): the rule of that time.</summary>
        static int LegacyWindowsFor(float dur) { return dur < 90f ? 1 : 3; }

        /// <summary>Windows are evenly spaced with a gap left after the last one, so the final
        /// window still opens before an attentive player's crop is ripe.</summary>
        public static float Period(float dur, int windows) { return dur / (windows + 1); }

        /// <summary>Wall-clock second (since planting) at which window <paramref name="k"/> closes:
        /// when window k+1 opens, or for the last window when the crop ripens.</summary>
        public static float WindowEnd(Plot p, int k)
        {
            int w = Windows(p);
            float period = Period(p.dur, w);
            return k < w ? (k + 1) * period : p.dur - p.cut;
        }

        /// <summary>Seconds since planting on the WALL clock — never reduced by watering.
        /// Window timing has to be independent of the reward it grants, or watering early would
        /// pull the next window toward the player and the floor would leak.</summary>
        public static float Nominal(Plot p)
        {
            return p == null || p.plantedAt <= 0 ? 0f : (float)((GS.Now - p.plantedAt) / 1000.0);
        }

        public static int Windows(Plot p)
        {
            return p.windowCount > 0 ? Mathf.Min(p.windowCount, 8) : LegacyWindowsFor(p.dur);
        }

        /// <summary>Seconds one watering of this plot takes off in fair weather: the crop's own time,
        /// frozen at plant — or, for a plot planted under the old rule, 20% of its duration split
        /// across its windows, so a crop already in the ground finishes the way it started.</summary>
        public static float BaseCut(Plot p)
        {
            if (p == null) return 0f;
            if (p.waterSec > 0f) return p.waterSec;
            return Mathf.Max(0f, p.dur) * LegacyShare / Mathf.Max(1, Windows(p));
        }

        /// <summary>The most all of this plot's waterings may take off together: its windows × its time
        /// (doubled in drought), and never more than half the duration.</summary>
        public static float Cap(Plot p)
        {
            return Mathf.Max(0f, Mathf.Min(Windows(p) * BaseCut(p) * Def(p).waterCut, p.dur * MaxShare));
        }

        /// <summary>What one watering would take off before the floor: the crop's time × this plot's
        /// drought factor.</summary>
        public static float FullCut(Plot p) { return BaseCut(p) * Def(p).waterCut; }

        /// <summary>The highest <see cref="Plot.cut"/> may reach once <paramref name="window"/> is
        /// watered at wall-clock second <paramref name="at"/>: low enough that the next window still
        /// to come opens (and stays open a little) before the crop is ripe, and never past the cap.</summary>
        static float Ceiling(Plot p, int window, float at)
        {
            float cap = Cap(p);
            int w = Windows(p);
            float period = Period(p.dur, w);
            for (int j = 1; j <= w; j++)
            {
                if (j == window || Consumed(p, j)) continue;
                float open = j * period;
                if (open > at) return Mathf.Min(cap, p.dur - open - period * OpenMargin);
                // already open and still unused: do not ripen the crop out from under it
                if (at < WindowEnd(p, j)) return Mathf.Min(cap, p.dur - at - period * OpenMargin);
            }
            return cap;
        }

        static bool Consumed(Plot p, int k) { return k >= 1 && k <= 8 && (p.waterMask & (1 << (k - 1))) != 0; }

        /// <summary>True when a window is open and unused right now.</summary>
        public static bool WindowOpen(Plot p, out int window)
        {
            window = 0;
            if (p == null || string.IsNullOrEmpty(p.crop)) return false;

            int w = Windows(p);
            float tn = Nominal(p), period = Period(p.dur, w);
            for (int k = 1; k <= w; k++)
            {
                if (Consumed(p, k)) continue;
                float start = k * period;
                if (tn >= start && tn < WindowEnd(p, k)) { window = k; return true; }
            }
            return false;
        }

        /// <summary>Seconds until the next window opens, or -1 when none is still ahead.
        /// Returns 0 while one is open.</summary>
        public static float NextWindowIn(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return -1f;
            if (WindowOpen(p, out _)) return 0f;

            int w = Windows(p);
            float tn = Nominal(p), period = Period(p.dur, w);
            for (int k = 1; k <= w; k++)
            {
                if (Consumed(p, k)) continue;
                float start = k * period;
                if (tn < start && start < p.dur - p.cut) return start - tn;
            }
            return -1f;
        }

        /// <summary>Windows still unused AND not yet past — what the plot popup counts.</summary>
        public static int Remaining(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return 0;
            int w = Windows(p), n = 0;
            float tn = Nominal(p), period = Period(p.dur, w);
            for (int k = 1; k <= w; k++)
                if (!Consumed(p, k) && tn < WindowEnd(p, k) && k * period < p.dur - p.cut) n++;
            return n;
        }

        /// <summary>The next window that has not been used yet, ignoring whether it is open —
        /// 0 when all of them are spent. This is what a "Bình tưới vàng" buys: it brings the next
        /// window forward rather than granting extra time, so the floor still holds and the
        /// item cannot outperform simply being present.</summary>
        public static int NextUnusedWindow(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return 0;
            int w = Windows(p);
            for (int k = 1; k <= w; k++)
                if (!Consumed(p, k)) return k;
            return 0;
        }

        /// <summary>Seconds watering <paramref name="window"/> would take off right now, floor included —
        /// the number the popup and the floating label show.</summary>
        public static float CutFor(Plot p, int window)
        {
            if (p == null || window <= 0) return 0f;
            float add = FullCut(p);
            return Mathf.Max(0f, Mathf.Min(add, Ceiling(p, window, Nominal(p)) - p.cut));
        }

        /// <summary>What the next watering (the open window, else the next unused one) would take off now.</summary>
        public static float CutNow(Plot p)
        {
            if (p == null) return 0f;
            int k = WindowOpen(p, out int open) ? open : NextUnusedWindow(p);
            return CutFor(p, k);
        }

        /// <summary>Consume window <paramref name="window"/> at wall-clock second <paramref name="at"/>
        /// (now, when negative). Returns the seconds actually removed.
        ///
        /// Drought doubles what a watering takes off, and that is the number that makes the worst
        /// weather survivable: slow to grow, but a player who is there to water gets twice the time
        /// back. The pain sits on inattention, never on presence.</summary>
        public static float Consume(Plot p, int window, bool byVisitor, float at = -1f)
        {
            if (p == null || window < 1 || window > 8) return 0f;
            if (at < 0f) at = Nominal(p);
            float add = Mathf.Max(0f, Mathf.Min(FullCut(p), Ceiling(p, window, at) - p.cut));
            p.waterMask |= (byte)(1 << (window - 1));
            if (byVisitor) p.friendMask |= (byte)(1 << (window - 1));
            p.cut = Mathf.Clamp(p.cut + add, 0f, Mathf.Max(0f, p.dur * MaxShare));
            return add;
        }

        /// <summary>Rain and storm water the field themselves: a window that opens while it is raining
        /// or storming is consumed on the spot, with no tap. Returns how many windows it took.
        ///
        /// The weather that counts is the weather in the slot the window OPENED in — a pure
        /// function of the world seed — so the same rule holds whether the game was open or not:
        /// with <paramref name="catchUp"/> every window that already opened is checked (on load),
        /// without it only the one open right now (the game's one-second tick). A caught-up window
        /// is watered as of the moment it opened, so the floor is the same as if the game had been open.
        ///
        /// It takes the window like a watering would — the same time, the same floor — but earns
        /// no mission credit and no energy: nobody did anything.</summary>
        public static int RainWater(PlayerState owner, Plot p, bool catchUp)
        {
            if (owner == null || p == null || p.locked || string.IsNullOrEmpty(p.crop) || p.plantedAt <= 0) return 0;
            if (PlotLogic.Elapsed(p) >= p.dur) return 0;
            int w = Windows(p);
            float tn = Nominal(p), period = Period(p.dur, w);
            int n = 0;
            for (int k = 1; k <= w; k++)
            {
                if (Consumed(p, k)) continue;
                float start = k * period;
                if (tn < start) break;
                if (!catchUp && tn >= WindowEnd(p, k)) continue;
                long openedAt = p.plantedAt + (long)(start * 1000.0);
                var weather = WeatherSys.At(owner, WeatherSys.SlotIndex(openedAt));
                if (weather != Weather.Rain && weather != Weather.Storm) continue;
                Consume(p, k, byVisitor: false, at: catchUp ? start : tn);
                n++;
            }
            return n;
        }

        /// <summary>The weather this plot was planted under.</summary>
        static WeatherDef Def(Plot p)
        {
            return WeatherSys.Def((Weather)Mathf.Clamp(p.plantWeather, 0, WeatherSys.All.Length - 1));
        }
    }
}
