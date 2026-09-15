using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the rules behind the map and the planting flow: quick-verb unlock levels,
    /// the seed sheet's ordering, cloud bridge geometry, island paging and weather looks.
    /// Pure data and maths — the visuals are checked by the screenshot pass.</summary>
    public static class FarmFlowTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra bản đồ & gieo trồng")]
        public static void Run()
        {
            var fails = new List<string>();

            QuickVerbsUnlockInOrder(fails);
            SeedSheetOrder(fails);
            BridgesSpanTheGap(fails);
            WeatherLooksDiffer(fails);

            if (fails.Count == 0) Debug.Log("Bản đồ & gieo trồng OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Bản đồ: " + f);
                Debug.LogError($"Bản đồ: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        /// <summary>Harvest, then plant, then water — and all before the second island, so a
        /// player never manages several farms plot by plot.</summary>
        static void QuickVerbsUnlockInOrder(List<string> fails)
        {
            Check(fails, QuickActions.HarvestLevel < QuickActions.PlantLevel, "gieo nhanh mở trước thu hoạch nhanh");
            Check(fails, QuickActions.PlantLevel < QuickActions.WaterLevel, "tưới nhanh mở trước gieo nhanh");
            Check(fails, QuickActions.WaterLevel <= IslandSys.Def(1).lv + 5,
                  $"tưới nhanh mở ở cấp {QuickActions.WaterLevel}, quá xa sau khi có đảo thứ hai");

            var s = new PlayerState(); s.NewGame();
            for (int lv = 1; lv <= 12; lv++)
            {
                s.lv = lv;
                Check(fails, QuickActions.HarvestUnlocked(s) == (lv >= QuickActions.HarvestLevel), $"cấp {lv}: thu hoạch nhanh sai");
                Check(fails, QuickActions.PlantUnlocked(s) == (lv >= QuickActions.PlantLevel), $"cấp {lv}: gieo nhanh sai");
                Check(fails, QuickActions.WaterUnlocked(s) == (lv >= QuickActions.WaterLevel), $"cấp {lv}: tưới nhanh sai");
                string name = QuickActions.UnlockedAt(lv);
                bool exact = lv == QuickActions.HarvestLevel || lv == QuickActions.PlantLevel || lv == QuickActions.WaterLevel;
                Check(fails, (name != null) == exact, $"cấp {lv}: thông báo mở khoá sai");
            }
        }

        /// <summary>Owned seeds first, then the buyable catalogue; never a locked seed.</summary>
        static void SeedSheetOrder(List<string> fails)
        {
            var s = new PlayerState(); s.NewGame();
            s.lv = 9;
            s.seeds.Clear();
            s.seeds["wheat"] = 3; s.seeds["corn"] = 1;

            var order = SeedSheet.Order(s);
            Check(fails, order.Count > 2, "thẻ hạt giống quá ít");
            bool seenUnowned = false;
            foreach (var seed in order)
            {
                Check(fails, seed.lv <= s.lv, $"{seed.name} (cấp {seed.lv}) hiện dù chưa mở khoá");
                s.seeds.TryGetValue(seed.id, out int n);
                if (n == 0) seenUnowned = true;
                else Check(fails, !seenUnowned, $"{seed.name} đang có trong túi nhưng xếp sau hạt chưa có");
            }
        }

        /// <summary>Every bridge must start on the island before and end on the next one, with a
        /// real gap between — a negative span would draw the bridge backwards across an island.</summary>
        static void BridgesSpanTheGap(List<string> fails)
        {
            for (int i = 1; i < IslandSys.Max; i++)
            {
                var a = ArchipelagoView.IslandOrigin(i - 1);
                var b = ArchipelagoView.IslandOrigin(i);
                float gapStart = a.x + IslandView.BridgeLandX, gapEnd = b.x - IslandView.BridgeLandX;
                Check(fails, gapEnd - gapStart > 150f, $"cầu {i - 1}→{i} chỉ dài {gapEnd - gapStart:0}");
                Check(fails, Mathf.Abs(b.y - a.y) < 120f, $"cầu {i - 1}→{i} dốc quá ({b.y - a.y:0})");
            }

            // The bridge's ends must clear the gate lantern, and its deck must fit on the lawn where
            // it lands — or the cushion sits on the lantern and the deck hangs off the island's tip.
            float land = IslandView.BridgeLandX;
            Check(fails, land - IslandView.LanternOuterX >= 8f,
                  $"cầu cập sát đèn lồng ({land - IslandView.LanternOuterX:0.0} đơn vị)");
            float deckHalf = ArchipelagoView.BridgeDeckHalf;
            Check(fails, IslandView.RimDistance(new Vector2(land, deckHalf)) < 0f && IslandView.RimDistance(new Vector2(land, -deckHalf)) < 0f,
                  "mặt cầu rộng hơn bãi cỏ ở chỗ cập đảo");
            float postX = land + ArchipelagoView.BridgePostOffset;
            Check(fails, postX - ArchipelagoView.BridgePostHalfWidth >= IslandView.LanternOuterX, "cọc đầu cầu đè lên đèn lồng cổng");
            // the corner posts are planted on the lawn, not in the air past the island's tip
            Check(fails, IslandView.RimDistance(new Vector2(postX, deckHalf + 2f)) < 0f && IslandView.RimDistance(new Vector2(postX, -deckHalf - 2f)) < 0f,
                  "cọc đầu cầu cắm ra ngoài bãi cỏ");
        }

        static void WeatherLooksDiffer(List<string> fails)
        {
            var seen = new HashSet<string>();
            foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
            {
                ArchipelagoView.LandLook(w, out var tint, out float snow);
                Check(fails, (snow > 0f) == (w == Weather.Snow), $"{w}: tuyết phủ sai ({snow})");
                seen.Add(tint.ToString() + snow);
            }
            Check(fails, seen.Count == System.Enum.GetValues(typeof(Weather)).Length,
                  "có hai kiểu thời tiết trông giống hệt nhau trên đảo");
        }
    }
}
