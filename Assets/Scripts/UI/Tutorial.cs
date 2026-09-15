using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The first-run walkthrough, and the one-time tips that follow it.
    ///
    /// The game taught nothing before this except a toast that said "tap a plot". Everything a new
    /// player needs to be shown — that an empty bed opens a seed picker, that a cracked bed with a
    /// blue rim is thirsty, that produce goes to a warehouse behind a menu and has to be sold — is
    /// shown here by doing it, on the player's own farm, one tap at a time.
    ///
    /// **Every step is a condition on the game, never a script of taps.** A step reads the state
    /// (is the sheet open? did the harvest count go up? is there a thirsty plot?) and moves on, back,
    /// or past itself accordingly. So the walkthrough survives the app being closed on any step,
    /// weather that auto-waters the crop, a player who taps ahead, and being replayed from the
    /// guide book on a level-20 farm with no empty beds — the steps that cannot happen are skipped.
    ///
    /// Steps that ask for one tap dim the screen and let only that tap through; steps that wait
    /// (for a crop to grow) leave the farm alone. Skipping is always one tap away.
    ///
    /// Tips are shown once each, after the walkthrough, at the moment they become true — the
    /// harvest button the level it unlocks, the chest the first time there is one — and only when
    /// nothing else is on screen.</summary>
    public class Tutorial
    {
        public enum Step
        {
            Welcome, TapEmpty, PickSeed, PlantMore, WaitWater, TapWater, WaitRipe, TapHarvest,
            HarvestMore, OpenMenu, OpenStore, Sell, Missions, Finish, Done,
        }

        readonly GameApp _app;
        readonly CoachView _coach;
        Step _step;
        float _nextThink, _lastThink, _thinkDt;

        /// <summary>While the walkthrough waits on a crop, the home island's clock runs this many
        /// times faster. A carrot now takes 2 minutes (its one watering window opens at 60 s): a new
        /// player staring at a card that says "wait" for that long decides the game is stuck. ×12
        /// brings the window to ~5 s and ripening to ~10 s, about what the old 40 s carrot felt like.</summary>
        public const float FastForward = 12f;
        int _plot = -1;
        int _baseHarvest, _basePlant, _baseWater, _baseSell;
        /// <summary>Set by the screenshot pass: the card stays as previewed and nothing is saved.</summary>
        bool _frozen;
        bool _previewing;

        const int HarvestsBeforeSelling = 3;

        public Tutorial(GameApp app, CoachView coach)
        {
            _app = app;
            _coach = coach;
            _step = Resume(GS.Local);
            Mark();
        }

        public bool Active => _step != Step.Done;
        public Step Current => _step;
        /// <summary>True while a step is dimming the screen — the Back key should not dismiss
        /// what it cannot see.</summary>
        public bool Blocking => _coach.Visible && _coach.Spec.dim;

        /// <summary>Where a save picks up. "" is a save written before the tutorial existed: a farm
        /// that has already planted or harvested learned the game without it, so it is done, and
        /// the tips for what that farm already has are marked seen — a level-12 player must not be
        /// told the harvest button just unlocked.</summary>
        public static Step Resume(PlayerState s)
        {
            if (string.IsNullOrEmpty(s.tutorial))
            {
                if (HasPlayed(s))
                {
                    s.tutorial = Step.Done.ToString();
                    foreach (var tip in Tips) if (tip.already(s)) s.tipsSeen.Add(tip.id);
                    return Step.Done;
                }
                s.tutorial = Step.Welcome.ToString();
                return Step.Welcome;
            }
            return Enum.TryParse(s.tutorial, out Step st) ? st : Step.Done;
        }

        /// <summary>Any sign the farm has been played: planted, harvested, earned XP, levelled or
        /// has a crop in the ground. Counters alone are not enough — a farm staged by the dev tools
        /// (or restored from elsewhere) can be level 12 with zero in every counter.</summary>
        static bool HasPlayed(PlayerState s)
        {
            if (s.stats.plant > 0 || s.stats.harvest > 0 || s.lv > 1 || s.xp > 0) return true;
            foreach (var isl in s.islands)
                foreach (var p in isl.plots)
                    if (p != null && !string.IsNullOrEmpty(p.crop)) return true;
            return false;
        }

        /// <summary>From the guide book: walk through it again on whatever farm this is.</summary>
        public void Restart()
        {
            Go(Step.Welcome);
        }

        void Go(Step next)
        {
            if (next == _step || _frozen) return;
            if (_previewing) { _step = next; Mark(); return; }
            _step = next;
            GS.Local.tutorial = next.ToString();
            Mark();
            _nextThink = 0f;
            GS.Save();
        }

        /// <summary>Counters are compared against their value when the step began, so a replay on
        /// a farm with 4.000 harvests still waits for one more.</summary>
        void Mark()
        {
            var st = GS.Local.stats;
            _baseHarvest = st.harvest; _basePlant = st.plant; _baseWater = st.water; _baseSell = st.sell;
        }

        void Skip()
        {
            Go(Step.Done);
            _coach.Hide();
            _app.Toast("Xem lại cách chơi ở Menu ▸ Hướng dẫn");
        }

        // ============================================================
        // tick
        // ============================================================
        public void Tick(float dt)
        {
            _coach.Tick(dt);
            float now = Time.unscaledTime;
            if (now < _nextThink) return;
            _thinkDt = _lastThink > 0f ? Mathf.Min(0.5f, now - _lastThink) : 0f;
            _lastThink = now;
            _nextThink = now + 0.15f;

            if (_app.Farm == null || _app.Hud == null || _frozen) return;
            if (_step == Step.Done) { ThinkTips(); return; }
            Think();
        }

        ArchipelagoView Farm => _app.Farm;
        Stats Stats => GS.Local.stats;

        /// <summary>A modal over the farm that the current step is not about.</summary>
        bool Interrupted(params Type[] allowed)
        {
            if (_app.RewardOpen) return true;
            var p = _app.Panel;
            if (p == null) return false;
            foreach (var t in allowed) if (t.IsInstanceOfType(p)) return false;
            return true;
        }

        void Think()
        {
            switch (_step)
            {
                case Step.Welcome:     Welcome(); break;
                case Step.TapEmpty:    TapEmpty(); break;
                case Step.PickSeed:    PickSeed(); break;
                case Step.PlantMore:   PlantMore(); break;
                case Step.WaitWater:   WaitWater(); break;
                case Step.TapWater:    TapWater(); break;
                case Step.WaitRipe:    WaitRipe(); break;
                case Step.TapHarvest:  TapHarvest(); break;
                case Step.HarvestMore: HarvestMore(); break;
                case Step.OpenMenu:    OpenMenu(); break;
                case Step.OpenStore:   OpenStore(); break;
                case Step.Sell:        Sell(); break;
                case Step.Missions:    Missions(); break;
                case Step.Finish:      Finish(); break;
            }
        }

        // ------------------------------------------------------------
        // steps
        // ------------------------------------------------------------
        void Welcome()
        {
            if (Interrupted()) { _coach.Hide(); return; }
            _app.Hud.CloseMenu();
            _coach.Show(new CoachSpec
            {
                key = "welcome", dim = true, centred = true,
                // the name the launcher shows, so a rename is one Player Settings field
                title = "Chào mừng đến " + Application.productName + "!",
                body = "Quần đảo nông trại trên mây đang chờ bạn. Mình sẽ cùng bạn làm vụ mùa đầu tiên, chỉ khoảng hai phút.",
                primary = "Bắt đầu", onPrimary = () => Go(Step.TapEmpty),
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void TapEmpty()
        {
            if (_app.SeedSheetOpen) { Go(Step.PickSeed); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (!OnHomeIsland()) return;

            var empty = Farm.EmptyPlots();
            if (empty.Count == 0) { Go(Growing() ? Step.WaitWater : Step.OpenMenu); return; }
            if (_plot < 0 || !empty.Contains(_plot)) _plot = CentralPlot(empty);
            int plot = _plot;
            EnsureVisible(plot);

            _coach.Show(new CoachSpec
            {
                key = "tapEmpty" + plot, dim = true,
                title = "Gieo hạt đầu tiên",
                body = "Chạm vào ô đất trống này để chọn hạt giống.",
                target = () => PlotRect(plot), holeRadius = 60f, holePad = 4f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void PickSeed()
        {
            if (Stats.plant > _basePlant) { Go(Step.PlantMore); return; }
            if (!_app.SeedSheetOpen) { Go(Step.TapEmpty); return; }
            if (Interrupted()) { _coach.Hide(); return; }

            // the quickest crop the player can plant right now, which on a new farm is carrot
            Seed pick = null;
            foreach (var seed in SeedSheet.Order(GS.Local))
            {
                bool can = (GS.Local.seeds.TryGetValue(seed.id, out int n) && n > 0) || GS.Local.coin >= seed.price;
                if (can && (pick == null || seed.grow < pick.grow)) pick = seed;
            }
            if (pick == null) { _app.CloseSeedSheet(); Go(Step.OpenMenu); return; }
            string id = pick.id;
            int secs = Mathf.RoundToInt(GS.Local.GrowTimeIn(pick, WeatherSys.Now(GS.Local), 0));

            _coach.Show(new CoachSpec
            {
                key = "pick" + id, dim = true,
                title = "Chọn " + pick.name,
                body = pick.name + " chín nhanh nhất, chỉ " + Fmt.Time(secs) + ". Chạm để gieo.",
                target = () => CoachView.ScreenRect(_app.Sheet?.CardFor(id)), holeRadius = 20f, holePad = 6f,
                avoid = () => CoachView.ScreenRect(_app.Sheet?.Root),
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void PlantMore()
        {
            if (!_app.SeedSheetOpen || Farm.EmptyPlots().Count == 0)
            {
                Go(Step.WaitWater);
                return;
            }
            if (Interrupted()) { _coach.Hide(); return; }
            _coach.Show(new CoachSpec
            {
                key = "plantMore", dim = true, passThrough = true, hand = false,
                title = "Gieo kín đảo",
                body = "Ô kế tiếp đã tự sáng. Chạm thêm hạt để gieo nốt.",
                target = () => CoachView.ScreenRect(_app.Sheet?.Root), holeRadius = 26f, holePad = 4f,
                avoid = () => CoachView.ScreenRect(_app.Sheet?.Root),
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void WaitWater()
        {
            if (Stats.water > _baseWater) { Go(Step.WaitRipe); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (!OnHomeIsland()) return;
            if (Farm.WaterablePlots().Count > 0) { Go(Step.TapWater); return; }

            if (!Growing())
            {
                Go(Farm.ReadyPlots().Count > 0 ? Step.TapHarvest : Farm.EmptyPlots().Count > 0 ? Step.TapEmpty : Step.OpenMenu);
                return;
            }
            // Rain waters on planting, and a crop has only so many windows: when none is left on
            // any plot there is nothing to teach, and waiting would stall forever.
            if (!AnyWindowLeft())
            {
                if (RainWatered()) _app.Toast("Trời mưa đã tưới giúp cây của bạn");
                Go(Step.WaitRipe);
                return;
            }
            SpeedUpGrowth();
            int growing = GrowingPlot();
            _coach.Show(new CoachSpec
            {
                key = "waitWater" + growing, dim = true, passThrough = true, hand = false,
                target = growing >= 0 ? () => PlotRect(growing) : (Func<Rect?>)null, holeRadius = 60f, holePad = 4f,
                title = "Cây đang lớn",
                body = "Ô nứt nẻ viền xanh là cây đang khát. Tưới để cây chín sớm hơn.",
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void TapWater()
        {
            if (Stats.water > _baseWater) { Go(Step.WaitRipe); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (!OnHomeIsland()) return;
            var list = Farm.WaterablePlots();
            if (list.Count == 0) { Go(Step.WaitWater); return; }
            if (_plot < 0 || !list.Contains(_plot)) _plot = list[0];
            int plot = _plot;
            EnsureVisible(plot);

            _coach.Show(new CoachSpec
            {
                key = "tapWater" + plot, dim = true,
                title = "Cây đang khát!",
                body = "Chạm vào ô viền xanh để tưới nước.",
                target = () => PlotRect(plot), holeRadius = 60f, holePad = 4f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void WaitRipe()
        {
            if (Interrupted()) { _coach.Hide(); return; }
            if (!OnHomeIsland()) return;
            if (Farm.ReadyPlots().Count > 0) { Go(Step.TapHarvest); return; }
            if (!Growing()) { Go(GS.Local.store.Count > 0 ? Step.OpenMenu : Step.TapEmpty); return; }

            int soonest = int.MaxValue, soonestPlot = -1;
            for (int i = 0; i < Farm.Plots.Count; i++)
            {
                var p = Farm.Plots[i];
                if (!IsGrowing(p)) continue;
                int left = Mathf.CeilToInt(PlotLogic.Remain(p));
                if (left < soonest) { soonest = left; soonestPlot = i; }
            }
            string when = soonest < int.MaxValue ? " Cây đầu tiên chín sau " + Fmt.Time(soonest) + "." : "";

            SpeedUpGrowth();
            bool watered = Stats.water > _baseWater;
            string body = "Cây chín có viền vàng lấp lánh." + when;
            _coach.Show(new CoachSpec
            {
                key = "waitRipe" + (watered ? "1" : "0") + "_" + soonestPlot, dim = true, passThrough = true, hand = false,
                target = soonestPlot >= 0 ? () => PlotRect(soonestPlot) : (Func<Rect?>)null, holeRadius = 60f, holePad = 4f,
                title = watered ? "Tưới xong rồi!" : "Sắp chín rồi",
                body = body,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
            _coach.SetBody(body);
        }

        void TapHarvest()
        {
            if (Stats.harvest > _baseHarvest) { Go(Step.HarvestMore); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (!OnHomeIsland()) return;
            var ready = Farm.ReadyPlots();
            if (ready.Count == 0) { Go(Step.WaitRipe); return; }
            if (_plot < 0 || !ready.Contains(_plot)) _plot = ready[0];
            int plot = _plot;
            EnsureVisible(plot);

            _coach.Show(new CoachSpec
            {
                key = "tapHarvest" + plot, dim = true,
                title = "Thu hoạch!",
                body = "Cây đã chín. Chạm để thu hoạch, nông sản sẽ bay vào Kho.",
                target = () => PlotRect(plot), holeRadius = 60f, holePad = 4f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void HarvestMore()
        {
            int done = Stats.harvest - _baseHarvest + 1;          // the first one was the previous step
            bool anyLeft = Growing() || Farm.ReadyPlots().Count > 0;
            if (done >= HarvestsBeforeSelling || !anyLeft)
            {
                Go(GS.Local.store.Count > 0 ? Step.OpenMenu : Step.Missions);
                return;
            }
            if (Interrupted()) { _coach.Hide(); return; }
            var ready = Farm.ReadyPlots();
            int plot = ready.Count > 0 ? ready[0] : -1;
            // nothing ripe yet: light the crop that ripens next rather than dimming a field with
            // nothing to look at
            int lit = plot >= 0 ? plot : SoonestPlot();
            _coach.Show(new CoachSpec
            {
                key = "harvestMore" + done + "_" + lit, dim = true, passThrough = true, hand = plot >= 0,
                title = "Thu hoạch thêm",
                body = "Thu hoạch thêm các cây vừa chín (" + done + "/" + HarvestsBeforeSelling + ").",
                target = lit >= 0 ? () => PlotRect(lit) : (Func<Rect?>)null, holeRadius = 60f, holePad = 4f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void OpenMenu()
        {
            if (_app.Panel is WarehousePanel) { Go(Step.Sell); return; }
            if (Stats.sell > _baseSell) { Go(Step.Missions); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (GS.Local.store.Count == 0) { Go(Step.Missions); return; }
            if (_app.SeedSheetOpen) _app.CloseSeedSheet();
            if (_app.Hud.MenuOpen) { Go(Step.OpenStore); return; }

            _coach.Show(new CoachSpec
            {
                key = "openMenu", dim = true,
                title = "Bán nông sản",
                body = "Nông sản vừa thu nằm trong Kho. Mở Menu nhé.",
                target = () => CoachView.ScreenRect(_app.Hud.MenuButton), holeRadius = 44f, holePad = 6f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void OpenStore()
        {
            if (_app.Panel is WarehousePanel) { Go(Step.Sell); return; }
            if (Interrupted()) { _coach.Hide(); return; }
            if (!_app.Hud.MenuOpen) { Go(Step.OpenMenu); return; }

            _coach.Show(new CoachSpec
            {
                key = "openStore", dim = true,
                title = "Vào Kho",
                body = "Chạm vào Kho để xem nông sản của bạn.",
                target = () => CoachView.ScreenRect(_app.Hud.MenuCell("store")), holeRadius = 40f, holePad = 8f,
                avoid = () => CoachView.ScreenRect(_app.Hud.MenuPanel),
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void Sell()
        {
            if (Stats.sell > _baseSell)
            {
                if (_app.RewardOpen) { _coach.Hide(); return; }
                // sold and admired the coins: put the warehouse away for the player, the next
                // thing to show is on the farm screen
                if (_app.Panel is WarehousePanel) _app.CloseAll();
                if (_app.Panel != null) { _coach.Hide(); return; }
                Go(Step.Missions);
                return;
            }
            if (!(_app.Panel is WarehousePanel wp))
            {
                if (_app.RewardOpen) { _coach.Hide(); return; }
                Go(GS.Local.store.Count > 0 ? Step.OpenMenu : Step.Missions);
                return;
            }
            if (_app.RewardOpen) { _coach.Hide(); return; }
            var btn = wp.SellButton;
            if (btn == null || !btn.interactable) { Go(Step.Missions); return; }

            _coach.Show(new CoachSpec
            {
                key = "sell", dim = true,
                title = "Bán sỉ",
                body = "Bán nông sản để lấy xu. Xu dùng để mua hạt giống và mở thêm ô đất.",
                // polled every frame: the panel can close between two thinks
                target = () => _app.Panel is WarehousePanel w && w.SellButton != null ? CoachView.ScreenRect((RectTransform)w.SellButton.transform) : null,
                holeRadius = 32f, holePad = 8f,
                secondary = "Bỏ qua hướng dẫn", onSecondary = Skip,
            });
        }

        void Missions()
        {
            if (_app.RewardOpen) { _coach.Hide(); return; }
            if (_app.Panel != null)
            {
                // the player opened the missions from the strip: that was the lesson
                if (_app.Panel is MissionsPanel) _sawMissions = true;
                _coach.Hide();
                return;
            }
            var strip = _app.Hud.MissionStrip;
            if (_sawMissions || strip == null || !strip.gameObject.activeInHierarchy) { Go(Step.Finish); return; }

            _coach.Show(new CoachSpec
            {
                key = "missions", dim = true,
                title = "Nhiệm vụ",
                body = "Làm nhiệm vụ để nhận XP và xu. Đầy thanh XP thì vào Menu ▸ Nâng cấp để lên cấp và mở hạt giống mới.",
                target = () => CoachView.ScreenRect(_app.Hud.MissionStrip), holeRadius = 22f, holePad = 6f,
                primary = "Đã hiểu", onPrimary = () => Go(Step.Finish),
            });
        }

        bool _sawMissions;

        void Finish()
        {
            if (Interrupted()) { _coach.Hide(); return; }
            _coach.Show(new CoachSpec
            {
                key = "finish", dim = true, centred = true,
                title = "Bạn đã sẵn sàng!",
                body = "• Thời tiết đổi mỗi 15 phút. Chạm đĩa thời tiết để xem cây nào được lợi.\n"
                     + "• Cấp " + QuickActions.HarvestLevel + " mở nút Thu hoạch nhanh, cấp " + IslandSys.Def(1).lv + " mở " + IslandSys.NameOf(1) + ".\n"
                     + "• Quên cách chơi? Menu ▸ Hướng dẫn.",
                primary = "Bắt đầu làm nông",
                onPrimary = () => { Go(Step.Done); _coach.Hide(); Sfx.Play(SfxId.Claim); },
            });
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------
        bool Growing()
        {
            foreach (var p in Farm.Plots) if (IsGrowing(p)) return true;
            return false;
        }

        /// <summary>Winds the home island's growing crops forward by the time since the last think
        /// times <see cref="FastForward"/> - 1. It moves the planting time back rather than adding to
        /// the cut, so the watering windows arrive sooner too; each think covers under a second of
        /// crop time, far shorter than any window, so none is skipped past.</summary>
        void SpeedUpGrowth()
        {
            if (_thinkDt <= 0f) return;
            long shift = (long)(_thinkDt * (FastForward - 1f) * 1000f);
            if (shift <= 0) return;
            foreach (var p in Farm.Plots)
                if (IsGrowing(p) && p.plantedAt > 0) p.plantedAt -= shift;
        }

        static bool IsGrowing(Plot p)
        {
            var st = PlotLogic.State(p);
            return st == PlotState.Growing || st == PlotState.Thirsty;
        }

        /// <summary>True when the windows are gone because rain spent them at planting — the one
        /// case worth saying out loud. A player who simply let them pass is taught later, by the
        /// "water" tip, the next time a plot is thirsty.</summary>
        bool RainWatered()
        {
            foreach (var p in Farm.Plots)
                if (IsGrowing(p) && p.plantWeather == (byte)Weather.Rain) return true;
            return false;
        }

        bool AnyWindowLeft()
        {
            foreach (var p in Farm.Plots)
                if (IsGrowing(p) && WaterSys.Remaining(p) > 0) return true;
            return false;
        }

        /// <summary>The walkthrough is about the home island. A player who wandered to a locked
        /// one is brought back rather than shown a hole over nothing.</summary>
        bool OnHomeIsland()
        {
            if (Farm.CurrentIsland == 0) return true;
            _coach.Hide();
            _app.GoToIsland(0);
            return false;
        }

        static int CentralPlot(List<int> plots)
        {
            int best = plots[0];
            float bestD = float.MaxValue;
            foreach (int i in plots)
            {
                var c = IslandView.CellPos(i);
                float d = Mathf.Abs(c.x) + Mathf.Abs(c.y) * 2f;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The plot the "wait" cards light up: the one the walkthrough was working on if it is
        /// still growing, else the first growing plot on the home island. -1 when none.</summary>
        int GrowingPlot()
        {
            var plots = Farm.Plots;
            if (_plot >= 0 && _plot < plots.Count && IsGrowing(plots[_plot])) return _plot;
            for (int i = 0; i < plots.Count; i++) if (IsGrowing(plots[i])) return i;
            return -1;
        }

        /// <summary>The growing plot that ripens first on the home island, or -1.</summary>
        int SoonestPlot()
        {
            int best = -1;
            float left = float.MaxValue;
            var plots = Farm.Plots;
            for (int i = 0; i < plots.Count; i++)
            {
                if (!IsGrowing(plots[i])) continue;
                float r = PlotLogic.Remain(plots[i]);
                if (r < left) { left = r; best = i; }
            }
            return best;
        }

        Rect? PlotRect(int i)
        {
            return CoachView.ScreenRect(Farm.PlotRoot(i), IslandView.PlotScale);
        }

        float _lastRefit;

        /// <summary>A step that dims everything but one plot must be sure that plot is on screen and
        /// big enough to hit — the player may have panned away or zoomed out to the map.</summary>
        void EnsureVisible(int plot)
        {
            var r = PlotRect(plot);
            if (!r.HasValue) return;
            var sr = new Rect(0, 0, Screen.width, Screen.height);
            bool onScreen = sr.Contains(r.Value.min) && sr.Contains(r.Value.max);
            bool bigEnough = r.Value.width > Screen.width * 0.08f;
            if ((onScreen && bigEnough) || Time.unscaledTime - _lastRefit < 1.5f) return;
            _lastRefit = Time.unscaledTime;
            _app.GoToIsland(Farm.CurrentIsland);
        }

        // ============================================================
        // tips
        // ============================================================
        public class Tip
        {
            public string id, title, body;
            /// <summary>True when the tip is worth showing now.</summary>
            public Func<PlayerState, bool> ready;
            /// <summary>True when an existing save already has what the tip announces (used once,
            /// migrating a pre-tutorial save). Defaults to <see cref="ready"/>.</summary>
            public Func<PlayerState, bool> alreadyFn;
            public Func<GameApp, RectTransform> target;
            public bool already(PlayerState s) { return (alreadyFn ?? ready)(s); }
        }

        public static readonly Tip[] Tips =
        {
            new Tip
            {
                id = "water", title = "Cây đang khát",
                body = "Ô nứt nẻ viền xanh là tới cữ tưới. Chạm để tưới: mỗi lần tưới, cây chín sớm hơn một khoảng (chạm vào cây để xem).",
                ready = s => s.stats.water == 0 && GameApp.I != null && GameApp.I.Farm != null && GameApp.I.Farm.WaterablePlots().Count > 0,
                alreadyFn = s => s.stats.water > 0,
                target = a => { var w = a.Farm.WaterablePlots(); return w.Count > 0 ? a.Farm.PlotRoot(w[0]) : null; },
            },
            new Tip
            {
                id = "quickHarvest", title = "Mới mở: Thu hoạch nhanh",
                body = "Một chạm thu hoạch mọi cây đã chín trên đảo. Số đỏ là số cây đang chờ.",
                ready = s => QuickActions.HarvestUnlocked(s),
                target = a => a.Hud.HarvestPill,
            },
            new Tip
            {
                id = "quickWater", title = "Mới mở: Tưới nhanh",
                body = "Một chạm tưới mọi ô đang tới cữ. Nút xám là chưa có ô nào khát.",
                ready = s => QuickActions.WaterUnlocked(s),
                target = a => a.Hud.WaterPill,
            },
            new Tip
            {
                id = "upgrade", title = "Lên cấp được rồi!",
                body = "Mở Menu ▸ Nâng cấp. Lên cấp giúp cây chín nhanh hơn, bán giá hơn, và mở hạt giống, ô đất, đảo mới.",
                ready = s => s.xp >= s.XpNeed && s.coin >= GameData.Level(s.lv).cost,
                alreadyFn = s => s.lv >= 2,
                target = a => a.Hud.MenuOpen ? a.Hud.MenuCell("upgrade") : a.Hud.MenuButton,
            },
            new Tip
            {
                id = "chest", title = "Bạn có rương!",
                body = "Thu hoạch tích năng lượng, đầy thanh là được một rương. Chạm vào đây để mở lấy xu và hạt giống.",
                ready = s => s.chests[0] + s.chests[1] + s.chests[2] + s.chests[3] > 0,
                alreadyFn = s => s.stats.chest > 0 || s.chests[0] + s.chests[1] + s.chests[2] + s.chests[3] > 0,
                target = a => a.Hud.EnergyChip,
            },
            new Tip
            {
                id = "weather", title = "Thời tiết vừa đổi",
                body = "Cứ 15 phút trời đổi một lần. Cây gieo lúc nào nhận hiệu ứng lúc đó: chín nhanh hay chậm, giá cao hay thấp. Chạm để xem.",
                ready = s => WeatherSys.Now(s) != Weather.Sunny,
                alreadyFn = s => true,
                target = a => a.Hud.WeatherDisc,
            },
            new Tip
            {
                id = "mutation", title = "Đột biến!",
                body = "Cây phát sáng là cây đột biến, bán giá gấp nhiều lần. Có 4 bậc: Ngọc Bích, Băng Giá, Viêm Hoả và hiếm nhất là Lôi Điện.",
                ready = s => s.stats.mutate > 0,
            },
            new Tip
            {
                id = "island", title = "Đủ cấp mở đảo mới",
                body = "Sang đảo bên cạnh xem cần nộp những nông sản gì để mở. Mỗi đảo tặng ô đất và một đặc quyền cho cả trang trại.",
                ready = s => s.islands.Count > 1 && !s.islands[1].unlocked && s.lv >= IslandSys.Def(1).lv,
                alreadyFn = s => s.lv >= IslandSys.Def(1).lv,
                target = a => a.Hud.PagerNext,
            },
            new Tip
            {
                id = "plot", title = "Mở thêm ô đất",
                body = "Bạn đủ xu mở thêm một ô. Chạm vào ô có ổ khoá để mua. Càng nhiều ô, mỗi vụ càng nhiều nông sản.",
                ready = s => s.PlotBuyable(0, out _, out _),
                alreadyFn = s => IslandSys.OpenCount(s.islands[0]) > 6,
            },
            new Tip
            {
                id = "pet", title = "Thú cưng đã mở!",
                body = "Ấp trứng để có bạn đồng hành: cứ 3 phút pet tự đi tưới và thu hoạch giúp bạn. Quả trứng đầu tiên miễn phí.",
                ready = s => PetSys.Unlocked(s) && s.petEggs == 0,
                alreadyFn = s => s.petEggs > 0,
                target = a => a.Hud.PetButton,
            },
        };

        /// <summary>Set by the screenshot pass, so a tip never lands in the middle of a shot.</summary>
        public static bool SuppressTips;
        float _tipQuietUntil;
        Tip _showing;

        void ThinkTips()
        {
            if (_showing != null)
            {
                // a tip pointing at a HUD element closes when that element is used
                bool gone = _app.Panel != null || _app.RewardOpen || _app.SeedSheetOpen;
                if (gone) { CloseTip(); }
                return;
            }
            if (SuppressTips || Time.unscaledTime < _tipQuietUntil) return;
            if (_app.Panel != null || _app.RewardOpen || _app.SeedSheetOpen || _app.Hud.MenuOpen) return;

            var s = GS.Local;
            foreach (var tip in Tips)
            {
                if (s.tipsSeen.Contains(tip.id) || !tip.ready(s)) continue;
                ShowTip(tip);
                return;
            }
        }

        void ShowTip(Tip tip)
        {
            _showing = tip;
            GS.Local.tipsSeen.Add(tip.id);
            GS.Save();
            var t = tip;
            _coach.Show(new CoachSpec
            {
                key = "tip_" + tip.id, dim = true, hand = tip.target != null, centred = tip.target == null,
                title = tip.title, body = tip.body,
                target = tip.target != null ? () => CoachView.ScreenRect(t.target(_app)) : (Func<Rect?>)null,
                holePad = 4f,
                primary = "Đã hiểu", onPrimary = CloseTip,
            });
            Sfx.Play(SfxId.Toggle, 0.7f);
        }

        void CloseTip()
        {
            _showing = null;
            _coach.Hide();
            // never two tips back to back
            _tipQuietUntil = Time.unscaledTime + 25f;
        }

        /// <summary>For the screenshot pass: show a step's card as it would appear, without
        /// touching the save.</summary>
        public void PreviewForAudit(Step step)
        {
            // Think may redirect (no thirsty plot → wait for one); follow it in memory only, a few
            // hops, and never write the step to the save the audit runs on.
            _frozen = false;
            _previewing = true;
            _step = step;
            Mark();
            for (int hop = 0; hop < 4; hop++)
            {
                var before = _step;
                Think();
                if (_step == before) break;
            }
            _previewing = false;
            _frozen = true;
        }

        /// <summary>For the screenshot pass: a tip's card, pointed at its target.</summary>
        public void PreviewTipForAudit(string id)
        {
            _frozen = true;
            foreach (var tip in Tips)
                if (tip.id == id)
                {
                    var t = tip;
                    _coach.Show(new CoachSpec
                    {
                        key = "tip_" + tip.id, dim = true, hand = tip.target != null, centred = tip.target == null,
                        title = tip.title, body = tip.body,
                        target = tip.target != null ? () => CoachView.ScreenRect(t.target(_app)) : (Func<Rect?>)null,
                        holePad = 4f, primary = "Đã hiểu", onPrimary = () => _coach.Hide(),
                    });
                }
        }

        /// <summary>Back to the step the save says, and to thinking.</summary>
        public void EndAuditPreview()
        {
            _coach.Hide();
            _step = Resume(GS.Local);
            _frozen = false;
            Mark();
        }
    }
}
