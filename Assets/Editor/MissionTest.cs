using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the contract board.
    ///
    /// Most of this system only misbehaves across time — a slot that refills too eagerly, a
    /// streak that survives an expiry, a day away that pays out thirty cycles of back-rewards.
    /// None of that is visible in a session, so it is asserted here instead.</summary>
    public static class MissionTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra nhiệm vụ")]
        public static void Run()
        {
            var fails = new List<string>();

            GradeTableIsOrdered(fails);
            SlotsGrowWithLevel(fails);
            BoardFillsAndHolds(fails);
            ExpiryBreaksStreak(fails);
            ClaimingBuildsStreak(fails);
            OfflineDoesNotBackPay(fails);
            DailyCapHolds(fails);
            RewardsScaleWithGrade(fails);
            DescribeAlwaysReadable(fails);

            if (fails.Count == 0) Debug.Log("Nhiệm vụ OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Nhiệm vụ: " + f);
                Debug.LogError($"Nhiệm vụ: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        static PlayerState Fresh(int level = 20)
        {
            var s = new PlayerState();
            s.NewGame();
            s.lv = level;
            s.SyncContracts();
            return s;
        }

        /// <summary>A diamond must ask more work AND pay more per unit of work than a bronze, or
        /// chasing the rare one would be irrational.</summary>
        static void GradeTableIsOrdered(List<string> fails)
        {
            for (int i = 2; i < MissionSys.Grades.Length; i++)
            {
                var a = MissionSys.Grades[i - 1];
                var b = MissionSys.Grades[i];
                Check(fails, b.need > a.need, $"{b.name}: yêu cầu không cao hơn {a.name}");
                Check(fails, b.reward > a.reward, $"{b.name}: thưởng không cao hơn {a.name}");
                Check(fails, b.reward / b.need > a.reward / a.need,
                      $"{b.name}: thưởng trên mỗi công việc không tốt hơn {a.name} — không đáng đuổi theo");
                Check(fails, b.weight0 < a.weight0, $"{b.name}: không hiếm hơn {a.name}");
                Check(fails, b.weightMax >= b.weight0, $"{b.name}: chuỗi không làm nó phổ biến hơn");
            }
        }

        static void SlotsGrowWithLevel(List<string> fails)
        {
            int prev = 0;
            for (int lv = 1; lv <= 40; lv++)
            {
                int n = MissionSys.SlotsFor(lv);
                Check(fails, n >= prev, $"cấp {lv}: số khe giảm");
                Check(fails, n >= 3 && n <= 5, $"cấp {lv}: {n} khe, ngoài khoảng 3..5");
                prev = n;
            }
        }

        static void BoardFillsAndHolds(List<string> fails)
        {
            var s = Fresh();
            Check(fails, s.contracts.Count == MissionSys.SlotsFor(s.lv), "số khe sai sau khi đồng bộ");

            int filled = 0;
            foreach (var m in s.contracts)
                if (!m.Empty)
                {
                    filled++;
                    Check(fails, m.need > 0, "đơn có yêu cầu bằng 0");
                    Check(fails, m.grade != Grade.None, "đơn không có hạng");
                    Check(fails, m.expiresAt > GS.Now, "đơn sinh ra đã hết hạn");
                }
            Check(fails, filled == s.contracts.Count, "bảng không được lấp đầy khi mở app");

            // a second sync must not churn the board
            var before = new List<string>();
            foreach (var m in s.contracts) before.Add(m.type + m.need + m.grade);
            s.SyncContracts();
            for (int i = 0; i < s.contracts.Count; i++)
            {
                var m = s.contracts[i];
                Check(fails, before[i] == m.type + m.need + m.grade, $"khe {i} tự đổi khi đồng bộ lại");
            }
        }

        /// <summary>Letting one lapse costs the streak. That is the entire mechanic — without it
        /// the streak is a counter that only ever goes up, which rewards nothing.</summary>
        static void ExpiryBreaksStreak(List<string> fails)
        {
            var s = Fresh();
            s.streak = 7;
            foreach (var m in s.contracts) m.expiresAt = GS.Now - 1000;
            s.SyncContracts();

            Check(fails, s.streak == 0, $"chuỗi còn {s.streak} sau khi để đơn hết hạn");
            foreach (var m in s.contracts)
                Check(fails, m.Empty || m.refillAt > GS.Now,
                      "khe hết hạn được lấp lại ngay, không có khoảng nghỉ");
        }

        static void ClaimingBuildsStreak(List<string> fails)
        {
            var s = Fresh();
            s.streak = 0;
            var m = s.contracts[0];
            m.p = m.need;

            int coinBefore = s.coin;
            bool ok = s.ClaimContract(0);
            Check(fails, ok, "không nhận được đơn đã hoàn thành");
            Check(fails, s.streak == 1, $"chuỗi = {s.streak} sau một lần nhận");
            Check(fails, s.coin > coinBefore, "nhận đơn nhưng không được xu");
            Check(fails, s.contracts[0].Empty, "khe không được dọn sau khi nhận");
            Check(fails, !s.ClaimContract(0), "nhận được đơn trống hai lần");
        }

        /// <summary>Thirty days away must return one board, not thirty cycles of back-pay.</summary>
        static void OfflineDoesNotBackPay(List<string> fails)
        {
            var s = Fresh();
            foreach (var m in s.contracts) { m.type = null; m.refillAt = GS.Now - 30L * 86_400_000L; }

            int before = s.contractsToday;
            s.SyncContracts();

            int filled = 0;
            foreach (var m in s.contracts) if (!m.Empty) filled++;
            Check(fails, filled == s.contracts.Count, "bảng không đầy sau khi offline lâu");
            Check(fails, s.contractsToday == before,
                  "offline lâu lại tính là đã hoàn thành đơn — đang trả bù");
        }

        static void DailyCapHolds(List<string> fails)
        {
            var s = Fresh();
            s.contractsToday = MissionSys.DailyCap;
            foreach (var m in s.contracts) { m.type = null; m.refillAt = 0; }
            s.SyncContracts();

            foreach (var m in s.contracts)
                Check(fails, m.Empty, "vẫn sinh đơn mới sau khi chạm trần ngày");
        }

        static void RewardsScaleWithGrade(List<string> fails)
        {
            var s = Fresh(25);
            int unit = MissionSys.Unit(s);
            Check(fails, unit > 0, "UNIT = 0, mọi phần thưởng sẽ bằng 0");

            int prev = 0;
            foreach (var g in new[] { Grade.Bronze, Grade.Silver, Grade.Gold, Grade.Diamond })
            {
                var m = new MissionRec { type = "harvest", grade = g, need = 10 };
                int c = MissionSys.CoinReward(s, m);
                Check(fails, c > prev, $"{MissionSys.Def(g).name}: thưởng {c} không cao hơn hạng dưới");
                prev = c;
            }

            // a crop-specific contract must beat the opportunity cost of ignoring it
            var plain = new MissionRec { type = "harvest", grade = Grade.Gold, need = 24 };
            var named = new MissionRec { type = "harvest", grade = Grade.Gold, need = 24, cropId = "carrot" };
            float ratio = MissionSys.CoinReward(s, named) / (float)MissionSys.CoinReward(s, plain);
            Check(fails, Mathf.Abs(ratio - MissionSys.CropSpecificBonus) < 0.05f,
                  $"đơn chỉ định cây chỉ hơn {ratio:0.00}×, kỳ vọng {MissionSys.CropSpecificBonus:0.00}×");
        }

        /// <summary>Every generated contract has to produce a sentence a player can read.</summary>
        static void DescribeAlwaysReadable(List<string> fails)
        {
            var s = Fresh(30);
            for (long cycle = 0; cycle < 500; cycle++)
                for (int slot = 0; slot < 5; slot++)
                {
                    var m = MissionSys.Generate(s, slot, cycle);
                    string d = MissionSys.Describe(m);
                    if (string.IsNullOrEmpty(d) || d.Contains("{"))
                    { fails.Add($"mô tả hỏng: '{d}' (type={m.type} crop={m.cropId})"); return; }
                    if (m.cropId != null && GameData.Get(m.cropId) == null)
                    { fails.Add($"đơn chỉ định cây không tồn tại: {m.cropId}"); return; }
                    if (m.cropId != null && GameData.Get(m.cropId).lv > s.lv)
                    { fails.Add($"đơn chỉ định cây chưa mở khoá: {m.cropId}"); return; }
                }
        }
    }
}
