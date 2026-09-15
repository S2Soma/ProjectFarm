using System.Collections.Generic;

namespace LQFarm
{
    /// <summary>Gift codes: typed in Menu ▸ Nhập code, each redeemable once per farm.
    ///
    /// The table ships inside the game, so anyone who unpacks the build can read it — these are
    /// for testers and events, not for anything that must stay secret. A server-side check can
    /// replace <see cref="Find"/> later without touching the panel or the save (redeemed codes
    /// are already stored per farm, in <c>social.redeemed</c>).</summary>
    public static class GiftCodes
    {
        public class Gift
        {
            public string code;
            public long xp, coin;
            /// <summary>Free pet eggs (<see cref="PlayerState.petFreeEggs"/>). Added to the tester kit
            /// after it shipped, so a farm that already redeemed the code still gets them once —
            /// see <see cref="Redeem(PlayerState, string, out Gift, out bool)"/>.</summary>
            public int eggs;
            public string note;
        }

        static readonly Gift[] All =
        {
            // tester kit: enough XP and coins to walk through every level, island and shop shelf
            // 300 eggs: enough to find all six pets and push most of them to level 5
            new Gift { code = "TONGDAIYUMMY", xp = 22_000_000L, coin = 22_000_000_000L, eggs = 300, note = "Quà thử nghiệm" },
        };

        public enum Result { Ok, Empty, Unknown, AlreadyUsed }

        /// <summary>Codes are matched without case and without spaces or dashes: a tester reading one
        /// off a chat message types "tongdai yummy" as often as "TONGDAIYUMMY".</summary>
        public static string Normalise(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        public static Gift Find(string input)
        {
            string key = Normalise(input);
            foreach (var g in All) if (g.code == key) return g;
            return null;
        }

        /// <summary>Check and, when valid, apply the gift to <paramref name="s"/>. Nothing changes
        /// unless the result is <see cref="Result.Ok"/>.</summary>
        public static Result Redeem(PlayerState s, string input, out Gift gift)
        {
            return Redeem(s, input, out gift, out _);
        }

        /// <summary>The same, and says whether this was only the part added to the code later (the
        /// eggs): <paramref name="onlyAdded"/> is true for a farm that had redeemed the code before
        /// it carried eggs, and then only the eggs are given — the XP and coins are not paid twice.
        /// The marker "CODE+eggs" cannot be typed as a code, since codes are letters and digits.</summary>
        public static Result Redeem(PlayerState s, string input, out Gift gift, out bool onlyAdded)
        {
            gift = null;
            onlyAdded = false;
            string key = Normalise(input);
            if (key.Length == 0) return Result.Empty;
            gift = Find(key);
            if (gift == null) return Result.Unknown;

            bool had = s.redeemedCodes.Contains(gift.code);
            string eggKey = gift.code + "+eggs";
            bool hadEggs = gift.eggs <= 0 || s.redeemedCodes.Contains(eggKey);
            if (had && hadEggs) return Result.AlreadyUsed;

            if (!had)
            {
                s.redeemedCodes.Add(gift.code);
                s.AddXp(gift.xp);
                s.AddCoin(gift.coin);
            }
            if (!hadEggs)
            {
                s.redeemedCodes.Add(eggKey);
                s.petFreeEggs += gift.eggs;
            }
            onlyAdded = had;
            return Result.Ok;
        }
    }
}
