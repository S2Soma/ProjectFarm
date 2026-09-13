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

        /// <summary>Fruits per harvest.
        ///
        /// <see cref="sell"/> is the value of a WHOLE harvest, so per-fruit price is sell/fruits
        /// and yield changes nothing about revenue. That is deliberate: making yield a revenue
        /// multiplier would six-times the economy overnight the moment a six-fruit crop unlocked.
        /// What yield actually does is split the crops into volume and value — a watermelon is
        /// one big fruit, cherries come in a punnet — which is what makes island tribute
        /// ("1080 Anh Đào") reachable and gives the two kinds of crop different jobs.</summary>
        public int fruits = 1;

        public Seed(string id, string name, string art, int r, int lv, int price,
                    int grow, int xp, int en, int sell, int colors, string badge, int fruits = 1)
        {
            this.id = id; this.name = name; this.art = art; this.r = r; this.lv = lv;
            this.price = price; this.grow = grow; this.xp = xp; this.en = en;
            this.sell = sell; this.colors = colors; this.badge = badge; this.fruits = fruits;
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
        // rarity: 0 thuong | 1 hiem | 2 su thi | 3 huyen thoai
        public static readonly Seed[] Seeds =
        {
            new Seed("carrot",     "Cà Rốt",       "carrot",     0, 1,  0,    40,  8,   2,  42,   4, null, 6),
            new Seed("wheat",      "Lúa Mì",       "wheat",      0, 1,  83,   55,  12,  2,  118,  4, null, 5),
            new Seed("tomato",     "Cà Chua",      "tomato",     0, 2,  224,  80,  20,  3,  268,  4, null, 5),
            new Seed("potato",     "Khoai Tây",    "potato",     0, 3,  251,  92,  24,  3,  302,  4, null, 5),
            new Seed("corn",       "Ngô",          "corn",       0, 4,  298,  105, 28,  3,  352,  4, null, 4),

            new Seed("mushroom",   "Nấm Rừng",     "mushroom",   1, 5,  137,  70,  42,  4,  176,  4, "exp", 4),
            new Seed("leek",       "Tỏi Tây",      "leek",       1, 6,  199,  96,  34,  4,  246,  4, null, 4),
            new Seed("garlic",     "Hành Bổ",      "garlic",     1, 7,  225,  110, 38,  4,  282,  4, null, 4),
            new Seed("broccoli",   "Súp Lơ Xanh",  "broccoli",   1, 8,  247,  122, 42,  4,  312,  4, null, 4),
            new Seed("pepper",     "Ớt Chuông",    "pepper",     1, 9,  266,  134, 46,  5,  338,  4, null, 4),
            new Seed("eggplant",   "Cà Tím",       "eggplant",   1, 10, 285,  146, 50,  5,  364,  4, null, 4),
            new Seed("cauliflower","Súp Lơ Trắng", "cauliflower",1, 11, 357,  168, 58,  5,  452,  4, "helm", 4),

            new Seed("pumpkin",    "Bí Ngô",       "pumpkin",    2, 12, 285,  150, 62,  6,  392,  4, null, 3),
            new Seed("radish",     "Củ Cải",       "radish",     2, 13, 385,  211, 65,  5,  406,  4, "sprout", 4),
            new Seed("beetroot",   "Củ Dền",       "beetroot",   2, 14, 412,  224, 72,  6,  528,  4, "helm", 3),
            new Seed("grape",      "Nho Tím",      "grape",      2, 15, 448,  240, 96,  6,  566,  4, "exp", 6),
            new Seed("cabbage",    "Bắp Cải",      "cabbage",    2, 16, 486,  258, 84,  7,  618,  4, "helm", 3),
            new Seed("lemon",      "Chanh Vàng",   "lemon",      2, 17, 522,  272, 90,  7,  664,  4, "helm", 4),
            new Seed("orange",     "Cam",          "orange",     2, 19, 604,  300, 104, 8,  772,  4, "helm", 4),
            new Seed("pear",       "Lê",           "pear",       2, 21, 668,  318, 112, 8,  856,  4, "helm", 3),

            new Seed("peach",      "Táo Đỏ",       "peach",      3, 22, 742,  340, 126, 9,  962,  4, "helm", 3),
            new Seed("strawberry", "Dâu Tây",      "strawberry", 3, 23, 806,  356, 168, 9,  1042, 4, "exp", 6),
            new Seed("cherries",   "Anh Đào",      "cherries",   3, 24, 874,  372, 142, 10, 1128, 4, "helm", 6),
            new Seed("banana",     "Chuối",        "banana",     3, 25, 948,  392, 152, 10, 1224, 4, "helm", 5),
            new Seed("watermelon", "Dưa Hấu",      "watermelon", 3, 26, 1026, 410, 164, 11, 1328, 4, "helm", 2),
            new Seed("pineapple",  "Dứa",          "pineapple",  3, 28, 1120, 430, 176, 11, 1440, 4, "helm", 2),
            new Seed("coconut",    "Dừa",          "coconut",    3, 29, 1180, 448, 188, 12, 1546, 4, "sprout", 3),
            new Seed("avocado",    "Bơ Sáp",       "avocado",    3, 30, 1280, 470, 204, 12, 1690, 4, "helm", 2),
        };

        static Dictionary<string, Seed> _byId;
        public static Dictionary<string, Seed> ById =>
            _byId ?? (_byId = Seeds.ToDictionary(s => s.id));

        public static Seed Get(string id)
        {
            Seed s; return id != null && ById.TryGetValue(id, out s) ? s : null;
        }

        /// <summary>16 plots max; a new plot every two levels.</summary>
        public static LevelInfo Level(int lv)
        {
            return new LevelInfo
            {
                xpNeed = Mathf.RoundToInt(3600f * Mathf.Pow(1.28f, lv - 1)),
                growCut = Mathf.Min(0.45f, 0.01f * lv),
                mutate = Mathf.Min(0.45f, 0.012f * lv + 0.06f),
                priceUp = Mathf.Min(1.2f, 0.02f * lv + 0.06f),
                energyUp = Mathf.Min(0.5f, 0.004f * lv),
                plots = Mathf.Min(16, 9 + (lv - 1) / 2),
                cost = 10000 * lv + 5000
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
                new Task("c2d", "Đạt cấp độ trang trại","level",   8,  3000, 3500),
            }),
            new Chapter("Chuyên gia cây trồng", new[]
            {
                new Task("c3a", "Thu hoạch khoai tây",  "harvest", 16, 5000, 5000, "potato"),
                new Task("c3b", "Khoai tây để bán",     "sellCrop",2,  5000, 5000, "potato"),
                new Task("c3c", "Đạt cấp độ trang trại","level",   16, 5000, 5000),
                new Task("c3d", "Thu hoạch cây đột biến","mutate", 5,  6000, 6000),
            }),
            new Chapter("Bậc thầy nông trại", new[]
            {
                new Task("c4a", "Thu hoạch ngô",        "harvest", 24, 9000,  9000, "corn"),
                new Task("c4b", "Thăm nom bạn bè",      "visit",   10, 9000,  9000),
                new Task("c4c", "Thu thập bộ sưu tập",  "collect", 12, 12000, 12000),
                new Task("c4d", "Đạt cấp độ trang trại","level",   25, 15000, 20000),
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

        /// <summary>Cosmetics. The sub-labels used to be baked strings like "0/1 có thể mua" and
        /// the corner badge a baked "22n 10g" — neither ever changed, so the counter still said
        /// 0/1 after buying and the countdown never counted. Both are now either computed in the
        /// panel or absent.</summary>
        public static readonly ShopItem[] ShopCoin =
        {
            new ShopItem("s1", "Khung ảnh Mùa Vàng",  "Trang trí hồ sơ",  "cheese",   19999, ""),
            new ShopItem("s2", "Hiệu ứng Lá Bay",     "Trang trí hồ sơ",  "bread",    19999, ""),
            new ShopItem("s3", "Chân dung nông dân",  "Trang trí hồ sơ",  "egg",      19999, ""),
            new ShopItem("s4", "Mảnh ngọc bí ẩn",     "Trang trí hồ sơ", "grape",    1200,  ""),
            new ShopItem("s5", "Khung hình bạc",      "Trang trí hồ sơ",  "salad",    8800,  ""),
            new ShopItem("s6", "Biểu cảm Vui Vẻ",     "Trang trí hồ sơ",  "honey",    6600,  ""),
            new ShopItem("s7", "Rương bí ẩn",         "Trang trí hồ sơ",  "coconut",  15000, ""),
            new ShopItem("s8", "Huy hiệu Nhà Nông",   "Trang trí hồ sơ",  "lemon",    9900,  ""),
            new ShopItem("s9", "Bó hồng nông trại",   "Trang trí hồ sơ",  "cherries", 3500,  ""),
        };

        /// <summary>The goods shelf. Prices here are IGNORED — see <see cref="ShopSys.PriceOf"/>,
        /// which quotes everything in UNIT so the shelf stays relevant at every level. The int is
        /// kept only so the type is shared with the cosmetics tab.
        ///
        /// "Mở rộng luống đất" is gone. A plot for 12.000 flat undercut the plot ladder, which is
        /// now the largest coin sink in the game and the reason levelling is worth anything — a
        /// shop item that sells the same thing cheaper turns that whole system off.</summary>
        public static readonly ShopItem[] ShopGoods =
        {
            new ShopItem("g1", "Bình tưới vàng",     "Tưới hết, bỏ qua cữ",   "lime",       0, "", "water3"),
            new ShopItem("g2", "Phân bón thần kỳ",   "Chín ngay 1 ô",         "avocado",    0, "", "instant"),
            new ShopItem("g3", "Bùa đột biến",       "+30% đột biến 10 phút", "watermelon", 0, "", "mutate"),
            new ShopItem("g4", "Túi hạt ngẫu nhiên", "×5 hạt giống",          "pineapple",  0, "", "seedbag"),
            new ShopItem("g5", "Năng lượng thần kỳ", "+300 năng lượng",       "banana",     0, "", "energy"),
            new ShopItem("g7", "Dự báo thời tiết",   "Xem trước 12 giờ",      "lemon",      0, "", "forecast"),
            new ShopItem("g8", "Đổi đơn hàng",       "Làm mới 1 đơn",         "radish",     0, "", "reroll"),
            new ShopItem("g9", "Nhà kính",           "6 lần gieo né thời tiết xấu", "cauliflower", 0, "", "green"),
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
