using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the watering-window arithmetic across every grow time the game can produce.
    ///
    /// The formula has one property that is easy to assert and impossible to eyeball: the LAST
    /// window has to still be open when an attentive player reaches it, and that player arrives
    /// earlier than a lazy one precisely because they watered the earlier windows. Get the period
    /// slightly wrong and the final window becomes unreachable — dead code that no screenshot
    /// would ever reveal.</summary>
    public static class WaterSysTest
    {
        /// <summary>Grow times the game can actually produce: the shortest clamp (6 s) through a
        /// legendary late crop (470 s base, +120%).</summary>
        static readonly float[] Durations =
        {
            6, 10, 22, 40, 54, 59, 60, 61, 79, 89, 90, 91, 105, 150, 200, 268,
            300, 330, 400, 470, 600, 800, 1034, 1200
        };

        [MenuItem("Tools/LQ Farm/Kiểm tra tưới nước")]
        public static void Run()
        {
            var fails = new List<string>();

            foreach (float dur in Durations)
            {
                TotalCutIsExact(fails, dur);
                WindowsDoNotOverlap(fails, dur);
                LastWindowIsReachable(fails, dur);
                CapHolds(fails, dur);
            }
            OfflineBanksNothing(fails);
            VisitorIsRecorded(fails);

            if (fails.Count == 0)
                Debug.Log($"Tưới nước OK — {Durations.Length} thời gian trồng, mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Tưới nước: " + f);
                Debug.LogError($"Tưới nước: {fails.Count} lỗi.");
            }
        }

        static Plot Make(float dur, float elapsedSeconds)
        {
            return new Plot
            {
                locked = false,
                crop = "wheat",
                dur = dur,
                windowCount = (byte)WaterSys.WindowsFor(dur),
                plantedAt = GS.Now - (long)(elapsedSeconds * 1000f),
            };
        }

        // ------------------------------------------------------------
        /// <summary>Watering every window must always give back exactly 20%, whether the crop has
        /// one window or three — otherwise a long crop would pay more per tap than a short one.</summary>
        static void TotalCutIsExact(List<string> fails, float dur)
        {
            var p = Make(dur, 0);
            int w = WaterSys.Windows(p);
            for (int k = 1; k <= w; k++) WaterSys.Consume(p, k, false);

            float want = dur * WaterSys.TotalCut;
            if (Mathf.Abs(p.cut - want) > 0.01f)
                fails.Add($"dur={dur}: tổng giảm {p.cut:0.##}s, kỳ vọng {want:0.##}s");
        }

        static void WindowsDoNotOverlap(List<string> fails, float dur)
        {
            var p = Make(dur, 0);
            int w = WaterSys.Windows(p);
            float period = WaterSys.Period(dur, w), open = WaterSys.OpenFor(dur);
            for (int k = 1; k < w; k++)
                if (k * period + open > (k + 1) * period + 0.001f)
                    fails.Add($"dur={dur}: lượt {k} và {k + 1} chồng nhau");
        }

        /// <summary>Walk the crop forward the way an attentive player does — watering each window
        /// the instant it opens — and check the crop is still growing when the last one arrives.</summary>
        static void LastWindowIsReachable(List<string> fails, float dur)
        {
            var p = Make(dur, 0);
            int w = WaterSys.Windows(p);
            float period = WaterSys.Period(dur, w);

            for (int k = 1; k <= w; k++)
            {
                float openAt = k * period;
                // ripe time under the cut accumulated so far
                float ripeAt = dur - p.cut;
                if (openAt >= ripeAt)
                {
                    fails.Add($"dur={dur}: lượt {k} mở ở {openAt:0.#}s nhưng cây đã chín ở {ripeAt:0.#}s — lượt chết");
                    return;
                }

                var at = Make(dur, openAt + 0.01f);
                at.cut = p.cut;
                at.waterMask = p.waterMask;
                if (!WaterSys.WindowOpen(at, out int got) || got != k)
                    fails.Add($"dur={dur}: lượt {k} phải mở ở {openAt:0.#}s nhưng WindowOpen không thấy");

                WaterSys.Consume(p, k, false);
            }
        }

        /// <summary>The 20% floor is the promise that stops fifty friends from making crops
        /// instant, so it has to hold even when Consume is called more times than there are
        /// windows.</summary>
        static void CapHolds(List<string> fails, float dur)
        {
            var p = Make(dur, 0);
            for (int k = 1; k <= 8; k++) WaterSys.Consume(p, Mathf.Clamp(k, 1, 3), false);

            float max = dur * WaterSys.TotalCut;
            if (p.cut > max + 0.01f)
                fails.Add($"dur={dur}: vượt trần, cut={p.cut:0.##}s > {max:0.##}s");
        }

        // ------------------------------------------------------------
        /// <summary>The anti-accumulation rule: time spent with the app closed must grant nothing.
        /// A plot left alone until every window is past has to end up with zero reduction and zero
        /// windows still available — no catch-up, no backlog.</summary>
        static void OfflineBanksNothing(List<string> fails)
        {
            foreach (float dur in Durations)
            {
                // six hours away, far past every window
                var p = Make(dur, 6 * 3600f);
                if (p.cut != 0f)
                    fails.Add($"offline dur={dur}: tự cộng {p.cut}s dù không ai tưới");
                if (WaterSys.WindowOpen(p, out _))
                    fails.Add($"offline dur={dur}: còn lượt đang mở sau 6 giờ");
                if (WaterSys.Remaining(p) != 0)
                    fails.Add($"offline dur={dur}: còn {WaterSys.Remaining(p)} lượt, kỳ vọng 0");
                if (WaterSys.NextWindowIn(p) >= 0f)
                    fails.Add($"offline dur={dur}: vẫn báo có lượt sắp tới");
            }
        }

        /// <summary>A visitor's watering has to be distinguishable from the owner's, or
        /// "Hà đã tưới 3 cây của bạn" needs a second data structure to exist.</summary>
        static void VisitorIsRecorded(List<string> fails)
        {
            var p = Make(300f, 0);
            WaterSys.Consume(p, 1, byVisitor: false);
            WaterSys.Consume(p, 2, byVisitor: true);

            if ((p.waterMask & 0b011) != 0b011) fails.Add("waterMask không ghi đủ 2 lượt");
            if ((p.friendMask & 0b001) != 0)    fails.Add("friendMask ghi nhầm lượt của chủ vườn");
            if ((p.friendMask & 0b010) == 0)    fails.Add("friendMask không ghi lượt của khách");
        }
    }
}
