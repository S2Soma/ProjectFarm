using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the goods shelf.
    ///
    /// The thing worth asserting is not that an item works — that is visible the moment you buy
    /// one — but that the shelf stays AFFORDABLE AND WORTH BUYING across thirty levels. That is a
    /// relationship between two tables nobody looks at together, and it is what quietly broke the
    /// old flat price list.</summary>
    public static class ShopTest
    {
        /// <summary>TONGDAIYUMMY gives exactly 22 triệu XP and 22 tỷ coins (past int's 2,1 tỷ), once per
        /// farm; case and spaces do not matter; a wrong code changes nothing.</summary>
        static void GiftCodeWorksOnce(List<string> fails)
        {
            var s = new PlayerState();
            s.NewGame();
            long coin0 = s.coin, xp0 = s.xp;

            var bad = GiftCodes.Redeem(s, "KHONGCOMA", out _);
            Check(fails, bad == GiftCodes.Result.Unknown && s.coin == coin0 && s.xp == xp0, "mã sai vẫn đổi được gì đó");
            Check(fails, GiftCodes.Redeem(s, "   ", out _) == GiftCodes.Result.Empty, "mã rỗng không bị từ chối");

            var ok = GiftCodes.Redeem(s, " tongdai-yummy ", out var gift);
            Check(fails, ok == GiftCodes.Result.Ok, $"mã TONGDAIYUMMY (chữ thường, có gạch) không nhận được: {ok}");
            Check(fails, s.xp - xp0 == 22_000_000L, $"TONGDAIYUMMY cho {s.xp - xp0} XP, kỳ vọng 22.000.000");
            Check(fails, s.coin - coin0 == 22_000_000_000L, $"TONGDAIYUMMY cho {s.coin - coin0} xu, kỳ vọng 22.000.000.000");

            Check(fails, s.petFreeEggs == 300, $"TONGDAIYUMMY cho {s.petFreeEggs} trứng thú cưng, kỳ vọng 300");

            long coin1 = s.coin;
            Check(fails, GiftCodes.Redeem(s, "TONGDAIYUMMY", out _) == GiftCodes.Result.AlreadyUsed && s.coin == coin1 && s.petFreeEggs == 300,
                  "dùng TONGDAIYUMMY được lần hai");

            // a farm that redeemed the code before it carried eggs gets the eggs once, and nothing else again
            var old = new PlayerState();
            old.NewGame();
            old.redeemedCodes.Add("TONGDAIYUMMY");
            long oldCoin = old.coin, oldXp = old.xp;
            var top = GiftCodes.Redeem(old, "TONGDAIYUMMY", out _, out bool onlyAdded);
            Check(fails, top == GiftCodes.Result.Ok && onlyAdded && old.petFreeEggs == 300 && old.coin == oldCoin && old.xp == oldXp,
                  "nông trại đã nhập TONGDAIYUMMY trước đây không nhận được trứng (hoặc nhận lại xu/XP)");
            Check(fails, GiftCodes.Redeem(old, "TONGDAIYUMMY", out _) == GiftCodes.Result.AlreadyUsed && old.petFreeEggs == 300,
                  "trứng từ TONGDAIYUMMY nhận được hai lần");
            Check(fails, GiftCodes.Find("TONGDAIYUMMYEGGS") == null, "mã đánh dấu trứng lại nhập được như một mã");
        }

        [MenuItem("Tools/LQ Farm/Kiểm tra cửa hàng")]
        public static void Run()
        {
            var fails = new List<string>();

            EveryGoodHasAPrice(fails);
            PricesTrackTheEconomy(fails);
            PricesStayInReach(fails);
            RushIsPricedByTime(fails);
            NoPlotForSale(fails);
            GreenhouseOnlySpendsOnBadWeather(fails);
            ShelterNeverCostsTheBonus(fails);
            ShelterDoesNotMutateTheTable(fails);
            ForecastExtends(fails);
            CosmeticsAreRealAndExclusive(fails);
            GiftCodeWorksOnce(fails);

            if (fails.Count == 0) Debug.Log("Cửa hàng OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Cửa hàng: " + f);
                Debug.LogError($"Cửa hàng: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        /// <summary>Every cosmetic has art and a slot the game shows; buying wears it; one item per
        /// slot; you cannot buy twice or on credit; and the placeholder shelf's buyers keep what
        /// they paid for.</summary>
        static void CosmeticsAreRealAndExclusive(List<string> fails)
        {
            var ids = new HashSet<string>();
            foreach (var c in Cosmetics.All)
            {
                Check(fails, ids.Add(c.id), $"trang trí trùng id {c.id}");
                Check(fails, Resources.Load<Sprite>("Art/items/" + c.art) != null, $"{c.name}: thiếu hình Art/items/{c.art}");
                Check(fails, c.price > 0, $"{c.name}: giá 0");
            }
            foreach (CosmeticSlot slot in System.Enum.GetValues(typeof(CosmeticSlot)))
            {
                Check(fails, System.Array.Exists(Cosmetics.All, c => c.slot == slot), $"ô {slot} không có món nào");
                Check(fails, (int)slot < Cosmetics.SlotNames.Length, $"ô {slot} không có tên hiển thị");
            }
            // every cosmetic has to DO something visible: an effect recipe with real sprites, a plot
            // border, or a toast frame the game knows
            foreach (var c in Cosmetics.All)
            {
                switch (c.slot)
                {
                    case CosmeticSlot.Plant: case CosmeticSlot.Water: case CosmeticSlot.Harvest:
                    case CosmeticSlot.Tap: case CosmeticSlot.Swipe:
                    {
                        var rs = CosmeticFx.For(c.id);
                        Check(fails, rs != null && rs.Length > 0, $"{c.name}: không có công thức hiệu ứng");
                        if (rs != null)
                            foreach (var r in rs)
                                foreach (var sp in r.sprites)
                                    Check(fails, Resources.Load<Sprite>(sp) != null, $"{c.name}: thiếu hình hạt {sp}");
                        break;
                    }
                    case CosmeticSlot.Plot:
                        Check(fails, c.id.StartsWith("pk_") && Resources.Load<Sprite>("Art/beds/skin_" + c.id.Substring(3)) != null,
                              $"{c.name}: thiếu hình viền ô đất");
                        break;
                    case CosmeticSlot.Toast:
                        Check(fails, c.id.StartsWith("ts_"), $"{c.name}: khung thông báo phải có id ts_");
                        break;
                }
            }
            foreach (var d in IslandView.Decor)
                Check(fails, Cosmetics.Get(d.id) != null && Cosmetics.Get(d.id).slot == CosmeticSlot.Decor, $"vật trang trí đảo {d.id} không có trong cửa hàng");
            foreach (var c in Cosmetics.All)
                if (c.slot == CosmeticSlot.Decor)
                    Check(fails, System.Array.Exists(IslandView.Decor, d => d.id == c.id), $"{c.name}: bán trong cửa hàng nhưng không có chỗ trên đảo");

            var s = new PlayerState();
            s.NewGame();
            s.coin = 100000;
            Check(fails, Cosmetics.Buy(s, "fr_silver") && Cosmetics.IsWorn(s, "fr_silver"), "mua khung bạc không tự đeo");
            long after = s.coin;
            Check(fails, !Cosmetics.Buy(s, "fr_silver") && s.coin == after, "mua được khung bạc lần hai");
            Check(fails, Cosmetics.Buy(s, "fr_gold") && Cosmetics.IsWorn(s, "fr_gold") && !Cosmetics.IsWorn(s, "fr_silver"),
                  "hai khung cùng đeo một lúc");
            Check(fails, Cosmetics.Wear(s, "fr_silver") && !Cosmetics.IsWorn(s, "fr_gold"), "đổi khung không tháo khung cũ");
            Check(fails, !Cosmetics.Wear(s, "bd_star"), "đeo được huy hiệu chưa mua");
            Cosmetics.TakeOff(s, CosmeticSlot.Frame);
            Check(fails, Cosmetics.Worn(s, CosmeticSlot.Frame) == null, "tháo khung không được");

            s.coin = 10;
            Check(fails, !Cosmetics.Buy(s, "dc_roses") && !Cosmetics.Owns(s, "dc_roses") && s.coin == 10, "mua chịu được đồ trang trí");

            var old = new PlayerState();
            old.NewGame();
            old.shopBought.Add("s1"); old.shopBought.Add("s4");
            Cosmetics.MigrateLegacy(old);
            Check(fails, Cosmetics.Owns(old, "fr_gold"), "người đã mua 'Khung ảnh Mùa Vàng' cũ bị mất khung vàng");
        }

        static PlayerState At(int lv)
        {
            var s = new PlayerState();
            s.NewGame();
            s.lv = lv;
            return s;
        }

        static void EveryGoodHasAPrice(List<string> fails)
        {
            var s = At(10);
            foreach (var it in GameData.ShopGoods)
            {
                Check(fails, !string.IsNullOrEmpty(it.effect), $"{it.name}: không có effect");
                Check(fails, ShopSys.PriceOf(s, it) > 0, $"{it.name}: giá bằng 0");
                Check(fails, !string.IsNullOrEmpty(it.sub), $"{it.name}: không có mô tả");
                Check(fails, Art.Crop(it.art) != null, $"{it.name}: thiếu ảnh '{it.art}'");
            }
        }

        /// <summary>A price quoted in UNIT must actually move with UNIT — this is the whole point
        /// of the rewrite, and a stray absolute number would be invisible until level 25.</summary>
        static void PricesTrackTheEconomy(List<string> fails)
        {
            var lo = At(3);
            var hi = At(28);
            foreach (var it in GameData.ShopGoods)
            {
                int a = ShopSys.PriceOf(lo, it);
                int b = ShopSys.PriceOf(hi, it);
                Check(fails, b > a * 4, $"{it.name}: {a} → {b} qua 25 cấp, giá không bám theo kinh tế");
            }
        }

        /// <summary>Every item must cost between three and forty UNIT — three to forty eight-hour stretches
        /// of one plot's best crop. Under three it is free and the shelf is noise; over forty nobody ever
        /// buys it and the shelf is scenery.</summary>
        static void PricesStayInReach(List<string> fails)
        {
            for (int lv = 1; lv <= 30; lv += 1)
            {
                var s = At(lv);
                int unit = MissionSys.Unit(s);
                foreach (var it in GameData.ShopGoods)
                {
                    float units = ShopSys.PriceOf(s, it) / (float)unit;
                    Check(fails, units >= 3f && units <= 40f,
                          $"cấp {lv} · {it.name}: {units:0.0} UNIT, ngoài khoảng 3..40");
                }
            }
        }

        /// <summary>"Chín ngay" is priced by the time it skips, not per crop: a carrot with a minute left
        /// is pocket change, a fresh 20-hour watermelon costs several UNIT, more time left never costs
        /// less, and skipping a crop always costs more than the plot would have earned growing it.</summary>
        static void RushIsPricedByTime(List<string> fails)
        {
            var s = At(26);
            s.clock.lastSeenUtc = (long)(System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
            var saved = GS.Local;
            GS.Local = s;
            try
            {
                int unit = MissionSys.Unit(s);
                Plot Planted(string crop, float leftFrac)
                {
                    var sd = GameData.Get(crop);
                    float dur = s.GrowTimeIn(sd, Weather.Sunny);
                    return new Plot { locked = false, crop = crop, dur = dur, windowCount = (byte)sd.waters, waterSec = sd.waterCut,
                                      plantedAt = GS.Now - (long)(dur * (1f - leftFrac) * 1000f) };
                }
                float carrot = ShopSys.RushPrice(s, Planted("carrot", 0.5f)) / (float)unit;
                Check(fails, carrot <= 0.2f, $"chín ngay cà rốt còn 1 phút = {carrot:0.00} UNIT, quá đắt");
                float melon = ShopSys.RushPrice(s, Planted("watermelon", 1f)) / (float)unit;
                Check(fails, melon >= 3f && melon <= 10f, $"chín ngay dưa hấu vừa gieo = {melon:0.0} UNIT, ngoài 3..10");
                int prev = 0;
                foreach (float left in new[] { 0.01f, 0.1f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    int price = ShopSys.RushPrice(s, Planted("watermelon", left));
                    Check(fails, price >= prev, $"chín ngay dưa hấu còn {left:P0} rẻ hơn khi còn ít hơn ({price} < {prev})");
                    prev = price;
                }
                foreach (var sd in GameData.Seeds)
                {
                    if (sd.big || sd.lv > s.lv) continue;
                    int price = ShopSys.RushPrice(s, Planted(sd.id, 1f));
                    int margin = s.HarvestValue(sd.id, 0) - sd.price;
                    float hoursValue = unit * s.GrowTimeIn(sd, Weather.Sunny) / (MissionSys.UnitHours * 3600f);
                    Check(fails, price >= Mathf.Min(margin, hoursValue),
                          $"chín ngay {sd.name} vừa gieo = {price}, rẻ hơn giá trị thời gian đó ({Mathf.Min(margin, hoursValue):0})");
                }
            }
            finally { GS.Local = saved; }
        }

        /// <summary>The plot item is gone and must stay gone: it sold, cheaply and flatly, the
        /// exact thing the plot ladder exists to charge for.</summary>
        static void NoPlotForSale(List<string> fails)
        {
            foreach (var it in GameData.ShopGoods)
                Check(fails, it.effect != "plot",
                      "'Mở rộng luống đất' vẫn còn — nó phá thang giá ô đất");
        }

        static void GreenhouseOnlySpendsOnBadWeather(List<string> fails)
        {
            var s = At(10);
            s.greenhouse = 2;

            Check(fails, !ShopSys.UseGreenhouse(s, Weather.Sunny), "nhà kính tiêu lượt khi trời nắng");
            Check(fails, s.greenhouse == 2, "nắng vẫn trừ lượt nhà kính");

            // find a weather the design calls harmful and check it fires exactly once
            Weather bad = Weather.Sunny;
            foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
            {
                var d = WeatherSys.Def(w);
                if (d.grow > 1f || d.sell < 1f || d.xp < 1f) { bad = w; break; }
            }
            Check(fails, bad != Weather.Sunny, "không có kiểu thời tiết xấu nào — nhà kính vô nghĩa");

            Check(fails, ShopSys.UseGreenhouse(s, bad), $"nhà kính không kích hoạt khi {WeatherSys.Def(bad).name}");
            Check(fails, s.greenhouse == 1, $"còn {s.greenhouse} lượt, đáng lẽ 1");

            s.greenhouse = 0;
            Check(fails, !ShopSys.UseGreenhouse(s, bad), "dùng được nhà kính khi đã hết lượt");
        }

        /// <summary>Shelter must never be worse than the raw hour on any axis.
        ///
        /// This is the trap the greenhouse walked into first time: it replaced the hour with Nắng,
        /// and since only Nắng is neutral, that threw away Bão's +60% mutation and Gió Lớn's 15%
        /// faster growth. The most expensive item on the shelf was a downgrade in four hours out
        /// of six.</summary>
        static void ShelterNeverCostsTheBonus(List<string> fails)
        {
            foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
            {
                var raw = WeatherSys.Def(w);
                var sh = WeatherSys.Sheltered(w);
                string n = raw.name;

                Check(fails, sh.grow <= raw.grow, $"{n}: che chắn làm cây mọc CHẬM hơn ({raw.grow}→{sh.grow})");
                Check(fails, sh.sell >= raw.sell, $"{n}: che chắn làm giá THẤP hơn ({raw.sell}→{sh.sell})");
                Check(fails, sh.xp >= raw.xp, $"{n}: che chắn làm xp THẤP hơn ({raw.xp}→{sh.xp})");
                Check(fails, sh.mutate >= raw.mutate, $"{n}: che chắn làm đột biến THẤP hơn ({raw.mutate}→{sh.mutate})");

                Check(fails, sh.grow <= 1f && sh.sell >= 1f && sh.xp >= 1f && sh.mutate >= 1f,
                      $"{n}: che chắn còn sót hình phạt");

                bool anyPenalty = raw.grow > 1f || raw.sell < 1f || raw.xp < 1f || raw.mutate < 1f;
                Check(fails, WeatherSys.IsHarmful(w) == anyPenalty,
                      $"{n}: IsHarmful={WeatherSys.IsHarmful(w)} nhưng hình phạt={anyPenalty}");
            }
        }

        /// <summary>WeatherDef is a struct and Sheltered edits a copy. If it ever becomes a class,
        /// this silently rewrites the weather table for the whole run.</summary>
        static void ShelterDoesNotMutateTheTable(List<string> fails)
        {
            float before = WeatherSys.Def(Weather.Storm).sell;
            WeatherSys.Sheltered(Weather.Storm);
            float after = WeatherSys.Def(Weather.Storm).sell;
            Check(fails, Mathf.Approximately(before, after),
                  $"Sheltered() sửa luôn bảng thời tiết: Bão bán {before} → {after}");
        }

        static void ForecastExtends(List<string> fails)
        {
            var s = At(10);
            Check(fails, !ShopSys.ForecastActive(s), "mới tạo đã có dự báo");

            s.forecastUntil = GS.Now + ShopSys.ForecastMs;
            Check(fails, ShopSys.ForecastActive(s), "mua rồi mà dự báo không chạy");
            Check(fails, WeatherSys.Revealed(s, GS.Now), "dự báo đang chạy mà giờ kế vẫn bị che");

            int slots = ShopSys.ForecastSlots(s);
            long want = ShopSys.ForecastMs / WeatherSys.SlotMs;
            Check(fails, slots >= want - 1 && slots <= want + 1, $"dự báo {slots} lượt, kỳ vọng ~{want}");

            s.forecastUntil = GS.Now - 1;
            Check(fails, !ShopSys.ForecastActive(s), "dự báo hết hạn vẫn chạy");
        }
    }
}
