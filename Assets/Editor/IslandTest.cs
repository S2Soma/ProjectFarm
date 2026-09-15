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
            LayoutsHoldTheirShape(fails);
            PlotSizesAreEnforced(fails);
            OldSavesKeepTheirIslands(fails);
            PerksCompound(fails);
            BedsTouchInStraightRows(fails);
            SceneryStandsInsideTheFence(fails);
            IslandLifeKeepsToTheYard(fails);
            ThirstyPlotsShowTheDrop(fails);

            if (fails.Count == 0) Debug.Log("Đảo & ô đất OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Đảo: " + f);
                Debug.LogError($"Đảo: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        /// <summary>The yard rules, in grid cells (beds span ±2): every prop stands inside the
        /// fence and off the beds, nothing tall stands in the front yard where it would cover a
        /// crop, the fence stays on the grass, and a gate is wide enough for a bridge to enter.</summary>
        static void SceneryStandsInsideTheFence(List<string> fails)
        {
            float F = IslandView.FenceCells;
            Check(fails, F < IslandView.RimCells - 0.2f, $"hàng rào ({F}) sát mép đảo ({IslandView.RimCells})");
            Check(fails, F > 2.6f, $"hàng rào ({F}) quá sát ô đất: rào trước sẽ che mép ô");
            Check(fails, IslandView.GateX < IslandView.BridgeLandX && IslandView.BridgeLandX < IslandView.TipX,
                  "cầu mây không cập vào khoảng giữa cổng và mũi đảo");
            Check(fails, IslandView.GateCells * 2f * IslandView.StepY > 70f, "cổng quá hẹp cho cầu mây");

            foreach (var d in IslandView.Decor)
            {
                float reach = Mathf.Max(Mathf.Abs(d.u), Mathf.Abs(d.v));
                Check(fails, reach < F - 0.2f && reach > 2.2f, $"đồ trang trí {d.id} ({d.u}, {d.v}) ngoài sân");
                foreach (var p in IslandView.Places)
                    if (p.style == IslandSys.Def(0).style)
                        Check(fails, Mathf.Max(Mathf.Abs(d.u - p.u), Mathf.Abs(d.v - p.v)) > 0.8f,
                              $"đồ trang trí {d.id} đứng chồng lên {p.art}");
            }
        }

        /// <summary>Every island's props (IslandView.Places) and swaying plants (IslandLife.PlantsFor) follow the yard
        /// rules: inside the fence, off the beds (which on Đảo Nước reach the fence on two sides), not in the river,
        /// clear of the bridges' gates, nothing tall in the front yard or in the top corner, no two props on one
        /// island standing on each other — and every prop has its art (the recoloured skirt where the island's
        /// ground is not grass, a light mask for the crystals) and its snow cap.</summary>
        static void IslandLifeKeepsToTheYard(List<string> fails)
        {
            float F = IslandView.FenceCells;
            var styles = new HashSet<int>();
            for (int i = 0; i < IslandSys.Max; i++) styles.Add(IslandSys.Def(i).style);
            foreach (var p in IslandView.Places)
            {
                string what = $"{p.art} (đảo tranh {p.style}, {p.u}, {p.v})";
                Check(fails, styles.Contains(p.style), what + ": không đảo nào có tranh này");
                float reach = Mathf.Max(Mathf.Abs(p.u), Mathf.Abs(p.v));
                Check(fails, reach < F - 0.2f, what + " nằm ngoài hoặc sát hàng rào");
                Check(fails, reach > 2.2f, what + " đứng trên ô đất");
                Check(fails, Mathf.Abs(p.u + F) + Mathf.Abs(p.v - F) >= 1.1f && Mathf.Abs(p.u - F) + Mathf.Abs(p.v + F) >= 1.1f,
                      what + " chắn cổng cầu");

                string path = IslandView.SpritePath(p.art, p.style);
                var sp = Art.Load(path);
                Check(fails, sp != null, what + $": thiếu ảnh {path}");
                IslandView.SplitArt(p.art, out var source, out var name);
                if (source == "decor")
                {
                    Check(fails, DecorCatalog.All.ContainsKey(name), what + ": không có trong DecorCatalog (chạy lại Tools/slice_decor.py)");
                    string ground = IslandView.GroundOf(p.style);
                    if (ground != null && DecorCatalog.All.TryGetValue(name, out var e))
                        Check(fails, path != "Art/decor/" + name || !e.skirt,
                              what + $": đảo nền {ground} mà vật còn bãi cỏ xanh (thêm biến thể {ground} trong slice_decor.py)");
                    if (DecorCatalog.All.TryGetValue(name, out var en))
                        foreach (var l in en.lights)
                            Check(fails, l.at.x >= 0f && l.at.x <= 1f && l.at.y >= 0f && l.at.y <= 1f, what + ": đèn nằm ngoài ảnh");
                    if (DecorCatalog.All.TryGetValue(name, out var ek) && ek.skirt)
                        Check(fails, Art.Load("Art/snow/" + name + "_skirt") != null, what + $": thiếu tuyết phủ bãi cỏ Art/snow/{name}_skirt (slice_decor.py)");
                    if (DecorCatalog.All.TryGetValue(name, out var es) && es.spinSize > 0f)
                        Check(fails, Art.Load("Art/decor/" + name + "_sails") != null, what + ": thiếu cánh quạt Art/decor/" + name + "_sails");
                    if (name == "crystals" && sp != null)
                        Check(fails, Art.Load("Art/decor/" + sp.name + "_glow") != null, what + ": thiếu mặt nạ sáng " + sp.name + "_glow");
                }
                // snow settles on everything but the vents (too hot) and the pines (snow painted on)
                if (name != "vent" && name != "pine")
                    Check(fails, Art.Load("Art/snow/" + name + "_snow") != null, what + $": thiếu mũ tuyết Art/snow/{name}_snow (Tools/gen_snow.py)");

                float h = sp != null ? p.w * sp.rect.height / sp.rect.width : 0f;
                bool frontYard = p.u > 2f || p.v > 2f;
                Check(fails, !frontYard || (p.low && h <= 80f), what + $" cao {h:0} mà đứng ở sân trước, sẽ che cây");
                Check(fails, !(p.u < -1.6f && p.v < -1.6f) || h <= 110f, what + $" cao {h:0} ở góc trên, màn 16:9 cắt mất");

                foreach (var q in IslandView.Places)
                {
                    if (q.style != p.style || (q.u == p.u && q.v == p.v && q.art == p.art)) continue;
                    Check(fails, Mathf.Max(Mathf.Abs(p.u - q.u), Mathf.Abs(p.v - q.v)) >= 0.75f, what + $" đứng chồng lên {q.art}");
                }
            }
            for (int i = 0; i < IslandSys.Max; i++)
            {
                var def = IslandSys.Def(i);
                if (def.layout != IslandLayout.River) continue;
                foreach (var p in IslandView.Places)
                {
                    if (p.style != def.style) continue;
                    Check(fails, Mathf.Abs(p.v) >= IslandSys.RiverHalf + 0.25f, $"{def.name}: {p.art} đứng dưới sông");
                    Check(fails, !(Mathf.Abs(p.v) > 1.8f && Mathf.Abs(p.u) < 2.2f), $"{def.name}: {p.art} đứng trên ô bờ sông");
                }
            }
            // PropsOn (pets walk round it) mirrors odd islands exactly as the scenery is drawn
            for (int i = 0; i < IslandSys.Max; i++)
            {
                var def = IslandSys.Def(i);
                var on = IslandView.PropsOn(i);
                int n = 0;
                foreach (var p in IslandView.Places) if (p.style == def.style) n++;
                Check(fails, on.Count == n + (i == 0 ? IslandView.Decor.Length : 0), $"{def.name}: PropsOn trả {on.Count} vật, bảng có {n}");
                bool mirror = (i & 1) == 1 && def.layout != IslandLayout.River;
                foreach (var pr in on)
                {
                    var world = IslandView.GridPoint(pr.cell.x, pr.cell.y);
                    bool found = false;
                    foreach (var p in IslandView.Places)
                    {
                        if (p.style != def.style || p.art != pr.art) continue;
                        var at = IslandView.GridPoint(p.u, p.v);
                        if (mirror) at.x = -at.x;
                        if ((at - world).sqrMagnitude < 0.01f) found = true;
                    }
                    if (!pr.art.StartsWith("decor_")) Check(fails, found, $"{def.name}: PropsOn lệch vị trí {pr.art}");
                    Check(fails, pr.radius > 0f && pr.radius < 1f, $"{def.name}: bán kính {pr.art} = {pr.radius:0.00} ô");
                }
            }

            for (int i = 0; i < IslandSys.Max; i++)
            {
                var def = IslandSys.Def(i);
                bool river = def.layout == IslandLayout.River;
                foreach (var pl in IslandLife.PlantsFor(i))
                {
                    float reach = Mathf.Max(Mathf.Abs(pl.u), Mathf.Abs(pl.v));
                    string what = $"{def.name}: cây ({pl.u:0.00}, {pl.v:0.00})";
                    Check(fails, reach < F - 0.12f, what + " nằm ngoài hàng rào");
                    Check(fails, reach > 2.1f || (river && Mathf.Abs(pl.u) > 2.05f), what + " mọc trên ô đất");
                    if (river)
                    {
                        Check(fails, !(Mathf.Abs(pl.v) > 0.45f && Mathf.Abs(pl.v) < 2.55f && Mathf.Abs(pl.u) < 2.05f), what + " mọc trên ô bờ sông");
                        float mean = IslandLife.Meander(pl.u);
                        Check(fails, Mathf.Abs(pl.v - mean) > IslandLife.RiverBand + 0.1f, what + " mọc giữa dòng sông");
                    }
                    bool frontYard = pl.u > 2f || pl.v > 2f;
                    Check(fails, pl.height <= 80f || !frontYard, what + $" cao {pl.height:0} mà ở sân trước");
                    Check(fails, Mathf.Abs(pl.u + F) + Mathf.Abs(pl.v - F) >= 1.0f && Mathf.Abs(pl.u - F) + Mathf.Abs(pl.v + F) >= 1.0f,
                          what + " chắn cổng cầu");
                }
            }
        }

        /// <summary>The thirsty signal (owner, 15/9): the drop stands inside its plot, off the crop's base and in front
        /// of it, and its art and shaders are all there — a missing shader would silently draw nothing.</summary>
        static void ThirstyPlotsShowTheDrop(List<string> fails)
        {
            var f = IslandView.ThirstFoot;
            float k = Mathf.Abs(f.x) / (IslandView.TW * 0.5f) + Mathf.Abs(f.y) / (IslandView.TH * 0.5f);
            Check(fails, k < 0.9f, $"giọt nước khát ({f}) nằm ra ngoài ô đất (k = {k:0.00})");
            Check(fails, f.y < -8f, "giọt nước khát đứng sau gốc cây: cây sẽ che nó");
            Check(fails, Mathf.Abs(f.x) > 24f, "giọt nước khát đứng ngay gốc cây");
            Check(fails, Resources.Load<Shader>("Shaders/UIThirst") != null, "thiếu shader MiT/UI Thirst");
            Check(fails, Resources.Load<Shader>("Shaders/UIThirstRim") != null, "thiếu shader MiT/UI Thirst Rim");
            Check(fails, Art.Load("Art/beds/thirst_badge") != null && Art.Load("Art/beds/bed_glow_thirst") != null, "thiếu ảnh thirst_badge / bed_glow_thirst");
        }

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
            // four plots handed over, or half the island when it only has four (Khổng Lồ)
            for (int i = 1; i < IslandSys.Defs.Length; i++)
            {
                int want = Mathf.Min(4, IslandSys.SlotCount(i) / 2);
                Check(fails, IslandSys.Defs[i].freePlots == want,
                      $"{IslandSys.Defs[i].name}: tặng {IslandSys.Defs[i].freePlots} ô, thiết kế là {want}");
            }
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
        /// farm can actually produce: sixteen plots running the crop for one day of the reference
        /// player's sessions (<see cref="EconomyModel"/>) — a 22-hour pineapple is harvested about once
        /// a day, a tomato many times, so "hours of play" no longer means anything on its own.</summary>
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
                    float perDay = EconomyModel.HarvestsPerDay(seed, s.GrowTime(seed)) * 16f * s.YieldOf(seed, 0);
                    Check(fails, t.need <= perDay,
                          $"{d.name}: cần {t.need} {seed.name} nhưng một ngày chơi trên 16 ô chỉ ra {perDay:0}");
                }
            }
        }

        static void PlotLadderRises(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
            {
                int prevPrice = -1, prevLv = 0;
                int slots = IslandSys.SlotCount(isl);
                for (int k = 0; k < slots; k++)
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

                // a ladder needs rungs: the ratio is only a design rule on islands with many plots
                if (slots - free >= 6)
                {
                    int first = IslandSys.PlotPrice(isl, free);
                    int last = IslandSys.PlotPrice(isl, slots - 1);
                    float ratio = last / (float)Mathf.Max(1, first);
                    Check(fails, ratio > 10f && ratio < 60f,
                          $"đảo {isl}: ô cuối đắt gấp {ratio:0.0} lần ô đầu — ngoài khoảng 10..60");
                }
            }
        }

        /// <summary>Every plot must be under a day of income at the level it becomes buyable, or
        /// "mua ô tiếp theo" stops being the obvious thing to do with loose coins — which is the
        /// only thing making levelling worth anything.
        ///
        /// Income is MEASURED, not assumed: a day of the reference player's sessions
        /// (<see cref="EconomyModel"/>) on the farm they have by then — every plot of the islands whose
        /// gate is at or below the level, plus the plots of this island already bought — each growing
        /// the crop that earns that plot the most in such a day. Crop margins only: contracts, missions
        /// and chests are left out, so the real day is richer than this.</summary>
        static void PlotLadderIsAffordable(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
                for (int k = IslandSys.Def(isl).freePlots; k < IslandSys.SlotCount(isl); k++)
                {
                    int lv = IslandSys.PlotLevel(isl, k);
                    var s = Fresh(lv);
                    float small = BestPlotDay(s, lv, false), big = BestPlotDay(s, lv, true);
                    float income = 0f;
                    for (int j = 0; j < IslandSys.Max; j++)
                    {
                        if (IslandSys.Def(j).lv > lv || j > isl) continue;
                        int plots = j == isl ? k : IslandSys.SlotCount(j);
                        income += plots * (IslandSys.Def(j).layout == IslandLayout.Giant ? big : small);
                    }

                    int price = IslandSys.PlotPrice(isl, k);
                    Check(fails, price <= income,
                          $"đảo {isl} ô {k}: {price} xu ở cấp {lv}, quá một ngày thu nhập ({income:0})");
                }
        }

        /// <summary>What one plot earns in a day of the reference player's sessions with its best crop.</summary>
        static float BestPlotDay(PlayerState s, int lv, bool big)
        {
            float best = 0f;
            foreach (var seed in GameData.Seeds)
            {
                if (seed.lv > lv || seed.big != big) continue;
                int margin = s.HarvestValue(seed.id, 0) - seed.price;
                if (margin <= 0) continue;
                best = Mathf.Max(best, margin * EconomyModel.HarvestsPerDay(seed, s.GrowTime(seed)));
            }
            return best;
        }

        static void FreePlotsAreDistinct(List<string> fails)
        {
            for (int isl = 0; isl < IslandSys.Max; isl++)
            {
                var seen = new HashSet<int>();
                int n = 0;
                for (int slot = 0; slot < IslandSys.PlotsPerIsland; slot++)
                    if (IslandSys.StartsUnlocked(isl, slot))
                        Check(fails, IslandSys.KindOf(isl, slot) != PlotKind.None, $"đảo {isl}: ô mở sẵn {slot} không phải đất");
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
            string crop = IslandSys.Def(1).tribute[0].crop;
            s.AddProduce(crop, 0, 50);
            s.AddProduce(crop, top, 5);

            IslandSys.Pay(s, 1, crop, 50);

            s.store.TryGetValue(crop + ":" + top, out int legendsLeft);
            s.store.TryGetValue(crop + ":0", out int plainLeft);
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

        /// <summary>Đảo Nước: two banks of eight beds with a river between them — each bank touches
        /// itself, the banks never overlap the river band, and every bed stays inside the fence.
        /// Khổng Lồ: four big beds that tile the field exactly.</summary>
        static void LayoutsHoldTheirShape(List<string> fails)
        {
            int river = -1, giant = -1;
            for (int i = 0; i < IslandSys.Defs.Length; i++)
            {
                if (IslandSys.Defs[i].layout == IslandLayout.River) river = i;
                if (IslandSys.Defs[i].layout == IslandLayout.Giant) giant = i;
            }
            Check(fails, river == 1, $"Đảo Nước phải là đảo thứ 2 (đang ở {river})");
            Check(fails, giant == 2, $"Khổng Lồ phải là đảo thứ 3 (đang ở {giant})");
            Check(fails, IslandSys.Def(3).name == "Đảo Gió", "Đảo Gió phải đứng ngay sau Khổng Lồ");

            if (river >= 0)
            {
                Check(fails, IslandSys.SlotCount(river) == 16, "Đảo Nước không đủ 16 ô");
                for (int i = 0; i < GS.PlotCount; i++)
                {
                    var c = IslandSys.SlotCell(river, i);
                    // a bed spans half a cell either side of its centre
                    Check(fails, Mathf.Abs(c.y) - 0.5f >= IslandSys.RiverHalf - 0.001f, $"Đảo Nước ô {i} lấn xuống sông (v = {c.y})");
                    Check(fails, Mathf.Abs(c.y) + 0.5f <= IslandView.FenceCells, $"Đảo Nước ô {i} ra ngoài hàng rào (v = {c.y})");
                    for (int j = i + 1; j < GS.PlotCount; j++)
                    {
                        var d = IslandSys.SlotCell(river, j) - c;
                        bool sameBank = (i / 4 < 2) == (j / 4 < 2);
                        bool neighbour = Mathf.Abs(i / 4 - j / 4) + Mathf.Abs(i % 4 - j % 4) == 1;
                        Check(fails, Mathf.Abs(d.x) >= 0.999f || Mathf.Abs(d.y) >= 0.999f, $"Đảo Nước ô {i} và {j} chồng nhau");
                        if (sameBank && neighbour)
                            Check(fails, Mathf.Abs(d.magnitude - 1f) < 0.001f, $"Đảo Nước ô {i} và {j} cùng bờ mà không khít");
                    }
                }
            }
            if (giant >= 0)
            {
                Check(fails, IslandSys.SlotCount(giant) == 4, $"Khổng Lồ có {IslandSys.SlotCount(giant)} ô, thiết kế là 4");
                var s = Fresh(30);
                foreach (var p in s.islands[giant].plots)
                    Check(fails, p.none || p.big, "Khổng Lồ có ô nhỏ");
                var corners = new List<Vector2>();
                for (int i = 0; i < GS.PlotCount; i++)
                    if (IslandSys.KindOf(giant, i) == PlotKind.Big) corners.Add(IslandSys.SlotCell(giant, i));
                foreach (var a in corners)
                {
                    Check(fails, Mathf.Abs(Mathf.Abs(a.x) - 1f) < 0.001f && Mathf.Abs(Mathf.Abs(a.y) - 1f) < 0.001f,
                          $"ô lớn đặt lệch ({a}): 4 ô lớn phải phủ khít cánh đồng");
                }
            }
        }

        /// <summary>Trees only in big plots, small crops only in small ones, and a big plot is never
        /// sold as a small one's worth.</summary>
        static void PlotSizesAreEnforced(List<string> fails)
        {
            var s = Fresh(30);
            s.EnsureIsland(2).unlocked = true;
            s.SyncPlots();
            s.clock.lastSeenUtc = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
            var small = s.islands[0].plots.Find(p => !p.locked && !p.none);
            var big = s.islands[2].plots.Find(p => !p.locked && p.big);
            Check(fails, big != null, "Khổng Lồ mở mà không có ô lớn nào sẵn");
            s.AddSeed("apple", 2); s.AddSeed("carrot", 2);
            int apples = s.seeds["apple"], carrots = s.seeds["carrot"];
            Check(fails, !PlotLogic.Plant(s, s, small, "apple") && s.seeds["apple"] == apples, "trồng được táo ở ô nhỏ (hoặc mất hạt)");
            if (big != null)
            {
                Check(fails, !PlotLogic.Plant(s, s, big, "carrot") && s.seeds["carrot"] == carrots, "trồng được cà rốt ở ô lớn (hoặc mất hạt)");
                Check(fails, PlotLogic.Plant(s, s, big, "apple"), "không trồng được táo ở ô lớn");
            }
            int bigSeeds = 0;
            foreach (var sd in GameData.Seeds)
                if (sd.big) { bigSeeds++; Check(fails, Resources.Load<Sprite>("Art/crops_gen/" + sd.art + "_3") != null, $"{sd.name}: thiếu hình"); }
            Check(fails, bigSeeds == 4, $"có {bigSeeds} cây lớn, thiết kế là 4 (táo, cam, chuối, dừa)");
            Check(fails, GameData.Get("apple") != null && GameData.Get("apple").lv <= IslandSys.Def(2).lv,
                  "cây lớn đầu tiên mở sau cả Khổng Lồ: đảo mở ra không có gì để trồng");
            Check(fails, s.BuyPlot(2, s.islands[2].plots.FindIndex(p => p.none)) == false, "mua được ô không phải đất");
        }

        /// <summary>A save written before the two new islands keeps every island it had under the
        /// same name, and a player already past them gets them opened.</summary>
        static void OldSavesKeepTheirIslands(List<string> fails)
        {
            var s = new PlayerState();
            s.NewGame();
            s.islands.Clear();
            s.islands.Add(new Island(0, true));
            var gio = new Island(1, true) { unlockedAt = 123 };
            gio.plots.Add(new Plot { locked = false, crop = "corn" });
            s.islands.Add(gio);
            s.islands.Add(new Island(2, false) { tribute = new Dictionary<string, int> { { "eggplant", 5 } } });
            SaveIO.MigrateIslands(s, 0);
            s.SyncPlots();
            Check(fails, s.islands.Count >= 5, $"sau khi chuyển còn {s.islands.Count} đảo");
            Check(fails, s.islands[3].plots[0].crop == "corn" && s.islands[3].unlocked, "Đảo Gió cũ không còn ở vị trí Đảo Gió");
            Check(fails, s.islands[4].tribute != null && s.islands[4].tribute["eggplant"] == 5, "cống nạp Đảo Băng cũ bị lạc");
            Check(fails, s.islands[1].unlocked && s.islands[2].unlocked, "người đã có Đảo Gió phải được mở sẵn Đảo Nước và Khổng Lồ");
            int before = s.islands.Count;
            SaveIO.MigrateIslands(s, SaveIO.IslandsVersion);
            Check(fails, s.islands.Count == before, "chuyển đổi chạy lại trên file đã chuyển");

            var fresh = new PlayerState();
            fresh.NewGame();
            fresh.islands.Clear();
            fresh.islands.Add(new Island(0, true));
            fresh.islands.Add(new Island(1, false));
            SaveIO.MigrateIslands(fresh, 0);
            Check(fails, !fresh.islands[1].unlocked, "người chưa mở Đảo Gió lại được tặng Đảo Nước");
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
