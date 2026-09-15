using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the rules the walkthrough, the tips and the level curve rest on.
    ///
    ///  - A new farm starts the walkthrough; a save from before it existed does not, and is not
    ///    told about things it already has (a level-12 farm must not hear the harvest button
    ///    "just unlocked").
    ///  - An unknown step name (a save from a newer build) never restarts the walkthrough.
    ///  - The first level is reachable from the starting purse, and no level is a wall: costs rise
    ///    every level, and never by more than the plan's steepest step.
    ///  - A watering window stays open until the next one (the rule the tutorial's water step
    ///    relies on is in WaterSysTest).</summary>
    public static class TutorialTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra hướng dẫn & cấp độ")]
        public static void Run()
        {
            var fails = new List<string>();

            NewFarmStartsAtWelcome(fails);
            OldSaveSkipsAndSeedsTips(fails);
            UnknownStepIsDone(fails);
            TipIdsAreUnique(fails);
            LevelCurveHasNoWalls(fails);

            if (fails.Count == 0) Debug.Log("Hướng dẫn & cấp độ OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Hướng dẫn & cấp độ: " + f);
                Debug.LogError($"Hướng dẫn & cấp độ: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok && fails.Count < 40) fails.Add(what); }

        static void NewFarmStartsAtWelcome(List<string> fails)
        {
            var s = new PlayerState();
            s.NewGame();
            Check(fails, s.tutorial == "Welcome", $"ván mới phải bắt đầu ở Welcome, đang là '{s.tutorial}'");
            Check(fails, Tutorial.Resume(s) == Tutorial.Step.Welcome, "Resume ván mới không ra Welcome");
            Check(fails, s.tipsSeen.Count == 0, "ván mới đã có mẹo được đánh dấu");

            // a save written before the tutorial, that never planted: still a new player
            var fresh = new PlayerState { tutorial = "" };
            Check(fails, Tutorial.Resume(fresh) == Tutorial.Step.Welcome, "file lưu cũ chưa gieo gì phải vào Welcome");
        }

        static void OldSaveSkipsAndSeedsTips(List<string> fails)
        {
            var s = new PlayerState { tutorial = "", lv = 12, coin = 286400 };
            s.stats.plant = 400; s.stats.harvest = 380; s.stats.water = 50; s.stats.chest = 3;
            var st = Tutorial.Resume(s);
            Check(fails, st == Tutorial.Step.Done, $"file lưu cũ đã chơi phải bỏ qua hướng dẫn, nhận {st}");
            Check(fails, s.tutorial == "Done", "file lưu cũ không được ghi Done");
            foreach (var id in new[] { "quickHarvest", "quickWater", "upgrade", "chest", "weather", "island", "water" })
                Check(fails, s.tipsSeen.Contains(id), $"người chơi cấp 12 vẫn sẽ bị báo mẹo '{id}'");

            // staged by the dev tools: level 12, every counter at zero — still not a new player
            var staged = new PlayerState { tutorial = "", lv = 12 };
            Check(fails, Tutorial.Resume(staged) == Tutorial.Step.Done, "ván cấp 12 không có thống kê bị coi là người chơi mới");

            // a level-2 player who never saw a chest is still owed the chest tip
            var low = new PlayerState { tutorial = "", lv = 2 };
            low.stats.plant = 20;
            Tutorial.Resume(low);
            Check(fails, !low.tipsSeen.Contains("chest"), "người chơi chưa có rương lại bị đánh dấu đã xem mẹo rương");
            Check(fails, !low.tipsSeen.Contains("quickHarvest"), "cấp 2 bị đánh dấu đã xem mẹo Thu hoạch nhanh (mở ở cấp 3)");
        }

        static void UnknownStepIsDone(List<string> fails)
        {
            var s = new PlayerState { tutorial = "SomeStepFromTheFuture" };
            Check(fails, Tutorial.Resume(s) == Tutorial.Step.Done, "bước lạ phải coi là xong, không được chạy lại hướng dẫn");
            foreach (Tutorial.Step step in System.Enum.GetValues(typeof(Tutorial.Step)))
            {
                var r = new PlayerState { tutorial = step.ToString() };
                Check(fails, Tutorial.Resume(r) == step, $"bước {step} không đọc lại được từ tên");
            }
        }

        static void TipIdsAreUnique(List<string> fails)
        {
            var seen = new HashSet<string>();
            foreach (var t in Tutorial.Tips)
            {
                Check(fails, seen.Add(t.id), $"mẹo trùng id '{t.id}'");
                Check(fails, !string.IsNullOrEmpty(t.title) && !string.IsNullOrEmpty(t.body), $"mẹo '{t.id}' thiếu chữ");
            }
        }

        /// <summary>Every level costs more XP and more coins than the last, the first is payable
        /// out of the starting 5.000, and no step up is steeper than the plan's first (×4,2) or
        /// flatter than the tail's ×1,05.</summary>
        static void LevelCurveHasNoWalls(List<string> fails)
        {
            var start = new PlayerState();
            start.NewGame();
            var l1 = GameData.Level(1);
            Check(fails, l1.cost <= start.coin, $"cấp 1 tốn {l1.cost} xu, nhiều hơn số xu khởi đầu {start.coin}");
            Check(fails, l1.xpNeed <= 400, $"cấp 1 cần {l1.xpNeed} XP — lần lên cấp đầu phải tới trong phiên chơi đầu");

            for (int lv = 2; lv <= 40; lv++)
            {
                var a = GameData.Level(lv - 1);
                var b = GameData.Level(lv);
                Check(fails, b.xpNeed > a.xpNeed, $"XP cấp {lv} ({b.xpNeed}) không lớn hơn cấp {lv - 1} ({a.xpNeed})");
                Check(fails, b.cost > a.cost, $"xu cấp {lv} ({b.cost}) không lớn hơn cấp {lv - 1} ({a.cost})");
                float rx = b.xpNeed / (float)a.xpNeed, rc = b.cost / (float)a.cost;
                Check(fails, rx <= 4.2f && rx >= 1.05f, $"XP cấp {lv - 1}→{lv} nhảy ×{rx:0.00}");
                Check(fails, rc <= 4.2f && rc >= 1.05f, $"xu cấp {lv - 1}→{lv} nhảy ×{rc:0.00}");
            }
        }
    }
}
