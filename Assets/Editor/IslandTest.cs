using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the archipelago: gates, tributes, plot ladder and perks.
    ///
    /// Almost everything here is a relationship BETWEEN two tables — the tribute table and the
    /// crop table, the plot ladder and the level table — and nothing in the game reads those
    /// together at runtime. So when someone moves a crop's unlock level, the only thing that
    /// notices is this file.</summary>
    public static class IslandTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra đảo & ô đất")]
        public static void Run()
        {
            var fails = new List<string>();

            TableIsOrdered(fails);
            NamesFitThePager(fails);
            TributeCropsExistAndAreCurrent(fails);
            TributeIsReachable(fails);
            PlotLadderRises(fails);
            PlotLadderIsAffordable(fails);
            FreePlotsAreDistinct(fails);
            PayConsumesCheapestFirst(fails);
            PayNeverOverfills(fails);
            UnlockIsOrdered(fails);
            PerksCompound(fails);
            BedsTouchInStraightRows(fails);

            if (fails.Count == 0) Debug.Log("Đảo & ô đất OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Đảo: " + f);
                Debug.LogError($"Đảo: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        static PlayerState Fresh(int lv = 1)
        {
            var s = new PlayerState();
            s.NewGame();
            s.lv = lv;
            s.SyncPlots();
            return s;
        }

        static void TableIsOrdered(List<string> fails)
        {
            Check(fails, IslandSys.Defs.Length == IslandSys.Max,
                  $"bảng có {IslandSys.Defs.Length} đảo, Max = {IslandSys.Max}");

            for (int i = 1; i < IslandSys.Defs.Length; i++)
            {
                var a = IslandSys.Defs[i - 1];
                var b = IslandSys.Defs[i];
                Check(fails, b.lv > a.lv, $"{b.name}: cấp mở không cao hơn {a.name}");
                Check(fails, b.coin > a.coin, $"{b.name}: giá không cao hơn {a.name}");
                Check(fails, b.plotMul > a.plotMul, $"{b.name}: giá ô đất không cao hơn {a.name}");
                Check(fails, !string.IsNullOrEmpty(b.perk), $"{b.name}: không có đặc quyền");
            }
            Check(fails, IslandSys.Defs[0].freePlots == 6, "Vườn Nhà không tặng 6 ô");
            for (int i = 1; i < IslandSys.Defs.Length; i++)
                Check(fails, IslandSys.Defs[i].freePlots == 4,
                      $"{IslandSys.Defs[i].name}: tặng {IslandSys.Defs[i].freePlots} ô, thiết kế là 4");
        }

        /// <summary>The pager slot is 208 px wide with a chevron at each end. Eight characters is
        /// what fits; the ninth does not fail loudly, it just slides under the arrow.</summary>
        static void NamesFitThePager(List<string> fails)
        {
            foreach (var d in IslandSys.Defs)
                Check(fails, d.name.Length <= 8, $"tên '{d.name}' dài {d.name.Length} ký tự, trần là 8");
        }

        /// <summary>Every tribute crop must exist, and must sit within the six levels below the
        /// island's own gate — the rule that keeps tribute something the player is already
        /// growing rather than a trip back to a crop they outgrew.</summary>
        static void TributeCropsExistAndAreCurrent(List<string> fails)
        {
            for (int i = 1; i < IslandSys.Defs.Length; i++)
            {
                var d = IslandSys.Defs[i];
                // REDESIGN.md contradicts itself here: the prose says "2 loại cho đảo 2-3, 3 cho
                // 4-5, 4 cho 6" while its own table gives Đảo Băng three crops. The table is the
                // spec that the tribute quantities were balanced against, so the table wins and
                // the invariant asserted is the one both readings agree on — between two and four
                // crops, never fewer than the island before.
                Check(fails, d.tribute.Length >= 2 && d.tribute.Length <= 4,
                      $"{d.name}: {d.tribute.Length} loại cây, ngoài khoảng 2..4");
                if (i > 1)
                    Check(fails, d.tribute.Length >= IslandSys.Defs[i - 1].tribute.Length,
                          $"{d.name}: ít loại cây hơn {IslandSys.Defs[i - 1].name}");

                foreach (var t in d.tribute)
                {
                    var seed = GameData.Get(t.crop);
                    if (seed == null) { fails.Add($"{d.name}: cây lạ '{t.crop}'"); continue; }
                    Check(fails, seed.lv <= d.lv,
                          $"{d.name}: {seed.name} mở ở cấp {seed.lv}, sau cả cấp mở đảo ({d.lv})");
                    Check(fails, seed.lv >= d.lv - 6,
                          $"{d.name}: {seed.name} (cấp {seed.lv}) cách quá xa cấp mở đảo {d.lv}");
                    Check(fails, t.need > 0, $"{d.name}: {seed.name} yêu cầu 0");
                }
            }
        }

        /// <summary>A tribute nobody can finish is a wall, not a goal. Measured against what the
        /// farm can actually produce: sixteen plots, running the crop, for a day.</summary>
        static void TributeIsReachable(List<string> fails)
        {
            for (int i = 1; i < IslandSys.Defs.Length; i++)
            {
                var d = IslandSys.Defs[i];
                var s = Fresh(d.lv);
                foreach (var t in d.tribute)
                {
                    var seed = GameData.Get(t.crop);
                    if (seed == null) continue;
                    float grow = s.GrowTime(seed);
                    // 16 plots, 8 hours of active play at this level
                    float cycles = 8f * 3600f / Mathf.Max(1f, grow);
                    float perDay = cycles * 16f * s.YieldOf(seed, 0);
                    Check(fails, t.need <= perDay,
                          $"{d.name}: cần {t.need} {seed.name} nhưng một ngày cày chỉ ra {perDay:0}");
                }
            }
        }

        static void PlotLadderRises(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
            {
                int prevPrice = -1, prevLv = 0;
                for (int k = 0; k < IslandSys.PlotsPerIsland; k++)
                {
                    int price = IslandSys.PlotPrice(isl, k);
                    int lv = IslandSys.PlotLevel(isl, k);
                    Check(fails, price >= prevPrice, $"đảo {isl} ô {k}: giá {price} thấp hơn ô trước");
                    Check(fails, lv >= prevLv, $"đảo {isl} ô {k}: cấp yêu cầu giảm");
                    prevPrice = price; prevLv = lv;
                }

                int free = IslandSys.Def(isl).freePlots;
                Check(fails, IslandSys.PlotPrice(isl, free - 1) == 0,
                      $"đảo {isl}: ô tặng thứ {free} lại có giá");
                Check(fails, IslandSys.PlotPrice(isl, free) > 0,
                      $"đảo {isl}: ô đầu tiên phải mua lại miễn phí");

                int first = IslandSys.PlotPrice(isl, free);
                int last = IslandSys.PlotPrice(isl, IslandSys.PlotsPerIsland - 1);
                float ratio = last / (float)Mathf.Max(1, first);
                Check(fails, ratio > 10f && ratio < 60f,
                      $"đảo {isl}: ô cuối đắt gấp {ratio:0.0} lần ô đầu — ngoài khoảng 10..60");
            }
        }

        /// <summary>Every plot must be under a day of income at the level it becomes buyable, or
        /// "mua ô tiếp theo" stops being the obvious thing to do with loose coins — which is the
        /// only thing making levelling worth anything.</summary>
        static void PlotLadderIsAffordable(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
                for (int k = IslandSys.Def(isl).freePlots; k < IslandSys.PlotsPerIsland; k++)
                {
                    int lv = IslandSys.PlotLevel(isl, k);
                    var s = Fresh(lv);

                    // Income is MEASURED, not assumed. The first version of this test guessed
                    // "60 cycles a day" and failed four islands by 5% — the guess was wrong, not
                    // the prices. Cycle time comes from the crop the player would actually grow.
                    float bestIncome = 0f;
                    foreach (var seed in GameData.Seeds)
                    {
                        if (seed.lv > lv) continue;
                        int margin = s.HarvestValue(seed.id, 0) - seed.price;
                        if (margin <= 0) continue;
                        float grow = s.GrowTime(seed);
                        float cycles = 8f * 3600f / Mathf.Max(1f, grow);   // 8 hours of play
                        float income = cycles * 16f * margin;              // 16 plots
                        if (income > bestIncome) bestIncome = income;
                    }

                    int price = IslandSys.PlotPrice(isl, k);
                    Check(fails, price <= bestIncome,
                          $"đảo {isl} ô {k}: {price} xu ở cấp {lv}, quá một ngày thu nhập ({bestIncome:0})");
                }
        }

        static void FreePlotsAreDistinct(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
            {
                var seen = new HashSet<int>();
                int n = 0;
                for (int slot = 0; slot < IslandSys.PlotsPerIsland; slot++)
                    if (IslandSys.StartsUnlocked(isl, slot)) { n++; seen.Add(slot); }
                Check(fails, n == IslandSys.Def(isl).freePlots,
                      $"đảo {isl}: {n} ô mở sẵn, bảng ghi {IslandSys.Def(isl).freePlots}");
                Check(fails, seen.Count == n, $"đảo {isl}: ô mở sẵn bị trùng");
            }
        }

        /// <summary>Paying tribute must spend plain crops before mutations. Taking the best tier
        /// first would quietly feed a player's legendaries into a quota that counts them the
        /// same — the worst trade in the game, made on their behalf.</summary>
        static void PayConsumesCheapestFirst(List<string> fails)
        {
            var s = Fresh(10);
            int top = Art.Elements.Length - 1;
            s.AddProduce("corn", 0, 50);
            s.AddProduce("corn", top, 5);

            IslandSys.Pay(s, 1, "corn", 50);

            s.store.TryGetValue("corn:" + top, out int legendsLeft);
            s.store.TryGetValue("corn:0", out int plainLeft);
            Check(fails, legendsLeft == 5, $"còn {legendsLeft}/5 quả huyền thoại sau khi nộp — đã tiêu mất");
            Check(fails, plainLeft == 0, $"còn {plainLeft} quả thường, đáng lẽ tiêu hết trước");
        }

        static void PayNeverOverfills(List<string> fails)
        {
            var s = Fresh(10);
            var t = IslandSys.Def(1).tribute[0];
            s.AddProduce(t.crop, 0, t.need * 3);

            IslandSys.Pay(s, 1, t.crop, t.need * 3);
            Check(fails, IslandSys.Paid(s, 1, t.crop) == t.need,
                  $"nộp {IslandSys.Paid(s, 1, t.crop)}/{t.need} — vượt quá yêu cầu");
            Check(fails, s.StockOf(t.crop) == t.need * 2,
                  "phần dư bị tiêu mất dù không cần tới");

            Check(fails, IslandSys.Payable(s, 1, t.crop) == 0, "vẫn nộp thêm được sau khi đủ");
        }

        static void UnlockIsOrdered(List<string> fails)
        {
            var s = Fresh(30);
            s.coin = 100_000_000;

            Check(fails, !IslandSys.CanClaim(s, 2), "mở được đảo 3 khi chưa mở đảo 2");

            // pay island 2 in full
            foreach (var t in IslandSys.Def(1).tribute) { s.AddProduce(t.crop, 0, t.need); IslandSys.Pay(s, 1, t.crop, t.need); }
            Check(fails, IslandSys.CanClaim(s, 1), "đã đủ mọi điều kiện nhưng không mở được đảo 2");
            Check(fails, IslandSys.Claim(s, 1), "Claim thất bại dù CanClaim đúng");
            Check(fails, s.islands[1].unlocked, "đảo 2 không được đánh dấu đã mở");
            Check(fails, !IslandSys.CanClaim(s, 1), "mở lại được đảo đã mở");

            s.SyncPlots();
            Check(fails, IslandSys.OpenCount(s.islands[1]) == 4,
                  $"đảo mới có {IslandSys.OpenCount(s.islands[1])} ô mở, thiết kế là 4");

            Check(fails, IslandSys.NextLocked(s) == 2, "đảo kế tiếp tính sai sau khi mở");
        }

        /// <summary>The field is one tilled patch: every bed touches its four neighbours along a
        /// full edge, no two beds overlap, and each row is a straight line.
        ///
        /// Two equal diamonds centred d apart overlap iff |dx|/(2a) + |dy|/(2b) &lt; 1, and share
        /// an edge iff it equals exactly 1 with both terms non-zero. Asserted because the grid used
        /// to be spread out with grass gaps and the "fix" for any future art that overlaps will be
        /// the tempting one — widening the gaps again.</summary>
        static void BedsTouchInStraightRows(List<string> fails)
        {
            float a = IslandView.TW * 0.5f * IslandView.PlotScale;
            float b = IslandView.TH * 0.5f * IslandView.PlotScale;
            const float eps = 0.01f;

            for (int i = 0; i < GS.PlotCount; i++)
                for (int j = i + 1; j < GS.PlotCount; j++)
                {
                    var d = IslandView.CellPos(j) - IslandView.CellPos(i);
                    float k = Mathf.Abs(d.x) / (2f * a) + Mathf.Abs(d.y) / (2f * b);
                    Check(fails, k >= 1f - eps, $"ô {i} và ô {j} chồng lên nhau (k = {k:0.000})");

                    int ri = i / 4, ci = i % 4, rj = j / 4, cj = j % 4;
                    bool neighbour = Mathf.Abs(ri - rj) + Mathf.Abs(ci - cj) == 1;
                    if (neighbour)
                        Check(fails, Mathf.Abs(k - 1f) < eps,
                              $"ô {i} và ô {j} kề nhau nhưng không khít (k = {k:0.000}, 1 là khít)");
                }

            // straight rows and columns: one constant step along each axis
            var stepC = IslandView.CellPos(1) - IslandView.CellPos(0);
            var stepR = IslandView.CellPos(4) - IslandView.CellPos(0);
            for (int i = 0; i < GS.PlotCount; i++)
            {
                int r = i / 4, c = i % 4;
                var want = IslandView.CellPos(0) + stepC * c + stepR * r;
                Check(fails, (IslandView.CellPos(i) - want).sqrMagnitude < eps,
                      $"ô {i} lệch khỏi hàng thẳng ({IslandView.CellPos(i)} thay vì {want})");
            }
        }

        static void PerksCompound(List<string> fails)
        {
            var s = Fresh(30);
            IslandSys.Perks(s, out float sell0, out _, out _, out _);
            Check(fails, Mathf.Abs(sell0 - 1f) < 0.001f, $"chưa mở đảo nào mà đã có ×{sell0:0.00} giá bán");

            s.EnsureIsland(1).unlocked = true;
            s.EnsureIsland(5).unlocked = true;
            IslandSys.Perks(s, out float sell, out _, out _, out _);
            float want = IslandSys.Def(1).sellMul * IslandSys.Def(5).sellMul;
            Check(fails, Mathf.Abs(sell - want) < 0.001f,
                  $"đặc quyền không nhân dồn: ×{sell:0.000}, kỳ vọng ×{want:0.000}");
        }
    }
}
