using UnityEngine;

namespace LQFarm
{
    /// <summary>Timed, repeatable watering.
    ///
    /// While a crop grows, watering windows open at fixed points. Each window watered takes a
    /// slice off the remaining time; the slices always add to the same 20% no matter how many
    /// windows a crop has, so a long crop is not worth more per tap than a short one.
    ///
    /// Derived from the owner's example — a 300 s crop, a window roughly every 90 s, 20 s off
    /// each — with one correction. Those numbers are RATIOS, not absolutes: a fixed 90 s interval
    /// gives carrot (40 s, and as little as 22 s at high level) no windows at all, which is most
    /// of the early game. And at the example's period of 0.30·dur the third window opens at
    /// 0.90·dur, by which point a player who watered the first two is already at 0.867·dur and
    /// harvesting — the third window would be dead code for anyone playing well. A period of
    /// dur/(W+1) fixes that and reproduces every other number in the example exactly:
    /// at dur = 300 the windows land at 75 / 150 / 225 s, each cutting 20 s, 60 s total.
    ///
    /// The anti-accumulation property is structural, not defensive. A window is open only during
    /// its own slice of WALL-CLOCK time since planting — <see cref="Nominal"/> deliberately
    /// ignores <see cref="Plot.cut"/>. Close the app for six hours and every unconsumed window is
    /// simply past: there is no catch-up pass to write, because there is nothing to catch up.</summary>
    public static class WaterSys
    {
        /// <summary>Total reduction available per crop, split evenly across its windows.
        /// This is also the floor on grow time: a plot can never finish faster than 0.80·dur
        /// however many people water it, which is what stops a player with fifty friends from
        /// having instant crops.</summary>
        public const float TotalCut = 0.20f;

        /// <summary>Below this, three windows are cramped enough to be annoying rather than
        /// engaging — at dur = 60 the last window is only usable for about seven seconds — so
        /// short crops get a single, comfortable one instead.</summary>
        const float MultiWindowMinDur = 90f;

        public const int MaxWindows = 3;

        public static int WindowsFor(float dur) { return dur < MultiWindowMinDur ? 1 : MaxWindows; }

        /// <summary>Windows are evenly spaced with a gap left after the last one, so the final
        /// window still opens before an attentive player's crop is ripe.</summary>
        public static float Period(float dur, int windows) { return dur / (windows + 1); }

        /// <summary>How long a window stays open. The 8 s floor keeps a fast crop from demanding
        /// reflexes.</summary>
        public static float OpenFor(float dur) { return Mathf.Max(8f, dur * 0.15f); }

        public static float CutPerWindow(float dur, int windows)
        {
            return windows <= 0 ? 0f : dur * TotalCut / windows;
        }

        /// <summary>Seconds since planting on the WALL clock — never reduced by watering.
        /// Window timing has to be independent of the reward it grants, or watering early would
        /// pull the next window toward the player and the 20% cap would leak.</summary>
        public static float Nominal(Plot p)
        {
            return p == null || p.plantedAt <= 0 ? 0f : (float)((GS.Now - p.plantedAt) / 1000.0);
        }

        public static int Windows(Plot p)
        {
            // windowCount is 0 on a plot planted before this system existed
            return p.windowCount > 0 ? p.windowCount : WindowsFor(p.dur);
        }

        static bool Consumed(Plot p, int k) { return (p.waterMask & (1 << (k - 1))) != 0; }

        /// <summary>True when a window is open and unused right now.</summary>
        public static bool WindowOpen(Plot p, out int window)
        {
            window = 0;
            if (p == null || string.IsNullOrEmpty(p.crop)) return false;

            int w = Windows(p);
            float tn = Nominal(p), period = Period(p.dur, w), open = OpenFor(p.dur);
            for (int k = 1; k <= w; k++)
            {
                if (Consumed(p, k)) continue;
                float start = k * period;
                if (tn >= start && tn < start + open) { window = k; return true; }
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
                if (tn < start) return start - tn;
            }
            return -1f;
        }

        /// <summary>Windows still unused AND not yet past — what the plot popup counts.</summary>
        public static int Remaining(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return 0;
            int w = Windows(p), n = 0;
            float tn = Nominal(p), period = Period(p.dur, w), open = OpenFor(p.dur);
            for (int k = 1; k <= w; k++)
                if (!Consumed(p, k) && tn < k * period + open) n++;
            return n;
        }

        /// <summary>The next window that has not been used yet, ignoring whether it is open —
        /// 0 when all of them are spent. This is what a "Bình tưới vàng" buys: it brings the next
        /// window forward rather than granting extra time, so the 20% cap still holds and the
        /// item cannot outperform simply being present.</summary>
        public static int NextUnusedWindow(Plot p)
        {
            if (p == null || string.IsNullOrEmpty(p.crop)) return 0;
            int w = Windows(p);
            for (int k = 1; k <= w; k++)
                if (!Consumed(p, k)) return k;
            return 0;
        }

        /// <summary>Consume window <paramref name="window"/>. Returns the seconds removed.</summary>
        public static float Consume(Plot p, int window, bool byVisitor)
        {
            int w = Windows(p);
            p.waterMask |= (byte)(1 << (window - 1));
            if (byVisitor) p.friendMask |= (byte)(1 << (window - 1));

            // Drought doubles what a watering takes off, and that is the number that makes the
            // worst weather survivable. Drought is x1.30 duration, but an engaged player waters
            // to 1.30 x 0.60 = 0.78 effective — against 0.80 in fair weather — while being paid
            // x1.15 to sell. So it is net POSITIVE for a player who taps and x1.30 punishing for
            // one who does not: the pain sits on inattention, never on presence.
            float add = CutPerWindow(p.dur, w) * Def(p).waterCut;
            // Belt and braces: the window bookkeeping already bounds this, but the cap is the
            // promise the whole design rests on, so it is enforced where the number is written.
            p.cut = Mathf.Min(p.cut + add, p.dur * CapFor(p));
            return add;
        }

        /// <summary>Percentage a single watering takes off, for the floating "-6.7%" label.</summary>
        public static int CutPercent(Plot p)
        {
            return Mathf.RoundToInt(TotalCut * 100f / Mathf.Max(1, Windows(p)) * Def(p).waterCut);
        }

        /// <summary>The weather this plot was planted under.</summary>
        static WeatherDef Def(Plot p)
        {
            return WeatherSys.Def((Weather)Mathf.Clamp(p.plantWeather, 0, WeatherSys.All.Length - 1));
        }

        /// <summary>Total reduction available to this plot, weather included. Drought's plots can
        /// reach 40%; everything else stays at 20%. The floor is still absolute — a plot can
        /// never finish faster than this no matter how many friends water it.</summary>
        public static float CapFor(Plot p) { return TotalCut * Def(p).waterCut; }
    }
}
