using UnityEngine;

namespace LQFarm
{
    public enum Weather : byte
    {
        Sunny,      // Nắng
        Rain,       // Mưa
        Wind,       // Gió Lớn
        Snow,       // Tuyết
        Storm,      // Bão
        Drought,    // Hạn Hán
    }

    public struct WeatherDef
    {
        public Weather id;
        public string name;
        public float weight;
        /// <summary>Multiplier on grow duration. Below 1 is faster.</summary>
        public float grow;
        public float sell, xp, mutate;
        /// <summary>Multiplier on what one watering takes off. Drought's 2.0 is the whole reason
        /// the worst weather is survivable.</summary>
        public float waterCut;
        public string hex;
        public string note;
    }

    /// <summary>Weather: one of six states, changing every <see cref="WeatherSys.SlotMs"/> (15 minutes),
    /// the same for every player.
    ///
    /// Two rules carry the whole design.
    ///
    /// **It is a pure function of (worldSeed, slot).** Nothing is stored, nothing is scheduled,
    /// and no catch-up pass exists — a client that has been closed for a week computes the same
    /// history as one that was open. That is also what lets a server hand out a seed later and
    /// have every client agree without a sync.
    ///
    /// **Multipliers snapshot at PLANT time and are stamped on the plot.** Nothing is ever
    /// revalued afterwards. This is the most important rule here: it makes weather structurally
    /// incapable of taking value away from a player who was offline, and it keeps elapsed growth
    /// a subtraction rather than an integral over the hours the app was closed. The alternative —
    /// pricing at harvest — reads to a player as "I slept and my crop lost value", and pushes
    /// them to time harvests, which is exactly wrong for a game of two-minute sessions.
    ///
    /// Long crops now outlast a window (the longest grows 24 hours against 15 minutes), and that is fine
    /// because of the snapshot: the weather it was planted in is the weather it is paid in.</summary>
    public static class WeatherSys
    {
        /// <summary>Length of one weather window: fifteen minutes (it was an hour). Weather is the
        /// live variable on screen, and an hour between changes was long enough for a player to
        /// stop looking at it.</summary>
        public const long SlotMs = 15L * 60_000L;

        public static readonly WeatherDef[] All =
        {
            //                              w      grow  sell   xp    mut   water
            new WeatherDef { id = Weather.Sunny,   name = "Nắng",     weight = 30f,
                grow = 1.00f, sell = 1.00f, xp = 1.00f, mutate = 1.00f, waterCut = 1f, hex = "#F5A524",
                note = "Hạt giống rẻ hơn 10%" },
            new WeatherDef { id = Weather.Rain,    name = "Mưa",      weight = 22f,
                grow = 0.80f, sell = 0.95f, xp = 0.95f, mutate = 1.10f, waterCut = 1f, hex = "#3E9BD8",
                note = "Trời tự tưới mỗi khi tới cữ" },
            new WeatherDef { id = Weather.Wind,    name = "Gió Lớn",  weight = 16f,
                grow = 0.85f, sell = 0.90f, xp = 1.20f, mutate = 0.90f, waterCut = 1f, hex = "#37B5A6",
                note = "8% cơ hội tự gieo lại khi thu hoạch" },
            new WeatherDef { id = Weather.Snow,    name = "Tuyết",    weight = 12f,
                grow = 1.25f, sell = 1.30f, xp = 0.90f, mutate = 1.35f, waterCut = 1f, hex = "#8FD8FF",
                note = "Chậm mà được giá, hợp gieo cây dài" },
            new WeatherDef { id = Weather.Storm,   name = "Bão",      weight = 10f,
                grow = 1.15f, sell = 0.80f, xp = 1.40f, mutate = 1.60f, waterCut = 1f, hex = "#8E64D8",
                note = "Chỉ làm chậm, trời tự tưới khi tới cữ" },
            new WeatherDef { id = Weather.Drought, name = "Hạn Hán",  weight = 10f,
                grow = 1.30f, sell = 1.15f, xp = 0.80f, mutate = 0.70f, waterCut = 2f, hex = "#E2574C",
                note = "Tưới nước hiệu quả gấp đôi" },
        };

        public static WeatherDef Def(Weather w) { return All[(int)w]; }

        /// <summary>A weather with its PENALTIES removed and its bonuses left alone.
        ///
        /// This is what the greenhouse actually does, and getting it wrong would have made the
        /// most expensive item on the shelf a trap. The first version simply swapped the hour for
        /// Nắng — but only Nắng is neutral; every other state is MIXED. Bão is +40% xp and +60%
        /// mutation alongside its −20% price, so "shelter" would have thrown away the best
        /// mutation odds in the game to dodge a price cut. Gió Lớn grows 15% FASTER.
        ///
        /// Flooring each axis in the player's favour is strictly better than the raw hour, which
        /// is the correct shape for something bought with coins.</summary>
        public static WeatherDef Sheltered(Weather w)
        {
            var d = All[(int)w];
            d.grow = Mathf.Min(d.grow, 1f);       // never slower
            d.sell = Mathf.Max(d.sell, 1f);       // never cheaper
            d.xp = Mathf.Max(d.xp, 1f);
            d.mutate = Mathf.Max(d.mutate, 1f);
            return d;                              // WeatherDef is a struct: All is untouched
        }

        /// <summary>Whether shelter would change anything. The greenhouse must not spend a charge
        /// on an hour it cannot improve.</summary>
        public static bool IsHarmful(Weather w)
        {
            var d = All[(int)w];
            return d.grow > 1f || d.sell < 1f || d.xp < 1f || d.mutate < 1f;
        }

        /// <summary>Drought is the only strictly-punishing state, so it is withheld until the
        /// player has a watering rhythm to defend themselves with.</summary>
        const int DroughtMinLevel = 8;

        /// <summary>No storm and no drought in an account's first day. A new player meeting the
        /// worst weather before they understand the good weather has no frame of reference.</summary>
        const long GraceMs = 24L * 3_600_000L;

        public static long SlotIndex(long nowMs) { return nowMs / SlotMs; }
        public static long SlotStart(long slot) { return slot * SlotMs; }
        public static long SlotEnd(long slot) { return (slot + 1) * SlotMs; }

        /// <summary>Milliseconds until the current window ends. The single most important number
        /// on screen: the multipliers are a static table a player memorises in a week, but this
        /// is the live variable, and it drives the only decision weather actually creates —
        /// plant now at these numbers, or wait for the reroll.</summary>
        public static long MsLeft(long nowMs) { return SlotEnd(SlotIndex(nowMs)) - nowMs; }

        /// <summary>The weather in a given window (<see cref="SlotIndex"/>). Pure; safe for any window, past or future.</summary>
        public static Weather At(PlayerState s, long hour)
        {
            if (s == null) return Weather.Sunny;
            return At(s.worldSeed, hour, s.lv, s.createdAt);
        }

        /// <summary>How far back the chain is rebuilt. The rule only looks one hour back, but
        /// "one hour back" has to mean the weather that was actually SHOWN then, not an
        /// unconstrained draw for that hour — those are different values, and using the second
        /// silently breaks the rule. A first version did exactly that and produced
        /// "Gió Lớn · Gió Lớn" back to back.
        ///
        /// Replaying a fixed window is the cheap fix: the chain forgets its own history within a
        /// few steps (any hour whose draw is already legal resets it), so 24 is far more than
        /// enough for the result to be stable, and it stays a pure function of (seed, hour).</summary>
        const int ChainDepth = 24;

        public static Weather At(long seed, long hour, int level, long createdAt)
        {
            var prev = (Weather)255;                       // nothing forbidden at the start
            for (long h = hour - ChainDepth; h <= hour; h++)
            {
                bool grace = createdAt > 0 && SlotStart(h) < createdAt + GraceMs;
                prev = Pick(seed, h, level, grace, prev, h > hour - ChainDepth);
            }
            return prev;
        }

        static Weather Pick(long seed, long hour, int level, bool grace, Weather prev, bool useHistory)
        {
            float total = 0f;
            System.Span<float> w = stackalloc float[All.Length];

            for (int i = 0; i < All.Length; i++)
            {
                var d = All[i];
                float weight = d.weight;

                if (d.id == Weather.Drought && (level < DroughtMinLevel || grace)) weight = 0f;
                if (d.id == Weather.Storm && grace) weight = 0f;

                if (useHistory)
                {
                    // A weather never immediately repeats: an hour that changes nothing is an
                    // hour with no reason to open the app.
                    if (d.id == prev) weight = 0f;
                    // Storm and drought never follow each other. Single bad windows are survivable;
                    // two in a row is what makes a player stop coming back, and the whole
                    // distribution is skewed around that asymmetry.
                    if ((d.id == Weather.Storm && prev == Weather.Drought) ||
                        (d.id == Weather.Drought && prev == Weather.Storm)) weight = 0f;
                }

                w[i] = weight;
                total += weight;
            }

            if (total <= 0f) return Weather.Sunny;

            float r = Frac(Mix(seed ^ 0x5D1B3A7CL, hour)) * total;
            for (int i = 0; i < All.Length; i++)
            {
                r -= w[i];
                if (r < 0f) return All[i].id;
            }
            return Weather.Sunny;
        }

        public static Weather Now(PlayerState s) { return At(s, SlotIndex(GS.Now)); }
        public static Weather Next(PlayerState s) { return At(s, SlotIndex(GS.Now) + 1); }

        /// <summary>The next window is revealed only near the end of the current one: the last five
        /// minutes, a third of the window. Seeing further would let a player plan the whole day and
        /// stop opening the app; a short notice creates a decision ("hold the seeds, storm is coming").</summary>
        public const long RevealMs = 5L * 60_000L;
        public static bool NextRevealed(long nowMs) { return MsLeft(nowMs) <= RevealMs; }

        /// <summary>Whether the player can see the next window: normally only in its last five
        /// minutes, always while a forecast from the shop is running.</summary>
        public static bool Revealed(PlayerState s, long nowMs)
        {
            return NextRevealed(nowMs) || ShopSys.ForecastActive(s);
        }

        // ---- deterministic hashing (SplitMix64) ----
        static long Mix(long seed, long n)
        {
            ulong z = (ulong)seed + (ulong)n * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (long)(z ^ (z >> 31));
        }

        /// <summary>Hash to [0,1).</summary>
        public static float Frac(long h)
        {
            return ((ulong)h >> 11) * (1f / 9007199254740992f);
        }
    }
}
