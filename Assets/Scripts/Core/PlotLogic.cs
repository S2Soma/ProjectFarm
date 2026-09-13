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
