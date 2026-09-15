using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks weather and crop tags over long stretches of simulated time.
    ///
    /// These are pure functions of (seed, time), which makes them cheap to test and easy to get
    /// subtly wrong: a first version of the weather chain compared each hour against an
    /// UNCONSTRAINED draw for the previous hour rather than against the weather actually shown
    /// then, and produced back-to-back repeats. Reading one log line caught it by luck; this
    /// catches it every time.</summary>
    public static class SeasonSysTest
    {
        const int Hours = 20000;      // a bit over two years of hourly windows
        const int Seeds = 24;

        [MenuItem("Tools/LQ Farm/Kiểm tra thời tiết & tag")]
        public static void Run()
        {
            var fails = new List<string>();

            NoRepeats(fails);
            NoStormDroughtAdjacency(fails);
            GraceWindowHolds(fails);
            DroughtGatedByLevel(fails);
            DistributionIsSane(fails);
            Deterministic(fails);

            TagSetShape(fails);
            TagRotationPeriod(fails);
            TagConstraints(fails);

            if (fails.Count == 0) Debug.Log($"Thời tiết & tag OK — {Seeds} seed × {Hours} giờ, mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Thời tiết/tag: " + f);
                Debug.LogError($"Thời tiết/tag: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        static long SeedAt(int i) { return unchecked(0x51AB3C7D1E9F2B05L * (i + 1) ^ (i * 7919L)); }

        // ------------------------------------------------------------
        /// <summary>An hour that changes nothing is an hour with no reason to open the app.</summary>
        static void NoRepeats(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                var prev = WeatherSys.At(seed, 0, 30, 0);
                for (long h = 1; h < Hours; h++)
                {
                    var w = WeatherSys.At(seed, h, 30, 0);
                    if (w == prev)
                    {
                        fails.Add($"seed#{si} giờ {h}: {WeatherSys.Def(w).name} lặp lại liền");
                        break;
                    }
                    prev = w;
                }
            }
        }

        /// <summary>Single bad windows are survivable; two in a row is what churns a player.</summary>
        static void NoStormDroughtAdjacency(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                var prev = WeatherSys.At(seed, 0, 30, 0);
                for (long h = 1; h < Hours; h++)
                {
                    var w = WeatherSys.At(seed, h, 30, 0);
                    bool bad = (w == Weather.Storm && prev == Weather.Drought)
                            || (w == Weather.Drought && prev == Weather.Storm);
                    if (bad) { fails.Add($"seed#{si} giờ {h}: Bão và Hạn Hán nối tiếp nhau"); break; }
                    prev = w;
                }
            }
        }

        /// <summary>A new player meeting the worst weather before they have met good weather has
        /// no frame of reference for it.</summary>
        static void GraceWindowHolds(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                long created = 1_700_000_000_000L + si * 97_000_000L;
                long h0 = WeatherSys.SlotIndex(created);
                long day = 24L * 3_600_000L / WeatherSys.SlotMs;
                for (long h = h0; h < h0 + day; h++)
                {
                    var w = WeatherSys.At(seed, h, 30, created);
                    if (w == Weather.Storm || w == Weather.Drought)
                    {
                        fails.Add($"seed#{si}: {WeatherSys.Def(w).name} xuất hiện trong 24 giờ đầu");
                        break;
                    }
                }
            }
        }

        static void DroughtGatedByLevel(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                for (long h = 0; h < 2000; h++)
                    if (WeatherSys.At(seed, h, 5, 0) == Weather.Drought)
                    { fails.Add($"seed#{si} giờ {h}: Hạn Hán ở cấp 5 (phải khoá tới cấp 8)"); break; }
            }
        }

        /// <summary>Bad weather must be RARER, and the reason is asymmetric rather than
        /// aesthetic: a good window adds to one session, a bad one risks the whole session. The
        /// realized share of storm+drought together should stay near a fifth.</summary>
        static void DistributionIsSane(List<string> fails)
        {
            var count = new int[WeatherSys.All.Length];
            int total = 0;
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                for (long h = 0; h < 3000; h++) { count[(int)WeatherSys.At(seed, h, 30, 0)]++; total++; }
            }
            float sunny = count[(int)Weather.Sunny] / (float)total;
            float bad = (count[(int)Weather.Storm] + count[(int)Weather.Drought]) / (float)total;

            Check(fails, sunny > 0.18f && sunny < 0.36f, $"Nắng chiếm {sunny:P1}, kỳ vọng ~27%");
            Check(fails, bad > 0.10f && bad < 0.30f, $"Bão+Hạn chiếm {bad:P1}, kỳ vọng ~20%");
            for (int i = 0; i < count.Length; i++)
                Check(fails, count[i] > 0, $"{WeatherSys.All[i].name} không bao giờ xuất hiện");
        }

        /// <summary>Same seed, same hour, same answer — the property that lets a server hand out
        /// a seed later and have every client agree without a sync.</summary>
        static void Deterministic(List<string> fails)
        {
            long seed = SeedAt(3);
            for (long h = 500; h < 520; h++)
                if (WeatherSys.At(seed, h, 30, 0) != WeatherSys.At(seed, h, 30, 0))
                { fails.Add("thời tiết không tất định"); return; }
        }

        // ------------------------------------------------------------
        /// <summary>Two crops per rarity band, always. Without it a random six would regularly
        /// hold nothing a low-level player can afford, and the feature would read as content for
        /// other people.</summary>
        static void TagSetShape(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                for (long e = 0; e < 400; e++)
                {
                    var set = TagSys.Set(seed, e);
                    if (set.Count != TagSys.TaggedCount)
                    { fails.Add($"seed#{si} epoch {e}: {set.Count} cây, kỳ vọng {TagSys.TaggedCount}"); break; }

                    int low = 0, mid = 0, high = 0;
                    var seen = new HashSet<string>();
                    foreach (var t in set)
                    {
                        if (!seen.Add(t.cropId)) { fails.Add($"seed#{si} epoch {e}: trùng cây {t.cropId}"); break; }
                        int r = GameData.Get(t.cropId).r;
                        if (r <= 1) low++; else if (r == 2) mid++; else high++;
                    }
                    if (low != 2 || mid != 2 || high != 2)
                    { fails.Add($"seed#{si} epoch {e}: phân bố bậc {low}/{mid}/{high}, kỳ vọng 2/2/2"); break; }
                }
            }
        }

        /// <summary>Twelve hours, landing at 06:00 and 18:00 ICT — the two real Vietnamese mobile
        /// peaks, so a fresh set arrives at login rather than mid-session.</summary>
        static void TagRotationPeriod(List<string> fails)
        {
            long e0 = TagSys.Epoch(1_789_000_000_000L);
            // measure from the START of an epoch, not from an arbitrary instant inside one — the
            // first version of this test did the latter and failed on its own arithmetic
            long start = TagSys.EpochEnd(e0 - 1);

            Check(fails, TagSys.Epoch(start) == e0, "mốc đầu epoch tính sai");
            Check(fails, TagSys.Epoch(start + TagSys.EpochMs - 1000) == e0, "epoch đổi sớm");
            Check(fails, TagSys.Epoch(start + TagSys.EpochMs) == e0 + 1, "chu kỳ tag không phải 12 giờ");

            // boundaries must land on 06:00 / 18:00 ICT — the two Vietnamese mobile peaks
            for (long e = e0; e < e0 + 6; e++)
            {
                long ictHour = ((TagSys.EpochEnd(e) + 7L * 3_600_000L) / 3_600_000L) % 24L;
                if (ictHour != 6 && ictHour != 18)
                { fails.Add($"mốc đổi rơi vào {ictHour}:00 ICT, kỳ vọng 6 hoặc 18"); break; }
            }
        }

        /// <summary>At most one season tag, at most two of any other, and no mutation tag on a
        /// crop where it would pay about +15% and read as an insult.</summary>
        static void TagConstraints(List<string> fails)
        {
            for (int si = 0; si < Seeds; si++)
            {
                long seed = SeedAt(si);
                for (long e = 0; e < 400; e++)
                {
                    var set = TagSys.Set(seed, e);
                    var counts = new Dictionary<CropTag, int>();
                    foreach (var t in set)
                    {
                        counts.TryGetValue(t.tag, out int c);
                        counts[t.tag] = c + 1;

                        if (t.tag == CropTag.None)
                        { fails.Add($"seed#{si} epoch {e}: cây được chọn nhưng không có tag"); return; }

                        if (t.tag == CropTag.Mutation && GameData.Get(t.cropId).r < 2)
                        { fails.Add($"seed#{si} epoch {e}: tag Đột Biến trên cây bậc thấp {t.cropId}"); return; }

                        if (t.tag == CropTag.Season
                            && t.weather != Weather.Sunny && t.weather != Weather.Rain && t.weather != Weather.Wind)
                        { fails.Add($"seed#{si} epoch {e}: Thời Vụ trỏ vào thời tiết hiếm {t.weather}"); return; }
                    }
                    foreach (var kv in counts)
                    {
                        int cap = kv.Key == CropTag.Season ? 1 : 2;
                        if (kv.Value > cap)
                        { fails.Add($"seed#{si} epoch {e}: {kv.Value} tag {kv.Key}, trần là {cap}"); return; }
                    }
                }
            }
        }
    }
}
