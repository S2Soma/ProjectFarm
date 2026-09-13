using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>What a locked island asks for before it opens.</summary>
    public struct Tribute
    {
        public string crop;
        public int need;
        public Tribute(string crop, int need) { this.crop = crop; this.need = need; }
    }

    /// <summary>The archipelago table: names, gates, tributes, perks and plot pricing.
    ///
    /// **Tribute is crops, not coins, and it is consumed.** A coin price would be paid out of a
    /// balance the player already has, which makes an island a withdrawal rather than a project.
    /// Asking for 160 Ngô means four days of actually running the farm the game is about, and it
    /// means the fruit count on a mutation is worth something beyond its sale price — a legendary
    /// cherry hands over nine fruits toward the tribute instead of six.
    ///
    /// **Every tribute crop sits inside the six levels below the island's gate**, so it is always
    /// something the player is already growing well rather than a trip back to a crop they
    /// outgrew. That rule is asserted in the editor test, because it is the one that silently
    /// breaks whenever a crop's level changes.
    ///
    /// **Each island carries one farm-wide perk.** They are deliberately on different axes —
    /// sell, mutation, speed, XP — so the second island is not simply a bigger first one, and so
    /// a player mid-way through the archipelago can name what they are working toward.</summary>
    public class IslandDef
    {
        public int index;
        /// <summary>Eight characters at most: it is rendered inside a 208 px pager slot between
        /// two chevrons, and Vietnamese stacks two diacritics so the line box cannot shrink.</summary>
        public string name;
        public string blurb;
        public int lv;
        public int coin;
        public Tribute[] tribute;

        public string perk;
        public float sellMul = 1f;
        public float growMul = 1f;
        public float xpMul = 1f;
        public float mutateAdd = 0f;

        /// <summary>Plots handed over with the island. A brand-new island with sixteen locked
        /// plots is an empty lot, not a farm; four working plots make it somewhere to be
        /// immediately.</summary>
        public int freePlots;
        /// <summary>Scales the plot price ladder. The last plot of an island always costs about
        /// 21x its first, so "buy the next plot" stays the obvious thing to do with loose coins
        /// at every point in the game.</summary>
        public float plotMul;
    }

    public static class IslandSys
    {
        public const int Max = 6;

        public static readonly IslandDef[] Defs =
        {
            new IslandDef {
                index = 0, name = "Vườn Nhà", blurb = "Nơi mọi thứ bắt đầu",
                lv = 1, coin = 0, tribute = new Tribute[0],
                perk = null, freePlots = 6, plotMul = 1f,
            },
            new IslandDef {
                index = 1, name = "Đảo Gió", blurb = "Gió biển thổi quanh năm",
                lv = 5, coin = 60_000,
                tribute = new[] { new Tribute("corn", 160), new Tribute("tomato", 200) },
                perk = "+5% giá bán toàn trang trại", sellMul = 1.05f,
                freePlots = 4, plotMul = 7f,
            },
            new IslandDef {
                index = 2, name = "Đảo Băng", blurb = "Lạnh giá, cây mọc lạ",
                lv = 10, coin = 420_000,
                tribute = new[] { new Tribute("eggplant", 260), new Tribute("pepper", 300), new Tribute("mushroom", 320) },
                perk = "+3% tỉ lệ đột biến", mutateAdd = 0.03f,
                freePlots = 4, plotMul = 20f,
            },
            new IslandDef {
                index = 3, name = "Đảo Hoả", blurb = "Đất núi lửa, cây lớn nhanh",
                lv = 16, coin = 1_500_000,
                tribute = new[] { new Tribute("cabbage", 330), new Tribute("grape", 660), new Tribute("beetroot", 300) },
                perk = "−8% thời gian mọc", growMul = 0.92f,
                freePlots = 4, plotMul = 40f,
            },
            new IslandDef {
                index = 4, name = "Đảo Lôi", blurb = "Sấm chớp nuôi cây",
                lv = 22, coin = 4_000_000,
                tribute = new[] { new Tribute("peach", 450), new Tribute("pear", 390), new Tribute("orange", 560) },
                perk = "+10% kinh nghiệm", xpMul = 1.10f,
                freePlots = 4, plotMul = 70f,
            },
            new IslandDef {
                index = 5, name = "Đảo Vàng", blurb = "Đảo cuối, giàu nhất",
                lv = 28, coin = 9_500_000,
                tribute = new[] { new Tribute("pineapple", 360), new Tribute("watermelon", 360), new Tribute("banana", 900), new Tribute("cherries", 1080) },
                perk = "+15% giá bán toàn trang trại", sellMul = 1.15f,
                freePlots = 4, plotMul = 115f,
            },
        };

        public static IslandDef Def(int index)
        {
            return Defs[Mathf.Clamp(index, 0, Defs.Length - 1)];
        }

        public static string NameOf(int index)
        {
            return index >= 0 && index < Defs.Length ? Defs[index].name : "Đảo " + (index + 1);
        }

        // ============================================================
        // plots
        // ============================================================
        public const int PlotsPerIsland = 16;

        /// <summary>Island one is hand-tuned rather than generated. It is the only island a player
        /// sees before they have any idea what a coin is worth, so its ladder is set by what each
        /// step should FEEL like — every rung under half a day of income at the level that unlocks
        /// it — instead of by a curve that happens to be tidy.</summary>
        static readonly int[] HomeLv   = { 1, 1, 1, 1, 1, 1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16 };
        static readonly int[] HomeCoin = { 0, 0, 0, 0, 0, 0, 1200, 2200, 3600, 5800, 8800, 13000, 19000, 29000, 43000, 63000 };

        /// <summary>The ladder is indexed by HOW MANY plots the island already has open, not by
        /// which tile was tapped.
        ///
        /// Both readings are defensible and only one survives contact with the map. Pricing per
        /// TILE means the sixteen plots carry sixteen different prices at once, so the correct
        /// play is to hunt the cheap corner and the layout the player ends up with is dictated by
        /// a price table rather than by where they want their farm. Pricing per COUNT means every
        /// locked tile costs the same today, the player buys the one they actually want next, and
        /// the ladder still rises exactly as designed.</summary>
        public static int PlotLevel(int island, int owned)
        {
            if (island == 0) return HomeLv[Mathf.Clamp(owned, 0, HomeLv.Length - 1)];
            // Islands 2-6 are gated by the island itself; a second gate inside one the player
            // just paid a tribute for would read as the game taking the purchase back.
            return Def(island).lv;
        }

        public static int PlotPrice(int island, int owned)
        {
            if (island == 0) return HomeCoin[Mathf.Clamp(owned, 0, HomeCoin.Length - 1)];

            var d = Def(island);
            int k = owned - d.freePlots;         // 0-based index among the purchasable ones
            if (k < 0) return 0;
            float raw = 500f * Mathf.Pow(1.32f, k) * d.plotMul;
            return Round(raw);
        }

        /// <summary>Prices are rounded to something a player can hold in their head. 1.32^k gives
        /// numbers like 18.437; nobody compares those, and the ladder reads as noise.</summary>
        static int Round(float v)
        {
            if (v < 1000f) return Mathf.RoundToInt(v / 50f) * 50;
            if (v < 10_000f) return Mathf.RoundToInt(v / 100f) * 100;
            if (v < 100_000f) return Mathf.RoundToInt(v / 1000f) * 1000;
            return Mathf.RoundToInt(v / 10_000f) * 10_000;
        }

        /// <summary>Plots open outward from the middle so a partly-bought island always reads as
        /// a block rather than a scatter. Only the FREE plots follow this order — everything after
        /// is wherever the player chose to spend.</summary>
        public static readonly int[] FreeOrder = { 5, 6, 9, 10, 4, 7, 1, 2, 13, 14, 8, 11, 0, 3, 12, 15 };

        /// <summary>Plots an island starts with unlocked.</summary>
        public static bool StartsUnlocked(int island, int slot)
        {
            int free = Def(island).freePlots;
            for (int i = 0; i < free && i < FreeOrder.Length; i++)
                if (FreeOrder[i] == slot) return true;
            return false;
        }

        public static int OpenCount(Island isl)
        {
            int n = 0;
            for (int i = 0; i < isl.plots.Count; i++) if (!isl.plots[i].locked) n++;
            return n;
        }

        // ============================================================
        // perks — the product across every island the player owns
        // ============================================================
        public static void Perks(PlayerState s, out float sell, out float grow, out float xp, out float mutate)
        {
            sell = 1f; grow = 1f; xp = 1f; mutate = 0f;
            for (int i = 0; i < s.islands.Count && i < Defs.Length; i++)
            {
                if (!s.islands[i].unlocked) continue;
                var d = Defs[i];
                sell *= d.sellMul; grow *= d.growMul; xp *= d.xpMul; mutate += d.mutateAdd;
            }
        }

        // ============================================================
        // unlocking
        // ============================================================
        public static bool LevelMet(PlayerState s, int index) { return s.lv >= Def(index).lv; }

        public static int Paid(PlayerState s, int index, string crop)
        {
            if (index < 0 || index >= s.islands.Count) return 0;
            var t = s.islands[index].tribute;
            return t != null && t.TryGetValue(crop, out int n) ? n : 0;
        }

        /// <summary>Fraction of the tribute handed over, averaged across the crops it names.</summary>
        public static float TributeProgress(PlayerState s, int index)
        {
            var d = Def(index);
            if (d.tribute.Length == 0) return 1f;
            float sum = 0f;
            foreach (var t in d.tribute) sum += Mathf.Clamp01(Paid(s, index, t.crop) / (float)t.need);
            return sum / d.tribute.Length;
        }

        public static bool TributeDone(PlayerState s, int index)
        {
            foreach (var t in Def(index).tribute)
                if (Paid(s, index, t.crop) < t.need) return false;
            return true;
        }

        /// <summary>How many of a crop the player could hand over right now, given the warehouse
        /// and what is still outstanding.</summary>
        public static int Payable(PlayerState s, int index, string crop)
        {
            var d = Def(index);
            int need = 0;
            foreach (var t in d.tribute) if (t.crop == crop) need = t.need;
            if (need == 0) return 0;
            int left = need - Paid(s, index, crop);
            if (left <= 0) return 0;
            return Mathf.Min(left, s.StockOf(crop));
        }

        /// <summary>Hand crops over. They are consumed on the way in — this is the point of the
        /// whole system, so it is deliberately not refundable.</summary>
        public static int Pay(PlayerState s, int index, string crop, int qty)
        {
            qty = Mathf.Min(qty, Payable(s, index, crop));
            if (qty <= 0) return 0;
            s.TakeStock(crop, qty);
            var isl = s.islands[index];
            if (isl.tribute == null) isl.tribute = new Dictionary<string, int>();
            isl.tribute.TryGetValue(crop, out int had);
            isl.tribute[crop] = had + qty;
            return qty;
        }

        public static bool CanClaim(PlayerState s, int index)
        {
            if (index <= 0 || index >= Defs.Length) return false;
            if (index < s.islands.Count && s.islands[index].unlocked) return false;
            if (index > s.islands.Count) return false;             // islands open in order
            return LevelMet(s, index) && TributeDone(s, index) && s.coin >= Def(index).coin;
        }

        public static bool Claim(PlayerState s, int index)
        {
            if (!CanClaim(s, index)) return false;
            s.AddCoin(-Def(index).coin);
            s.EnsureIsland(index);
            var isl = s.islands[index];
            isl.unlocked = true;
            isl.unlockedAt = GS.Now;
            return true;
        }

        /// <summary>The island the player is currently working toward, or -1 when the archipelago
        /// is complete. Islands open in order, so this is always the first locked one.</summary>
        public static int NextLocked(PlayerState s)
        {
            for (int i = 1; i < Defs.Length; i++)
                if (i >= s.islands.Count || !s.islands[i].unlocked) return i;
            return -1;
        }
    }
}
