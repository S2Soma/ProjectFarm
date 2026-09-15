using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    public enum Grade : byte { None, Bronze, Silver, Gold, Diamond }

    /// <summary>Short-term contracts: random, graded, and they expire.
    ///
    /// **The grade is rolled when the contract appears, and shown.** The alternative — one
    /// mission with four thresholds, paying for however far you got — is more forgiving and
    /// removes the only decision the system creates. A player has to be able to look at a board
    /// and choose whether to chase the gold one, which means knowing it is gold before they start.
    ///
    /// **The streak is the whole engagement mechanic.** Completing contracts consecutively
    /// shifts the grade weights; letting one expire resets it to zero. A player who reliably
    /// clears their board sees gold about twice as often as a drive-by player, and "chuỗi 7" is
    /// legible in a way a hidden luck modifier never is.
    ///
    /// Contracts are generated deterministically from (worldSeed, slot, cycle), so only progress
    /// and expiry are stateful — the definition itself is reproducible after any save round-trip.</summary>
    public static class MissionSys
    {
        public struct GradeDef
        {
            public Grade id;
            public string name;
            public string hex;
            /// <summary>Requirement multiplier against the bronze baseline.</summary>
            public float need;
            /// <summary>Reward multiplier. Deliberately steeper than the requirement — a diamond
            /// asks 8x the work of a bronze and pays 12x, so chasing the rare one is correct
            /// rather than merely flashier.</summary>
            public float reward;
            /// <summary>Spawn weight at streak 0.</summary>
            public float weight0;
            /// <summary>Spawn weight at the streak cap.</summary>
            public float weightMax;
        }

        public static readonly GradeDef[] Grades =
        {
            new GradeDef { id = Grade.None,    name = "",            hex = "#8A8478", need = 0f, reward = 0f,  weight0 = 0f,  weightMax = 0f },
            new GradeDef { id = Grade.Bronze,  name = "Đồng",        hex = "#C98A3A", need = 1f, reward = 1f,  weight0 = 50f, weightMax = 30f },
            new GradeDef { id = Grade.Silver,  name = "Bạc",         hex = "#9FB0BE", need = 2f, reward = 2.2f, weight0 = 32f, weightMax = 40f },
            new GradeDef { id = Grade.Gold,    name = "Vàng",        hex = "#F5A524", need = 4f, reward = 5f,  weight0 = 15f, weightMax = 24f },
            new GradeDef { id = Grade.Diamond, name = "Kim Cương",   hex = "#5AC8E0", need = 8f, reward = 12f, weight0 = 3f,  weightMax = 6f },
        };

        public static GradeDef Def(Grade g) { return Grades[(int)g]; }

        /// <summary>Streak at which the weights stop improving. Ten is reachable in a day of
        /// normal play, so the reward for consistency arrives while it still feels connected to
        /// the behaviour that earned it.</summary>
        public const int StreakCap = 10;

        /// <summary>Contracts completed per day, capped so a very heavy session cannot outpace
        /// the economy the rest of the design is balanced against.</summary>
        public const int DailyCap = 24;

        /// <summary>Guaranteed diamond if none has appeared in this many contracts.</summary>
        public const int DiamondPity = 40;

        public static int SlotsFor(int level)
        {
            if (level >= 25) return 5;
            if (level >= 15) return 4;
            return 3;
        }

        /// <summary>A fresh slot is not instant — otherwise clearing the board would just spawn
        /// another board and the player would never reach a natural stopping point.</summary>
        public const long RefillMs = 8L * 60_000L;

        // ============================================================
        // the work a contract asks for
        // ============================================================
        public struct Kind
        {
            public string type;
            public string label;      // "Thu hoạch {0} cây"
            public int baseNeed;      // bronze requirement
            public float hours;       // lifetime at bronze
            public bool cropSpecific;
        }

        /// <summary>Lifetimes are a couple of hours, not half an hour: with crops that take hours, a
        /// player's day is a handful of sessions, and a contract that lapses between two of them breaks
        /// the streak of exactly the player who comes back.</summary>
        static readonly Kind[] Kinds =
        {
            new Kind { type = "harvest", label = "Thu hoạch {0} cây",      baseNeed = 6,  hours = 2f },
            new Kind { type = "plant",   label = "Gieo trồng {0} hạt",     baseNeed = 6,  hours = 2f },
            new Kind { type = "water",   label = "Tưới nước {0} lần",      baseNeed = 5,  hours = 2f },
            new Kind { type = "sell",    label = "Bán {0} nông sản",       baseNeed = 10, hours = 2f },
            new Kind { type = "mutate",  label = "Thu {0} cây đột biến",   baseNeed = 2,  hours = 6f },
            new Kind { type = "harvest", label = "Thu hoạch {0} {1}",      baseNeed = 6,  hours = 2f, cropSpecific = true },
        };

        /// <summary>A contract names only a crop that grows in this long or less, and lives long enough
        /// to grow it: "Thu hoạch 6 Bơ Sáp" would be a day of six plots for a reward sized for minutes.</summary>
        public const int MaxContractGrowSeconds = 3 * 3600;

        /// <summary>Crop-specific contracts pay this much more.
        ///
        /// The number is load-bearing and it is not flavour. Coin margin rises near-monotonically
        /// across all twenty-eight crops, so at any level exactly one crop is correct to plant and
        /// it is always the newest — by level 10 every plot on the farm grows the same thing, the
        /// decision space is empty, and that is what kills a farm game around level 8-12.
        ///
        /// At 1.8 a gold "24 Cà Rốt" pays 4,698 against a 4,176 opportunity cost, so switching is
        /// CORRECT rather than merely thematic. At 1.0 it pays 2,610 and every player rightly
        /// ignores it. This one multiplier is the cheapest defence the design has against
        /// monoculture, which is why 40% of contracts name a crop.</summary>
        public const float CropSpecificBonus = 1.8f;
        public const float CropSpecificShare = 0.40f;

        // ============================================================
        // generation
        // ============================================================
        public static long CycleOf(long nowMs) { return nowMs / RefillMs; }

        /// <summary>Build the contract for a slot. Pure in everything except the streak, which is
        /// passed in so the same call can be replayed from a save.</summary>
        public static MissionRec Generate(PlayerState s, int slot, long cycle)
        {
            long h = Hash(s.worldSeed, slot, cycle);
            var kind = Kinds[(int)(WeatherSys.Frac(h) * Kinds.Length) % Kinds.Length];

            // 40% crop-specific: re-roll the kind toward a named crop when the dice say so
            bool wantCrop = WeatherSys.Frac(Hash(s.worldSeed, slot, cycle ^ 0x51)) < CropSpecificShare;
            if (wantCrop) kind = Kinds[Kinds.Length - 1];
            else if (kind.cropSpecific) kind = Kinds[0];

            var grade = RollGrade(s, h);

            string cropId = null;
            if (kind.cropSpecific)
            {
                // name something the player can grow, preferring one they have neglected
                var pool = new List<Seed>();
                foreach (var seed in GameData.Seeds)
                    if (seed.lv <= s.lv && !seed.big && seed.grow <= MaxContractGrowSeconds) pool.Add(seed);
                if (pool.Count == 0) pool.Add(GameData.Seeds[0]);
                cropId = pool[(int)(WeatherSys.Frac(Hash(s.worldSeed, slot, cycle ^ 0xA3)) * pool.Count) % pool.Count].id;
            }

            var gd = Def(grade);
            int need = Mathf.Max(1, Mathf.RoundToInt(kind.baseNeed * gd.need));
            float hours = kind.hours * Mathf.Max(1f, gd.need * 0.5f);
            var named = GameData.Get(cropId);
            if (named != null) hours += named.grow * 1.5f / 3600f;       // time to actually grow it

            return new MissionRec
            {
                slot = slot,
                genCycle = cycle,
                type = kind.type,
                cropId = cropId,
                grade = grade,
                need = need,
                p = 0,
                claimed = false,
                expiresAt = GS.Now + (long)(hours * 3_600_000L),
            };
        }

        static Grade RollGrade(PlayerState s, long h)
        {
            if (s.sinceDiamond >= DiamondPity) return Grade.Diamond;

            float t = Mathf.Clamp01(s.streak / (float)StreakCap);
            float total = 0f;
            System.Span<float> w = stackalloc float[Grades.Length];
            for (int i = 1; i < Grades.Length; i++)
            {
                w[i] = Mathf.Lerp(Grades[i].weight0, Grades[i].weightMax, t);
                total += w[i];
            }

            float r = WeatherSys.Frac(h ^ 0x7E3D) * total;
            for (int i = 1; i < Grades.Length; i++)
            {
                r -= w[i];
                if (r < 0f) return Grades[i].id;
            }
            return Grade.Bronze;
        }

        public static string Describe(MissionRec m)
        {
            foreach (var k in Kinds)
            {
                if (k.type != m.type) continue;
                if (k.cropSpecific != (m.cropId != null)) continue;
                if (k.cropSpecific)
                {
                    var seed = GameData.Get(m.cropId);
                    return string.Format(k.label, m.need, seed != null ? seed.name : m.cropId);
                }
                return string.Format(k.label, m.need);
            }
            return m.type + " " + m.need;
        }

        // ============================================================
        // rewards
        // ============================================================
        /// <summary>The length of time UNIT measures: one plot's best earnings over this many hours.</summary>
        public const float UnitHours = 8f;

        /// <summary>UNIT — the coin amount every reward and price in the game is quoted in, so the
        /// tables keep their meaning as the economy grows instead of needing a rebalance every few levels.
        ///
        /// **UNIT is what one plot earns the player in one 8-hour absence** — a night's sleep, a school or
        /// work day — with the best crop they can grow: the whole margin of a crop that is ready within
        /// the eight hours, or eight hours' share of a longer one.
        ///
        /// It used to be the best margin PER HARVEST. That was fine while every crop took minutes; with a
        /// 2-minute-to-24-hour ladder it nearly triples between level 16 (an 8-hour cabbage) and level 30
        /// (a 24-hour avocado) while what a plot earns per hour moves by a fifth, and every per-plot price
        /// ("Chín ngay", a watering can) would be quoted in days of one plot. Per HOUR fails the other way: the best hourly crop is always
        /// the carrot someone taps every two minutes, so UNIT would never grow at all. Eight hours is the
        /// one absence every play style has every day, and it grows with each unlock that fits it
        /// (steeply through the early game, by the level's price perk after the 8-hour cabbage).
        /// Base grow times are used, not the level-shortened ones, so a level-up never moves UNIT sideways.</summary>
        public static int Unit(PlayerState s) { return Unit(s, UnitHours); }

        /// <summary>UNIT measured over another span — for the journey report, which checks the choice.</summary>
        public static int Unit(PlayerState s, float hours)
        {
            float slot = hours * 3600f, best = 1f;
            foreach (var seed in GameData.Seeds)
            {
                // trees are four cells of land per plot; counting one would quadruple every price
                if (seed.lv > s.lv || seed.big) continue;
                float margin = s.HarvestValue(seed.id, 0) - seed.price;
                float v = margin * Mathf.Min(1f, slot / Mathf.Max(1f, seed.grow));
                if (v > best) best = v;
            }
            return Mathf.RoundToInt(best);
        }

        /// <summary>The same measure in XP: one plot's best XP over an 8-hour absence.</summary>
        public static int XpUnit(PlayerState s)
        {
            float slot = UnitHours * 3600f, best = 1f;
            foreach (var seed in GameData.Seeds)
            {
                if (seed.lv > s.lv || seed.big) continue;
                float v = s.XpFor(seed, 0) * Mathf.Min(1f, slot / Mathf.Max(1f, seed.grow));
                if (v > best) best = v;
            }
            return Mathf.RoundToInt(best);
        }

        /// <summary>Coins for visiting a friend: 0,8 to 2 UNIT. It was 400–1.300 flat, which was a
        /// quarter of a level at level 1 and nothing at level 20.</summary>
        public static int VisitCoin(PlayerState s, float roll)
        {
            return Mathf.Max(10, Mathf.RoundToInt(Unit(s) * (0.8f + 1.2f * Mathf.Clamp01(roll)) / 10f) * 10);
        }

        const float BaseReward = 2f;

        public static int CoinReward(PlayerState s, MissionRec m)
        {
            float mul = Def(m.grade).reward * (m.cropId != null ? CropSpecificBonus : 1f);
            return Mathf.RoundToInt(Unit(s) * BaseReward * mul);
        }

        public static int XpReward(PlayerState s, MissionRec m)
        {
            float mul = Def(m.grade).reward * (m.cropId != null ? CropSpecificBonus : 1f);
            return Mathf.RoundToInt(XpUnit(s) * BaseReward * mul);
        }

        public static int EnergyReward(MissionRec m)
        {
            return Mathf.RoundToInt(4f * Def(m.grade).reward);
        }

        // ============================================================
        // chapter missions
        // ============================================================
        /// <summary>Every level's fixed set ends on a diamond. Four missions, pre-graded, so the
        /// last thing a player does before levelling is the biggest one.</summary>
        public static Grade ChapterGrade(int indexInChapter)
        {
            switch (indexInChapter)
            {
                case 0:  return Grade.Bronze;
                case 1:  return Grade.Silver;
                case 2:  return Grade.Gold;
                default: return Grade.Diamond;
            }
        }

        public static int ChapterCoin(PlayerState s, int indexInChapter)
        {
            return Mathf.RoundToInt(Unit(s) * BaseReward * Def(ChapterGrade(indexInChapter)).reward);
        }

        public static int ChapterXp(PlayerState s, int indexInChapter)
        {
            return Mathf.RoundToInt(XpUnit(s) * BaseReward * Def(ChapterGrade(indexInChapter)).reward);
        }

        /// <summary>A daily's reward. The table's numbers are weights read as UNIT / 400, so
        /// "Thăm nom 5 người bạn" (1500) is 3,75 of the best crop's margin at every level. They
        /// used to be paid as written: 1.500 XP at level 1 was four levels for tapping five
        /// friends, and at level 30 it was nothing.</summary>
        public static int DailyXp(PlayerState s, Task t)   { return Mathf.Max(10, Mathf.RoundToInt(XpUnit(s) * t.xp / 400f)); }
        public static int DailyCoin(PlayerState s, Task t) { return Mathf.Max(10, Mathf.RoundToInt(Unit(s) * t.coin / 400f)); }

        /// <summary>What claiming <paramref name="t"/> pays right now: the panel, the claim and the
        /// reward card all read this one number.</summary>
        public static int TaskXp(PlayerState s, Task t, bool daily)
        {
            return daily ? DailyXp(s, t) : ChapterXp(s, IndexInChapter(t));
        }

        public static int TaskCoin(PlayerState s, Task t, bool daily)
        {
            return daily ? DailyCoin(s, t) : ChapterCoin(s, IndexInChapter(t));
        }

        public static int IndexInChapter(Task t)
        {
            foreach (var ch in GameData.Chapters)
                for (int i = 0; i < ch.tasks.Length; i++)
                    if (ch.tasks[i] == t) return i;
            return 0;
        }

        static long Hash(long seed, int slot, long cycle)
        {
            ulong z = (ulong)(seed ^ 0x3F1A29C5L) + (ulong)cycle * 0x9E3779B97F4A7C15UL
                    + (ulong)(uint)(slot * 7919) * 0xD1B54A32D192ED03UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (long)(z ^ (z >> 31));
        }
    }
}
