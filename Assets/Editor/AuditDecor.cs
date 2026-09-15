using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Screenshots of the painted yard props and the thirsty signal (owner, 15/9): D{island}{0 day | 1 snow |
    /// 2 night}_*.png for all eight islands, D80–D86 for the thirsty drop — at farm zoom by day, pulled back a little,
    /// at night, under snow and in the rain, with a crop's popup open, and a burst of six frames of the hop.
    ///
    /// Islands the player has not bought are opened for the pass and closed again; plots on the first three islands
    /// are staged ripe / thirsty / growing. Saving is held off the whole time (<see cref="PlayerState.loaded"/> is
    /// lowered, which SaveIO.Save refuses), so nothing staged here can reach the file — exit Play afterwards.</summary>
    public static class AuditDecor
    {
        [MenuItem("Tools/LQ Farm/Chụp decor & ô đất")]
        static void Capture()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(Run(GameApp.I));
        }

        public static IEnumerator Run(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(UiAudit.OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            var s = GS.Local;
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            bool wasLoaded = s.loaded;
            s.loaded = false;
            var was = new List<bool>();
            for (int i = 0; i < IslandSys.Max; i++)
            {
                var isl = s.EnsureIsland(i);
                was.Add(isl.unlocked);
                isl.unlocked = true;
            }
            // no tip lands in a shot: every tip counts as seen (in memory only), and one already up closes with a panel
            foreach (var tip in Tutorial.Tips) s.tipsSeen.Add(tip.id);
            app.Open(new UpgradePanel(app));
            yield return new WaitForSecondsRealtime(0.8f);
            app.CloseAll();
            if (s.lv < 12) s.lv = 12;
            s.SyncPlots();
            for (int i = 0; i < 3; i++) StagePlots(s, i);
            app.SyncIslands();
            app.Farm.SyncBridges(animateNew: false);
            app.ForceRedraw();
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();

            string[] keys = { "vuonnha", "nuoc", "khonglo", "gio", "bang", "hoa", "loi", "vang" };
            for (int i = 0; i < IslandSys.Max; i++)
            {
                app.GoToIsland(i);
                yield return new WaitForSecondsRealtime(1.2f);
                foreach (var (w, h, tag) in new[] { (Weather.Sunny, 12f, "0_day"), (Weather.Snow, 12f, "1_snow"), (Weather.Sunny, 22.5f, "2_night") })
                {
                    Sky(app, w, h);
                    yield return new WaitForSecondsRealtime(0.9f);
                    yield return Shot(dir, "D" + i + tag + "_" + keys[i]);
                }
            }

            // the thirsty drop on the home island
            app.GoToIsland(0);
            Sky(app, Weather.Sunny, 12f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot(dir, "D80_thirst_farm_zoom");
            var cam = app.Farm.Camera;
            cam.FlyTo(ArchipelagoView.IslandOrigin(0), cam.ZBase * 0.66f, 0.3f);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(dir, "D81_thirst_pulled_back");
            app.GoToIsland(0);
            yield return new WaitForSecondsRealtime(1.0f);
            Sky(app, Weather.Sunny, 22.5f);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "D82_thirst_night");
            Sky(app, Weather.Snow, 12f);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "D83_thirst_snow");
            Sky(app, Weather.Rain, 15f);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "D84_thirst_rain");
            Sky(app, Weather.Sunny, 12f);
            yield return new WaitForSecondsRealtime(0.6f);
            // the popup of a growing crop: the times live there now that the bars are gone
            var plots = app.Farm.Plots;
            for (int i = 0; i < plots.Count; i++)
                if (PlotLogic.State(plots[i]) == PlotState.Growing) { app.OpenPlot(i); break; }
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(dir, "D85_popup");
            app.ClosePlotPopup();
            yield return new WaitForSecondsRealtime(0.4f);
            // six frames of the hop, a sixth of a period apart
            for (int f = 0; f < 6; f++)
            {
                string path = System.IO.Path.Combine(dir, "D86_hop_" + f + ".png");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(0.19f);
            }
            yield return new WaitForSecondsRealtime(0.6f);

            // put everything back
            for (int i = 0; i < IslandSys.Max && i < was.Count; i++) s.islands[i].unlocked = was[i];
            app.SyncIslands();
            app.Farm.SyncBridges(animateNew: false);
            app.ForceRedraw();
            app.GoToIsland(0);
            app.WeatherView.ReleaseWeather(WeatherSys.Now(s));
            DayCycle.Release();
            app.SkyView.Repaint();
            s.loaded = wasLoaded;
            Tutorial.SuppressTips = quiet;
            Debug.Log("[AuditDecor] Decor & ô đất xong — thoát Play (trạng thái ô đã bị dựng lại trong bộ nhớ).");
        }

        static void Sky(GameApp app, Weather w, float hour)
        {
            app.WeatherView.ForceWeather(w);
            DayCycle.ForceHour(hour);
            app.SkyView.Repaint();
        }

        /// <summary>Ripe, thirsty and growing crops on every open plot of one island (small crops on small plots, apples
        /// on big ones), two left empty.</summary>
        static void StagePlots(PlayerState s, int island)
        {
            var isl = s.islands[island];
            int n = 0;
            for (int i = 0; i < isl.plots.Count; i++)
            {
                var p = isl.plots[i];
                if (p.none) continue;
                if (island > 0) p.locked = false;
                if (p.locked) continue;
                if (n == 7) { p.crop = null; n++; continue; }
                string[] small = { "carrot", "wheat", "tomato", "pumpkin" };
                var seed = GameData.Get(p.big ? "apple" : small[n % small.Length]);
                p.crop = seed.id;
                p.dur = s.GrowTime(seed);
                p.cut = 0;
                p.waterMask = 0;
                p.friendMask = 0;
                p.windowCount = (byte)WaterSys.WindowsFor(seed);
                p.waterSec = seed.waterCut;
                p.plantWeather = 0;
                p.variant = 0;
                float period = WaterSys.Period(p.dur, WaterSys.Windows(p));
                // 0 ripe · 1, 3 thirsty · 2 growing
                float at = (n % 4) switch { 0 => p.dur + 5f, 1 => period + 1f, 3 => period + 1f, _ => period * 0.5f };
                p.plantedAt = GS.Now - (long)(at * 1000f);
                n++;
            }
        }

        static IEnumerator Shot(string dir, string name)
        {
            string path = System.IO.Path.Combine(dir, name + ".png");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[AuditDecor] " + path);
        }
    }
}
