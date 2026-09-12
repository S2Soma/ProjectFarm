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

            app.CloseAll();
            Debug.Log("[UiAudit] Xong. Ảnh nằm ở: " + dir);
        }
    }
}
