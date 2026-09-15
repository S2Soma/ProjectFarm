using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the pets: the egg odds and the guarantee, levelling from duplicates, egg
    /// prices across the economy, the patrol's job order, and the pet's taste in snacks.</summary>
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
                SnacksPreferRareProduce(fails);
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
    }
}
