using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the watering rule across every crop, weather, mutation tier and level the game
    /// can produce: a fixed number of waterings per crop, each worth a fixed time (2026-09-15).
    ///
    /// The property that is easy to assert and impossible to eyeball: the LAST window has to still be
    /// open when an attentive player reaches it, and that player arrives earlier than a lazy one
    /// precisely because they watered the earlier windows. Get the floor slightly wrong and the final
    /// window becomes unreachable — dead code no screenshot would ever reveal. With fixed times per
    /// crop this is no longer automatic (drought doubles the time, the level perk shortens the crop),
    /// which is why it is checked on every combination rather than on a list of durations.</summary>
    public static class WaterSysTest
    {
        static readonly int[] Levels = { 1, 12, 30, 40 };

        [MenuItem("Tools/LQ Farm/Kiểm tra tưới nước")]
        public static void Run()
        {
            var fails = new List<string>();
            var saved = GS.Local;
            int combos = 0;
            try
            {
                GS.Local = new PlayerState();
                GS.Local.NewGame();
                GS.Local.clock.lastSeenUtc = NowMs();

                SeedTableIsSound(fails);
                foreach (var seed in GameData.Seeds)
                {
                    WindowCountIsTheCrops(fails, seed);
                    CutsAddUpExactly(fails, seed);
                    foreach (int lv in Levels)
                        foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
                            for (int v = 0; v < Art.Elements.Length; v++)
                            {
                                combos++;
                                var p = Make(seed, lv, w, v, 0f);
                                NeverPastTheCeiling(fails, seed, lv, w, v, p);
                                WindowsAreContiguous(fails, p, Tag(seed, lv, w, v));
                                LastWindowIsReachable(fails, seed, lv, w, v);
                                CapHolds(fails, seed, lv, w, v);
                            }
                }
                OfflineBanksNothing(fails);
                VisitorIsRecorded(fails);
                GoldenCanKeepsTheFloor(fails);
                OldPlotsFinishSanely(fails);
                RainWatersOnItsOwn(fails);
            }
            finally { GS.Local = saved; }

            if (fails.Count == 0)
                Debug.Log($"Tưới nước OK — {GameData.Seeds.Length} cây × {Levels.Length} cấp × 6 thời tiết × {Art.Elements.Length} bậc = {combos} tổ hợp, mọi bất biến đạt.");
            else
            {
                int shown = 0;
                foreach (var f in fails) { if (shown++ >= 60) break; Debug.LogError("Tưới nước: " + f); }
                Debug.LogError($"Tưới nước: {fails.Count} lỗi.");
            }
        }

        static long NowMs() { return (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds; }
        static string Tag(Seed s, int lv, Weather w, int v) { return $"{s.id} cấp {lv} {w} bậc {v}"; }

        /// <summary>A plot as <see cref="PlotLogic.Plant"/> stamps it, planted <paramref name="ago"/>
        /// seconds before now, without the random mutation roll.</summary>
        static Plot Make(Seed seed, int lv, Weather w, int variant, float ago)
        {
            var s = GS.Local;
            s.lv = lv;
            return new Plot
            {
                locked = false,
                crop = seed.id,
                big = seed.big,
                variant = variant,
                dur = s.GrowTimeIn(seed, w, variant),
                windowCount = (byte)WaterSys.WindowsFor(seed),
                waterSec = seed.waterCut,
                plantWeather = (byte)w,
                plantedAt = GS.Now - (long)(ago * 1000f),
            };
        }

        static Plot At(Plot src, float nominal)
        {
            return new Plot
            {
                locked = false, crop = src.crop, big = src.big, variant = src.variant, dur = src.dur,
                windowCount = src.windowCount, waterSec = src.waterSec, plantWeather = src.plantWeather,
                cut = src.cut, waterMask = src.waterMask, friendMask = src.friendMask,
                plantedAt = GS.Now - (long)(nominal * 1000.0),
            };
        }

        // ------------------------------------------------------------
        /// <summary>Every crop has 1..4 waterings worth a positive, readable time, and all of them
        /// together take off a meaningful but bounded share of the crop (10–30%).</summary>
        static void SeedTableIsSound(List<string> fails)
        {
            foreach (var s in GameData.Seeds)
            {
                if (s.waters < 1 || s.waters > WaterSys.MaxWindows)
                    fails.Add($"{s.id}: {s.waters} lần tưới, ngoài 1..{WaterSys.MaxWindows}");
                if (s.waterCut <= 0) fails.Add($"{s.id}: mỗi lần tưới sớm {s.waterCut}s");
                float share = s.waters * s.waterCut / (float)s.grow;
                if (share < 0.10f || share > 0.30f)
                    fails.Add($"{s.id}: tưới đủ {s.waters} lần sớm {share:P0} thời gian, ngoài 10–30%");
                if (s.grow > GameData.MaxGrowSeconds) fails.Add($"{s.id}: thời gian gốc {s.grow}s quá 24 giờ");
            }
            // longer crops never drink less often than shorter ones
            Seed prev = null;
            foreach (var s in GameData.Seeds)
            {
                if (s.big) continue;
                if (prev != null && s.grow >= prev.grow && s.waters < prev.waters)
                    fails.Add($"{s.id} ({s.grow}s) tưới {s.waters} lần, ít hơn {prev.id} ({prev.grow}s, {prev.waters} lần)");
                prev = s;
            }
        }

        /// <summary>Planting through the game's own code stamps the crop's own count and time.</summary>
        static void WindowCountIsTheCrops(List<string> fails, Seed seed)
        {
            var s = GS.Local;
            s.lv = Mathf.Max(seed.lv, 1);
            s.buffMutateUntil = 0;
            var p = new Plot { locked = false, big = seed.big };
            s.AddSeed(seed.id, 1);
            if (!PlotLogic.Plant(s, s, p, seed.id)) { fails.Add($"{seed.id}: không gieo được để kiểm tra"); return; }
            if (p.windowCount != seed.waters) fails.Add($"{seed.id}: gieo ra {p.windowCount} cữ, bảng ghi {seed.waters}");
            if (!Mathf.Approximately(p.waterSec, seed.waterCut)) fails.Add($"{seed.id}: gieo ra {p.waterSec}s/lần, bảng ghi {seed.waterCut}s");
            if (WaterSys.Windows(p) != seed.waters) fails.Add($"{seed.id}: Windows() = {WaterSys.Windows(p)}, bảng ghi {seed.waters}");
        }

        /// <summary>Watering every window of a crop in fair weather gives back exactly N × the crop's time.</summary>
        static void CutsAddUpExactly(List<string> fails, Seed seed)
        {
            var p = Make(seed, seed.lv, Weather.Sunny, 0, 0f);
            int w = WaterSys.Windows(p);
            float period = WaterSys.Period(p.dur, w);
            for (int k = 1; k <= w; k++) WaterSys.Consume(p, k, false, k * period);
            float want = seed.waters * seed.waterCut;
            if (Mathf.Abs(p.cut - want) > 0.01f)
                fails.Add($"{seed.id}: tưới đủ {w} lần sớm {p.cut:0.#}s, kỳ vọng {seed.waters} × {seed.waterCut}s = {want}s");
        }

        /// <summary>The 24-hour ceiling: no weather, tier, level or island perk makes a plot longer.</summary>
        static void NeverPastTheCeiling(List<string> fails, Seed seed, int lv, Weather w, int v, Plot p)
        {
            if (p.dur > GameData.MaxGrowSeconds + 0.01f)
                fails.Add($"{Tag(seed, lv, w, v)}: {p.dur:0}s, vượt trần 24 giờ");
            if (p.dur <= 0f) fails.Add($"{Tag(seed, lv, w, v)}: thời gian {p.dur}");
            float sheltered = GS.Local.GrowTimeIn(seed, w, v, sheltered: true);
            if (sheltered > GameData.MaxGrowSeconds + 0.01f)
                fails.Add($"{Tag(seed, lv, w, v)} (nhà kính): {sheltered:0}s, vượt trần 24 giờ");
        }

        /// <summary>A window stays open until the next one opens (the last until ripe), so there is never
        /// a gap and never two at once. Sampled just either side of every boundary.</summary>
        static void WindowsAreContiguous(List<string> fails, Plot probe, string tag)
        {
            int w = WaterSys.Windows(probe);
            float period = WaterSys.Period(probe.dur, w);
            float eps = Mathf.Min(0.25f, period * 0.01f);
            for (int k = 1; k <= w; k++)
            {
                if (k < w && Mathf.Abs(WaterSys.WindowEnd(probe, k) - (k + 1) * period) > 0.001f)
                    fails.Add($"{tag}: cữ {k} không kết thúc đúng lúc cữ {k + 1} mở");

                // earlier windows spent, as by a player who kept up (cut left at 0: the boundary is wall-clock)
                var late = At(probe, WaterSys.WindowEnd(probe, k) - eps);
                for (int j = 1; j < k; j++) late.waterMask |= (byte)(1 << (j - 1));
                if (!WaterSys.WindowOpen(late, out int got) || got != k)
                    fails.Add($"{tag}: cữ {k} đã tắt trước khi cữ sau mở");
                if (k < w)
                {
                    // missed entirely: it is gone once the next one opens, never two at once
                    var after = At(probe, (k + 1) * period + eps);
                    WaterSys.WindowOpen(after, out int now);
                    if (now != k + 1) fails.Add($"{tag}: lỡ cữ {k} mà cữ {k} vẫn còn khi cữ {k + 1} mở");
                }
            }
        }

        /// <summary>Walk the crop forward the way an attentive player does — watering each window the
        /// instant it opens — and check the crop is still growing when every window arrives.</summary>
        static void LastWindowIsReachable(List<string> fails, Seed seed, int lv, Weather w, int v)
        {
            var p = Make(seed, lv, w, v, 0f);
            int n = WaterSys.Windows(p);
            float period = WaterSys.Period(p.dur, n);
            for (int k = 1; k <= n; k++)
            {
                float openAt = k * period;
                float ripeAt = p.dur - p.cut;
                if (openAt >= ripeAt)
                {
                    fails.Add($"{Tag(seed, lv, w, v)}: cữ {k} mở ở {openAt:0}s nhưng cây đã chín ở {ripeAt:0}s, cữ chết");
                    return;
                }
                var at = At(p, openAt + Mathf.Min(0.05f, period * 0.001f));
                if (!WaterSys.WindowOpen(at, out int got) || got != k)
                {
                    fails.Add($"{Tag(seed, lv, w, v)}: cữ {k} phải mở ở {openAt:0}s nhưng WindowOpen không thấy");
                    return;
                }
                WaterSys.Consume(p, k, false, openAt);
            }
        }

        /// <summary>However often Consume is called, the cut stays under the crop's N × time (× drought)
        /// and under half the duration, and never goes negative.</summary>
        static void CapHolds(List<string> fails, Seed seed, int lv, Weather w, int v)
        {
            var p = Make(seed, lv, w, v, 0f);
            for (int k = 1; k <= 12; k++) WaterSys.Consume(p, Mathf.Clamp(k % 5, 1, 4), false, 0f);
            float capByTime = seed.waters * seed.waterCut * WeatherSys.Def(w).waterCut;
            if (p.cut > capByTime + 0.01f) fails.Add($"{Tag(seed, lv, w, v)}: sớm {p.cut:0}s, vượt {capByTime:0}s");
            if (p.cut > p.dur * WaterSys.MaxShare + 0.01f) fails.Add($"{Tag(seed, lv, w, v)}: sớm {p.cut:0}s, quá nửa thời gian");
            if (p.cut < 0f) fails.Add($"{Tag(seed, lv, w, v)}: thời gian tưới âm {p.cut}");
        }

        // ------------------------------------------------------------
        /// <summary>The anti-accumulation rule: time spent with the app closed grants nothing. A plot
        /// left alone past its ripe time has zero reduction and no window left — no catch-up, no backlog.</summary>
        static void OfflineBanksNothing(List<string> fails)
        {
            foreach (var seed in GameData.Seeds)
                foreach (Weather w in new[] { Weather.Sunny, Weather.Snow, Weather.Drought })
                {
                    var p = Make(seed, seed.lv, w, 0, GameData.MaxGrowSeconds + 3600f);
                    string tag = $"offline {seed.id} {w}";
                    if (p.cut != 0f) fails.Add($"{tag}: tự cộng {p.cut}s dù không ai tưới");
                    if (PlotLogic.State(p) != PlotState.Ready) fails.Add($"{tag}: sau 25 giờ vẫn chưa chín");
                    if (WaterSys.WindowOpen(p, out _)) fails.Add($"{tag}: còn cữ đang mở");
                    if (WaterSys.Remaining(p) != 0) fails.Add($"{tag}: còn {WaterSys.Remaining(p)} cữ, kỳ vọng 0");
                    if (WaterSys.NextWindowIn(p) >= 0f) fails.Add($"{tag}: vẫn báo có cữ sắp tới");

                    // and half-way through, only the one current window is open — never a pile of missed ones
                    var mid = Make(seed, seed.lv, w, 0, p.dur * 0.99f);
                    int open = 0, n = WaterSys.Windows(mid);
                    for (int k = 1; k <= n; k++)
                    {
                        var probe = At(mid, mid.dur * 0.99f);
                        probe.waterMask = (byte)(0xFF & ~(1 << (k - 1)));
                        if (WaterSys.WindowOpen(probe, out int got) && got == k) open++;
                    }
                    if (open > 1) fails.Add($"{tag}: {open} cữ cùng mở một lúc");
                }
        }

        /// <summary>A visitor's watering has to be distinguishable from the owner's, or
        /// "Hà đã tưới 3 cây của bạn" needs a second data structure to exist.</summary>
        static void VisitorIsRecorded(List<string> fails)
        {
            var p = Make(GameData.Get("pumpkin"), 12, Weather.Sunny, 0, 0f);
            WaterSys.Consume(p, 1, byVisitor: false);
            WaterSys.Consume(p, 2, byVisitor: true);

            if ((p.waterMask & 0b011) != 0b011) fails.Add("waterMask không ghi đủ 2 lượt");
            if ((p.friendMask & 0b001) != 0)    fails.Add("friendMask ghi nhầm lượt của chủ vườn");
            if ((p.friendMask & 0b010) == 0)    fails.Add("friendMask không ghi lượt của khách");
        }

        /// <summary>The golden can waters ahead of schedule, through the game's own code, and can never
        /// get past the floor: used right after planting on every crop and weather, the crop still has
        /// time left and never more taken off than its own waterings are worth.</summary>
        static void GoldenCanKeepsTheFloor(List<string> fails)
        {
            var s = GS.Local;
            foreach (var seed in GameData.Seeds)
                foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
                {
                    var p = Make(seed, 30, w, 0, 1f);
                    int n = 0;
                    while (PlotLogic.WaterAhead(s, s, p, false) >= 0 && n < 10) n++;
                    if (n != WaterSys.Windows(p)) fails.Add($"bình tưới vàng {seed.id} {w}: tưới {n} lần, cây có {WaterSys.Windows(p)} cữ");
                    if (p.cut > WaterSys.Cap(p) + 0.01f) fails.Add($"bình tưới vàng {seed.id} {w}: sớm {p.cut:0}s, vượt trần {WaterSys.Cap(p):0}s");
                    if (PlotLogic.Remain(p) <= 0) fails.Add($"bình tưới vàng {seed.id} {w}: chín ngay khi vừa gieo");
                }
        }

        /// <summary>A plot planted before per-crop watering (no <c>waterSec</c>, maybe no window count)
        /// finishes under the old 20% rule: no exception, no negative time, every window reachable.</summary>
        static void OldPlotsFinishSanely(List<string> fails)
        {
            foreach (float dur in new[] { 22f, 40f, 89f, 268f, 1034f, 4200f })
                foreach (byte wc in new byte[] { 0, 1, 3 })
                {
                    var p = new Plot { locked = false, crop = "wheat", dur = dur, windowCount = wc, plantedAt = GS.Now };
                    int n = WaterSys.Windows(p);
                    float period = WaterSys.Period(dur, n);
                    for (int k = 1; k <= n; k++)
                    {
                        if (k * period >= dur - p.cut) { fails.Add($"ô cũ dur={dur} cữ={wc}: cữ {k} chết"); break; }
                        WaterSys.Consume(p, k, false, k * period);
                    }
                    if (p.cut < 0f || p.cut > dur * WaterSys.LegacyShare + 0.01f)
                        fails.Add($"ô cũ dur={dur} cữ={wc}: sớm {p.cut:0.#}s, ngoài 0..20%");
                    if (float.IsNaN(p.cut)) fails.Add($"ô cũ dur={dur} cữ={wc}: NaN");
                }
        }

        /// <summary>Rain and storm take a window as it opens, even while the game is closed; a window
        /// that opened in any other weather is left for the player.</summary>
        static void RainWatersOnItsOwn(List<string> fails)
        {
            var s = GS.Local;
            s.lv = 12;
            long now = NowMs();
            s.clock.lastSeenUtc = now;
            s.createdAt = now - 30L * 24 * 3_600_000L;       // past the calm first day
            var seed = GameData.Get("eggplant");
            float dur = s.GrowTimeIn(seed, Weather.Sunny);
            bool sawWet = false, sawDry = false;
            for (long ws = 1; ws < 400 && !(sawWet && sawDry); ws++)
            {
                s.worldSeed = ws;
                var p = new Plot { locked = false, crop = seed.id, dur = dur, plantedAt = now - (long)(dur * 0.95f * 1000f),
                                   windowCount = (byte)WaterSys.WindowsFor(seed), waterSec = seed.waterCut };
                int w = WaterSys.Windows(p);
                float period = WaterSys.Period(dur, w);
                long openedAt = p.plantedAt + (long)(period * 1000f);
                var weather = WeatherSys.At(s, WeatherSys.SlotIndex(openedAt));
                bool wet = weather == Weather.Rain || weather == Weather.Storm;
                float cutBefore = p.cut;
                WaterSys.RainWater(s, p, catchUp: true);
                bool took = (p.waterMask & 1) != 0;
                if (wet)
                {
                    sawWet = true;
                    if (!took) fails.Add($"seed {ws}: cữ 1 mở lúc {weather} mà trời không tự tưới");
                    if (took && p.cut <= cutBefore) fails.Add($"seed {ws}: trời tưới mà cây không nhanh hơn");
                }
                else
                {
                    sawDry = true;
                    if (took) fails.Add($"seed {ws}: cữ 1 mở lúc {weather} mà vẫn tự tưới");
                }
                if (p.cut > WaterSys.Cap(p) + 0.01f) fails.Add($"seed {ws}: trời tưới vượt trần");
            }
            if (!sawWet) fails.Add("không tìm được giờ mưa để kiểm tra tự tưới");
        }
    }
}
