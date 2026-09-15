using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Screenshots of the sky and the clouds, C00–C39, and a measurement of what the sky costs the
    /// canvases (Tools ▸ LQ Farm ▸ Chụp mây).
    ///
    /// The whole map at five hours on a clear day, the map under every weather at noon and at golden hour,
    /// mid zoom and a close island view, four frames of cloud motion, and the start screen. Islands the
    /// player has not bought are opened for the pass and put back; saving is held off throughout
    /// (<see cref="PlayerState.loaded"/> lowered), exactly like the islands pass.</summary>
    public static class AuditClouds
    {
        [MenuItem("Tools/LQ Farm/Chụp mây")]
        static void Menu()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(Run(GameApp.I, "C", true));
        }

        [MenuItem("Tools/LQ Farm/Đo chi phí bầu trời")]
        static void MenuProfile()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(Profile(GameApp.I));
        }

        static string Dir
        {
            get
            {
                string dir = System.IO.Path.GetFullPath(UiAudit.OutputDir);
                System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static IEnumerator Shot(string name)
        {
            string path = System.IO.Path.Combine(Dir, name + ".png");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[AuditClouds] " + path);
        }

        static IEnumerator Look(GameApp app, Weather w, float hour, float settle = 0.8f)
        {
            app.WeatherView.ForceWeather(w);
            DayCycle.ForceHour(hour);
            app.SkyView.Repaint();
            yield return new WaitForSecondsRealtime(settle);
        }

        public static IEnumerator Run(GameApp app, string prefix, bool withStart)
        {
            app.EnsureBuilt();
            yield return null;
            var s = GS.Local;
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            app.Tutorial?.EndAuditPreview();          // a tip already on screen would dim every shot
            bool wasLoaded = s.loaded;
            s.loaded = false;
            var was = new List<bool>();
            for (int i = 0; i < IslandSys.Max; i++)
            {
                var isl = s.EnsureIsland(i);
                was.Add(isl.unlocked);
                isl.unlocked = true;
            }
            s.SyncPlots();
            app.SyncIslands();
            app.Farm.SyncBridges(animateNew: false);
            app.ForceRedraw();
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();

            // ---- the whole map ----
            app.ShowArchipelago();
            yield return new WaitForSecondsRealtime(1.4f);
            var hours = new (float h, string tag)[] { (6f, "0600"), (12f, "1200"), (17.25f, "1715"), (18.5f, "1830"), (23f, "2300") };
            for (int i = 0; i < hours.Length; i++)
            {
                yield return Look(app, Weather.Sunny, hours[i].h);
                yield return Shot(prefix + "0" + i + "_map_" + hours[i].tag);
            }
            var weathers = new (Weather w, string tag)[] { (Weather.Rain, "rain"), (Weather.Storm, "storm"), (Weather.Snow, "snow"), (Weather.Drought, "drought"), (Weather.Wind, "wind") };
            for (int i = 0; i < weathers.Length; i++)
            {
                yield return Look(app, weathers[i].w, 12f, 2.6f);
                yield return Shot(prefix + "0" + (5 + i) + "_map_1200_" + weathers[i].tag);
            }
            for (int i = 0; i < weathers.Length; i++)
            {
                yield return Look(app, weathers[i].w, 17.25f, 2.6f);
                yield return Shot(prefix + "1" + i + "_map_1715_" + weathers[i].tag);
            }
            yield return Look(app, Weather.Storm, 18.5f, 2.6f);
            yield return Shot(prefix + "15_map_1830_storm");
            yield return Look(app, Weather.Snow, 23f, 2.6f);
            yield return Shot(prefix + "16_map_2300_snow");
            yield return Look(app, Weather.Snow, 6f, 2.6f);
            yield return Shot(prefix + "17_map_0600_snow");

            // ---- clouds moving: four frames a second apart ----
            yield return Look(app, Weather.Sunny, 12f, 2.6f);
            for (int f = 0; f < 4; f++)
            {
                yield return Shot(prefix + "2" + f + "_motion");
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // ---- mid zoom and a close island ----
            app.Farm.Camera.FlyTo(ArchipelagoView.IslandOrigin(1), app.Farm.Camera.ZBase * 0.59f, 0.3f);
            yield return new WaitForSecondsRealtime(1.2f);
            foreach (var (h, tag, n) in new[] { (12f, "1200", 0), (17.25f, "1715", 1), (18.5f, "1830", 2), (23f, "2300", 3) })
            {
                yield return Look(app, Weather.Sunny, h);
                yield return Shot(prefix + "3" + n + "_mid_" + tag);
            }
            yield return Look(app, Weather.Snow, 17.25f, 2.6f);
            yield return Shot(prefix + "34_mid_1715_snow");
            app.GoToIsland(0);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Look(app, Weather.Sunny, 12f, 2.6f);
            yield return Shot(prefix + "35_close_1200");
            yield return Look(app, Weather.Sunny, 17.25f);
            yield return Shot(prefix + "36_close_1715");
            yield return Look(app, Weather.Rain, 12f, 2.6f);
            yield return Shot(prefix + "37_close_1200_rain");

            // ---- the start screen ----
            if (withStart)
            {
                yield return Look(app, Weather.Sunny, 12f, 2.6f);
                var start = app.OpenStartForAudit();
                start.PreviewForAudit("home");
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot(prefix + "38_start");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(prefix + "39_start_later");
                start.Dispose();
                yield return null;
            }

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
            Debug.Log("[AuditClouds] xong.");
        }

        /// <summary>Canvas batching with the sky running and with it frozen, on the whole map and at farm
        /// zoom, at noon (the palette holds) and at 17:45 (the palette moves every second). The difference
        /// is what the sky costs.</summary>
        public static IEnumerator Profile(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            Tutorial.SuppressTips = true;
            app.Tutorial?.EndAuditPreview();
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();
            var lines = new List<string>();
            foreach (var (map, label) in new[] { (true, "map"), (false, "farm") })
            {
                if (map) app.ShowArchipelago(); else app.GoToIsland(0);
                foreach (var hour in new[] { 12f, 17.75f })
                {
                    app.WeatherView.ForceWeather(Weather.Sunny);
                    DayCycle.ForceHour(hour);
                    app.SkyView.Repaint();
                    yield return new WaitForSecondsRealtime(1.5f);
                    foreach (bool skyOn in new[] { true, false })
                    {
                        app.SkyView.enabled = skyOn;
                        yield return new WaitForSecondsRealtime(0.3f);
                        var r = new Measure();
                        yield return r.Run(180);
                        lines.Add(string.Format("{0} {1:00.00}h sky {2}: {3}", label, hour, skyOn ? "on " : "off", r));
                    }
                    app.SkyView.enabled = true;
                }
            }
            app.GoToIsland(0);
            app.WeatherView.ReleaseWeather(WeatherSys.Now(GS.Local));
            DayCycle.Release();
            app.SkyView.Repaint();
            Debug.Log("[AuditClouds] profile\n" + string.Join("\n", lines));
        }

        class Measure
        {
            double _buildCount, _buildMs, _willRenderMs, _batches, _setPass;
            int _frames;

            public IEnumerator Run(int frames)
            {
                using (var build = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "Canvas.BuildBatch", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame))
                using (var will = ProfilerRecorder.StartNew(ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame))
                using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
                using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
                {
                    yield return null;
                    yield return null;
                    for (int i = 0; i < frames; i++)
                    {
                        yield return null;
                        if (build.Count > 0) { var smp = build.GetSample(0); _buildCount += smp.Count; _buildMs += smp.Value / 1e6; }
                        if (will.Count > 0) _willRenderMs += will.GetSample(0).Value / 1e6;
                        _batches += batches.LastValue;
                        _setPass += setPass.LastValue;
                        _frames++;
                    }
                }
            }

            public override string ToString()
            {
                float n = Mathf.Max(1, _frames);
                return string.Format("BuildBatch {0:0.00}/frame {1:0.000} ms · WillRenderCanvases {2:0.000} ms · batches {3:0.0} · setpass {4:0.0} ({5} frames)",
                    _buildCount / n, _buildMs / n, _willRenderMs / n, _batches / n, _setPass / n, _frames);
            }
        }
    }
}
