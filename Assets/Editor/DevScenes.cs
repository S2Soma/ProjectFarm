using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Stages game state so a single screenshot shows every plot state at once.
    ///
    /// Watering windows open at fixed fractions of a crop's grow time, so reaching the "thirsty"
    /// state honestly means planting and then waiting — no use at all for checking that it looks
    /// right. These back-date <c>plantedAt</c> instead, which is exactly how the real thing
    /// reaches those states and therefore exercises the same code.
    ///
    /// Editor-only, play-mode-only, and nothing in the game calls it.</summary>
    public static class DevScenes
    {
        /// <summary>A mid-game farm for screenshots: level 12, three islands open with their
        /// bridges, twelve beds on the home island with ripe, thirsty and growing crops, a chest
        /// waiting. The screenshot pass runs on whatever save the Editor has, so without a
        /// reproducible state its pictures depend on what the last session happened to do.</summary>
        [MenuItem("Tools/LQ Farm/Dev: dựng ván giữa game (cấp 12)")]
        public static void StageMidGame()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            var app = GameApp.I;
            if (app == null) { Debug.LogWarning("Chưa có GameApp."); return; }
            app.EnsureBuilt();

            var s = GS.Local;
            s.lv = 12;
            s.xp = 1200;
            s.coin = 286400;
            s.energy = 222;
            s.chests[0] = 1;
            for (int i = 1; i <= 2; i++)
            {
                var isl = s.EnsureIsland(i);
                isl.unlocked = true;
                isl.unlockedAt = GS.Now;
            }
            s.SyncPlots();
            var home = s.islands[0];
            for (int k = 0; k < 12 && k < home.plots.Count; k++) home.plots[k].locked = false;

            app.SyncIslands();
            app.Farm.SyncBridges(animateNew: false);
            StagePlotStates();
            GS.Save();
            Debug.Log("Dev: đã dựng ván giữa game (cấp 12, 3 đảo).");
        }

        [MenuItem("Tools/LQ Farm/Dev: dựng đủ trạng thái ô đất")]
        public static void StagePlotStates()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            var app = GameApp.I;
            if (app == null) { Debug.LogWarning("Chưa có GameApp."); return; }
            app.EnsureBuilt();

            var s = GS.Local;
            s.AddSeed("carrot", 20); s.AddSeed("wheat", 20); s.AddSeed("tomato", 20);

            // a four-hour pumpkin among them, so a long crop's popup and bars are in frame too
            string[] crops = { "carrot", "wheat", "tomato", "pumpkin" };
            int staged = 0;

            for (int i = 0; i < GS.PlotCount; i++)
            {
                var p = s.plots[i];
                if (p.locked) continue;

                // leave two plots empty so the "chưa gieo" tile is in frame too
                if (staged >= 7) { p.crop = null; staged++; continue; }

                var seed = GameData.Get(crops[staged % crops.Length]);
                p.crop = seed.id;
                p.dur = s.GrowTime(seed);
                p.cut = 0;
                p.waterMask = 0;
                p.friendMask = 0;
                p.windowCount = (byte)WaterSys.WindowsFor(seed);
                p.waterSec = seed.waterCut;
                p.plantWeather = 0;
                p.variant = staged == 2 ? 3 : 0;      // one legendary so the glow is in frame

                int w = WaterSys.Windows(p);
                float period = WaterSys.Period(p.dur, w);

                // 0,3 = ripe · 1,4 = window open · 2,5,6 = growing between windows
                float at = (staged % 3) switch
                {
                    0 => p.dur + 5f,                  // Ready
                    1 => period + 1f,                 // Thirsty — just inside the first window
                    _ => period * 0.5f,               // Growing — before the first window
                };
                p.plantedAt = GS.Now - (long)(at * 1000f);

                // one already-tended plot, so the damp tile is in frame next to the dry ones
                if (staged == 5) { p.waterMask = 1; p.cut = WaterSys.BaseCut(p); }

                staged++;
            }

            app.ForceRedraw();
            Debug.Log("Dev: đã dựng trạng thái ô đất (chín / khát / đang lớn / trống).");
        }

        /// <summary>Open the popup of the first plot whose watering window is currently open.
        /// The popup is the only place the watering rhythm is spelled out in numbers, so it is
        /// worth being able to look at it without waiting for a window to come round.</summary>
        [MenuItem("Tools/LQ Farm/Dev: mở popup ô đang khát")]
        public static void OpenThirstyPopup()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            var app = GameApp.I;
            if (app == null) { Debug.LogWarning("Chưa có GameApp."); return; }
            app.EnsureBuilt();

            var s = GS.Local;
            for (int i = 0; i < GS.PlotCount; i++)
                if (PlotLogic.State(s.plots[i]) == PlotState.Thirsty) { app.OpenPlot(i); return; }

            // fall back to any growing plot, which shows the "chưa tới cữ" branch instead
            for (int i = 0; i < GS.PlotCount; i++)
                if (PlotLogic.State(s.plots[i]) == PlotState.Growing) { app.OpenPlot(i); return; }

            Debug.LogWarning("Dev: không có ô nào đang lớn.");
        }

        /// <summary>Give the player a second island and plant it, so the multi-island paths run
        /// against real content. The unlock economy lands later; this is the machinery only.</summary>
        [MenuItem("Tools/LQ Farm/Dev: thêm đảo thứ 2")]
        public static void AddSecondIsland()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            var app = GameApp.I;
            if (app == null) { Debug.LogWarning("Chưa có GameApp."); return; }
            app.EnsureBuilt();

            var s = GS.Local;
            if (s.islands.Count < 2)
            {
                var isl = new Island(s.islands.Count, true) { unlockedAt = GS.Now };
                for (int i = 0; i < GS.PlotCount; i++) isl.plots.Add(new Plot { locked = i >= 8 });
                s.islands.Add(isl);
            }

            app.SyncIslands();

            // plant the new island so it is not an empty grid in the screenshot
            var second = s.islands[1];
            string[] crops = { "carrot", "wheat", "tomato" };
            for (int i = 0; i < second.plots.Count; i++)
            {
                var p = second.plots[i];
                if (p.locked) continue;
                var seed = GameData.Get(crops[i % crops.Length]);
                p.crop = seed.id;
                p.dur = s.GrowTime(seed);
                p.windowCount = (byte)WaterSys.WindowsFor(seed);
                p.waterSec = seed.waterCut;
                p.plantedAt = GS.Now - (long)(p.dur * (0.2f + 0.1f * (i % 5)) * 1000f);
            }

            app.ForceRedraw();
            Debug.Log("Dev: đã thêm đảo thứ 2, tổng = " + s.islands.Count);
        }

        static int _previewWeather = -1;

        /// <summary>Step the map through the six weathers (visual only — values, the HUD and the
        /// real hour are untouched). Run "Dev: thời tiết thật" to return.</summary>
        [MenuItem("Tools/LQ Farm/Dev: xem thời tiết kế tiếp")]
        public static void NextWeather()
        {
            if (!Application.isPlaying || GameApp.I == null) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            GameApp.I.EnsureBuilt();
            int n = System.Enum.GetValues(typeof(Weather)).Length;
            _previewWeather = (_previewWeather + 1) % n;
            var w = (Weather)_previewWeather;
            GameApp.I.WeatherView.ForceWeather(w);
            Debug.Log("Dev: đang xem thời tiết " + WeatherSys.Def(w).name);
        }

        [MenuItem("Tools/LQ Farm/Dev: thời tiết thật")]
        public static void RealWeather()
        {
            if (!Application.isPlaying || GameApp.I == null) return;
            GameApp.I.EnsureBuilt();
            _previewWeather = -1;
            GameApp.I.WeatherView.ReleaseWeather(WeatherSys.Now(GS.Local));
        }

        // ---- time of day ----
        static void ShowHour(float h)
        {
            if (!Application.isPlaying || GameApp.I == null) { Debug.LogWarning("Cần đang ở Play mode."); return; }
            GameApp.I.EnsureBuilt();
            DayCycle.ForceHour(h);
            GameApp.I.SkyView.Repaint();
            Debug.Log($"Dev: đang xem {(int)h:00}:{(int)((h % 1f) * 60f):00}");
        }

        [MenuItem("Tools/LQ Farm/Dev: giờ 06:00 (bình minh)")] public static void Hour06() { ShowHour(6.0f); }
        [MenuItem("Tools/LQ Farm/Dev: giờ 12:00 (trưa)")]      public static void Hour12() { ShowHour(12.0f); }
        [MenuItem("Tools/LQ Farm/Dev: giờ 17:15 (nắng vàng)")]  public static void Hour17() { ShowHour(17.25f); }
        [MenuItem("Tools/LQ Farm/Dev: giờ 18:30 (chạng vạng)")] public static void Hour18() { ShowHour(18.5f); }
        [MenuItem("Tools/LQ Farm/Dev: giờ 23:00 (đêm)")]       public static void Hour23() { ShowHour(23.0f); }

        [MenuItem("Tools/LQ Farm/Dev: giờ thật")]
        public static void RealHour()
        {
            if (!Application.isPlaying || GameApp.I == null) return;
            GameApp.I.EnsureBuilt();
            DayCycle.Release();
            GameApp.I.SkyView.Repaint();
        }

        [MenuItem("Tools/LQ Farm/Dev: xoá file lưu")]
        public static void WipeSave()
        {
            SaveIO.Delete();
            Debug.Log("Dev: đã xoá file lưu. Vào Play để bắt đầu ván mới.");
        }
    }
}
