using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>The reference player the economy is balanced for, shared by the suites that measure
    /// "a day of play" (<see cref="JourneyTest"/>, <see cref="IslandTest"/>).
    ///
    /// Crops run from two minutes to a day, so "hours of continuous play" no longer describes anyone.
    /// The reference player opens the game four times a day — before school or work, at lunch, after,
    /// and in the evening — for 85 minutes in all, and sleeps ten and a half hours between the last
    /// session and the first. The first day is a little longer: a new player explores.</summary>
    public static class EconomyModel
    {
        /// <summary>(start hour of day, minutes) for every session of a normal day.</summary>
        public static readonly (float hour, float minutes)[] Day =
        {
            (7.0f, 20f), (12.0f, 15f), (17.5f, 20f), (20.5f, 30f),
        };

        /// <summary>Day one: the same hours, longer sessions (115 minutes).</summary>
        public static readonly (float hour, float minutes)[] FirstDay =
        {
            (7.0f, 40f), (12.0f, 20f), (17.5f, 25f), (20.5f, 30f),
        };

        public static (float hour, float minutes)[] Sessions(int day) { return day == 0 ? FirstDay : Day; }

        /// <summary>Start and end of session <paramref name="i"/> of day <paramref name="day"/>, in seconds
        /// since midnight of day 0.</summary>
        public static void Window(int day, int i, out double start, out double end)
        {
            var s = Sessions(day)[i];
            start = day * 86400.0 + s.hour * 3600.0;
            end = start + s.minutes * 60.0;
        }

        /// <summary>When a crop that ripens at <paramref name="ripeAt"/> is actually harvested: at once if
        /// the player is in the game, otherwise at the start of the next session.</summary>
        public static double HarvestTime(double ripeAt)
        {
            int day = Mathf.Max(0, (int)(ripeAt / 86400.0));
            for (int d = day; d < day + 3; d++)
                for (int i = 0; i < Sessions(d).Length; i++)
                {
                    Window(d, i, out double a, out double b);
                    if (ripeAt <= b) return ripeAt < a ? a : ripeAt;
                }
            return ripeAt;
        }

        /// <summary>Harvests one plot gives this player per day with a crop of <paramref name="growSec"/>
        /// (in fair weather, watering whatever window opens while they are in the game): replanted the
        /// moment it is harvested, averaged over a week of normal days.</summary>
        public static float HarvestsPerDay(Seed seed, float growSec)
        {
            int n = Mathf.Max(1, seed.waters);
            double t = 86400.0 + Day[0].hour * 3600.0, end = t + 7 * 86400.0;
            int harvests = 0;
            for (int guard = 0; guard < 100000 && t < end; guard++)
            {
                // watered windows: those the player is in the game for before the next one opens
                double cut = 0, period = growSec / (n + 1);
                for (int k = 1; k <= n; k++)
                {
                    double open = t + k * period;
                    if (HarvestTime(open) < open + period) cut += seed.waterCut;
                }
                double ripe = t + growSec - Mathf.Min((float)cut, growSec * 0.5f);
                t = HarvestTime(ripe);
                if (t < end) harvests++;
            }
            return harvests / 7f;
        }
    }
}
