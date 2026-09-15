using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Static game content. Mirrors the balance table the web build shipped with.</summary>
    public class Seed
    {
        public string id, name, art, badge;
        public int r, lv, price, grow, xp, en, sell, colors;

        /// <summary>A tree: planted only in a big plot (Đảo Khổng Lồ), and a small crop never is.
        /// Big crops sit outside the small ladder — they are left out of UNIT, contracts and the
        /// energy goal — because one big plot is four cells of land and pays like four.</summary>
        public bool big;

        /// <summary>Fruits per harvest.
        ///
        /// <see cref="sell"/> is the value of a WHOLE harvest, so per-fruit price is sell/fruits
        /// and yield changes nothing about revenue. That is deliberate: making yield a revenue
        /// multiplier would six-times the economy overnight the moment a six-fruit crop unlocked.
        /// What yield actually does is split the crops into volume and value — a watermelon is
        /// one big fruit, cherries come in a punnet — which is what makes island tribute
        /// ("1080 Anh Đào") reachable and gives the two kinds of crop different jobs.</summary>
        public int fruits = 1;

        /// <summary>How many times this crop gets thirsty while it grows (1 to <see cref="WaterSys.MaxWindows"/>).</summary>
        public int waters = 1;

        /// <summary>Seconds ONE watering takes off the grow time. A time, not a share: the player reads
        /// "chín sớm 16p" and the number is the same in every weather except drought, which doubles it.</summary>
        public int waterCut;

        public Seed(string id, string name, string art, int r, int lv, int price,
                    int grow, int xp, int en, int sell, int colors, string badge, int fruits,
                    int waters, int waterCut)
        {
            this.id = id; this.name = name; this.art = art; this.r = r; this.lv = lv;
            this.price = price; this.grow = grow; this.xp = xp; this.en = en;
            this.sell = sell; this.colors = colors; this.badge = badge; this.fruits = fruits;
            this.waters = waters; this.waterCut = waterCut;
        }

        /// <summary>What one fruit sells for before any multiplier.</summary>
        public int PerFruit => Mathf.Max(1, Mathf.RoundToInt(sell / (float)Mathf.Max(1, fruits)));
    }

    public struct LevelInfo
    {
        public int xpNeed, plots, cost;
        public float growCut, mutate, priceUp, energyUp;
    }

    public class Chest
    {
        public int id, need; public string name, desc;
        public Chest(int id, string name, int need, string desc)
        { this.id = id; this.name = name; this.need = need; this.desc = desc; }
    }

    public class Task
    {
        public string id, t, type, crop;
        public int need, xp, coin;
        public Task(string id, string t, string type, int need, int xp, int coin, string crop = null)
        { this.id = id; this.t = t; this.type = type; this.need = need; this.xp = xp; this.coin = coin; this.crop = crop; }
    }

    public class Chapter
    {
        public string name; public Task[] tasks;
        public Chapter(string name, Task[] tasks) { this.name = name; this.tasks = tasks; }
    }

    public class ShopItem
    {
        public string id, name, sub, art, time, effect;
        public int price;
        public ShopItem(string id, string name, string sub, string art, int price, string time, string effect = null)
        { this.id = id; this.name = name; this.sub = sub; this.art = art; this.price = price; this.time = time; this.effect = effect; }
    }

    public class Friend
    {
        public string id, name, av; public int lv; public Color color;
        public Friend(string id, string name, int lv, string av, string hex)
        { this.id = id; this.name = name; this.lv = lv; this.av = av; ColorUtility.TryParseHtmlString(hex, out color); }
    }

    public class CollectItem
    {
        public string crop, name; public int v;
        public CollectItem(string crop, int v, string name) { this.crop = crop; this.v = v; this.name = name; }
        public string Key => crop + ":" + v;
    }

    public class CollectionSet
    {
        public string id, name; public int coin, xp; public CollectItem[] items;
        public CollectionSet(string id, string name, int coin, int xp, CollectItem[] items)
        { this.id = id; this.name = name; this.coin = coin; this.xp = xp; this.items = items; }
    }

    public static class GameData
    {
        /// <summary>The hard ceiling on how long any plot takes, after weather, mutation and every
        /// perk (<see cref="PlayerState.GrowTimeIn"/> clamps to it). A crop planted before bed is
        /// always ripe by the same time tomorrow.</summary>
        public const float MaxGrowSeconds = 24f * 3600f;

        /// <summary>Durations a player can read at a glance: whole seconds under a minute, 5 s under
        /// ten minutes, whole minutes under two hours, then 5 minutes. "Chín 1p 59s" was the level
        /// perk taking 0,6% off a two-minute carrot.</summary>
        public static float NiceSeconds(float sec)
        {
            float step = sec < 60f ? 1f : sec < 600f ? 5f : sec < 7200f ? 60f : 300f;
            return Mathf.Max(1f, Mathf.Round(sec / step) * step);
        }

        // rarity: 0 thuong | 1 hiem | 2 su thi | 3 huyen thoai
        //
        // 2026-09-15: a Hay Day ladder, 2 minutes (carrot) to 24 hours (avocado), set by the owner.
        // The numbers come from one rule (written down in CLAUDE.md, checked by Kiểm tra hành trình chơi):
        //
        //  - PER HARVEST, a longer crop always pays more, so it is the right thing to plant before
        //    leaving the game: margin grows with grow time^0,55 (and 1,5% per unlock level).
        //  - PER HOUR, a short crop always pays more, so it is the right thing to plant while playing:
        //    a tended carrot earns ~10x an avocado's hour. Past ten hours the hourly rate stops
        //    falling (and creeps up with level), so every late unlock is better than the one before
        //    at the long absence it is for.
        //  - XP leans harder on short crops than coins do (time^0,45): active play is how you level.
        //  - Seeds cost 40-55% of the base sale price by rarity; energy grows with the square root of
        //    the time. Waterings: 1 up to 5 min, 2 up to 45 min, 3 up to 8 h, 4 beyond; together they
        //    take ~25% off a carrot, 20% off crops up to 8 h, ~16% off the long ones.
        //
        //                                                    lv   price   grow     xp   en    sell     quả  tưới  giây/lần
        public static readonly Seed[] Seeds =
        {
            new Seed("carrot",     "Cà Rốt",       "carrot",     0,  1,      0,    120,    10,   2,     90, 4, null,   6, 1,    30),
            new Seed("wheat",      "Lúa Mì",       "wheat",      0,  1,     90,    300,    15,   3,    225, 4, null,   5, 1,    60),
            new Seed("tomato",     "Cà Chua",      "tomato",     0,  2,    140,    600,    20,   4,    350, 4, null,   5, 2,    60),
            new Seed("potato",     "Khoai Tây",    "potato",     0,  3,    170,    900,    25,   5,    425, 4, null,   5, 2,    90),
            new Seed("corn",       "Ngô",          "corn",       0,  4,    190,   1200,    30,   6,    480, 4, null,   4, 2,   120),

            new Seed("mushroom",   "Nấm Rừng",     "mushroom",   1,  5,    290,   1800,    40,   8,    640, 4, "exp",  4, 2,   180),
            new Seed("leek",       "Tỏi Tây",      "leek",       1,  6,    360,   2700,    45,   9,    800, 4, null,   4, 2,   270),
            new Seed("garlic",     "Hành Bổ",      "garlic",     1,  7,    410,   3600,    55,  11,    920, 4, null,   4, 3,   240),
            new Seed("broccoli",   "Súp Lơ Xanh",  "broccoli",   1,  8,    500,   5400,    70,  13,   1120, 4, null,   4, 3,   360),
            new Seed("pepper",     "Ớt Chuông",    "pepper",     1,  9,    590,   7200,    80,  15,   1320, 4, null,   4, 3,   480),
            new Seed("eggplant",   "Cà Tím",       "eggplant",   1, 10,    670,   9000,    90,  17,   1480, 4, null,   4, 3,   600),
            new Seed("cauliflower","Súp Lơ Trắng", "cauliflower",1, 11,    720,  10800,   100,  19,   1600, 4, "helm", 4, 3,   720),

            new Seed("pumpkin",    "Bí Ngô",       "pumpkin",    2, 12,    990,  14400,   120,  22,   1980, 4, null,   3, 3,   960),
            new Seed("radish",     "Củ Cải",       "radish",     2, 13,   1100,  18000,   140,  24,   2240, 4, "sprout", 4, 3, 1200),
            new Seed("beetroot",   "Củ Dền",       "beetroot",   2, 14,   1200,  21600,   150,  27,   2430, 4, "helm", 3, 3,  1440),
            new Seed("grape",      "Nho Tím",      "grape",      2, 15,   1300,  25200,   170,  29,   2640, 4, "exp",  6, 3,  1680),
            new Seed("cabbage",    "Bắp Cải",      "cabbage",    2, 16,   1400,  28800,   180,  31,   2820, 4, "helm", 3, 3,  1800),
            new Seed("lemon",      "Chanh Vàng",   "lemon",      2, 17,   1600,  36000,   210,  35,   3160, 4, "helm", 4, 4,  1440),
            new Seed("pear",       "Lê",           "pear",       2, 21,   1800,  43200,   270,  38,   3600, 4, "helm", 3, 4,  1740),

            new Seed("peach",      "Đào",          "peach",      3, 22,   2400,  50400,   320,  41,   4350, 4, "helm", 3, 4,  2100),
            new Seed("strawberry", "Dâu Tây",      "strawberry", 3, 23,   2600,  57600,   360,  44,   4740, 4, "exp",  6, 4,  2400),
            new Seed("cherries",   "Anh Đào",      "cherries",   3, 24,   2850,  64800,   410,  46,   5220, 4, "helm", 6, 4,  2700),
            new Seed("watermelon", "Dưa Hấu",      "watermelon", 3, 26,   3150,  72000,   480,  49,   5700, 4, "helm", 2, 4,  3000),
            new Seed("pineapple",  "Dứa",          "pineapple",  3, 28,   3350,  79200,   550,  51,   6100, 4, "helm", 2, 4,  3300),
            new Seed("avocado",    "Bơ Sáp",       "avocado",    3, 30,   3600,  86400,   620,  54,   6500, 4, "helm", 2, 4,  3600),

            // Big crops: trees for the big plots of Đảo Khổng Lồ. One big plot is four cells, so a
            // tree pays what four small plots would earn from a small crop of the same length at the
            // tree's level, and gives twice (not four times) the energy: chests would otherwise come
            // every other harvest.
            new Seed("apple",      "Táo",          "apple",      1,  6,   4500,  21600,   480,  54,  10000, 4, null,   8, 3,  1440) { big = true },
            new Seed("orange",     "Cam",          "orange",     2,  8,   7000,  36000,   640,  69,  14000, 4, null,   8, 4,  1440) { big = true },
            new Seed("banana",     "Chuối",        "banana",     2, 11,   9900,  57600,  1000,  88,  19800, 4, null,   6, 4,  2400) { big = true },
            new Seed("coconut",    "Dừa",          "coconut",    3, 15,  15600,  86400,  1600, 107,  28400, 4, null,   4, 4,  3600) { big = true },
        };

        static Dictionary<string, Seed> _byId;
        public static Dictionary<string, Seed> ById =>
            _byId ?? (_byId = Seeds.ToDictionary(s => s.id));

        public static Seed Get(string id)
        {
            Seed s; return id != null && ById.TryGetValue(id, out s) ? s : null;
        }

        /// <summary>16 plots max; a new plot every two levels.</summary>
        /// <summary>What a level costs, as REDESIGN.md §4.3 set it: about 0.30 + 0.15·(lv−1) days of
        /// play per level, XP and coins alike.
        ///
        /// The web build's formula (3.600 XP × 1,28^lv and 10.000·lv + 5.000 coins) was flat at
        /// both ends: the FIRST level cost 15.000 coins against a 5.000 start and ~45 coins a
        /// carrot — three days — so a new player filled the XP bar and then sat on "Chưa đủ điều
        /// kiện" with no idea why; and by level 27 a level took eleven days. Anchor rows from the
        /// plan, log-interpolated between them, and ×1,08 per level past 30.
        ///
        /// 2026-09-14: the first ten levels made cheaper again (a new player found them slow) —
        /// level 1 is now a couple of minutes of carrots, level 5 about a quarter of an hour.
        ///
        /// 2026-09-15 (crops of 2 min to 24 h): re-fitted to the session journey with no gift code. A level
        /// takes minutes on day one, about a day at level 10, two days at 20 and two and a half at 30, and
        /// never more than four (Kiểm tra hành trình chơi prints the table). XP flattens from 17 to 22,
        /// where the farm stops growing between Đảo Hoả and Đảo Lôi and levels 18–20 unlock no crop; coins
        /// carry more of the late levels so they do not pile up with nothing to buy. Saves priced on the old
        /// table are rescaled on load (SaveIO.MigrateEconomy).</summary>
        static readonly (int lv, float xp, float coin)[] LevelAnchors =
        {
            (1, 150f, 800f), (2, 400f, 2000f), (3, 900f, 4500f), (4, 1800f, 9000f), (5, 3000f, 16000f),
            (6, 7000f, 40000f), (8, 25000f, 180000f), (10, 50000f, 480000f), (12, 80000f, 900000f),
            (15, 130000f, 1500000f), (20, 225000f, 2600000f), (25, 370000f, 5500000f), (30, 520000f, 9000000f),
        };

        /// <summary>The table every save written before <see cref="SaveIO.EconomyVersion"/> 1 was balanced
        /// for (2026-09-14, crops of 40 s to 70 min). Kept ONLY so <see cref="SaveIO.MigrateEconomy"/> can
        /// tell where on that curve an old farm stood; nothing in the game reads it.</summary>
        static readonly (int lv, float xp, float coin)[] LegacyLevelAnchors =
        {
            (1, 150f, 800f), (2, 320f, 1500f), (3, 650f, 2600f), (4, 1100f, 4200f), (5, 2000f, 6500f),
            (6, 3200f, 9500f), (8, 7000f, 18000f), (10, 14000f, 32000f), (12, 26000f, 55000f),
            (15, 60000f, 120000f), (20, 160000f, 260000f), (25, 450000f, 620000f), (30, 1000000f, 1150000f),
        };

        static float LevelCurve(int lv, bool xp) { return Curve(LevelAnchors, lv, xp); }

        static float Curve((int lv, float xp, float coin)[] anchors, int lv, bool xp)
        {
            lv = Mathf.Max(1, lv);
            var last = anchors[anchors.Length - 1];
            if (lv >= last.lv) return (xp ? last.xp : last.coin) * Mathf.Pow(1.08f, lv - last.lv);
            for (int i = 1; i < anchors.Length; i++)
            {
                var a = anchors[i - 1]; var b = anchors[i];
                if (lv > b.lv) continue;
                float va = xp ? a.xp : a.coin, vb = xp ? b.xp : b.coin;
                float t = (lv - a.lv) / (float)(b.lv - a.lv);
                return va * Mathf.Pow(vb / va, t);
            }
            return xp ? anchors[0].xp : anchors[0].coin;
        }

        /// <summary>XP and coins a level cost under the old table (see <see cref="LegacyLevelAnchors"/>).</summary>
        public static void LegacyLevel(int lv, out int xpNeed, out int cost)
        {
            xpNeed = Nice(Curve(LegacyLevelAnchors, lv, true));
            cost = Nice(Curve(LegacyLevelAnchors, lv, false));
        }

        /// <summary>Two significant figures' worth of rounding, so the panel reads 5.700 rather
        /// than 5.677.</summary>
        static int Nice(float v)
        {
            float step = v < 1000f ? 10f : v < 100000f ? 100f : 1000f;
            return Mathf.Max(10, Mathf.RoundToInt(v / step) * (int)step);
        }

        public static LevelInfo Level(int lv)
        {
            return new LevelInfo
            {
                xpNeed = Nice(LevelCurve(lv, true)),
                // 0,6% faster per level, capped at 20%: at 1% (cap 45%) the long late crops were
                // cut back into short ones, undoing the point of making them long
                growCut = Mathf.Min(0.20f, 0.006f * lv),
                mutate = Mathf.Min(0.45f, 0.012f * lv + 0.06f),
                priceUp = Mathf.Min(1.2f, 0.02f * lv + 0.06f),
                energyUp = Mathf.Min(0.5f, 0.004f * lv),
                plots = Mathf.Min(16, 9 + (lv - 1) / 2),
                cost = Nice(LevelCurve(lv, false))
            };
        }

        public static readonly Chest[] Chests =
        {
            new Chest(0, "Rương thường", 300,
                "Có 90% cơ hội nhận được xu nông trại và vật phẩm thông thường, 10% cơ hội nhận được hạt giống."),
            new Chest(1, "Rương quý", 600,
                "Có 80% cơ hội nhận được xu nông trại, vật phẩm thông thường và 20% cơ hội nhận được một vật phẩm hiếm."),
            new Chest(2, "Rương thần kỳ", 1200,
                "Có 75% cơ hội nhận được xu nông trại, vật phẩm thông thường và 25% cơ hội nhận được một vật phẩm hiếm."),
            new Chest(3, "Rương huyền thoại", 3000,
                "Chắc chắn nhận được một vật phẩm hiếm cùng lượng lớn xu nông trại."),
        };

        /// <summary>The story missions — the strip under the player card always shows the first
        /// unclaimed one, so no step may be a wall. 2026-09-15: the last step is level 12 (Đảo Băng's
        /// gate), not 13 — with hour-long crops and a session-based day, 12 → 13 alone took the
        /// reference player three days. Tuned against Tools ▸ LQ Farm ▸ Kiểm tra hành
        /// trình chơi, which plays the game from a new farm: the level steps were 8 / 16 / 25 (the
        /// strip sat on "Đạt cấp độ 16" for 6–10 hours of non-stop play) and the visit step asked for
        /// ten visits in a game with six neighbours who can each be visited once a day.</summary>
        public static readonly Chapter[] Chapters =
        {
            new Chapter("Nông dân tập sự", new[]
            {
                new Task("c1a", "Thu hoạch cà rốt",     "harvest", 6,  800,  800, "carrot"),
                new Task("c1b", "Gieo trồng bất kỳ",    "plant",   10, 800,  800),
                new Task("c1c", "Tưới nước cho cây",    "water",   8,  1000, 1000),
                new Task("c1d", "Đạt cấp độ trang trại","level",   4,  1200, 1500),
            }),
            new Chapter("Người bán nông sản", new[]
            {
                new Task("c2a", "Bán nông sản bất kỳ",  "sell",    20, 2000, 2000),
                new Task("c2b", "Thu hoạch cà chua",    "harvest", 12, 2000, 2000, "tomato"),
                new Task("c2c", "Mở rương thần kỳ",     "chest",   3,  2500, 2500),
                new Task("c2d", "Đạt cấp độ trang trại","level",   7,  3000, 3500),
            }),
            new Chapter("Chuyên gia cây trồng", new[]
            {
                new Task("c3a", "Thu hoạch khoai tây",  "harvest", 16, 5000, 5000, "potato"),
                new Task("c3b", "Khoai tây để bán",     "sellCrop",2,  5000, 5000, "potato"),
                new Task("c3c", "Đạt cấp độ trang trại","level",   10, 5000, 5000),
                new Task("c3d", "Thu hoạch cây đột biến","mutate", 5,  6000, 6000),
            }),
            new Chapter("Bậc thầy nông trại", new[]
            {
                new Task("c4a", "Thu hoạch ngô",        "harvest", 24, 9000,  9000, "corn"),
                new Task("c4b", "Thăm nom bạn bè",      "visit",   6,  9000,  9000),
                new Task("c4c", "Thu thập bộ sưu tập",  "collect", 12, 12000, 12000),
                new Task("c4d", "Đạt cấp độ trang trại","level",   12, 15000, 20000),
            }),
        };

        public static readonly Task[] Daily =
        {
            new Task("d1", "Thu hoạch 15 cây trồng", "harvest", 15, 1200, 1200),
            new Task("d2", "Tưới nước 10 lần",       "water",   10, 1000, 1000),
            new Task("d3", "Gieo trồng 12 hạt giống","plant",   12, 1000, 1000),
            new Task("d4", "Bán 30 nông sản",        "sell",    30, 1500, 1800),
            new Task("d5", "Thăm nom 5 người bạn",   "visit",   5,  1500, 1500),
        };

        /// <summary>The goods shelf. Prices here are IGNORED — see <see cref="ShopSys.PriceOf"/>,
        /// which quotes everything in UNIT so the shelf stays relevant at every level. The Trang trí
        /// shelf lives in <see cref="Cosmetics"/>.
        ///
        /// "Mở rộng luống đất" is gone. A plot for 12.000 flat undercut the plot ladder, which is
        /// now the largest coin sink in the game and the reason levelling is worth anything — a
        /// shop item that sells the same thing cheaper turns that whole system off.</summary>
        public static readonly ShopItem[] ShopGoods =
        {
            new ShopItem("g1", "Bình tưới vàng",     "Tưới hết, bỏ qua cữ",   "item_can",   0, "", "water3"),
            new ShopItem("g2", "Phân bón thần kỳ",   "Chín ngay 1 ô",         "item_fert",  0, "", "instant"),
            new ShopItem("g3", "Bùa đột biến",       "+30% đột biến 10 phút", "item_charm", 0, "", "mutate"),
            new ShopItem("g4", "Túi hạt ngẫu nhiên", "×5 hạt giống",          "item_seedbag", 0, "", "seedbag"),
            new ShopItem("g5", "Năng lượng thần kỳ", "Đầy thanh, ra 1 rương", "item_energy", 0, "", "energy"),
            new ShopItem("g7", "Dự báo thời tiết",   "Dự báo suốt 12 giờ", "item_forecast", 0, "", "forecast"),
            new ShopItem("g8", "Đổi đơn hàng",       "Làm mới 1 đơn",         "item_reroll", 0, "", "reroll"),
            new ShopItem("g9", "Nhà kính",           "6 lần gieo né thời tiết xấu", "item_green", 0, "", "green"),
            new ShopItem("g10", "Bùa kinh nghiệm",   "×2 XP thu hoạch 10 phút", "item_xpcharm", 0, "", "xp2"),
            new ShopItem("g11", "Thuốc lớn nhanh",   "Cả đảo chín nhanh 50%", "item_tonic", 0, "", "tonic"),
            new ShopItem("g12", "Rương quý",         "Mở ra vật phẩm quý",    "item_chest", 0, "", "chest"),
            new ShopItem("g13", "Túi hạt quý",       "×3 hạt cây xịn nhất",   "item_seedgold", 0, "", "seedbest"),
        };

        public static readonly Friend[] Friends =
        {
            new Friend("f1", "Mạnh • Vườn Xanh",  9,  "broccoli",    "#4a9c6a"),
            new Friend("f2", "Hà • Đồng Nội",     10, "peach",       "#c98a3a"),
            new Friend("f3", "Tuấn • Nhà Kính",   9,  "grape",       "#5a86c9"),
            new Friend("f4", "Linh • Mật Ong",    12, "cauliflower", "#7a5ac9"),
            new Friend("f5", "Khoa • Đất Đỏ",     11, "mushroom",    "#c95a6a"),
            new Friend("f6", "Vy • Nắng Vàng",    8,  "orange",      "#c9a83a"),
        };

        /// <summary>Collection sets.
        ///
        /// The shape is unchanged from the four-element build — a set is still four cells of one
        /// crop, or four of one tier across crops — because merging rarity into the element axis
        /// meant the book never needed rebuilding. Twenty-eight crops x five states (plain plus
        /// four tiers) is 140 cells, against the 135 the old book had.</summary>
        public static readonly CollectionSet[] Collections =
        {
            new CollectionSet("k1", "Lúa Mì Tứ Bậc", 12000, 3000, new[]
            {
                new CollectItem("wheat", 1, "Lúa Mì Ngọc"),  new CollectItem("wheat", 2, "Lúa Mì Băng"),
                new CollectItem("wheat", 3, "Lúa Mì Hoả"),   new CollectItem("wheat", 4, "Lúa Mì Lôi"),
            }),
            new CollectionSet("k2", "Khoai Tây Tứ Bậc", 14000, 3600, new[]
            {
                new CollectItem("potato", 1, "Khoai Tây Ngọc"), new CollectItem("potato", 2, "Khoai Tây Băng"),
                new CollectItem("potato", 3, "Khoai Tây Hoả"),  new CollectItem("potato", 4, "Khoai Tây Lôi"),
            }),
            new CollectionSet("k3", "Cà Chua Tứ Bậc", 16000, 4200, new[]
            {
                new CollectItem("tomato", 1, "Cà Chua Ngọc"), new CollectItem("tomato", 2, "Cà Chua Băng"),
                new CollectItem("tomato", 3, "Cà Chua Hoả"),  new CollectItem("tomato", 4, "Cà Chua Lôi"),
            }),
            new CollectionSet("k4", "Vườn Ngọc Bích", 18000, 4400, new[]
            {
                new CollectItem("carrot", 1, "Cà Rốt Ngọc"),  new CollectItem("corn", 1, "Ngô Ngọc"),
                new CollectItem("pumpkin", 1, "Bí Ngô Ngọc"), new CollectItem("grape", 1, "Nho Ngọc"),
            }),
            new CollectionSet("k5", "Vườn Băng Giá", 22000, 5200, new[]
            {
                new CollectItem("carrot", 2, "Cà Rốt Băng"),  new CollectItem("corn", 2, "Ngô Băng"),
                new CollectItem("pumpkin", 2, "Bí Ngô Băng"), new CollectItem("grape", 2, "Nho Băng"),
            }),
            new CollectionSet("k6", "Vườn Lôi Điện", 30000, 7000, new[]
            {
                new CollectItem("carrot", 4, "Cà Rốt Lôi"),     new CollectItem("corn", 4, "Ngô Lôi"),
                new CollectItem("strawberry", 4, "Dâu Tây Lôi"),new CollectItem("watermelon", 4, "Dưa Hấu Lôi"),
            }),
        };

        public static readonly int[] CollectMilestones = { 6, 20, 45, 80 };

        /// <summary>28 crops x 5 states (plain + four mutation tiers).</summary>
        public static readonly int CollectTotal = Seeds.Length * Art.Elements.Length;
    }
}
