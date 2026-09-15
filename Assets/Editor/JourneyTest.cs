using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Plays the game from a new farm, headless, the way a real player does: in sessions, and
    /// without a gift code.
    ///
    /// Crops run from two minutes to a day (2026-09-15), so "hours of continuous play" no longer
    /// describes anyone. The reference player (<see cref="EconomyModel"/>) opens the game four times a
    /// day for 85 minutes in all and sleeps ten and a half hours. In a session they harvest what is
    /// ripe, water what is thirsty, sell, claim missions, contracts and chests, visit the neighbours
    /// once a day, level up, buy plots and pay tribute as soon as they can, hatch a pet egg when the
    /// coins for the next level are already put aside, and replant — with a limited number of taps a
    /// minute (<see cref="TapsPerMinute"/>), so a hundred plots of two-minute carrots are not free. Each
    /// seed is chosen by what it earns until it is actually harvested: a carrot while there is time to
    /// tend it, the crop that ripens by the next session before leaving. Everything runs the game's own
    /// rules — <see cref="PlotLogic"/>, <see cref="PlayerState"/>, <see cref="IslandSys"/>,
    /// <see cref="MissionSys"/>, <see cref="PetSys"/>, <see cref="WaterSys.RainWater"/> — on a clock it
    /// winds forward (<see cref="GS.Now"/> never runs below a state's <c>clock.lastSeenUtc</c>).
    ///
    /// The walls are what a player would feel: level 2 inside the first 20 minutes, pets (level 5) on the
    /// first day, Đảo Nước by the second, Đảo Gió within four days, Đảo Băng within ten, no level taking
    /// more than four days up to level 30, no chapter step stuck for more than three days, never an empty
    /// bed left behind or a session with nothing to do — and a farm saved under the old economy
    /// (<see cref="LegacySaveFixture"/>) goes on levelling at a normal pace after its migration.</summary>
    public static class JourneyTest
    {
        const float Step = 5f;                       // seconds of game time per tick in a session
        const float TapsPerMinute = 30f;
        const int ReportDays = 60;
        const int CheckDays = 14;
        const int LegacyDays = 7;
        const int EggGoal = 10;

        [MenuItem("Tools/LQ Farm/Kiểm tra hành trình chơi")]
        public static void Run()
        {
            var fails = new List<string>();
            var log = new StringBuilder();
            var rs = Random.state;
            var saved = GS.Local;
            try
            {
                for (int run = 0; run < 3; run++)
                {
                    var j = Play(FreshFarm(run), run, 0, run == 0 ? ReportDays : CheckDays);
                    CheckFresh(j, fails);
                    if (run == 0) ReportFresh(j, log);
                }
                var legacy = Play(LegacyFarm(), 3, 1, LegacyDays);
                CheckLegacy(legacy, fails, log);
            }
            finally { Random.state = rs; GS.Local = saved; }

            Debug.Log("Hành trình chơi — báo cáo lượt đầu:\n" + log);
            if (fails.Count == 0) Debug.Log("Hành trình chơi OK — 3 lượt theo phiên từ ván mới + 1 save cũ cấp 12, không có chỗ kẹt.");
            else
            {
                foreach (var f in fails.Distinct()) Debug.LogError("Hành trình chơi: " + f);
                Debug.LogError($"Hành trình chơi: {fails.Distinct().Count()} lỗi.");
            }
        }

        /// <summary>"ngày 2 12:07 · 1g 35p chơi".</summary>
        static string When(double t, double played)
        {
            int day = (int)(t / 86400.0);
            double h = (t - day * 86400.0) / 3600.0;
            return $"ngày {day + 1} {(int)h:00}:{(int)((h % 1) * 60):00} · {Fmt.Time((int)played)} chơi";
        }

        // ------------------------------------------------------------
        // the farms
        // ------------------------------------------------------------
        static PlayerState FreshFarm(int run)
        {
            var s = new PlayerState();
            s.NewGame();
            s.worldSeed = 0x5EED0000L + run * 7919L;
            s.createdAt = 0;                                  // stamped when the clock starts
            return s;
        }

        /// <summary>The old level-12 save, loaded exactly as <see cref="GS.Load"/> loads a file: read (and so
        /// migrated), world seed ensured, unknown ids pruned, plots synced.</summary>
        static PlayerState LegacyFarm()
        {
            var s = new PlayerState();
            SaveIO.FromJson(s, LegacySaveFixture.Level12, out _);
            s.EnsureWorldSeed();
            s.PruneUnknown();
            s.SyncPlots();
            return s;
        }

        // ------------------------------------------------------------
        // one journey
        // ------------------------------------------------------------
        struct Mark { public double t, played; public long income; public double waitXp, waitCoin; }

        sealed class Journey
        {
            public int run, days, startLv;
            public PlayerState s;
            public long epoch;                     // unix ms of midnight, day 0
            public double t;                       // seconds since that midnight
            public double played;                  // seconds spent in sessions
            public float taps;
            public System.Random rng;
            public long spent;                     // coins spent on seeds, plots, levels, islands, eggs
            public long plotSpent, islandSpent, eggSpent, levelSpent, seedSpent;
            public int chestsOpened, contractsDone;
            public long xpGained;
            /// <summary>Income by source: 0 harvest (sale + bonus), 1 chapter & daily missions, 2 contracts,
            /// 3 chests, 4 visits.</summary>
            public readonly long[] coinBy = new long[5], xpBy = new long[5];
            public long Income => coinBy.Sum();
            public void Credit(int src, long coin0, long xp0) { coinBy[src] += s.coin - coin0; xpBy[src] += s.xp - xp0; }
            public double waitXp, waitCoin;          // session seconds spent short of only XP / only coins
            public void SetClock() { s.clock.lastSeenUtc = epoch + (long)(t * 1000.0); }
            public Mark Now() { return new Mark { t = t, played = played, income = Income, waitXp = waitXp, waitCoin = waitCoin }; }

            public readonly Dictionary<int, Mark> levelAt = new Dictionary<int, Mark>();
            public readonly Dictionary<int, Mark> islandAt = new Dictionary<int, Mark>();
            public readonly List<Mark> eggAt = new List<Mark>();
            public readonly Dictionary<string, double> taskActiveAt = new Dictionary<string, double>();
            public readonly Dictionary<string, double> taskDoneAt = new Dictionary<string, double>();
            public readonly List<string> dayRows = new List<string>();
            public readonly List<string> firstDay = new List<string>();
            public int emptyLeft, idleSessions;
            public double longestIdleDay1;
            public void Event(string what) { if (t < 86400.0 * (startDay + 1)) firstDay.Add($"    {When(t, played)} — {what}"); }
            public int startDay;
        }

        static Journey Play(PlayerState s, int run, int startDay, int days)
        {
            Random.InitState(7919 + run * 31);
            var j = new Journey { s = s, run = run, days = days, startDay = startDay, startLv = s.lv, rng = new System.Random(run * 31 + 7) };
            long nowMs = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
            // tomorrow's local (UTC+7) midnight: always ahead of the real clock (and of any old save's clock)
            j.epoch = (System.Math.Max(nowMs, s.clock.lastSeenUtc) / 86_400_000L + 2) * 86_400_000L - 7L * 3_600_000L;
            EconomyModel.Window(startDay, 0, out double firstStart, out _);
            j.t = firstStart;
            j.SetClock();
            if (s.createdAt == 0) s.createdAt = s.clock.lastSeenUtc;
            GS.Local = s;
            j.levelAt[s.lv] = j.Now();

            for (int day = startDay; day < startDay + days; day++)
            {
                long spentStart = j.spent, incomeStart = j.Income, xpStart = s.xp + j.xpGained;
                var coinSrc0 = (long[])j.coinBy.Clone(); var xpSrc0 = (long[])j.xpBy.Clone();
                int chestsStart = j.chestsOpened, contractsStart = j.contractsDone;
                double waitXp0 = j.waitXp, waitCoin0 = j.waitCoin;
                var sessions = EconomyModel.Sessions(day);
                for (int si = 0; si < sessions.Length; si++)
                {
                    EconomyModel.Window(day, si, out double start, out double end);
                    j.t = start;
                    j.SetClock();
                    // what GS.Load does on opening the game
                    s.CheckDay();
                    s.SyncContracts();
                    foreach (var isl in s.islands) foreach (var p in isl.plots) WaterSys.RainWater(s, p, catchUp: true);
                    if (si == 0) Visit(j);

                    double idleFor = 0, longestIdle = 0;
                    int harvestsBefore = s.stats.harvest, plantsBefore = s.stats.plant;
                    while (j.t < end)
                    {
                        j.SetClock();
                        j.taps = Mathf.Min(j.taps + TapsPerMinute * Step / 60f, TapsPerMinute);
                        bool acted = Tick(j, end, false);
                        var gate = s.Info;
                        if (s.xp >= gate.xpNeed && s.coin < gate.cost) j.waitCoin += Step;
                        else if (s.coin >= gate.cost && s.xp < gate.xpNeed) j.waitXp += Step;
                        idleFor = acted || AnyThingBefore(s, (float)(end - j.t)) ? 0 : idleFor + Step;
                        longestIdle = System.Math.Max(longestIdle, idleFor);
                        j.t += Step;
                        j.played += Step;
                    }
                    // before closing the game, every empty bed gets a seed (no tap limit: a minute more)
                    j.t = end;
                    j.SetClock();
                    Tick(j, end, true);
                    int empty = EmptySmallPlots(s);
                    if (empty > 0) { j.emptyLeft++; j.dayRows.Add($"  ! {When(j.t, j.played)}: {empty} ô trống khi rời game"); }
                    if (longestIdle >= 300) { j.idleSessions++; j.dayRows.Add($"  ! {When(j.t, j.played)}: {Fmt.Time((int)longestIdle)} không có gì làm (cấp {s.lv}, {Describe(s)})"); }
                    if (day == startDay)
                    {
                        j.longestIdleDay1 = System.Math.Max(j.longestIdleDay1, longestIdle);
                        j.Event($"rời game sau phiên {si + 1}: thu {s.stats.harvest - harvestsBefore} · gieo {s.stats.plant - plantsBefore} · lâu nhất không có việc {Fmt.Time((int)longestIdle)} · cấp {s.lv} · {s.OpenPlots} ô · {Fmt.N(s.coin)} xu · để lại {LongestCrops(s)}");
                    }
                }

                long income = j.Income - incomeStart;
                int unit = MissionSys.Unit(s);
                string Split(long[] now, long[] then)
                {
                    long tot = 0; for (int k = 0; k < now.Length; k++) tot += now[k] - then[k];
                    if (tot <= 0) return "-";
                    return string.Join("/", Enumerable.Range(0, now.Length).Select(k => ((now[k] - then[k]) * 100 / tot).ToString()));
                }
                j.dayRows.Add($"  ngày {day + 1,2}: cấp {s.lv,2} · {s.OpenPlots,3} ô · thu {Fmt.Short(income),9} xu · tiêu {Fmt.Short(j.spent - spentStart),9}" +
                              $" · UNIT {Fmt.N(unit),7} = {income / (float)Mathf.Max(1, unit),5:0} UNIT/ngày" +
                              $" (4g {income / (float)Mathf.Max(1, MissionSys.Unit(s, 4f)),4:0} · 24g {income / (float)Mathf.Max(1, MissionSys.Unit(s, 24f)),4:0})" +
                              $" · rương {j.chestsOpened - chestsStart,2} · đơn {j.contractsDone - contractsStart,2}" +
                              $" · XP {Fmt.Short(s.xp + j.xpGained - xpStart)} · đợi XP {(j.waitXp - waitXp0) / 60:0}p / đợi xu {(j.waitCoin - waitCoin0) / 60:0}p" +
                              $" · nguồn xu thu/nv/đơn/rương/thăm % {Split(j.coinBy, coinSrc0)}");
            }
            return j;
        }

        /// <summary>One tick of a session. Returns whether the player did anything.</summary>
        static bool Tick(Journey j, double sessionEnd, bool leaving)
        {
            var s = j.s;
            bool acted = false;
            bool Spend(float n) { if (leaving) return true; if (j.taps < n) return false; j.taps -= n; return true; }

            s.CheckDay();
            s.SyncContracts();

            // harvest and water: one button each once unlocked, otherwise a tap per plot
            bool bulkHarvest = QuickActions.HarvestUnlocked(s), bulkWater = QuickActions.WaterUnlocked(s);
            bool harvestPaid = false, waterPaid = false;
            for (int ii = 0; ii < s.islands.Count; ii++)
            {
                var isl = s.islands[ii];
                if (!isl.unlocked) continue;
                foreach (var p in isl.plots)
                {
                    if (p.none || p.locked) continue;
                    WaterSys.RainWater(s, p, catchUp: false);
                    var st = PlotLogic.State(p);
                    if (st == PlotState.Ready && (bulkHarvest ? (harvestPaid || (harvestPaid = Spend(1))) : Spend(1)))
                    { long c0 = s.coin, x0 = s.xp; PlotLogic.Harvest(s, s, p, out _); j.Credit(0, c0, x0); acted = true; }
                    else if (st == PlotState.Thirsty && (bulkWater ? (waterPaid || (waterPaid = Spend(1))) : Spend(1)))
                    { PlotLogic.Water(s, s, p, false); acted = true; }
                }
            }

            PayTribute(j);
            if (s.store.Count > 0) { long c0 = s.coin, x0 = s.xp; s.SellAll(out _); j.Credit(0, c0, x0); acted = true; }
            acted |= ClaimEverything(j);
            for (int tier = 0; tier < 4; tier++)
                if (s.chests[tier] > 0) { long c0 = s.coin, x0 = s.xp; j.chestsOpened += s.chests[tier]; s.OpenChests(tier, () => (float)j.rng.NextDouble(), null); j.Credit(3, c0, x0); acted = true; }
            while (true)
            {
                long cost = s.Info.cost, need = s.Info.xpNeed;
                if (!s.LevelUp()) break;
                j.spent += cost; j.levelSpent += cost;
                j.xpGained += need;          // XP a level consumed, so a day's XP is xp now + what levels took
                acted = true;
                if (!j.levelAt.ContainsKey(s.lv)) { j.levelAt[s.lv] = j.Now(); j.Event($"lên cấp {s.lv}{Unlocks(s.lv)}"); }
            }
            acted |= BuyPlots(j);
            acted |= HatchEgg(j);

            // replant
            var w = WeatherSys.Now(s);
            for (int ii = 0; ii < s.islands.Count; ii++)
            {
                var isl = s.islands[ii];
                if (!isl.unlocked) continue;
                foreach (var p in isl.plots)
                {
                    if (p.none || p.locked || PlotLogic.State(p) != PlotState.Empty) continue;
                    string pick = ChooseSeed(j, p.big, w, sessionEnd, leaving);
                    if (pick == null) continue;
                    if (!Spend(1)) return acted;
                    if (EnsureSeed(j, pick) && PlotLogic.Plant(s, s, p, pick)) acted = true;
                }
            }
            return acted;
        }

        /// <summary>What a level opens, for the first-day log and the level table.</summary>
        static string Unlocks(int lv)
        {
            var parts = new List<string>();
            foreach (var sd in GameData.Seeds) if (sd.lv == lv) parts.Add((sd.big ? "cây lớn " : "") + sd.name + " " + Fmt.Time(sd.grow));
            for (int i = 1; i < IslandSys.Defs.Length; i++) if (IslandSys.Defs[i].lv == lv) parts.Add("đủ cấp " + IslandSys.Defs[i].name);
            if (lv == PetSys.UnlockLevel) parts.Add("thú cưng");
            string quick = QuickActions.UnlockedAt(lv);
            if (quick != null) parts.Add(quick);
            if (MissionSys.SlotsFor(lv) > MissionSys.SlotsFor(lv - 1)) parts.Add("+1 khe đơn hàng");
            return parts.Count == 0 ? "" : " · mở " + string.Join(", ", parts);
        }

        static string LongestCrops(PlayerState s)
        {
            var counts = new Dictionary<string, int>();
            foreach (var isl in s.islands)
                if (isl.unlocked)
                    foreach (var p in isl.plots)
                        if (!p.none && !p.locked && !string.IsNullOrEmpty(p.crop))
                        { counts.TryGetValue(p.crop, out int n); counts[p.crop] = n + 1; }
            return string.Join(" ", counts.OrderByDescending(kv => kv.Value).Take(4).Select(kv => GameData.Get(kv.Key).name + "×" + kv.Value));
        }

        // ------------------------------------------------------------
        // walls and reports
        // ------------------------------------------------------------
        static void CheckFresh(Journey j, List<string> fails)
        {
            string R(string what) { return $"lượt {j.run}: {what}"; }
            var at = j.levelAt;
            if (!at.TryGetValue(2, out var l2) || l2.played > 20 * 60)
                fails.Add(R($"lên cấp 2 mất {(at.ContainsKey(2) ? Fmt.Time((int)l2.played) + " chơi" : "quá " + j.days + " ngày")}, trần 20 phút"));
            if (!at.TryGetValue(PetSys.UnlockLevel, out var l5) || l5.t >= 86400)
                fails.Add(R($"mở thú cưng (cấp {PetSys.UnlockLevel}) {(at.ContainsKey(PetSys.UnlockLevel) ? "vào " + When(l5.t, l5.played) : "không tới")}, phải trong ngày đầu"));
            void IslandBy(int idx, int dayLimit)
            {
                if (!j.islandAt.TryGetValue(idx, out var m) || m.t >= dayLimit * 86400.0)
                    fails.Add(R($"mở {IslandSys.NameOf(idx)} {(j.islandAt.ContainsKey(idx) ? "vào " + When(m.t, m.played) : "không tới")}, trần {dayLimit} ngày"));
            }
            IslandBy(1, 2);
            IslandBy(3, 4);
            IslandBy(4, 10);
            // no level is a wall: every level up to 30 inside four days (the ones the run reached)
            for (int lv = 2; lv <= 30; lv++)
            {
                if (!at.TryGetValue(lv, out var a)) continue;
                double end = at.TryGetValue(lv + 1, out var b) ? b.t : j.t;
                if (!at.ContainsKey(lv + 1) && lv >= 30) continue;
                if (end - a.t > 4 * 86400.0)
                    fails.Add(R($"cấp {lv} → {lv + 1} mất {(end - a.t) / 86400.0:0.0} ngày{(at.ContainsKey(lv + 1) ? "" : " (chưa xong)")}, trần 4 ngày"));
            }
            foreach (var ch in GameData.Chapters)
                foreach (var task in ch.tasks)
                {
                    if (!j.taskActiveAt.TryGetValue(task.id, out double from)) continue;
                    double done = j.taskDoneAt.TryGetValue(task.id, out double d) ? d : j.t;
                    if (done - from > 3 * 86400.0)
                        fails.Add(R($"\"{task.t}\" ({ch.name}) đứng trên dải nhiệm vụ {(done - from) / 86400.0:0.0} ngày, trần 3 ngày"));
                }
            if (j.emptyLeft > 0) fails.Add(R($"{j.emptyLeft} phiên kết thúc mà còn ô trống chưa gieo"));
            if (j.idleSessions > 0) fails.Add(R($"{j.idleSessions} phiên có ≥ 5 phút không có gì để làm"));
            if (j.eggAt.Count < 2 || j.eggAt[1].t > 2 * 86400.0)
                fails.Add(R($"trứng thú cưng thứ 2 {(j.eggAt.Count >= 2 ? "vào " + When(j.eggAt[1].t, j.eggAt[1].played) : "không mua nổi")}, trần 2 ngày"));
        }

        /// <summary>An old level-12 farm after its migration must play on like any level-12 farm: at least two
        /// levels in a week of sessions, none of them a wall, and nothing left idle.</summary>
        static void CheckLegacy(Journey j, List<string> fails, StringBuilder log)
        {
            var s = j.s;
            if (s.lv < j.startLv + 2)
                fails.Add($"save cũ cấp {j.startLv}: sau {j.days} ngày chơi chỉ tới cấp {s.lv}, kỳ vọng ≥ {j.startLv + 2}");
            for (int lv = j.startLv; lv < s.lv; lv++)
                if (j.levelAt.TryGetValue(lv, out var a) && j.levelAt.TryGetValue(lv + 1, out var b) && b.t - a.t > 4 * 86400.0)
                    fails.Add($"save cũ: cấp {lv} → {lv + 1} mất {(b.t - a.t) / 86400.0:0.0} ngày");
            if (j.emptyLeft > 0 || j.idleSessions > 0) fails.Add($"save cũ: {j.emptyLeft} phiên bỏ ô trống, {j.idleSessions} phiên ngồi không");

            log.AppendLine($"Save cũ cấp {j.startLv} (286.400 xu, 1.200 XP trước khi quy đổi) · {j.days} ngày theo phiên → cấp {s.lv} · {Fmt.N(s.coin)} xu · {s.OpenPlots} ô · đảo mở {s.islands.Count(x => x.unlocked)}");
            foreach (var kv in j.levelAt.OrderBy(k => k.Key).Skip(1)) log.AppendLine($"  cấp {kv.Key,2}: {When(kv.Value.t, kv.Value.played)}");
            foreach (var kv in j.islandAt.OrderBy(k => k.Key)) log.AppendLine($"  mở {IslandSys.NameOf(kv.Key)}: {When(kv.Value.t, kv.Value.played)}");
        }

        static void ReportFresh(Journey j, StringBuilder log)
        {
            var s = j.s;
            log.AppendLine($"Mô phỏng {j.days} ngày theo phiên, không mã quà ({EconomyModel.Day.Length} phiên, {EconomyModel.Day.Sum(x => x.minutes):0} phút/ngày; ngày đầu {EconomyModel.FirstDay.Sum(x => x.minutes):0} phút) · cấp {s.lv} · {Fmt.N(s.coin)} xu · {s.OpenPlots} ô · đảo mở {s.islands.Count(x => x.unlocked)}");
            log.AppendLine($"  tiêu: cấp {Fmt.Short(j.levelSpent)} · ô đất {Fmt.Short(j.plotSpent)} · đảo {Fmt.Short(j.islandSpent)} · hạt {Fmt.Short(j.seedSpent)} · trứng {Fmt.Short(j.eggSpent)} · thu tổng {Fmt.Short(j.Income)}");

            log.AppendLine("  NGÀY ĐẦU:");
            foreach (var e in j.firstDay) log.AppendLine(e);

            log.AppendLine("  cấp | tới lúc              | giờ chơi | ở cấp (ngày) | XP cần    | xu cần     | thu/ngày   | xu cần ÷ thu/ngày | đợi XP/xu | trứng / shop (UNIT×) | mở");
            for (int lv = 1; lv <= 32; lv++)
            {
                if (!j.levelAt.TryGetValue(lv, out var a)) break;
                bool done = j.levelAt.TryGetValue(lv + 1, out var b);
                if (!done) b = j.Now();
                double days = (b.t - a.t) / 86400.0;
                double perDay = days > 0.01 ? (b.income - a.income) / days : 0;
                var info = GameData.Level(lv);
                var probe = new PlayerState(); probe.NewGame(); probe.lv = lv;
                for (int i = 1; i < s.islands.Count && i < IslandSys.Max; i++)
                    probe.EnsureIsland(i).unlocked = j.islandAt.TryGetValue(i, out var io) && io.t <= a.t;
                probe.petEggs = 1;
                long egg = PetSys.EggPrice(probe, 1);
                int unit = MissionSys.Unit(probe);
                log.AppendLine($"  {lv,3} | {When(a.t, a.played),-26} | {a.played / 3600.0,7:0.0}g | {days,6:0.00}{(done ? " " : "…")}      | {Fmt.N(info.xpNeed),9} | {Fmt.N(info.cost),10} | {Fmt.Short((long)perDay),10} | {(perDay > 0 ? info.cost / perDay : 0),6:0.00}           | {(b.waitXp - a.waitXp) / 60,4:0}p/{(b.waitCoin - a.waitCoin) / 60,4:0}p | {Fmt.Short(egg)} / {Fmt.Short(unit * 5)}–{Fmt.Short(unit * 30)} | {Unlocks(lv).Replace(" · mở ", "")}");
            }
            foreach (var kv in j.islandAt.OrderBy(k => k.Key))
            {
                var d = IslandSys.Def(kv.Key);
                string lvAt = j.levelAt.TryGetValue(d.lv, out var gate) ? $"{(kv.Value.t - gate.t) / 86400.0:0.0} ngày sau khi đủ cấp {d.lv}" : "";
                log.AppendLine($"  mở {d.name}: {When(kv.Value.t, kv.Value.played)} · {Fmt.N(d.coin)} xu + " +
                               string.Join(", ", d.tribute.Select(t => t.need + " " + GameData.Get(t.crop).name)) + " · " + lvAt);
            }
            for (int n = 0; n < j.eggAt.Count; n++)
                if (n == 0 || n == 1 || n == 4 || n == 9)
                    log.AppendLine($"  trứng #{n + 1}: {When(j.eggAt[n].t, j.eggAt[n].played)}");
            foreach (var ch in GameData.Chapters)
                foreach (var task in ch.tasks)
                {
                    string took = j.taskActiveAt.TryGetValue(task.id, out double a2) && j.taskDoneAt.TryGetValue(task.id, out double d2)
                        ? (d2 - a2 < 3600 ? Fmt.Time((int)(d2 - a2)) : $"{(d2 - a2) / 86400.0:0.00} ngày") : j.taskActiveAt.ContainsKey(task.id) ? "CHƯA XONG" : "chưa tới";
                    log.AppendLine($"  [{ch.name}] {task.t} ×{task.need}: {took}");
                }
            foreach (var row in j.dayRows) log.AppendLine(row);
            log.AppendLine(CropTable());
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------
        /// <summary>Idle means stuck: nothing to tap now and nothing that ripens or gets thirsty before the
        /// session ends. Waiting a few minutes for a crop due before the player leaves is just playing.</summary>
        static bool AnyThingBefore(PlayerState s, float secondsLeft)
        {
            foreach (var isl in s.islands)
            {
                if (!isl.unlocked) continue;
                foreach (var p in isl.plots)
                {
                    if (p.none || p.locked) continue;
                    var st = PlotLogic.State(p);
                    if (st != PlotState.Growing) return true;
                    if (PlotLogic.Remain(p) <= secondsLeft || (WaterSys.NextWindowIn(p) >= 0 && WaterSys.NextWindowIn(p) <= secondsLeft)) return true;
                }
            }
            return false;
        }

        static string Describe(PlayerState s)
        {
            var counts = new Dictionary<string, int>();
            int soonest = int.MaxValue;
            foreach (var isl in s.islands)
                if (isl.unlocked)
                    foreach (var p in isl.plots)
                    {
                        if (p.none || p.locked) continue;
                        string k = PlotLogic.State(p) + ":" + p.crop;
                        counts.TryGetValue(k, out int n); counts[k] = n + 1;
                        if (!string.IsNullOrEmpty(p.crop)) soonest = Mathf.Min(soonest, PlotLogic.Remain(p));
                    }
            return string.Join(" ", counts.Select(kv => kv.Key + "×" + kv.Value)) + " · sớm nhất " + Fmt.Time(soonest == int.MaxValue ? 0 : soonest);
        }

        static int EmptySmallPlots(PlayerState s)
        {
            int n = 0;
            foreach (var isl in s.islands)
                if (isl.unlocked)
                    foreach (var p in isl.plots)
                        if (!p.none && !p.locked && !p.big && PlotLogic.State(p) == PlotState.Empty) n++;
            return n;
        }

        // ------------------------------------------------------------
        // the player's choices
        // ------------------------------------------------------------
        /// <summary>Coins (plus XP at what the next level charges per XP) per second until the crop is
        /// actually in the warehouse. Crops the active chapter step, a contract or the next island's
        /// tribute still needs are planted on a good share of beds when they fit the moment.</summary>
        static string ChooseSeed(Journey j, bool big, Weather w, double sessionEnd, bool leaving)
        {
            var s = j.s;
            var info = s.Info;
            float coinPerXp = info.cost / (float)Mathf.Max(1, info.xpNeed);

            var wanted = new HashSet<string>();
            var active = s.ActiveMission(out _);
            if (active != null && active.crop != null && (active.type == "harvest" || active.type == "sellCrop")) wanted.Add(active.crop);
            int next = IslandSys.NextLocked(s);
            if (next > 0 && s.lv >= IslandSys.Def(next).lv - 1)
                foreach (var tr in IslandSys.Def(next).tribute)
                    if (IslandSys.Paid(s, next, tr.crop) + s.StockOf(tr.crop) < tr.need) wanted.Add(tr.crop);
            foreach (var m in s.contracts)
                if (!m.Empty && !m.claimed && m.cropId != null && m.p < m.need) wanted.Add(m.cropId);

            Seed best = null; double bestScore = double.MinValue, bestAt = 0;
            var options = new List<(Seed sd, double score, double at)>();
            foreach (var sd in GameData.Seeds)
            {
                if (sd.lv > s.lv || sd.big != big) continue;
                s.seeds.TryGetValue(sd.id, out int have);
                if (have <= 0 && s.coin < sd.price) continue;
                float grow = s.GrowTimeIn(sd, w);
                // windows the player will be in the game for take their time off
                double period = grow / (sd.waters + 1), cut = 0;
                for (int k = 1; k <= sd.waters; k++)
                    if (EconomyModel.HarvestTime(j.t + k * period) < j.t + (k + 1) * period) cut += sd.waterCut;
                double ripe = j.t + grow - System.Math.Min(cut, grow * 0.5);
                double harvestAt = ripe <= sessionEnd && !leaving ? ripe : EconomyModel.HarvestTime(System.Math.Max(ripe, sessionEnd + 1));
                double value = s.HarvestValue(sd.id, 0) - sd.price + s.XpFor(sd, 0) * coinPerXp;
                double score = value / System.Math.Max(1.0, harvestAt - j.t);
                options.Add((sd, score, harvestAt));
                if (score > bestScore) { bestScore = score; best = sd; bestAt = harvestAt; }
            }
            // A player follows the mission strip and the island sign on a good share of their beds —
            // when the wanted crop fits the moment: it is back this session, or the best choice is
            // leaving a crop for the next session anyway and this one is back by then too.
            if (best != null && wanted.Count > 0 && j.rng.NextDouble() < 0.6)
            {
                Seed pick = null; double pickScore = double.MinValue;
                foreach (var o in options)
                {
                    if (!wanted.Contains(o.sd.id)) continue;
                    bool fits = o.at <= sessionEnd || (bestAt > sessionEnd && o.at <= bestAt + 1);
                    if (fits && o.score > pickScore) { pickScore = o.score; pick = o.sd; }
                }
                if (pick != null) return pick.id;
            }
            return best?.id;
        }

        static bool EnsureSeed(Journey j, string id)
        {
            var s = j.s;
            if (s.seeds.TryGetValue(id, out int have) && have > 0) return true;
            var sd = GameData.Get(id);
            if (sd == null || s.coin < sd.price) return false;
            s.AddCoin(-sd.price);
            j.spent += sd.price; j.seedSpent += sd.price;
            s.AddSeed(id, 1);
            return true;
        }

        static void PayTribute(Journey j)
        {
            var s = j.s;
            int next = IslandSys.NextLocked(s);
            if (next <= 0) return;
            s.EnsureIsland(next);
            if (IslandSys.LevelMet(s, next))
                foreach (var tr in IslandSys.Def(next).tribute)
                    IslandSys.Pay(s, next, tr.crop, IslandSys.Payable(s, next, tr.crop));
            long coin = IslandSys.Def(next).coin;
            if (IslandSys.Claim(s, next))
            {
                j.spent += coin; j.islandSpent += coin;
                s.SyncPlots();
                j.islandAt[next] = j.Now();
                j.Event("mở " + IslandSys.NameOf(next));
            }
        }

        static bool ClaimEverything(Journey j)
        {
            var s = j.s;
            bool any = false;
            for (int guard = 0; guard < 20; guard++)
            {
                var a = s.ActiveMission(out var pr);
                // a step already met when it came up is claimed in the same tick: it was active for 0 s
                if (a != null && !j.taskActiveAt.ContainsKey(a.id)) j.taskActiveAt[a.id] = j.t;
                if (a == null || pr.p < a.need) break;
                long c0 = s.coin, x0 = s.xp;
                if (s.ClaimTask(a, false)) { j.Credit(1, c0, x0); j.taskDoneAt[a.id] = j.t; any = true; } else break;
            }
            foreach (var d in GameData.Daily) { long c0 = s.coin, x0 = s.xp; if (s.ClaimTask(d, true)) { j.Credit(1, c0, x0); any = true; } }
            for (int i = 0; i < s.contracts.Count; i++)
            {
                long c0 = s.coin, x0 = s.xp;
                if (!s.contracts[i].Empty && s.contracts[i].p >= s.contracts[i].need && s.ClaimContract(i)) { j.Credit(2, c0, x0); j.contractsDone++; any = true; }
            }
            return any;
        }

        /// <summary>The neighbours, once a day, through the same reward the Bạn bè panel pays.</summary>
        static void Visit(Journey j)
        {
            var s = j.s;
            foreach (var f in GameData.Friends)
            {
                if (s.stealLeft <= 0 || s.visited.Contains(f.id)) continue;
                s.visited.Add(f.id);
                s.stealLeft--;
                long c0 = s.coin, x0 = s.xp;
                s.AddCoin(MissionSys.VisitCoin(s, (float)j.rng.NextDouble()));
                j.Credit(4, c0, x0);
                s.AddEnergy(s.EnergyGain(4));
                s.Track("visit", 1);
            }
        }

        static bool BuyPlots(Journey j)
        {
            var s = j.s;
            bool any = false;
            for (int ii = 0; ii < s.islands.Count; ii++)
            {
                if (!s.islands[ii].unlocked) continue;
                for (int guard = 0; guard < 16 && s.PlotBuyable(ii, out int price, out _); guard++)
                {
                    int slot = s.islands[ii].plots.FindIndex(p => p.locked && !p.none);
                    if (slot < 0 || !s.BuyPlot(ii, slot)) break;
                    j.spent += price; j.plotSpent += price;
                    any = true;
                }
            }
            return any;
        }

        /// <summary>The first egg as soon as pets open (it is free); after that one more egg whenever the
        /// coins for the next level — and for the next island, once its level is met — are already put
        /// aside, up to <see cref="EggGoal"/>. A pet is a treat bought with spare coins, not a sink that
        /// holds levels back.</summary>
        static bool HatchEgg(Journey j)
        {
            var s = j.s;
            if (!PetSys.Unlocked(s) || s.petEggs >= EggGoal) return false;
            long price = PetSys.EggPrice(s, 1);
            long reserve = s.Info.cost;
            int next = IslandSys.NextLocked(s);
            if (next > 0 && IslandSys.LevelMet(s, next)) reserve += IslandSys.Def(next).coin;
            if (price > 0 && s.coin - price < reserve) return false;
            if (PetSys.Hatch(s, 1, () => (float)j.rng.NextDouble()) == null) return false;
            j.spent += price; j.eggSpent += price;
            j.eggAt.Add(j.Now());
            j.Event($"ấp trứng #{s.petEggs} ({(price == 0 ? "miễn phí" : Fmt.N(price) + " xu")})");
            return true;
        }

        /// <summary>The crop table as the report prints it: time, waterings, time per watering, and what a
        /// harvest and an hour are worth at the crop's own unlock level.</summary>
        public static string CropTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("  cây           cấp   thời gian  tưới  sớm/lần      xp    bán    hạt   xp/giờ  xu/giờ  lãi/vụ");
            foreach (var sd in GameData.Seeds)
            {
                var s = new PlayerState();
                s.NewGame();
                s.lv = sd.lv;
                float grow = sd.grow;
                int margin = s.HarvestValue(sd.id, 0) - sd.price;
                sb.AppendLine($"  {sd.id + (sd.big ? "*" : ""),-12} {sd.lv,4} {Fmt.Time(sd.grow),10} {sd.waters,5} {Fmt.Time(sd.waterCut),8} {sd.xp,7} {sd.sell,6} {sd.price,6} {sd.xp / (grow / 3600f),8:0} {margin / (grow / 3600f),7:0} {margin,7}");
            }
            sb.Append("  (* cây lớn, 1 ô lớn = 4 ô; xu/giờ và lãi/vụ tính ở cấp mở khoá của cây)");
            return sb.ToString();
        }
    }
}
