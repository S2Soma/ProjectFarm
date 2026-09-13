using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the save system against a string, never against the player's real file.
    ///
    /// A serialiser that compiles proves nothing — the failure mode is silent data loss, which
    /// only shows up as a farm that came back empty. So every field that carries progress is
    /// written, read back and compared here, plus the three paths that must NOT load: a v1 file,
    /// a file from a future build, and a corrupt one.</summary>
    public static class SaveRoundTripTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra file lưu")]
        public static void Run()
        {
            var fails = new List<string>();

            RoundTrip(fails);
            RefusesV1(fails);
            RefusesFuture(fails);
            RefusesCorrupt(fails);
            KeepsUnknownFields(fails);

            if (fails.Count == 0) Debug.Log("File lưu OK — 5/5 kiểm tra đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("File lưu: " + f);
                Debug.LogError($"File lưu: {fails.Count} kiểm tra HỎNG.");
            }
        }

        static void Check(List<string> fails, bool ok, string what)
        {
            if (!ok) fails.Add(what);
        }

        // ------------------------------------------------------------
        static void RoundTrip(List<string> fails)
        {
            var a = new PlayerState();
            a.NewGame();
            a.displayName = "Mạnh • Đảo Gió";      // diacritics must survive the encoder
            a.lv = 12; a.coin = 158300; a.xp = 40122; a.energy = 940;
            a.worldSeed = unchecked((long)0xF3A891C40DE27B15);
            a.stealLeft = 18;
            a.day = 20343;
            a.buffMutateUntil = 1789207815434L;
            a.chests = new[] { 2, 1, 0, 3 };
            a.stats = new Stats { harvest = 812, plant = 840, water = 1960, sell = 3011, chest = 22, visit = 61, mutate = 74 };
            a.clock.lastSeenUtc = 1789207815434L;
            a.clock.resetOffsetMinutes = 420;
            a.clock.clockSuspect = true;

            // NewGame() already stocked the three starter seeds; clear so the count is asserted
            // against a known set rather than against whatever SeedStarter happens to hand out.
            a.seeds.Clear();
            a.seeds["carrot"] = 6; a.seeds["wheat"] = 4;
            a.store["wheat:0"] = 31; a.store["tomato:2"] = 1;
            a.missions["c1a"] = new TaskRec { p = 6, claimed = true };
            a.daily["d2"] = new TaskRec { p = 3, claimed = false };
            a.collected.Add("wheat:0"); a.collected.Add("wheat:1");
            a.claimedSets.Add("k1");
            a.claimedMs.Add(6); a.claimedMs.Add(20);
            a.shopBought.Add("s4");
            a.visited.Add("f1");

            // Part-paid tribute on a locked island — the state that exists for days at a time
            // while a player works toward Đảo Gió.
            a.EnsureIsland(1).tribute = new Dictionary<string, int> { { "corn", 137 } };

            a.plots[3].locked = false;
            a.plots[3].crop = "tomato";
            a.plots[3].plantedAt = 1789207815434L;   // > 2^53 ulp territory for a float; must stay exact
            a.plots[3].dur = 268f;
            a.plots[3].cut = 17.9f;
            a.plots[3].variant = 2;
            a.plots[3].waterMask = 0b010;
            a.plots[3].friendMask = 0b010;
            a.plots[3].windowCount = 3;

            string json = SaveIO.ToJson(a);

            var b = new PlayerState();
            var res = SaveIO.FromJson(b, json, out string tag);

            Check(fails, res == SaveIO.LoadResult.Loaded, $"vòng lặp: kỳ vọng Loaded, nhận {res}");
            Check(fails, tag == null, "vòng lặp: không được cách ly file hợp lệ");

            Check(fails, b.displayName == a.displayName, "tên có dấu sai");
            Check(fails, b.lv == a.lv && b.coin == a.coin && b.xp == a.xp && b.energy == a.energy, "ví/cấp sai");
            Check(fails, b.worldSeed == a.worldSeed, $"worldSeed sai: {b.worldSeed:x16} != {a.worldSeed:x16}");
            Check(fails, b.stealLeft == a.stealLeft && b.day == a.day, "stealLeft/day sai");
            Check(fails, b.buffMutateUntil == a.buffMutateUntil, "buffMutateUntil sai");
            Check(fails, b.chests.SequenceEqual(a.chests), "rương sai");
            Check(fails, b.stats.harvest == 812 && b.stats.mutate == 74, "stats sai");

            Check(fails, b.clock.lastSeenUtc == a.clock.lastSeenUtc, "clock.lastSeenUtc sai");
            Check(fails, b.clock.resetOffsetMinutes == 420, "clock.resetOffsetMinutes sai");
            Check(fails, b.clock.clockSuspect, "clock.clockSuspect sai");

            Check(fails, b.seeds.Count == 2 && b.seeds["carrot"] == 6, "hạt giống sai");
            Check(fails, b.store.Count == 2 && b.store["tomato:2"] == 1, "kho sai");
            Check(fails, b.missions.Count == 1 && b.missions["c1a"].p == 6 && b.missions["c1a"].claimed, "nhiệm vụ sai");
            Check(fails, b.daily.Count == 1 && b.daily["d2"].p == 3 && !b.daily["d2"].claimed, "hằng ngày sai");
            Check(fails, b.collected.SetEquals(a.collected), "sưu tập sai");
            Check(fails, b.claimedSets.SetEquals(a.claimedSets), "bộ đã nhận sai");
            Check(fails, b.claimedMs.SetEquals(a.claimedMs), "mốc đã nhận sai");
            Check(fails, b.shopBought.SetEquals(a.shopBought), "đã mua sai");
            Check(fails, b.visited.SetEquals(a.visited), "đã thăm sai");

            // Six island records, one of them unlocked. SyncPlots creates a record for every
            // island in the table whether or not it is open, because a locked island still holds
            // tribute paid toward it and still gets drawn on the shared map.
            Check(fails, b.islands.Count == a.islands.Count, $"số đảo sai: {b.islands.Count} != {a.islands.Count}");
            Check(fails, b.islands[0].unlocked, "đảo nhà không mở");
            for (int i = 1; i < b.islands.Count; i++)
                Check(fails, b.islands[i].unlocked == a.islands[i].unlocked, $"trạng thái đảo {i} sai");

            // Tribute must survive the round trip, or a player who pays 400 Ngô and closes the
            // app pays it again.
            int paidA = IslandSys.Paid(a, 1, "corn"), paidB = IslandSys.Paid(b, 1, "corn");
            Check(fails, paidA == 137, $"cống nạp không được ghi vào state gốc: {paidA}");
            Check(fails, paidB == paidA, $"cống nạp sai: {paidB} != {paidA}");
            Check(fails, b.plots.Count == GS.PlotCount, $"số ô sai: {b.plots.Count}");

            var pa = a.plots[3];
            var pb = b.plots[3];
            Check(fails, !pb.locked && pb.crop == "tomato", "ô 3: cây sai");
            // The reason plantedAt is int64 and not float: as a float this value is ~1.789e12,
            // which has a ulp of 131072 ms — every crop would finish up to two minutes early or
            // late purely from rounding.
            Check(fails, pb.plantedAt == pa.plantedAt, $"ô 3: plantedAt sai {pb.plantedAt} != {pa.plantedAt}");
            Check(fails, Mathf.Approximately(pb.dur, 268f), "ô 3: dur sai");
            Check(fails, Mathf.Approximately(pb.cut, 17.9f), "ô 3: cut sai");
            Check(fails, pb.variant == 2, "ô 3: variant sai");
            Check(fails, pb.waterMask == 0b010 && pb.friendMask == 0b010 && pb.windowCount == 3,
                  $"ô 3: mặt nạ tưới sai {pb.waterMask}/{pb.friendMask}/{pb.windowCount}");
        }

        // ------------------------------------------------------------
        static void RefusesV1(List<string> fails)
        {
            // Lifted from a real file the shipped build wrote. Two traps in here, and an earlier
            // version of this test missed both by using a hand-written snippet:
            //   - the key is "version", not "v"
            //   - "chests" is an int ARRAY, where v2 writes an object — so deserialising straight
            //     into SaveDto throws, and a naive reader reports "corrupt" instead of "old"
            const string v1 =
                "{\"version\":1,\"lv\":9,\"coin\":5000,\"xp\":118,\"energy\":36," +
                "\"plots\":[{\"locked\":false,\"crop\":\"wheat\",\"plantedAt\":1789207815434.1309,\"dur\":200.0}]," +
                "\"chests\":[0,0,0,0],\"stats\":{\"harvest\":12}," +
                "\"seedK\":[\"carrot\"],\"seedV\":[6]}";
            var s = new PlayerState();
            var res = SaveIO.FromJson(s, v1, out string tag);
            Check(fails, res == SaveIO.LoadResult.NewGame, $"v1: kỳ vọng NewGame, nhận {res}");
            Check(fails, tag == "v1", $"v1: phải cách ly với nhãn v1, nhận '{tag}'");
            Check(fails, s.lv == 1, "v1: phải là ván mới, không lấy lv từ file cũ");

            // The same file with no version key at all still has to be recognised as pre-v2.
            var s2 = new PlayerState();
            var res2 = SaveIO.FromJson(s2, v1.Replace("\"version\":1,", ""), out string tag2);
            Check(fails, res2 == SaveIO.LoadResult.NewGame, $"không version: kỳ vọng NewGame, nhận {res2}");
            Check(fails, tag2 == "v0", $"không version: kỳ vọng nhãn v0, nhận '{tag2}'");
        }

        static void RefusesFuture(List<string> fails)
        {
            const string future = "{\"v\":9,\"minV\":9,\"player\":{\"level\":30,\"coin\":999999}}";
            var s = new PlayerState();
            var res = SaveIO.FromJson(s, future, out string tag);
            Check(fails, res == SaveIO.LoadResult.RefusedTooNew, $"tương lai: kỳ vọng RefusedTooNew, nhận {res}");
            Check(fails, tag != null, "tương lai: phải được giữ lại");
            Check(fails, s.coin != 999999, "tương lai: không được đọc dữ liệu từ schema chưa biết");
        }

        static void RefusesCorrupt(List<string> fails)
        {
            var s = new PlayerState();
            var res = SaveIO.FromJson(s, "{\"v\":2,\"player\":{\"level\":", out string tag);
            Check(fails, res == SaveIO.LoadResult.Corrupt, $"hỏng: kỳ vọng Corrupt, nhận {res}");
            Check(fails, tag != null, "hỏng: phải được cách ly");
        }

        // ------------------------------------------------------------
        /// <summary>The whole reason JsonUtility was dropped: a key this build has never heard of
        /// must still be there after a load-then-save cycle.</summary>
        static void KeepsUnknownFields(List<string> fails)
        {
            var a = new PlayerState();
            a.NewGame();
            a.coin = 4321;

            // pretend a newer build wrote two fields this one knows nothing about
            var doc = Newtonsoft.Json.Linq.JObject.Parse(SaveIO.ToJson(a));
            doc["guildId"] = "g-77";
            ((Newtonsoft.Json.Linq.JObject)doc["player"])["prestige"] = 3;

            var b = new PlayerState();
            SaveIO.FromJson(b, doc.ToString(), out _);
            var after = Newtonsoft.Json.Linq.JObject.Parse(SaveIO.ToJson(b));

            Check(fails, (string)after["guildId"] == "g-77", "khoá lạ cấp gốc bị mất");
            Check(fails, (int?)after["player"]?["prestige"] == 3, "khoá lạ trong player bị mất");
            Check(fails, (int?)after["player"]?["coin"] == 4321, "dữ liệu quen bị hỏng khi giữ khoá lạ");
        }
    }
}
