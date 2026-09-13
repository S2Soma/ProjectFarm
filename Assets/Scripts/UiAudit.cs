using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Walks every screen in turn and saves a PNG of each, so the interface can be
    /// reviewed as it actually renders rather than as it was intended to render.
    /// Driven from the editor menu: Tools > LQ Farm > Chụp toàn bộ giao diện.</summary>
    public static class UiAudit
    {
        public static string OutputDir =>
            System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");

        public static IEnumerator Run(GameApp app)
        {
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);

            var shots = new List<(string name, System.Func<PanelBase> make)>
            {
                ("00_farm",       null),
                ("01_upgrade",    () => new UpgradePanel(app)),
                ("02_chest",      () => new ChestPanel(app)),
                ("03_missions",   () => new MissionsPanel(app)),
                ("04_warehouse",  () => new WarehousePanel(app)),
                ("05_seedshop",   () => new SeedShopPanel(app)),
                ("06_friends",    () => new FriendsPanel(app)),
                ("07_shop",       () => new ShopPanel(app)),
                ("08_collection", () => new CollectionPanel(app)),
                ("09_season",     () => new SeasonPanel(app)),
                ("10_island",     () => new IslandPanel(app, IslandSys.NextLocked(GS.Local))),
            };

            foreach (var shot in shots)
            {
                app.CloseAll();
                yield return null;

                if (shot.make != null) app.Open(shot.make());

                // let the pop-in settle and any staggered rows finish
                yield return new WaitForSecondsRealtime(0.75f);

                string path = System.IO.Path.Combine(dir, shot.name + ".png");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);

                // CaptureScreenshot writes at end of frame, asynchronously
                yield return new WaitForSecondsRealtime(0.6f);
                Debug.Log("[UiAudit] " + path);
            }

            // The tray is not a panel — it is HUD furniture — so it needs its own pass.
            app.CloseAll();
            yield return null;
            app.Hud.ShowTrayForAudit();
            yield return new WaitForSecondsRealtime(0.5f);
            {
                string path = System.IO.Path.Combine(dir, "11_tray.png");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(0.6f);
                Debug.Log("[UiAudit] " + path);
            }
            app.Hud.HideTray();

            // Two map shots. The tribute board is painted on the locked island itself, which is
            // the whole argument for putting every island on one canvas — so it is worth a
            // standing screenshot rather than a one-off.
            int locked = IslandSys.NextLocked(GS.Local);
            if (locked > 0)
            {
                app.GoToIsland(locked);
                yield return new WaitForSecondsRealtime(1.1f);
                yield return Shot(dir, "12_locked_island");
            }

            // the cloud bridge between the first two islands, close enough to read
            if (GS.Local.islands.Count > 1 && GS.Local.islands[1].unlocked)
            {
                var mid = (ArchipelagoView.IslandOrigin(0) + ArchipelagoView.IslandOrigin(1)) * 0.5f;
                app.Farm.Camera.FlyTo(mid, app.Farm.Camera.ZBase * 0.59f, 0.3f);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot(dir, "14_bridge");
            }

            app.ShowArchipelago();
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Shot(dir, "13_archipelago");

            app.GoToIsland(0);
            app.CloseAll();
            yield return new WaitForSecondsRealtime(0.9f);

            // The farm in every weather: the map has to look different in each one.
            var names = new[] { "sunny", "rain", "storm", "snow", "drought", "wind" };
            var states = new[] { Weather.Sunny, Weather.Rain, Weather.Storm, Weather.Snow, Weather.Drought, Weather.Wind };
            for (int i = 0; i < states.Length; i++)
            {
                app.WeatherView.ForceWeather(states[i]);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot(dir, "2" + i + "_weather_" + names[i]);
            }
            app.WeatherView.ReleaseWeather(WeatherSys.Now(GS.Local));

            // The seed sheet, opened on the first empty bed of the home island.
            var empties = app.Farm.EmptyPlots();
            if (empties.Count > 0)
            {
                app.OpenSeedSheet(empties[0]);
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Shot(dir, "30_seed_sheet");
                app.CloseSeedSheet();
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Debug.Log("[UiAudit] Xong. Ảnh nằm ở: " + dir);
        }

        /// <summary>CaptureScreenshot writes at end of frame, asynchronously, and only works from
        /// inside a running coroutine — an MCP command that calls it directly is refused.</summary>
        static IEnumerator Shot(string dir, string name)
        {
            string path = System.IO.Path.Combine(dir, name + ".png");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[UiAudit] " + path);
        }
    }
}
