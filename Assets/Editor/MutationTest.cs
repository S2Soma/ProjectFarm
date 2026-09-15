using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the mutation tiers and the yield rule.
    ///
    /// The one that would hurt most if it broke is the no-double-dip rule. It is invisible in
    /// normal play — every harvest just pays "some coins" — and the failure shows up weeks later
    /// as an economy nobody can price, so it is worth an explicit assertion rather than trust.</summary>
    public static class MutationTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra đột biến & số quả")]
        public static void Run()
        {
            var fails = new List<string>();

            TierTableIsOrdered(fails);
            WeightsSumToOne(fails);
            NoDoubleDip(fails);
            YieldRises(fails);
            GrowCapHolds(fails);
            PerFruitPricing(fails);
            PityFires(fails);
            RatesAreSane(fails);
            CollectionCovers(fails);

            if (fails.Count == 0) Debug.Log("Đột biến & số quả OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Đột biến: " + f);
                Debug.LogError($"Đột biến: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        /// <summary>Every axis must rise with the tier, or a "better" mutation would be worse.</summary>
        static void TierTableIsOrdered(List<string> fails)
        {
            for (int v = 2; v < Art.Elements.Length; v++)
            {
                var a = Art.Elements[v - 1];
                var b = Art.Elements[v];
                Check(fails, b.sell > a.sell, $"bậc {v}: giá không cao hơn bậc {v - 1}");
                Check(fails, b.xp > a.xp, $"bậc {v}: xp không cao hơn bậc {v - 1}");
                Check(fails, b.grow >= a.grow, $"bậc {v}: thời gian không dài hơn bậc {v - 1}");
                Check(fails, b.yieldAdd >= a.yieldAdd, $"bậc {v}: số quả không nhiều hơn bậc {v - 1}");
                Check(fails, b.chance < a.chance, $"bậc {v}: không hiếm hơn bậc {v - 1}");
            }
        }

        static void WeightsSumToOne(List<string> fails)
        {
            float sum = 0f;
            for (int v = 1; v < Art.Elements.Length; v++) sum += Art.Elements[v].chance;
            Check(fails, Mathf.Abs(sum - 1f) < 0.001f, $"tổng trọng số bậc = {sum:0.000}, phải bằng 1");
        }

        /// <summary>Value is base x level x tier. The extra fruits must NOT multiply on top —
        /// with them a legendary avocado pays 25x instead of the 10x it advertises.</summary>
        static void NoDoubleDip(List<string> fails)
        {
            var s = new PlayerState();
            s.NewGame();
            s.lv = 30;

            int top = Art.Elements.Length - 1;
            int plain = s.HarvestValue("avocado", 0);
            int legend = s.HarvestValue("avocado", top);

            float ratio = legend / (float)plain;
            float want = Art.Elements[top].sell;
            Check(fails, Mathf.Abs(ratio - want) < 0.02f,
                  $"huyền thoại = {ratio:0.00}× cây thường, kỳ vọng {want:0.00}× (đang nhân thêm số quả?)");

            // and the per-fruit price times the fruit count must not exceed the harvest value
            int fruits = s.YieldOf(GameData.Get("avocado"), top);
            int perFruit = s.SellPrice("avocado", top);
            Check(fails, perFruit * fruits <= legend * 1.35f,
                  $"bán lẻ {fruits} quả × {perFruit} = {perFruit * fruits} vượt xa giá trị vụ {legend}");
        }

        static void YieldRises(List<string> fails)
        {
            var s = new PlayerState(); s.NewGame();
            foreach (var seed in GameData.Seeds)
            {
                Check(fails, seed.fruits >= 1, $"{seed.id}: fruits = {seed.fruits}");
                int prev = 0;
                for (int v = 0; v < Art.Elements.Length; v++)
                {
                    int y = s.YieldOf(seed, v);
                    Check(fails, y >= prev, $"{seed.id} bậc {v}: số quả giảm");
                    prev = y;
                }
            }
        }

        /// <summary>A better tier takes longer, by at most an hour, and never past the 24-hour ceiling:
        /// a good roll is never a punishment for sleeping. (Rounding to a readable duration can add up
        /// to half a 5-minute step, hence the tolerance.)</summary>
        static void GrowCapHolds(List<string> fails)
        {
            var s = new PlayerState(); s.NewGame();
            int top = Art.Elements.Length - 1;

            foreach (int lv in new[] { 1, 30 })
                foreach (var seed in GameData.Seeds)
                    foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
                    {
                        s.lv = lv;
                        float plain = s.GrowTimeIn(seed, w, 0);
                        for (int v = 1; v <= top; v++)
                        {
                            float mutated = s.GrowTimeIn(seed, w, v);
                            Check(fails, mutated >= plain, $"{seed.id}/{w} cấp {lv} bậc {v}: đột biến lại nhanh hơn cây thường");
                            Check(fails, mutated - plain <= Art.MaxMutationGrowAddSeconds + 150f,
                                  $"{seed.id}/{w} cấp {lv} bậc {v}: cộng thêm {mutated - plain:0}s, trần là {Art.MaxMutationGrowAddSeconds:0}s");
                            Check(fails, mutated <= GameData.MaxGrowSeconds, $"{seed.id}/{w} cấp {lv} bậc {v}: {mutated:0}s vượt trần 24 giờ");
                        }
                        // a short crop still shows its tier in time: a legendary tomato is not a plain one
                        if (seed.grow <= 1800 && w == Weather.Sunny)
                            Check(fails, s.GrowTimeIn(seed, w, top) > plain * 1.5f, $"{seed.id} cấp {lv}: huyền thoại gần như không lâu hơn");
                    }
        }

        /// <summary>The warehouse stocks fruits, not harvests. A six-fruit crop must not price
        /// each cherry as if it were the whole punnet.</summary>
        static void PerFruitPricing(List<string> fails)
        {
            foreach (var seed in GameData.Seeds)
            {
                int recombined = seed.PerFruit * seed.fruits;
                Check(fails, Mathf.Abs(recombined - seed.sell) <= seed.fruits,
                      $"{seed.id}: {seed.fruits}×{seed.PerFruit} = {recombined}, giá vụ là {seed.sell}");
            }
        }

        static void PityFires(List<string> fails)
        {
            var s = new PlayerState(); s.NewGame();
            s.lv = 30;
            s.sinceLegendary = PlayerState.LegendaryPity;

            int top = Art.Elements.Length - 1;
            bool got = false;
            for (int i = 0; i < 200 && !got; i++)
            {
                s.sinceLegendary = PlayerState.LegendaryPity;
                if (s.RollVariantFor("wheat", Weather.Sunny) == top) got = true;
            }
            Check(fails, got, "bảo hiểm xui không bao giờ kích hoạt");
            Check(fails, s.sinceLegendary < PlayerState.LegendaryPity, "bộ đếm bảo hiểm không reset");
        }

        /// <summary>Absolute rates, so the tiers stay a slot machine rather than a certainty or a
        /// myth. Legendary should land around 1 in 240 plantings at cap.</summary>
        static void RatesAreSane(List<string> fails)
        {
            var s = new PlayerState(); s.NewGame(); s.lv = 30;
            int top = Art.Elements.Length - 1;

            float p = s.MutateChanceFor("wheat", Weather.Sunny);
            Check(fails, p > 0.3f && p <= 0.75f, $"tỉ lệ đột biến cấp 30 = {p:P1}");

            float legendRate = p * Art.Elements[top].chance;
            float oneIn = 1f / Mathf.Max(1e-6f, legendRate);
            Check(fails, oneIn > 120f && oneIn < 600f,
                  $"huyền thoại 1/{oneIn:0} lần gieo — ngoài khoảng hợp lý 120..600");
        }

        /// <summary>Every collection cell must name a crop and a tier that can actually occur.</summary>
        static void CollectionCovers(List<string> fails)
        {
            Check(fails, GameData.CollectTotal == GameData.Seeds.Length * Art.Elements.Length,
                  $"CollectTotal = {GameData.CollectTotal}");

            foreach (var set in GameData.Collections)
                foreach (var it in set.items)
                {
                    Check(fails, GameData.Get(it.crop) != null, $"bộ {set.id}: cây lạ {it.crop}");
                    Check(fails, it.v >= 0 && it.v < Art.Elements.Length,
                          $"bộ {set.id}: bậc {it.v} không tồn tại");
                }
        }
    }
}
