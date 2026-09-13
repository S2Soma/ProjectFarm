using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    public enum CropTag : byte
    {
        None,
        Xp,          // Kinh Nghiệm
        HighPrice,   // Giá Cao
        Mutation,    // Đột Biến
        Season,      // Thời Vụ — a weather BONUS, never a lock
    }

    /// <summary>Crop tags: six of twenty-eight crops carry a bonus, rotating twice a day.
    ///
    /// Like weather, a pure function of (worldSeed, epoch) — nothing stored, every client agrees,
    /// and the tag a crop was planted under is recovered from <c>plantedAt</c> rather than saved.
    ///
    /// **Twelve hours, not six, and not twenty-four.** A rotation has to span enough sessions
    /// that a player can act on it — stock seeds, clear plots, plan — and still be short enough
    /// that a once-a-day player sees it change. The 12:1 ratio against weather's one hour is the
    /// structural point: a tag set spans twelve weather windows, so every player meets close to
    /// the full weather distribution inside one set. That is what makes the Season tag playable,
    /// and it keeps the two systems reading as different things — weather is tactical (which
    /// minute), tags are strategic (which half-day). Sharing a period would collapse them into
    /// one perceived state.
    ///
    /// Rotation lands at 06:00 and 18:00 ICT, the two real Vietnamese mobile peaks, so a fresh
    /// set arrives as a reward at login rather than mid-session.</summary>
    public static class TagSys
    {
        public const long EpochMs = 12L * 3_600_000L;

        /// <summary>Shift applied before the 12-hour division, so the boundaries land on 06:00
        /// and 18:00 Vietnam time rather than midnight and noon.
        ///
        /// It is UTC+7 minus the 06:00 anchor, i.e. one hour — NOT the seven-hour timezone
        /// offset, which is the obvious-looking value and puts the rotation at 00:00/12:00 ICT:
        /// the middle of the night and the middle of the working day, the two times a Vietnamese
        /// player is least likely to be holding the phone. Everything stored stays UTC; this
        /// affects only where the boundary falls.</summary>
        const long AnchorOffsetMs = 7L * 3_600_000L - 6L * 3_600_000L;

        /// <summary>Six of twenty-eight — about a fifth, enough to change what you plant today
        /// without making the other twenty-two pointless.</summary>
        public const int TaggedCount = 6;

        public static long Epoch(long nowMs) { return (nowMs + AnchorOffsetMs) / EpochMs; }
        public static long EpochEnd(long epoch) { return (epoch + 1) * EpochMs - AnchorOffsetMs; }
        public static long MsLeft(long nowMs) { return EpochEnd(Epoch(nowMs)) - nowMs; }

        public struct TagDef
        {
            public CropTag id;
            public string name, code;
            public string hex;
            public float sell, xp, mutate;
            public string blurb;
        }

        public static readonly TagDef[] Defs =
        {
            new TagDef { id = CropTag.None, name = "", code = "", hex = "#8A8478",
                         sell = 1f, xp = 1f, mutate = 1f, blurb = "" },

            // XP peaks for early players: xpNeed grows 1.28^lv, so xp is the scarce resource
            // exactly when the player has the fewest ways to get it. Touches no coins at all.
            new TagDef { id = CropTag.Xp, name = "Kinh Nghiệm", code = "XP", hex = "#3E9BD8",
                         sell = 1f, xp = 2.00f, mutate = 1f, blurb = "Kinh nghiệm ×2" },

            // 1.35 and not higher: seed cost does not scale with the tag, so a sell multiplier is
            // heavily amplified in MARGIN. At level 30 the top five crops sit within 1.17x of each
            // other; 1.35 on the fifth beats the first by 1.41x — decisive enough to change what
            // you plant, small enough that untagged crops still fill the other plots. At 1.60 the
            // margin gap becomes 2.3x and twenty-two crops stop being worth growing.
            new TagDef { id = CropTag.HighPrice, name = "Giá Cao", code = "GC", hex = "#F5A524",
                         sell = 1.35f, xp = 1f, mutate = 1f, blurb = "Giá bán ×1,35" },

            // Gated to rarity 2-3 crops: at low level the mutation rate is ~7%, so this tag would
            // deliver about +15%. A tag that reads as a reward and pays +15% is worse than no tag.
            new TagDef { id = CropTag.Mutation, name = "Đột Biến", code = "ĐB", hex = "#8E64D8",
                         sell = 1f, xp = 1f, mutate = 1.75f, blurb = "Tỉ lệ đột biến ×1,75" },

            // A BONUS, never a lock. The locked version was specced first and the arithmetic
            // killed it: if the named weather is drought (10.2% of hours), a player with five
            // sessions across the 12-hour window has a 42% chance of never once being in the game
            // during it — a reward advertised on a login card and then structurally denied, which
            // reads as a bug. As a bonus the peak moment is unchanged and nothing is ever refused.
            new TagDef { id = CropTag.Season, name = "Thời Vụ", code = "TV", hex = "#37B5A6",
                         sell = 2.20f, xp = 2.20f, mutate = 1f, blurb = "×2,2 khi đúng thời tiết" },
        };

        public static TagDef Def(CropTag t) { return Defs[(int)t]; }

        // ============================================================
        // the rotating set
        // ============================================================
        public struct Tagged
        {
            public string cropId;
            public CropTag tag;
            /// <summary>Season tags only.</summary>
            public Weather weather;
        }

        static long _cachedEpoch = long.MinValue;
        static long _cachedSeed;
        static readonly List<Tagged> _cached = new List<Tagged>();

        /// <summary>The six tagged crops for an epoch. Cached, because the HUD and the seed
        /// picker both ask every frame they redraw.</summary>
        public static List<Tagged> Set(long seed, long epoch)
        {
            if (epoch == _cachedEpoch && seed == _cachedSeed) return _cached;
            _cachedEpoch = epoch; _cachedSeed = seed;
            _cached.Clear();
            Build(seed, epoch, _cached);
            return _cached;
        }

        public static List<Tagged> Now(PlayerState s)
        {
            return s == null ? _cached : Set(s.worldSeed, Epoch(GS.Now));
        }

        /// <summary>Two crops from each of three rarity bands.
        ///
        /// The composition is fixed, and it is the part that matters: without it a random six
        /// would regularly contain nothing a low-level player can afford to plant, and the
        /// feature would read as content for other people. Two per band guarantees every player
        /// at every level has at least two tagged crops they can actually grow.</summary>
        static void Build(long seed, long epoch, List<Tagged> into)
        {
            AddBand(seed, epoch, into, 0, 1, 0);   // rarity 0-1 — always affordable
            AddBand(seed, epoch, into, 2, 2, 1);   // rarity 2 — mid game
            AddBand(seed, epoch, into, 3, 3, 2);   // rarity 3 — end game
        }

        static void AddBand(long seed, long epoch, List<Tagged> into, int rMin, int rMax, int band)
        {
            var pool = new List<Seed>();
            foreach (var s in GameData.Seeds)
                if (s.r >= rMin && s.r <= rMax) pool.Add(s);
            if (pool.Count == 0) return;

            for (int pick = 0; pick < 2 && pool.Count > 0; pick++)
            {
                long h = WeatherSys.Frac(Hash(seed, epoch, band * 17 + pick)) == 0f ? 0 : 0;
                int idx = Mathf.Clamp(
                    (int)(WeatherSys.Frac(Hash(seed, epoch, band * 17 + pick)) * pool.Count),
                    0, pool.Count - 1);
                var crop = pool[idx];
                pool.RemoveAt(idx);

                var tag = PickTag(seed, epoch, crop, into);
                var w = Weather.Sunny;
                if (tag == CropTag.Season)
                {
                    // Only common weathers are ever named. A season tag on snow or storm would
                    // point at a window most players never see, which is the denial this design
                    // exists to avoid — the bonus shape removes the hard refusal, but pointing at
                    // a 10% window would still waste the tag on most of the player base.
                    var common = new[] { Weather.Sunny, Weather.Rain, Weather.Wind };
                    w = common[Mathf.Clamp(
                        (int)(WeatherSys.Frac(Hash(seed, epoch, 900 + band * 7 + pick)) * common.Length),
                        0, common.Length - 1)];
                }
                into.Add(new Tagged { cropId = crop.id, tag = tag, weather = w });
                _ = h;
            }
        }

        static CropTag PickTag(long seed, long epoch, Seed crop, List<Tagged> soFar)
        {
            int seasons = 0, others = 0;
            var counts = new Dictionary<CropTag, int>();
            foreach (var t in soFar)
            {
                if (t.tag == CropTag.Season) seasons++;
                counts.TryGetValue(t.tag, out int c);
                counts[t.tag] = c + 1;
            }
            _ = others;

            var options = new List<CropTag> { CropTag.Xp, CropTag.HighPrice };
            if (crop.r >= 2) options.Add(CropTag.Mutation);      // worthless at low level
            if (seasons < 1) options.Add(CropTag.Season);        // at most one per rotation

            // no more than two of any one type, so a rotation is never all the same reward
            options.RemoveAll(o => counts.TryGetValue(o, out int c) && c >= 2);
            if (options.Count == 0) options.Add(CropTag.HighPrice);

            int idx = Mathf.Clamp(
                (int)(WeatherSys.Frac(Hash(seed, epoch, crop.id.GetHashCode())) * options.Count),
                0, options.Count - 1);
            return options[idx];
        }

        static long Hash(long seed, long epoch, int salt)
        {
            ulong z = (ulong)(seed ^ 0x2C1B3F91L) + (ulong)epoch * 0x9E3779B97F4A7C15UL
                    + (ulong)(uint)salt * 0xD1B54A32D192ED03UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (long)(z ^ (z >> 31));
        }

        // ============================================================
        // lookup
        // ============================================================
        public static CropTag TagOf(PlayerState s, string cropId, long atMs)
        {
            if (s == null || string.IsNullOrEmpty(cropId)) return CropTag.None;
            foreach (var t in Set(s.worldSeed, Epoch(atMs)))
                if (t.cropId == cropId) return t.tag;
            return CropTag.None;
        }

        public static Weather SeasonWeatherOf(PlayerState s, string cropId, long atMs)
        {
            if (s == null || string.IsNullOrEmpty(cropId)) return Weather.Sunny;
            foreach (var t in Set(s.worldSeed, Epoch(atMs)))
                if (t.cropId == cropId) return t.weather;
            return Weather.Sunny;
        }
    }
}
