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
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            Tutorial.SuppressTips = true;

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

            // The chapter tab: graded rows (the medals) that the contract board may not be showing
            // when its slots are waiting to refill.
            app.CloseAll();
            yield return null;
            MissionsPanel.OpenOnTab(1);
            app.Open(new MissionsPanel(app));
            yield return new WaitForSecondsRealtime(0.75f);
            yield return Shot(dir, "03b_chapters");
            MissionsPanel.OpenOnTab(0);

            // The menu is not a panel — it is HUD furniture — so it needs its own pass.
            app.CloseAll();
            yield return null;
            app.Hud.OpenMenuForAudit();
            yield return new WaitForSecondsRealtime(0.5f);
            {
                string path = System.IO.Path.Combine(dir, "11_menu.png");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(0.6f);
                Debug.Log("[UiAudit] " + path);
            }
            app.Hud.CloseMenu();
            yield return new WaitForSecondsRealtime(0.4f);

            // Mid-animation frames, so the motion itself is on record and not only its end state:
            // the menu 90 ms into its rise, and the weather popup 60 ms into its pop.
            app.Hud.OpenMenuAnimated();
            yield return new WaitForSecondsRealtime(0.09f);
            yield return Shot(dir, "11b_menu_rising");
            app.Hud.CloseMenu();
            yield return new WaitForSecondsRealtime(0.4f);
            app.Hud.TapWeatherForAudit();
            yield return new WaitForSecondsRealtime(0.06f);
            yield return Shot(dir, "09b_weather_opening");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(dir, "09c_weather_open");
            app.CloseAll();
            yield return new WaitForSecondsRealtime(0.3f);

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

            // The day, round the clock, in clear weather (the real hour's weather would hide the
            // moon, the stars and the fireflies), then the night under the two weathers that
            // change it most, the whole map at night, and a bridge by day and by night.
            app.WeatherView.ForceWeather(Weather.Sunny);
            var hours = new[] { (6.0f, "40_dawn"), (12.0f, "41_day"), (17.25f, "42_golden"), (18.5f, "43_dusk"), (23.0f, "44_night") };
            foreach (var (h, name) in hours)
            {
                DayCycle.ForceHour(h);
                app.SkyView.Repaint();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot(dir, name);
            }
            app.WeatherView.ForceWeather(Weather.Storm);
            yield return new WaitForSecondsRealtime(2.6f);
            yield return Shot(dir, "45_night_storm");
            app.WeatherView.ForceWeather(Weather.Snow);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "46_night_snow");
            app.WeatherView.ForceWeather(Weather.Sunny);
            app.ShowArchipelago();
            yield return new WaitForSecondsRealtime(1.4f);
            yield return Shot(dir, "47_night_map");
            if (GS.Local.islands.Count > 1 && GS.Local.islands[1].unlocked)
            {
                var mid = (ArchipelagoView.IslandOrigin(0) + ArchipelagoView.IslandOrigin(1)) * 0.5f;
                app.Farm.Camera.FlyTo(mid, app.Farm.Camera.ZBase * 0.59f, 0.3f);
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot(dir, "48_night_bridge");
                DayCycle.ForceHour(11f);
                app.SkyView.Repaint();
                yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot(dir, "49_day_bridge");
            }
            app.GoToIsland(0);
            app.WeatherView.ReleaseWeather(WeatherSys.Now(GS.Local));
            DayCycle.Release();
            app.SkyView.Repaint();
            yield return new WaitForSecondsRealtime(0.9f);

            // The Trang trí shelf, and a portrait frame, a badge and the rose bush worn (previewed:
            // nothing is bought and the save is not touched).
            app.CloseAll();
            ShopPanel.OpenOnTab(1);
            app.Open(new ShopPanel(app));
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "07b_cosmetics");
            app.CloseAll();
            ShopPanel.OpenOnTab(0);
            Cosmetics.Preview("fr_gold"); Cosmetics.Preview("bd_farmer"); Cosmetics.Preview("dc_roses");
            app.Farm.RenderAll(); app.Hud.Render();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(dir, "32_cosmetics_worn");
            Cosmetics.ClearPreview();
            app.Farm.RenderAll(); app.Hud.Render();
            yield return new WaitForSecondsRealtime(0.3f);

            // A phone with a notch in landscape: 44 pt cut off the left edge and a 21 pt gesture bar
            // along the bottom (an iPhone 14-class device, as shares of the screen). Every HUD
            // cluster must move inside it; the scenery must still fill the whole screen.
            SafeAreaFitter.Simulated = new Rect(0.052f, 0.054f, 0.896f, 0.946f);
            yield return new WaitForSecondsRealtime(0.4f);
            app.Hud.OpenMenuForAudit();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot(dir, "33_safe_area_notch");
            app.Hud.CloseMenu();
            // a panel on the same phone: the scrim must reach past the notch, the card must not
            app.Open(new ShopPanel(app));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "34_notch_panel");
            app.CloseAll();
            SafeAreaFitter.Simulated = null;
            yield return new WaitForSecondsRealtime(0.4f);

            // a growing plot's popup, and a refusal tapped twice (one toast, counted)
            var growing = app.Farm.TickingPlots();
            if (growing.Count > 0)
            {
                app.OpenPlot(growing[0]);
                app.Toast("Chưa tới cữ tưới");
                app.Toast("Chưa tới cữ tưới");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot(dir, "35_plot_popup");
                app.ClosePlotPopup();
                yield return new WaitForSecondsRealtime(2.4f);
            }

            // The harvest receipts: a single harvest's number, multiplier chips and mutation ring, and
            // the harvest-all summary card — staged, not harvested.
            app.PreviewHarvestReceipts(5);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return Shot(dir, "31_harvest_receipts");
            yield return new WaitForSecondsRealtime(3.8f);

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
            yield return RunCosmetics(app);
            yield return RunPets(app);
            yield return RunAccount(app);
            yield return RunHelp(app);
            Tutorial.SuppressTips = false;
            Debug.Log("[UiAudit] Xong. Ảnh nằm ở: " + dir);
        }

        /// <summary>The walkthrough's steps, two tips and every page of the guide book, on the
        /// developer's own save: steps are previewed in memory and nothing they show is saved.
        /// The produce put in the warehouse for the selling steps is taken out again.</summary>
        public static IEnumerator RunHelp(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            var tut = app.Tutorial;
            if (tut == null) yield break;
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;

            app.CloseAll();
            app.CloseSeedSheet();
            app.Hud.CloseMenu();
            app.GoToIsland(0);
            yield return new WaitForSecondsRealtime(0.8f);

            IEnumerator Step(Tutorial.Step st, string name, float settle = 0.9f)
            {
                tut.PreviewForAudit(st);
                yield return new WaitForSecondsRealtime(settle);
                yield return Shot(dir, name);
            }

            yield return Step(Tutorial.Step.Welcome, "70_tut_welcome");
            yield return Step(Tutorial.Step.TapEmpty, "71_tut_tap_plot");

            var empties = app.Farm.EmptyPlots();
            if (empties.Count > 0)
            {
                app.OpenSeedSheet(empties[0]);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Step(Tutorial.Step.PickSeed, "72_tut_pick_seed");
                app.CloseSeedSheet();
                yield return new WaitForSecondsRealtime(0.4f);
            }
            yield return Step(Tutorial.Step.TapWater, "73_tut_water");
            yield return Step(Tutorial.Step.WaitRipe, "74_tut_wait_ripe");
            yield return Step(Tutorial.Step.TapHarvest, "75_tut_harvest");

            bool addedProduce = GS.Local.store.Count == 0;
            if (addedProduce) GS.Local.store["carrot:0"] = 6;
            yield return Step(Tutorial.Step.OpenMenu, "76_tut_menu");
            app.Hud.OpenMenuForAudit();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Step(Tutorial.Step.OpenStore, "77_tut_store");
            app.Hud.CloseMenu();
            app.Open(new WarehousePanel(app));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Step(Tutorial.Step.Sell, "78_tut_sell");
            app.CloseAll();
            if (addedProduce) GS.Local.store.Remove("carrot:0");
            yield return new WaitForSecondsRealtime(0.3f);

            yield return Step(Tutorial.Step.Missions, "79_tut_missions");
            yield return Step(Tutorial.Step.Finish, "79b_tut_finish");

            tut.PreviewTipForAudit("quickHarvest");
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "79c_tip_harvest");
            tut.PreviewTipForAudit("chest");
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "79d_tip_chest");
            tut.EndAuditPreview();
            yield return new WaitForSecondsRealtime(0.3f);

            // the guide book, every page
            var guide = new GuidePanel(app);
            app.Open(guide);
            yield return new WaitForSecondsRealtime(0.6f);
            for (int p = 0; p < GuidePanel.PageCount; p++)
            {
                guide.ShowPage(p);
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot(dir, "8" + p + "_guide_" + p);
            }
            app.CloseAll();
            app.Hud.OpenMenuForAudit();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot(dir, "11_menu");
            app.Hud.CloseMenu();
            Tutorial.SuppressTips = quiet;
            Debug.Log("[UiAudit] Xong phần hướng dẫn.");
        }

        /// <summary>CaptureScreenshot writes at end of frame, asynchronously, and only works from
        /// inside a running coroutine — an MCP command that calls it directly is refused.</summary>
        /// <summary>Pets, 90–97: the HUD button, the empty book, the hatchery, both reveals, a full
        /// book, and the pet at work and snacking on the farm. Everything is put back afterwards:
        /// the pets and eggs shown here are not the developer's to keep.</summary>
        public static IEnumerator RunPets(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            var s = GS.Local;
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            var pets = new Dictionary<string, int>(s.pets);
            string active = s.petActive;
            int eggs = s.petEggs, pity = s.petPity, snacks = s.petSnacks;

            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();
            app.GoToIsland(0);
            s.pets.Clear(); s.petActive = ""; s.petEggs = 0;
            app.Hud.Render();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(dir, "90_pet_hud");

            PetPanel.OpenOnTab(0);
            app.Open(new PetPanel(app));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "91_pet_book_empty");
            app.CloseAll();

            s.petEggs = 3; s.petPity = 12;
            PetPanel.OpenOnTab(1);
            var hatch = new PetPanel(app);
            app.Open(hatch);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "92_pet_hatchery");

            hatch.RevealForAudit(new List<(PetDef, bool, int)> { (PetSys.Def("mit"), true, 1) });
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Shot(dir, "93_pet_reveal");
            app.CloseAll();

            var ten = new List<(PetDef, bool, int)>();
            string[] order = { "bega", "pinkteriii", "shushi", "bega", "yummy", "tim", "pinkteriii", "bega", "shushi", "mit" };
            for (int i = 0; i < order.Length; i++) ten.Add((PetSys.Def(order[i]), i < 6, i < 6 ? 1 : 2));
            var hatch10 = new PetPanel(app);
            app.Open(hatch10);
            yield return new WaitForSecondsRealtime(0.4f);
            hatch10.RevealForAudit(ten);
            yield return new WaitForSecondsRealtime(2.6f);
            yield return Shot(dir, "94_pet_reveal10");
            app.CloseAll();

            s.pets["mit"] = 3; s.pets["tim"] = 1; s.pets["pinkteriii"] = 2; s.pets["yummy"] = 1;
            s.petActive = "mit";
            PetPanel.OpenOnTab(0);
            app.Open(new PetPanel(app));
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(dir, "95_pet_book");
            app.CloseAll();
            app.Hud.Render();

            yield return new WaitForSecondsRealtime(0.6f);
            app.Pets.StageForAudit(PetActor.Pose.Walk, 5);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(dir, "96_pet_farm");
            var grape = GameData.Get("grape");
            app.Pets.StageForAudit(PetActor.Pose.Happy, 10, grape != null ? Art.Icon(grape.art, 3) : null);
            app.Toast("MiT ăn vụng 1 Nho Tím Viêm Hoả!");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(dir, "97_pet_snack");
            app.Pets.StageForAudit(PetActor.Pose.Idle, 10);
            app.Pets.EndAudit();

            s.pets.Clear(); foreach (var kv in pets) s.pets[kv.Key] = kv.Value;
            s.petActive = active; s.petEggs = eggs; s.petPity = pity; s.petSnacks = snacks;
            app.Hud.Render();
            Tutorial.SuppressTips = quiet;
            Debug.Log("[UiAudit] Thú cưng xong.");
        }

        /// <summary>Trang trí, 60–65: the shelf and its filter, then worn effects on the field (a plot
        /// skin, a planting, a watering, a harvest with a toast frame, a tap and a swipe trail). Worn
        /// through Cosmetics.Preview, so nothing is bought or saved.</summary>
        public static IEnumerator RunCosmetics(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();
            app.GoToIsland(0);
            yield return new WaitForSecondsRealtime(0.6f);

            ShopPanel.OpenOnTab(1); ShopPanel.OpenOnSlot(-1);
            app.Open(new ShopPanel(app));
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "60_cos_shelf");
            app.CloseAll();
            ShopPanel.OpenOnSlot((int)CosmeticSlot.Plot);
            app.Open(new ShopPanel(app));
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "61_cos_shelf_plot");
            app.CloseAll();
            ShopPanel.OpenOnSlot(-1); ShopPanel.OpenOnTab(0);

            foreach (var id in new[] { "pk_flower", "ts_candy", "pl_firework", "wt_rainbow", "hv_confetti", "tp_heart", "sw_rainbow" })
                Cosmetics.Preview(id);
            app.Farm.RenderAll();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot(dir, "62_cos_plot_skin");

            var ready = app.Farm.ReadyPlots();
            if (ready.Count > 0) app.Farm.PreviewCosmeticFx(ready[0], "hv_confetti");
            if (ready.Count > 1) app.Farm.PreviewCosmeticFx(ready[1], "wt_rainbow");
            app.Toast("Đang dùng Thông Báo Kẹo Dâu");
            var layer = GameObject.Find("touchfx")?.transform as RectTransform;
            if (layer != null && FxKit.I != null)
            {
                CosmeticFx.Emit(layer, new Vector2(-300, 40), "tp_heart");
                var trail = CosmeticFx.For("sw_rainbow");
                for (int k = 0; k <= 40; k++)
                {
                    var at = Vector2.Lerp(new Vector2(-60, -200), new Vector2(460, 140), k / 40f) + new Vector2(0, Mathf.Sin(k * 0.25f) * 50f);
                    foreach (var r in trail) FxKit.I.Emit(layer, at, r);
                }
            }
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot(dir, "63_cos_effects");

            Cosmetics.ClearPreview();
            app.Farm.RenderAll();
            Tutorial.SuppressTips = quiet;
            Debug.Log("[UiAudit] Trang trí xong.");
        }

        /// <summary>Accounts, A0–A9: every start-screen mode, then Menu ▸ Tài khoản for nobody, a
        /// guest and an email account, and the in-game save choice. Sessions here are fakes held in
        /// memory; the real one (if any) is put back and nothing talks to the server.</summary>
        public static IEnumerator RunAccount(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();

            var real = Supa.Session;
            var guest = new SupaSession { userId = "3f9a1c2e-7b4d-4e11-9a0c-5d2b8e6f1a37", refreshToken = "audit", accessToken = "audit", anonymous = true };
            var mail = new SupaSession { userId = "8c21d0aa-19f3-4b7e-b5d4-0e6a4c9f2b18", refreshToken = "audit", accessToken = "audit", email = "nongdan.mit@gmail.com" };

            var start = app.OpenStartForAudit();
            var modes = new[] { "home", "home_error", "login", "signup", "busy", "ready", "ready_offline", "choose" };
            for (int i = 0; i < modes.Length; i++)
            {
                Supa.UseSessionForAudit(modes[i].StartsWith("ready") ? mail : null);
                start.PreviewForAudit(modes[i]);
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Shot(dir, "A" + i + "_start_" + modes[i]);
            }
            start.Dispose();
            yield return null;

            var state = CloudSync.State;
            Supa.UseSessionForAudit(null);
            app.Open(new AccountPanel(app));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "A8_account_none");

            Supa.UseSessionForAudit(guest);
            CloudSync.State = SyncState.Offline;
            app.Open(new AccountPanel(app));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "A9_account_guest");

            Supa.UseSessionForAudit(mail);
            CloudSync.State = SyncState.Synced;
            app.Open(new AccountPanel(app));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "AA_account_email");

            string raw = SaveIO.ToJson(GS.Local);
            var check = new CloudSync.Check { local = SaveSummary.Of(raw) };
            check.local.savedAt = GS.Now;
            check.cloud = check.local;
            check.cloud.level = Mathf.Max(1, check.local.level + 2);
            check.cloud.coin = check.local.coin * 2;
            check.cloud.savedAt = GS.Now + 40L * 60 * 1000;
            app.Open(new SaveChoicePanel(app, check, _ => { }));
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "AB_save_choice");
            app.CloseAll();

            Supa.UseSessionForAudit(real);
            CloudSync.State = state;
            app.Hud.OpenMenuForAudit();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot(dir, "AC_menu_account");
            app.Hud.CloseMenu();
            Tutorial.SuppressTips = quiet;
            Debug.Log("[UiAudit] Tài khoản xong.");
        }

        /// <summary>The islands alive, I00–I95: every island by day, under snow by day and by night, and on a
        /// clear night; the river and waterfall as a motion sequence; the whole map by day and under snow.
        ///
        /// Islands the player has not bought are opened for the pass and closed again afterwards. Saving is
        /// held off for the whole pass (<see cref="PlayerState.loaded"/> is lowered, which SaveIO.Save
        /// refuses), so a borrowed unlock can never reach the file.</summary>
        public static IEnumerator RunIslandsAlive(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            string dir = System.IO.Path.GetFullPath(OutputDir);
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
            s.SyncPlots();
            app.SyncIslands();
            app.Farm.SyncBridges(animateNew: false);
            app.ForceRedraw();
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();

            string[] keys = { "vuonnha", "nuoc", "khonglo", "gio", "bang", "hoa", "loi", "vang" };
            for (int i = 0; i < IslandSys.Max; i++)
            {
                app.GoToIsland(i);
                yield return new WaitForSecondsRealtime(1.2f);
                foreach (var (w, h, tag) in new[] { (Weather.Sunny, 12f, "0_day"), (Weather.Snow, 12f, "1_snow_day"), (Weather.Snow, 23f, "2_snow_night"), (Weather.Sunny, 23f, "3_night") })
                {
                    app.WeatherView.ForceWeather(w);
                    DayCycle.ForceHour(h);
                    app.SkyView.Repaint();
                    yield return new WaitForSecondsRealtime(0.9f);
                    yield return Shot(dir, "I" + i + tag + "_" + keys[i]);
                }
            }

            // the river and the waterfall moving: six frames a fifth of a second apart
            app.WeatherView.ForceWeather(Weather.Sunny);
            DayCycle.ForceHour(12f);
            app.SkyView.Repaint();
            app.GoToIsland(1);
            yield return new WaitForSecondsRealtime(1.4f);
            for (int f = 0; f < 6; f++)
            {
                string path = System.IO.Path.Combine(dir, "I8" + f + "_river_motion.png");
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            yield return new WaitForSecondsRealtime(0.6f);

            app.ShowArchipelago();
            yield return new WaitForSecondsRealtime(1.4f);
            yield return Shot(dir, "I90_map_day");
            app.WeatherView.ForceWeather(Weather.Snow);
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot(dir, "I91_map_snow");
            app.Farm.Camera.FlyTo(ArchipelagoView.IslandOrigin(1), app.Farm.Camera.ZBase * 0.59f, 0.3f);
            app.WeatherView.ForceWeather(Weather.Sunny);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot(dir, "I92_mid_zoom");

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
            Debug.Log("[UiAudit] Đảo sống xong.");
        }

        /// <summary>The flight from the start screen into the farm, T00–T29: the farm is torn down
        /// (saved first), an idle start screen comes back without signing in, and its way in is
        /// taken exactly as a button takes it. The cinematic's clock is held by this pass and stopped
        /// on each beat, so every frame is the shot at that exact time; the build still happens for
        /// real, behind the veil. Runs on the Supabase host, which outlives the restart.</summary>
        public static IEnumerator RunCinematic(float step = 0.1f, string prefix = "T")
        {
            string dir = System.IO.Path.GetFullPath(OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            GameApp.RebootToStartForAudit();
            yield return null;
            yield return null;
            float wait = 0f;
            while ((GameApp.I == null || GameApp.I.StartForAudit == null) && wait < 5f) { wait += Time.unscaledDeltaTime; yield return null; }
            if (GameApp.I == null || GameApp.I.StartForAudit == null) { Debug.LogWarning("[UiAudit] Không mở được màn hình bắt đầu."); yield break; }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot(dir, prefix + "00_start");

            EnterCinematic.AuditScrub = true;
            GameApp.I.StartForAudit.EnterForAudit();
            var cine = EnterCinematic.Current;
            int n = 1;
            for (float v = 0f; cine != null && v < EnterCinematic.Total + 0.001f; v += step, n++)
            {
                cine.AuditTime = v;
                yield return null;
                // past the cover the film waits for the farm to be built and laid out
                while (cine != null && v > EnterCinematic.CoverAt + EnterCinematic.Hold && !cine.Revealing) yield return null;
                yield return null;
                yield return Shot(dir, prefix + n.ToString("00") + "_" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s");
            }
            if (cine != null) cine.AuditTime = EnterCinematic.Total + 1f;
            EnterCinematic.AuditScrub = false;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot(dir, prefix + n.ToString("00") + "_end");
            Debug.Log("[UiAudit] Chuyển cảnh xong.");
        }

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
