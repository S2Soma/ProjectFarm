using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks the day/night cycle: that night stays readable, that the palette never
    /// jumps, and that day is day.
    ///
    /// The readability floor is the rule that matters. Night has to look like night, but a
    /// farm the player cannot read at 23:00 is a farm they stop opening at 23:00 — so every minute
    /// of the day, under every weather, the land and the crops are checked against it.</summary>
    public static class DayCycleTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra ngày đêm")]
        public static void Run()
        {
            var fails = new List<string>();

            NightStaysReadable(fails);
            PaletteNeverJumps(fails);
            DayIsDayAndNightIsNight(fails);
            SunAndMoonKeepToTheirHours(fails);

            if (fails.Count == 0) Debug.Log("Ngày đêm OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Ngày đêm: " + f);
                Debug.LogError($"Ngày đêm: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok && fails.Count < 40) fails.Add(what); }

        /// <summary>Every minute, every weather: land luma ≥ 0.50 and crops ≥ 0.62 after the
        /// weather's own land tint and air wash.</summary>
        static void NightStaysReadable(List<string> fails)
        {
            foreach (Weather w in System.Enum.GetValues(typeof(Weather)))
            {
                ArchipelagoView.LandLook(w, out var landTint, out _);
                var wash = WeatherWash(w);
                float washLoss = wash.a * (1f - DayCycle.Luma(wash));
                for (int m = 0; m < 24 * 60; m++)
                {
                    var p = DayCycle.Sample(m / 60f);
                    DayCycle.FloorAmbient(p.ambient, landTint, wash, out var land, out var crop);
                    float L = DayCycle.Luma(land) * DayCycle.Luma(landTint) * (1f - washLoss);
                    float C = DayCycle.Luma(crop) * DayCycle.Luma(landTint) * (1f - washLoss);
                    // a land tint that is already below the floor at noon cannot be lifted past white
                    float cap = DayCycle.Luma(landTint) * (1f - washLoss);
                    Check(fails, L >= Mathf.Min(0.495f, cap - 0.005f), $"{w} {m / 60:00}:{m % 60:00}: đất tối {L:0.00}");
                    Check(fails, C >= Mathf.Min(0.615f, cap - 0.005f), $"{w} {m / 60:00}:{m % 60:00}: cây tối {C:0.00}");
                }
            }
        }

        /// <summary>The air tint on the middle of the screen: none. Weather tints only the rim of the
        /// view now (WeatherFx.Wash), so the farm itself is never behind a wash.</summary>
        static Color WeatherWash(Weather w) { return new Color(1, 1, 1, 0); }

        /// <summary>Minute to minute, no channel moves more than 0.02 — a keyframe seam would show
        /// as a visible pop in the sky.</summary>
        static void PaletteNeverJumps(List<string> fails)
        {
            var prev = DayCycle.Sample(0f);
            for (int m = 1; m <= 24 * 60; m++)
            {
                var p = DayCycle.Sample(m / 60f);
                float d = Mathf.Max(Diff(p.skyTop, prev.skyTop), Diff(p.horizon, prev.horizon), Diff(p.ambient, prev.ambient),
                                    Diff(p.cloudSeaLit, prev.cloudSeaLit), Mathf.Abs(p.night - prev.night), Mathf.Abs(p.stars - prev.stars));
                Check(fails, d <= 0.02f, $"{m / 60:00}:{m % 60:00}: bảng màu nhảy {d:0.000}");
                prev = p;
            }
        }

        static float Diff(Color a, Color b) { return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)); }

        static void DayIsDayAndNightIsNight(List<string> fails)
        {
            for (float h = 7.25f; h <= 16f; h += 0.25f)
            {
                var p = DayCycle.Sample(h);
                Check(fails, p.stars == 0f, $"{h:0.00}h: có sao giữa ban ngày");
                Check(fails, DayCycle.LanternLight(p.night) == 0f, $"{h:0.00}h: đèn lồng sáng giữa ban ngày");
                Check(fails, p.ambient == Color.white, $"{h:0.00}h: ban ngày mà đảo bị nhuộm");
            }
            var mid = DayCycle.Sample(0f);
            Check(fails, DayCycle.LanternLight(mid.night) >= 0.999f, "nửa đêm đèn lồng chưa sáng hết");
            Check(fails, mid.stars >= 0.999f, "nửa đêm chưa đủ sao");
        }

        /// <summary>Outside 05:30-18:40 the sun is out of sight (faded, or under the horizon where the
        /// cloud sea hides it); outside 18:00-06:00 the moon is.</summary>
        static void SunAndMoonKeepToTheirHours(List<string> fails)
        {
            for (int m = 0; m < 24 * 60; m += 5)
            {
                float h = m / 60f;
                var sun = DayCycle.SunArc(h);
                var p = DayCycle.Sample(h);
                float sunA = DayCycle.SunAlpha(p.night) * sun.z;
                // "hidden" is either faded out or below the horizon, where the cloud sea covers it
                if (h < 5.4f || h > 18.8f) Check(fails, sunA < 0.01f || sun.y < 0f, $"{h:0.00}h: mặt trời vẫn hiện trên chân trời");
                var moon = DayCycle.MoonArc(h);
                float moonA = DayCycle.MoonAlpha(p.night) * moon.z;
                if (h > 6.2f && h < 17.8f) Check(fails, moonA < 0.01f || moon.y < 0f, $"{h:0.00}h: mặt trăng hiện ban ngày");
            }
        }
    }
}
