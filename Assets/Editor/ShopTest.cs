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
        [MenuItem("Tools/LQ Farm/Kiểm tra cửa hàng")]
        public static void Run()
        {
            var fails = new List<string>();

            EveryGoodHasAPrice(fails);
            PricesTrackTheEconomy(fails);
            PricesStayInReach(fails);
            NoPlotForSale(fails);
            GreenhouseOnlySpendsOnBadWeather(fails);
            ShelterNeverCostsTheBonus(fails);
            ShelterDoesNotMutateTheTable(fails);
            ForecastExtends(fails);

            if (fails.Count == 0) Debug.Log("Cửa hàng OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Cửa hàng: " + f);
                Debug.LogError($"Cửa hàng: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

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

        /// <summary>Every item must cost between about three and forty harvests. Under three it is
        /// free and the shelf is noise; over forty nobody ever buys it and the shelf is scenery.</summary>
        static void PricesStayInReach(List<string> fails)
        {
            for (int lv = 1; lv <= 30; lv += 3)
            {
                var s = At(lv);
                int unit = MissionSys.Unit(s);
                foreach (var it in GameData.ShopGoods)
                {
                    float harvests = ShopSys.PriceOf(s, it) / (float)unit;
                    Check(fails, harvests >= 3f && harvests <= 40f,
                          $"cấp {lv} · {it.name}: {harvests:0.0} lần thu hoạch, ngoài khoảng 3..40");
                }
            }
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

            int hours = ShopSys.ForecastHours(s);
            Check(fails, hours >= 11 && hours <= 13, $"dự báo {hours} giờ, kỳ vọng ~12");

            s.forecastUntil = GS.Now - 1;
            Check(fails, !ShopSys.ForecastActive(s), "dự báo hết hạn vẫn chạy");
        }
    }
}
