using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the pets: the egg odds and the guarantee, levelling from duplicates, egg
    /// prices across the economy, the patrol's job order, the pet's taste in snacks (any produce, the
    /// rare first), and where it may walk: every island's walkable grid, its paths, its gates, the river
    /// crossing, and trips over the bridges.</summary>
    public static class PetTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra thú cưng")]
        public static void Run()
        {
            var fails = new List<string>();
            var saved = GS.Local;
            try
            {
                TableIsSound(fails);
                OddsHold(fails);
                PityGuarantees(fails);
                DuplicatesLevelUp(fails);
                EggsArePricedInUnits(fails);
                HatchRefusesWithoutSpending(fails);
                PatrolWatersFirst(fails);
                PatrolStaysNearAndOffLockedIslands(fails);
                SnacksPreferRareProduce(fails);
                SnacksTakeAnyProduce(fails);
                PetPaths.ClearCache();
                WalkableGridsAreSound(fails);
                RoutesCrossBridgesGateToGate(fails);
            }
            finally { GS.Local = saved; }

            if (fails.Count == 0) Debug.Log("Thú cưng OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Thú cưng: " + f);
                Debug.LogError($"Thú cưng: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok && fails.Count < 40) fails.Add(what); }

        static PlayerState Fresh(int lv)
        {
            var s = new PlayerState();
            s.NewGame();
            s.lv = lv;
            s.SyncPlots();
            GS.Local = s;
            return s;
        }

        static void TableIsSound(List<string> fails)
        {
            float sum = 0f;
            foreach (var o in PetSys.RarityOdds) sum += o;
            Check(fails, Mathf.Abs(sum - 1f) < 1e-4f, $"tổng tỉ lệ = {sum}");
            var ids = new HashSet<string>();
            for (int r = 0; r < PetSys.RarityOdds.Length; r++)
            {
                int n = 0;
                foreach (var d in PetSys.All) if (d.rarity == r) n++;
                Check(fails, n > 0, $"không có thú cưng bậc {PetSys.RarityName[r]}");
            }
            foreach (var d in PetSys.All)
            {
                Check(fails, ids.Add(d.id), $"trùng id {d.id}");
                Check(fails, GameData.Get(d.favourite) != null, $"{d.name}: món khoái khẩu '{d.favourite}' không phải cây trồng");
                foreach (var pose in new[] { "portrait", "idle", "sleep", "happy", "walk_0", "walk_1" })
                    Check(fails, Resources.Load<Sprite>("Art/pets/" + d.id + "/" + pose) != null, $"{d.name}: thiếu ảnh {pose}");
            }
        }

        static void OddsHold(List<string> fails)
        {
            var s = Fresh(10);
            var rng = new System.Random(7);
            var count = new int[4];
            const int N = 100_000;
            for (int i = 0; i < N; i++)
            {
                s.petPity = 0;               // odds without the guarantee
                count[PetSys.Roll(s, (float)rng.NextDouble(), (float)rng.NextDouble()).rarity]++;
            }
            for (int r = 0; r < 4; r++)
            {
                float got = count[r] / (float)N;
                Check(fails, Mathf.Abs(got - PetSys.RarityOdds[r]) < 0.01f,
                      $"{PetSys.RarityName[r]}: ra {got:P1}, bảng ghi {PetSys.RarityOdds[r]:P0}");
            }
        }

        static void PityGuarantees(List<string> fails)
        {
            var s = Fresh(10);
            int best = 0;
            for (int i = 0; i < PetSys.Pity; i++) best = Mathf.Max(best, PetSys.Roll(s, 0.999f, 0.5f).rarity);
            Check(fails, best >= 2, $"{PetSys.Pity} trứng xui liên tiếp mà không nở Sử thi");
            Check(fails, s.petPity == 0, "bảo hiểm không đặt lại sau khi nở Sử thi");
            PetSys.Roll(s, 0.999f, 0.5f);
            Check(fails, s.petPity == 1, "đếm bảo hiểm không tăng sau trứng thường");
        }

        static void DuplicatesLevelUp(List<string> fails)
        {
            var s = Fresh(10);
            var d = PetSys.Def("bega");
            PetSys.Grant(s, d, out bool isNew, out int lv);
            Check(fails, isNew && lv == 1 && s.petActive == "bega", "thú cưng đầu tiên không cấp 1 hoặc không tự đi theo");
            PetSys.Grant(s, PetSys.Def("mit"), out _, out _);
            Check(fails, s.petActive == "bega", "nở thú cưng thứ hai lại đổi thú đang đi theo");
            for (int i = 0; i < 10; i++) PetSys.Grant(s, d, out isNew, out lv);
            Check(fails, !isNew && lv == PetSys.MaxLevel, $"trùng 10 lần ra cấp {lv}, trần là {PetSys.MaxLevel}");
            Check(fails, PetSys.JobsPerPatrol(PetSys.Def("mit"), 1) > PetSys.JobsPerPatrol(d, 1), "thú hiếm hơn không làm được nhiều ô hơn");
            Check(fails, PetSys.JobsPerPatrol(d, 5) > PetSys.JobsPerPatrol(d, 1), "lên cấp không làm thêm ô");
        }

        static void EggsArePricedInUnits(List<string> fails)
        {
            for (int lv = PetSys.UnlockLevel; lv <= 30; lv++)
            {
                var s = Fresh(lv);
                Check(fails, PetSys.EggPrice(s, 1) == 0, $"cấp {lv}: trứng đầu tiên không miễn phí");
                s.petEggs = 1;
                long one = PetSys.EggPrice(s, 1);
                float units = one / (float)MissionSys.Unit(s);
                Check(fails, units >= 15f && units <= 40f, $"cấp {lv}: 1 trứng = {units:0.0} UNIT, ngoài 15–40");
                Check(fails, PetSys.EggPrice(s, 10) == one * 9, $"cấp {lv}: 10 trứng không bằng giá 9");
            }
        }

        static void HatchRefusesWithoutSpending(List<string> fails)
        {
            var s = Fresh(PetSys.UnlockLevel - 1);
            s.coin = 10_000_000;
            Check(fails, PetSys.Hatch(s, 1, () => 0.5f) == null && s.pets.Count == 0, "ấp được trứng khi chưa đủ cấp");

            s = Fresh(PetSys.UnlockLevel);
            s.petEggs = 1;
            s.coin = PetSys.EggPrice(s, 1) - 1;
            long before = s.coin;
            Check(fails, PetSys.Hatch(s, 1, () => 0.5f) == null && s.coin == before && s.petEggs == 1, "thiếu xu mà vẫn ấp / vẫn trừ xu");

            s.coin = PetSys.EggPrice(s, 10);
            var got = PetSys.Hatch(s, 10, () => 0.5f);
            Check(fails, got != null && got.Count == 10 && s.coin == 0 && s.petEggs == 11, "ấp 10 không ra 10 con hoặc trừ sai xu");

            // free eggs (gift code) hatch without coins, and only when they cover the whole hatch
            s.petFreeEggs = 12;
            s.coin = 0;
            Check(fails, PetSys.Hatch(s, 10, () => 0.5f) != null && s.petFreeEggs == 2 && s.coin == 0, "trứng miễn phí không dùng được / vẫn trừ xu");
            Check(fails, PetSys.Hatch(s, 10, () => 0.5f) == null && s.petFreeEggs == 2, "ấp 10 bằng 2 trứng miễn phí mà không có xu");
            Check(fails, PetSys.Hatch(s, 1, () => 0.5f) != null && s.petFreeEggs == 1, "ấp 1 bằng trứng miễn phí không được");
        }

        static void PatrolWatersFirst(List<string> fails)
        {
            var s = Fresh(12);
            s.clock.lastSeenUtc = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
            var isl = s.islands[0];
            int open = 0;
            foreach (var p in isl.plots) if (!p.locked) open++;
            Check(fails, open >= 4, "Vườn Nhà có ít hơn 4 ô mở");

            // plots 0-1 ripe, plot 2 thirsty, the rest empty
            var plots = new List<Plot>();
            foreach (var p in isl.plots) if (!p.locked) plots.Add(p);
            for (int i = 0; i < 3; i++)
            {
                s.AddSeed("tomato", 1);
                PlotLogic.Plant(s, s, plots[i], "tomato");
            }
            plots[0].cut = plots[0].dur; plots[1].cut = plots[1].dur;
            var th = plots[2];
            // walk the clock to the first window, whatever the mutation roll did to the duration
            // (a rain planting would have spent it, so the mask is cleared)
            th.waterMask = 0;
            for (int k = 0; k < 2000 && PlotLogic.State(th) != PlotState.Thirsty; k++) th.plantedAt -= 500;
            Check(fails, PlotLogic.State(th) == PlotState.Thirsty,
                  $"không dựng được ô khát: {PlotLogic.State(th)}, dur {th.dur}, cữ {WaterSys.Windows(th)}, đã qua {WaterSys.Nominal(th):0.0}s, mask {th.waterMask}, v {th.variant}");

            var jobs = PetSys.FindJobs(s, 10, 0);
            Check(fails, jobs.Count == 3, $"tìm được {jobs.Count} việc, kỳ vọng 3");
            Check(fails, jobs.Count > 0 && jobs[0].water, "việc tưới không đứng đầu");
            Check(fails, PetSys.FindJobs(s, 2, 0).Count == 2, "không giới hạn số việc mỗi lượt");

            // and doing them with the game's own rules empties the list
            foreach (var j in jobs)
            {
                var p = s.islands[j.island].plots[j.plot];
                if (j.water) PlotLogic.Water(s, s, p, false); else PlotLogic.Harvest(s, s, p, out _);
            }
            Check(fails, PetSys.FindJobs(s, 10, 0).Count == 0, "làm xong mà vẫn còn việc");
        }

        static void SnacksPreferRareProduce(List<string> fails)
        {
            var s = Fresh(12);
            var pet = PetSys.Def("bega");
            Check(fails, PetSys.PickSnack(s, pet, 0.5f) == null, "kho trống mà vẫn chọn được món ăn vụng");

            s.AddProduce("carrot", 0, 200);
            s.AddProduce("grape", 4, 1);
            int rare = 0;
            var rng = new System.Random(3);
            for (int i = 0; i < 10_000; i++)
                if (PetSys.PickSnack(s, pet, (float)rng.NextDouble()) == "grape:4") rare++;
            // 1 of 201 pieces; by count alone it would be picked ~50 times in 10.000
            Check(fails, rare > 2_500, $"1 nho Lôi Điện giữa 200 cà rốt chỉ bị chọn {rare}/10.000 lần");

            Check(fails, PetSys.Eat(s, "grape:4") && !s.store.ContainsKey("grape:4") && s.petSnacks == 1, "ăn vụng không trừ đúng kho");
            Check(fails, !PetSys.Eat(s, "grape:4"), "ăn được món đã hết");
        }

        // ============================================================
        // the patrol, across islands
        // ============================================================
        static void PatrolStaysNearAndOffLockedIslands(List<string> fails)
        {
            var s = Fresh(30);
            s.clock.lastSeenUtc = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
            // islands 0-3 open, 4 locked, 5 open (an impossible save, but the rule must not care)
            for (int i = 0; i < IslandSys.Max; i++) s.islands[i].unlocked = i <= 3 || i == 5;
            s.SyncPlots();
            void Ripen(int island)
            {
                foreach (var p in s.islands[island].plots)
                {
                    if (p.none) continue;
                    p.locked = false;
                    var crop = p.big ? "apple" : "tomato";
                    s.AddSeed(crop, 1);
                    if (!PlotLogic.Plant(s, s, p, crop)) continue;
                    p.cut = p.dur;
                    break;
                }
            }
            for (int i = 0; i < IslandSys.Max; i++) Ripen(i);
            // island 4 is locked with a ripe plot; island 5 lies beyond it
            Check(fails, PlotLogic.State(FirstCrop(s, 4)) != PlotState.Empty, "không dựng được ô chín trên đảo khoá");

            var jobs = PetSys.FindJobs(s, 20, 2, 9);
            foreach (var j in jobs)
            {
                Check(fails, s.islands[j.island].unlocked, $"việc trên đảo khoá {j.island}");
                Check(fails, j.island <= 3, $"việc trên đảo {j.island} ở sau một đảo khoá");
            }
            Check(fails, jobs.Count > 0 && jobs[0].island == 2, "việc trên đảo thú cưng đang đứng không đứng đầu");
            for (int i = 1; i < jobs.Count; i++)
                Check(fails, Mathf.Abs(jobs[i].island - 2) >= Mathf.Abs(jobs[i - 1].island - 2), "việc ở đảo xa hơn đứng trước đảo gần hơn");
            foreach (var j in PetSys.FindJobs(s, 20, 2, 1))
                Check(fails, Mathf.Abs(j.island - 2) <= 1, $"đi tuần quá {1} cầu: việc trên đảo {j.island}");
            Check(fails, PetSys.FindJobs(s, 20, 5, 9).TrueForAll(j => j.island == 5), "thú cưng bên kia đảo khoá vẫn nhận việc qua đảo khoá");
            Check(fails, !PetSys.Reachable(s, 3, 5) && PetSys.Reachable(s, 0, 3), "Reachable sai khi có đảo khoá ở giữa");

            // a job the player already did is dropped
            if (jobs.Count > 0)
            {
                var j = jobs[0];
                PlotLogic.Harvest(s, s, s.islands[j.island].plots[j.plot], out _);
                Check(fails, !PetSys.StillWanted(s, j), "việc đã được người chơi làm vẫn còn");
            }
        }

        static Plot FirstCrop(PlayerState s, int island)
        {
            foreach (var p in s.islands[island].plots) if (!string.IsNullOrEmpty(p.crop)) return p;
            return null;
        }

        // ============================================================
        // snacks: any produce
        // ============================================================
        static void SnacksTakeAnyProduce(List<string> fails)
        {
            foreach (var pet in PetSys.All)
            {
                var s = Fresh(30);
                // the favourite, plain crops of every rarity, and a mutated one that out-weighs them all
                var keys = new List<string>();
                void Put(string crop, int v, int n) { if (GameData.Get(crop) == null) return; s.AddProduce(crop, v, n); keys.Add(crop + ":" + v); }
                Put(pet.favourite, 0, 5);
                foreach (var other in new[] { "carrot", "wheat", "corn", "garlic", "cabbage", "lemon", "avocado" })
                    if (other != pet.favourite) Put(other, 0, 2);
                Put("pumpkin", 4, 1);
                var got = new Dictionary<string, int>();
                var rng = new System.Random(11 + pet.rarity);
                for (int i = 0; i < 40_000; i++)
                {
                    string k = PetSys.PickSnack(s, pet, (float)rng.NextDouble());
                    if (k == null) continue;
                    got.TryGetValue(k, out int c);
                    got[k] = c + 1;
                }
                foreach (var k in keys)
                    Check(fails, got.ContainsKey(k) && got[k] > 0, $"{pet.name} không bao giờ ăn {k} dù có trong kho");
                foreach (var k in got.Keys)
                    Check(fails, keys.Contains(k), $"{pet.name} ăn món không có trong kho: {k}");
                // favourite: a preference (x2), not a filter and not an obsession
                Check(fails, Mathf.Approximately(PetSys.Appetite(pet, pet.favourite, 0), 2f * PetSys.Appetite(null, pet.favourite, 0)),
                      $"{pet.name}: món khoái khẩu không phải ×2");
            }
        }

        // ============================================================
        // where the pet walks
        // ============================================================
        /// <summary>Hard geometry, no clearances: may a pet's feet be here at all?</summary>
        static string Illegal(PetGrid g, Vector2 c)
        {
            float F = IslandView.FenceCells;
            bool inside = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)) < F - 0.02f || PetPaths.InGateMouth(c, 0.05f, 0.02f);
            if (!inside) return $"ra ngoài hàng rào ({c.x:0.00}, {c.y:0.00})";
            // between the front beds and the front fence the pet is drawn over the rails
            if (PetPaths.InFrontYard(c, PetPaths.FrontBand - 0.05f, PetPaths.GateCorner - 0.05f, 0.2f) && !PetPaths.InGateMouth(c, 0.05f, 0.02f))
                return $"đứng sát hàng rào trước, bị vẽ đè lên rào ({c.x:0.00}, {c.y:0.00})";
            if (IslandView.RimDistance(IslandView.GridPoint(c.x, c.y)) > -0.05f) return $"ra mép đảo ({c.x:0.00}, {c.y:0.00})";
            if (g.layout == IslandLayout.River && PetPaths.InRiver(c, 0f)) return $"lội xuống sông ({c.x:0.00}, {c.y:0.00})";
            foreach (var o in g.obstacles)
                if (o.Distance(c) < o.radius) return $"đi xuyên {o.name} ({c.x:0.00}, {c.y:0.00})";
            return null;
        }

        /// <summary>Every walked stretch of a path, sampled finely, on legal ground; hops only over the river, at
        /// the crossing, bank to bank. Returns the number of hops.</summary>
        static int CheckPath(List<string> fails, PetGrid g, List<PetPoint> path, string what)
        {
            string name = IslandSys.Def(g.island).name;
            if (path == null || path.Count == 0) { Check(fails, false, $"{name}: không có đường {what}"); return 0; }
            int hops = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (i == 0) continue;
                var a = path[i - 1].cell; var b = path[i].cell;
                if (path[i].hop)
                {
                    hops++;
                    Check(fails, g.layout == IslandLayout.River, $"{name}: nhảy ở đảo không có sông ({what})");
                    Check(fails, Mathf.Abs(a.x - PetPaths.CrossingU) < 0.07f && Mathf.Abs(b.x - PetPaths.CrossingU) < 0.07f,
                          $"{name}: nhảy qua sông ngoài chỗ qua ({a} → {b})");
                    float m = IslandLife.Meander(a.x);
                    Check(fails, (a.y - m) * (b.y - m) < 0f && Vector2.Distance(a, b) < 1.4f, $"{name}: cú nhảy không qua sông từ bờ này sang bờ kia ({a} → {b})");
                    continue;
                }
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.03f));
                for (int k = 0; k <= n; k++)
                {
                    string bad = Illegal(g, Vector2.Lerp(a, b, k / (float)n));
                    if (bad != null) { Check(fails, false, $"{name}: đường {what} {bad}"); return hops; }
                }
            }
            return hops;
        }

        /// <summary>How far, in screen units, the front fence stands over a pet whose feet are at
        /// <paramref name="c"/> (0 when it is clear). The pet is drawn above the island, so a fence post in front
        /// of it that reaches up into its sprite reads as the pet standing on the rails. The fence is taken as a
        /// solid band as tall as a post along both front runs (IslandView.AddFence: 8 segments a run), open only
        /// where the river leaves through it.</summary>
        static float FrontFenceOverlap(PetGrid g, Vector2 c)
        {
            float F = IslandView.FenceCells, gate = IslandView.GateCells;
            float postH = 167f * IslandView.FenceScale, postHalf = 22.5f * IslandView.FenceScale;
            float petHalf = PetActor.Height * 0.375f;
            var p = IslandView.GridPoint(c.x, c.y);
            float worst = 0f;
            for (int run = 0; run < 2; run++)
                for (float t = -F + gate; t <= F; t += 0.02f)
                {
                    // run 0: the front-right fence (u = F), run 1: the front-left fence (v = F)
                    var f = run == 0 ? new Vector2(F, t) : new Vector2(t, F);
                    if (run == 0 && g.layout == IslandLayout.River && Mathf.Abs(t) < IslandSys.RiverHalf + 0.18f) continue;
                    var fp = IslandView.GridPoint(f.x, f.y);
                    if (fp.y >= p.y || Mathf.Abs(fp.x - p.x) >= petHalf + postHalf) continue;
                    worst = Mathf.Max(worst, fp.y + postH - p.y);
                }
            return worst;
        }

        /// <summary>Near a gate (this far along the field's diagonal) the pet has to pass the gate posts; elsewhere
        /// the front fence may reach at most this far up over its feet.</summary>
        const float FenceOverlapAllowed = 24f;

        /// <summary>The corner of a gate: the way in from the landing to the corner of the field, and the end of the
        /// back yard beside it — where the pet has to pass the gate posts.</summary>
        static bool NearGate(Vector2 c)
        {
            float s = Mathf.Max(-c.x + c.y, c.x - c.y), t = c.x + c.y;
            if (Mathf.Abs(t) <= IslandView.GateCells && s >= 3.5f) return true;      // PetPaths.InFrontYard's way in
            return (c.x < -2f && c.y > 1.6f) || (c.y < -2f && c.x > 1.6f);
        }

        static void WalkableGridsAreSound(List<string> fails)
        {
            // every island bare, and Vườn Nhà once more with each yard decoration worn
            var cases = new List<(int island, string decor)>();
            for (int i = 0; i < IslandSys.Max; i++) cases.Add((i, null));
            foreach (var d in IslandView.Decor) cases.Add((0, d.id));
            string wornBefore = PetPaths.WornDecor;
            foreach (var (island, decor) in cases)
            {
                PetPaths.WornDecor = decor;
                var g = PetPaths.For(island);
                string name = IslandSys.Def(island).name + (decor != null ? " (" + decor + ")" : "");
                Check(fails, decor == null || g.obstacles.Exists(o => o.name == decor), $"{name}: đồ trang trí đang đeo không chắn đường");
                int walk = 0;
                for (int n = 0; n < PetGrid.N * PetGrid.N; n++) if (g.Walkable(n)) walk++;
                Check(fails, walk > 600, $"{name}: chỉ có {walk} điểm đi được");
                Check(fails, g.Pruned <= walk * 0.02f, $"{name}: {g.Pruned} điểm đi được bị cô lập (hàng rào/vật cản bít kín)");

                // every walkable point is legal ground (the lattice itself, before any path), and away from the
                // gates the front fence never stands over the pet on screen
                float worstOver = 0f; Vector2 worstAt = Vector2.zero;
                for (int n = 0; n < PetGrid.N * PetGrid.N; n++)
                {
                    if (!g.Walkable(n)) continue;
                    var c = PetGrid.CellOf(n);
                    string bad = Illegal(g, c);
                    if (bad != null) { Check(fails, false, $"{name}: điểm lưới {bad}"); break; }
                    if (NearGate(c)) continue;
                    float over = FrontFenceOverlap(g, c);
                    if (over > worstOver) { worstOver = over; worstAt = c; }
                }
                Check(fails, worstOver <= FenceOverlapAllowed, $"{name}: ở {worstAt} hàng rào trước vẽ đè lên pet {worstOver:0} px (cho phép {FenceOverlapAllowed:0})");

                // the gates: both landings are walkable and in the one piece
                Check(fails, g.LeftGateNode >= 0 && g.RightGateNode >= 0 && g.piece[g.LeftGateNode] == g.piece[g.RightGateNode],
                      $"{name}: hai cổng không nối với nhau");
                Check(fails, !float.IsInfinity(g.CostAt(PetPaths.LeftLanding)) && !float.IsInfinity(g.CostAt(PetPaths.RightLanding)),
                      $"{name}: chỗ cầu cập không đi được");

                var across = g.FindPath(PetPaths.LeftLanding, PetPaths.RightLanding);
                int hops = CheckPath(fails, g, across, "cổng trái → cổng phải");
                if (g.layout == IslandLayout.River)
                {
                    Check(fails, g.hops.Count == 1, $"{name}: có {g.hops.Count} chỗ qua sông, cần đúng 1");
                    foreach (var h in g.hops)
                        foreach (var end in new[] { h.a, h.b })
                        {
                            float over = FrontFenceOverlap(g, PetGrid.CellOf(end));
                            Check(fails, over <= 0f, $"{name}: chỗ nhảy qua sông {PetGrid.CellOf(end)} bị hàng rào trước vẽ đè {over:0} px");
                        }
                    Check(fails, hops == 1, $"{name}: đi từ cổng trái sang cổng phải nhảy {hops} lần, cần 1 (hai cổng ở hai bờ)");
                }
                else Check(fails, hops == 0 && g.hops.Count == 0, $"{name}: có chỗ nhảy dù không có sông");

                // every plot has a work spot the pet can reach from both gates, on legal ground
                for (int slot = 0; slot < IslandSys.PlotsPerIsland; slot++)
                {
                    if (IslandSys.KindOf(island, slot) == PlotKind.None)
                    {
                        Check(fails, g.WorkSpot(slot, Vector2.zero) == null, $"{name}: ô {slot} không phải đất mà có chỗ làm việc");
                        continue;
                    }
                    var spot = g.WorkSpot(slot, PetPaths.LeftLanding);
                    Check(fails, spot != null, $"{name}: ô {slot} không có chỗ đứng làm việc");
                    if (spot == null) continue;
                    var c = IslandSys.SlotCell(island, slot);
                    float half = IslandSys.SizeOf(island, slot) * 0.5f;
                    Check(fails, Mathf.Abs(Mathf.Max(Mathf.Abs(spot.Value.x - c.x), Mathf.Abs(spot.Value.y - c.y)) - half) < 0.07f,
                          $"{name}: chỗ làm việc ô {slot} không nằm ở mép luống");
                    CheckPath(fails, g, g.FindPath(PetPaths.LeftLanding, spot.Value), $"cổng trái → ô {slot}");
                    CheckPath(fails, g, g.FindPath(PetPaths.RightLanding, spot.Value), $"cổng phải → ô {slot}");
                }

                // strolls: many yard spots to many others
                var rng = new System.Random(97 + island);
                System.Func<float> rand = () => (float)rng.NextDouble();
                var at = PetPaths.LeftLanding;
                for (int k = 0; k < 40; k++)
                {
                    var to = g.YardSpot(rand, at, 0f, 1e6f);
                    Check(fails, g.CostAt(to) == PetGrid.Yard, $"{name}: chỗ dạo chơi không nằm ở sân sau ({to})");
                    var path = g.FindPath(at, to);
                    CheckPath(fails, g, path, $"dạo {k}");
                    if (path != null) at = to;
                }
            }
            PetPaths.WornDecor = wornBefore;
        }

        static void RoutesCrossBridgesGateToGate(List<string> fails)
        {
            bool AllOpen(int i) { return i >= 0 && i < IslandSys.Max; }
            // the bridge line starts and ends exactly where the island paths do
            for (int b = 1; b < IslandSys.Max; b++)
            {
                var path = PetPaths.Bridge(b);
                var start = ArchipelagoView.PetPosOfCell(b - 1, PetPaths.RightLanding);
                var end = ArchipelagoView.PetPosOfCell(b, PetPaths.LeftLanding);
                Check(fails, Vector2.Distance(path.Start, start) < 0.5f && Vector2.Distance(path.End, end) < 0.5f,
                      $"cầu {b}: đầu cầu lệch chỗ cập ({path.Start} / {start}, {path.End} / {end})");
                Check(fails, path.Length > 200f && path.Length < 900f, $"cầu {b}: dài {path.Length:0}");
                float prevX = float.MinValue;
                for (int k = 0; k <= 40; k++)
                {
                    var p = path.At(path.Length * k / 40f);
                    Check(fails, p.x >= prevX - 0.01f, $"cầu {b}: đường đi trên cầu quay ngược");
                    prevX = p.x;
                    // on the deck: the planks' own curve, never under it or off to the side
                    float t = Mathf.InverseLerp(path.Start.x, path.End.x, p.x);
                    Check(fails, Mathf.Abs(ArchipelagoView.BridgeCentre(b, t).y - p.y) < 3f, $"cầu {b}: đường đi lệch khỏi ván cầu ở {t:0.00}");
                }
            }

            var home = PetPaths.For(0).YardSpot(() => 0.37f, Vector2.zero, 0f, 1e6f);
            var far = PetPaths.For(3).WorkSpot(5, PetPaths.LeftLanding);
            var legs = PetPaths.Route(0, home, 3, far ?? Vector2.zero, AllOpen);
            Check(fails, legs != null && legs.Count == 7, $"Vườn Nhà → Đảo Gió: {(legs == null ? "không có đường" : legs.Count + " chặng")}, cần 7");
            if (legs != null)
                for (int i = 0; i < legs.Count; i++)
                {
                    var leg = legs[i];
                    bool bridge = i % 2 == 1;
                    Check(fails, bridge == (leg.bridge >= 0), $"chặng {i} sai loại (cầu / đảo xen kẽ)");
                    if (bridge) { Check(fails, leg.forward && leg.bridge == (i + 1) / 2, $"chặng {i}: cầu {leg.bridge}"); continue; }
                    Check(fails, leg.island == i / 2, $"chặng {i}: đảo {leg.island}, cần {i / 2}");
                    var pts = leg.points;
                    if (i > 0) Check(fails, Vector2.Distance(pts[0].cell, PetPaths.LeftLanding) < 0.01f, $"chặng {i} không bắt đầu ở cổng trái");
                    if (i < legs.Count - 1) Check(fails, Vector2.Distance(pts[pts.Count - 1].cell, PetPaths.RightLanding) < 0.01f, $"chặng {i} không kết thúc ở cổng phải");
                    CheckPath(fails, PetPaths.For(leg.island), pts, $"chặng {i}");
                }

            // back the other way
            var back = PetPaths.Route(2, PetPaths.RightLanding, 0, home, AllOpen);
            Check(fails, back != null && back.Count == 5 && back[1].bridge == 2 && !back[1].forward && back[3].bridge == 1 && !back[3].forward,
                  "Khổng Lồ → Vườn Nhà không đi ngược qua cầu 2 rồi cầu 1");
            if (back != null && back.Count == 5)
                Check(fails, Vector2.Distance(back[2].points[0].cell, PetPaths.RightLanding) < 0.01f
                          && Vector2.Distance(back[2].points[back[2].points.Count - 1].cell, PetPaths.LeftLanding) < 0.01f,
                      "đi ngược: Đảo Nước không đi từ cổng phải sang cổng trái");

            // never through, or onto, a locked island
            bool Upto2(int i) { return i >= 0 && i <= 2; }
            Check(fails, PetPaths.Route(0, home, 3, Vector2.zero, Upto2) == null, "có đường tới đảo khoá");
            bool Hole(int i) { return i >= 0 && i != 1; }
            Check(fails, PetPaths.Route(0, home, 2, Vector2.zero, Hole) == null, "có đường đi xuyên qua đảo khoá");
            Check(fails, PetPaths.Hops(0, 2, Hole) < 0 && PetPaths.Hops(0, 2, Upto2) == 2, "đếm số cầu sai");
            var stay = PetPaths.Route(1, PetPaths.LeftLanding, 1, PetPaths.RightLanding, Upto2);
            Check(fails, stay != null && stay.Count == 1 && stay[0].island == 1, "đi trong một đảo mà lại qua cầu");
        }
    }
}
