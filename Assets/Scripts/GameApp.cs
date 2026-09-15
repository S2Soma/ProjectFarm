using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Boots the game and owns every screen. The whole interface is built in code,
    /// so the scene only needs to exist — nothing has to be wired in the editor.</summary>
    public class GameApp : MonoBehaviour
    {
        public const float RefW = 1280f, RefH = 720f;

        public static GameApp I { get; private set; }

        RectTransform _root, _world, _overlayLayer, _popupLayer, _toastLayer;

        /// <summary>The sky layer (SkyView's canvas). The arrival cinematic lifts it a little as the
        /// camera comes down onto the home island.</summary>
        RectTransform _bgLayer;
        public RectTransform BackgroundLayer => _bgLayer;
        Canvas _canvas;
        ArchipelagoView _farm;
        /// <summary>The HUD needs to count what is ready, waterable and plantable every frame it
        /// renders; it does not need to act on any of it.</summary>
        public ArchipelagoView Farm => _farm;
        Hud _hud;
        public Hud Hud => _hud;

        // modal frame
        RectTransform _scrim, _card;
        CanvasGroup _modalGroup;
        PanelBase _panel;

        // plot popup
        RectTransform _plotPop;
        int _popIndex = -1;

        float _nextTick;
        float _nextSave;
        List<int> _ticking = new List<int>();
        bool _tickingDirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("LQFarm");
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            // Keep ticking when the Editor loses focus. Without this the player loop
            // freezes the moment you click away, which stalls coroutines mid-run and
            // (on device) would stop crops growing while the app is backgrounded.
            Application.runInBackground = true;

            // the farm is laid out wide; keep the device in landscape
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            BuildCanvas();

            // The start screen signs in and settles which save to play (this device's or the
            // account's) BEFORE the farm loads, so a save pulled from the account is simply the file
            // the game then opens — no rebuild of a running farm.
            Supa.RestoreSession();
            bool skip = s_skipStart;
            s_skipStart = false;
            if (s_auditStart)
            {
                // the screenshot pass: the start screen, idle — no sign-in, no network
                s_auditStart = false;
                _start = StartScreen.ShowIdle(Layer("start", 30, true), this);
                _start.PreviewForAudit("home");
            }
            else if (!skip && StartScreen.Wanted)
                _start = StartScreen.Show(Layer("start", 30, true), this);
            else
                BuildGame();
        }

        StartScreen _start;
        static bool s_skipStart, s_auditStart;

        /// <summary>The start screen while it is up (the screenshot pass drives it).</summary>
        public StartScreen StartForAudit => _start;

        /// <summary>The flight from the start screen into the farm, while it plays.</summary>
        EnterCinematic _cinematic;
        public bool InCinematic => _cinematic != null;
        string _heldToast;
        /// <summary>The walkthrough and tips wait a moment after the cinematic: a card landing on
        /// the very frame the HUD settles reads as part of the film breaking.</summary>
        float _tutorialAfter;

        /// <summary>The farm exists (the start screen is gone).</summary>
        public bool Built => _farm != null;

        /// <summary>Signing out: the session is dropping on purpose, not expiring.</summary>
        public bool LeavingForStart { get; private set; }

        /// <summary>For the Editor's screenshot and dev tools, which drive a farm that has to exist:
        /// dismiss the start screen and play this device's save, offline.</summary>
        public void EnsureBuilt()
        {
            // instant, always: a tool that needs the farm does not wait for a film to end
            if (_cinematic != null) _cinematic.Skip();
            if (Built) return;
            if (_start != null) { _start.Dispose(); _start = null; }
            BuildGame();
        }

        /// <summary>Called by the start screen once sign-in and the save question are settled.</summary>
        public void EnterFromStart()
        {
            if (Built || _cinematic != null) return;
            var start = _start;
            _start = null;
            if (start == null) { BuildGame(); return; }
            // The farm is built only once the cinematic's veil covers the screen, so the second or
            // two a phone spends in BuildGame is a still frame of cloud, never a half-built farm.
            _cinematic = EnterCinematic.Play(this, Layer("cinematic", 31, true), start, () => { if (!Built) BuildGame(); });
        }

        /// <summary>The cinematic has handed the screen over: the walkthrough may speak now, and a
        /// toast that arrived during it (a sync notice) is shown.</summary>
        public void OnCinematicEnded(EnterCinematic c)
        {
            if (_cinematic != c) return;
            _cinematic = null;
            _tutorialAfter = Time.unscaledTime + 0.7f;
            if (_heldToast != null) { var m = _heldToast; _heldToast = null; Toast(m); }
        }

        /// <summary>Tear the game down and boot it again: after signing out (back to the start
        /// screen) or after the account's save replaced this device's (straight into the farm).</summary>
        public static void Restart(bool showStart)
        {
            var old = I;
            if (old != null)
            {
                old.LeavingForStart = showStart;
                if (old._farm != null && GS.Local.loaded) GS.Save();
                GS.Local.loaded = false;          // nothing left running may write the file now
                I = null;
                Destroy(old.gameObject);
            }
            s_skipStart = !showStart;
            Supa.Run(RebootNextFrame());
        }

        /// <summary>The screenshot pass for the arrival cinematic: tear the farm down (saved first,
        /// as any restart) and come back on an idle start screen that does not sign in.</summary>
        public static void RebootToStartForAudit()
        {
            Restart(true);
            s_auditStart = true;
        }

        static IEnumerator RebootNextFrame()
        {
            yield return null;                    // let Destroy finish: one EventSystem, one canvas
            GS.Viewing = null;
            GS.Local = new PlayerState();
            CloudSync.Verified = CloudSync.Verified && Supa.SignedIn;
            new GameObject("LQFarm").AddComponent<GameApp>();
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>For the screenshot pass: the start screen over the running farm, idle (no
        /// sign-in attempted). Dispose it afterwards.</summary>
        public StartScreen OpenStartForAudit() { return StartScreen.ShowIdle(Layer("start_audit", 30, true), this); }

        void BuildGame()
        {
            GS.Load();

            // Each layer is its own nested Canvas. With everything on one canvas, a single
            // moving cloud or bobbing badge forced a rebuild of every graphic in the game
            // once per frame — that was the lag.
            _sky = gameObject.AddComponent<SkyView>();
            _bgLayer = Layer("background", 1, false);
            _sky.Build(_bgLayer);

            _world = Layer("world", 2, true);

            _farm = gameObject.AddComponent<ArchipelagoView>();
            _farm.Build(_world);
            _farm.onPlotTapped = OpenPlot;
            _farm.onIslandTapped = i => Open(new IslandPanel(this, i));

            // Weather falls across the islands but never across a button: its own layer, above
            // the world and below the HUD, and not interactive.
            _weather = gameObject.AddComponent<WeatherFx>();
            _weather.Init(Layer("weather", 3, false), _farm);
            _sky.Bind(_weather, _farm);

            _hud = new Hud();
            _hud.Build(Layer("hud", 4, true, true), this);
            _farm.storeAnchorWorld = () => _hud.WarehouseWorld;

            _popupLayer = Layer("popups", 5, true, true);
            _overlayLayer = Layer("overlay", 6, true, true);
            // The coach dims EVERYTHING it is not pointing at, panels included (the Bán sỉ button
            // lives in one), so it sits above them. Not safe-area inset: the dark must reach the
            // screen edge; the card keeps itself inside the safe area.
            _coach = new CoachView();
            _coach.Build(Layer("coach", 7, true));
            _toastLayer = Layer("toasts", 8, false, true);
            // taps and swipe trails, over everything and never in the way of a tap
            var touchLayer = Layer("touchfx", 9, false);
            FxKit.Ensure(gameObject);
            gameObject.AddComponent<TouchFx>().layer = touchLayer;

            _farm.RenderAll();
            _hud.Render(true);
            _weather.SetWeather(WeatherSys.Now(GS.Local));
            _farm.onIslandChanged = _ =>
            {
                _tickingDirty = true;
                ClosePlotPopup();
                CloseSeedSheet();
                _hud.Render();
            };
            _farm.onGroundTapped = () => { ClosePlotPopup(); CloseSeedSheet(); };

            var watcher = _root.gameObject.AddComponent<ResizeWatcher>();
            watcher.onResize = FitFarm;
            FitFarm();

            _tutorial = new Tutorial(this, _coach);
            Pets = PetDirector.Attach(this);
            CloudSync.Attach(this);
        }

        CoachView _coach;
        Tutorial _tutorial;
        public Tutorial Tutorial => _tutorial;

        // ---- what the tutorial reads ----
        public PanelBase Panel => _panel;
        public bool RewardOpen => _reward != null;
        public bool SeedSheetOpen => _sheet != null && _sheet.IsOpen;
        public SeedSheet Sheet => _sheet;

        SkyView _sky;
        public SkyView SkyView => _sky;
        WeatherFx _weather;
        public WeatherFx WeatherView => _weather;

        /// <summary>Re-fit the world to the current canvas.
        ///
        /// The fitting formula itself now lives in <see cref="MapCamera.Recompute"/> — it became
        /// the base zoom level rather than a one-off scale assignment, because the field is no
        /// longer a thing with one fixed size but a world the player can move around in.</summary>
        void FitFarm()
        {
            if (_farm != null) _farm.Refit();
        }

        // ============================================================
        // scaffolding
        // ============================================================
        void BuildCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            // Expand keeps the whole 1280x720 design on screen whatever the aspect ratio:
            // a wide phone gains side room, a 4:3 tablet gains height. Nothing is ever cut.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _root = UIKit.Node("ui", go.transform);
            _root.Stretch();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        /// <summary>One nested canvas, so what changes inside it only rebuilds itself.</summary>
        RectTransform Layer(string name, int order, bool interactive, bool safeArea = false)
        {
            var rt = UIKit.Node(name, _root);
            rt.Stretch();
            var c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = order;
            // a nested canvas is not raycast by its parent's raycaster; it needs its own
            if (interactive) rt.gameObject.AddComponent<GraphicRaycaster>();
            // Scenery fills the whole screen, notch included; controls must not. Anchoring
            // chrome straight to the screen edge hides it under a notch or a gesture bar.
            if (safeArea) rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        // ============================================================
        // main loop
        // ============================================================
        void Update()
        {
            if (_farm == null) return;            // still on the start screen
            float now = Time.unscaledTime;

            // Badges and plants are animated by FieldAnimator; the loop here only has to advance
            // the countdowns, once a second. It used to re-render every ready plot and
            // allocate a fresh list every frame.
            if (now >= _nextTick)
            {
                _nextTick = now + 1f;
                // the hour turned: the map re-dresses itself (a no-op when nothing changed)
                _weather.SetWeather(WeatherSys.Now(GS.Local));
                if (_tickingDirty) { _ticking = _farm.TickingPlots(); _tickingDirty = false; }

                bool finished = false;
                foreach (int i in _ticking)
                {
                    _farm.RenderPlot(i);
                    // _farm.Plots, not GS.Viewing.plots: the ticking list comes from the island
                    // being LOOKED AT, and reading its state out of island zero meant every
                    // countdown on islands two through six was compared against the home farm.
                    if (PlotLogic.State(_farm.Plots[i]) == PlotState.Ready) finished = true;
                }
                if (finished) _tickingDirty = true;
                // rain and storm water whatever window just opened, on every island
                RainTick();
                // whatever ripened or turned thirsty outside the ticking list (see RefreshStale)
                _farm.RefreshStale();
                RefreshPlotPop();
                if (_panel is PetPanel petPanel) petPanel.TickTimer();

                _hud.Render();
            }

            if (now >= _nextSave) { _nextSave = now + 5f; GS.Save(); }

            // not over the cinematic: a Welcome card landing mid-flight would break the shot
            if (_cinematic == null && now >= _tutorialAfter) _tutorial?.Tick(Time.unscaledDeltaTime);

            if (EscapePressed()) Back();
        }

        /// <summary>Escape / Android Back: close the topmost thing, one per press — the reward card,
        /// then the panel under it, the seed sheet, the menu, the plot popup. A tutorial step that
        /// has dimmed the screen swallows it: Back must not dismiss a panel the step points into.</summary>
        void Back()
        {
            if (_reward != null) { CloseReward(); return; }
            if (_tutorial != null && _tutorial.Blocking) return;
            if (_panel != null) { CloseAll(); return; }
            if (SeedSheetOpen) { CloseSeedSheet(); return; }
            if (_hud != null && _hud.MenuOpen) { _hud.CloseMenu(); return; }
            ClosePlotPopup();
        }

        /// <summary>Escape / Android back, under whichever input backend the project uses.</summary>
        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        /// <summary>Resuming redraws the field: crops kept growing on the wall clock while the
        /// app was away, so every timer and badge on screen is stale.
        ///
        /// The null guard is not defensive padding. Setting <c>Application.runInBackground</c>
        /// raises this callback SYNCHRONOUSLY, and that assignment happens in <see cref="Awake"/>
        /// before the world is built — so on the first launch of an Editor session this fired
        /// with <c>_farm</c> still null and threw. Unity swallows the exception, which is why it
        /// sat in the log looking harmless.</summary>
        void OnApplicationPause(bool paused)
        {
            if (_farm == null) return;
            if (paused) GS.Save();
            else { _tickingDirty = true; _farm.RenderAll(); }
        }

        void OnApplicationQuit() { if (_farm != null) GS.Save(); }

        void MarkDirty() { _tickingDirty = true; }

        // ============================================================
        // modal frame
        // ============================================================
        public void Open(PanelBase panel)
        {
            Sfx.Play(SfxId.PanelOpen);
            _hud?.CloseMenu();
            CloseSeedSheet();
            CloseAll();
            ClosePlotPopup();
            _panel = panel;

            _scrim = UIKit.Node("scrim", _overlayLayer);
            _scrim.Stretch();
            // the dark reaches the screen edges; the card stays centred in the safe area
            var bleed = UIKit.Node("bleed", _scrim);
            bleed.Stretch();
            FullBleed.On(bleed);
            var scrimIm = bleed.gameObject.AddComponent<Image>();
            scrimIm.color = Theme.Scrim;
            var scrimBtn = _scrim.gameObject.AddComponent<Button>();
            scrimBtn.targetGraphic = scrimIm;
            var sc = scrimBtn.colors; sc.fadeDuration = 0f; scrimBtn.colors = sc;
            scrimBtn.onClick.AddListener(CloseAll);

            _modalGroup = _scrim.gameObject.AddComponent<CanvasGroup>();
            _modalGroup.alpha = 0f;
            Tween.Fade(_modalGroup, 1f, 0.14f);

            var holder = UIKit.Node("cardHolder", _scrim);
            holder.Anchor(UIKit.Center, Vector2.zero, panel.Size);

            // shrink to fit rather than overflow when the canvas is smaller than the design
            Vector2 room = _root.rect.size - new Vector2(32f, 32f);
            float fit = Mathf.Min(1f, Mathf.Min(room.x / panel.Size.x, room.y / panel.Size.y));
            if (fit < 1f) holder.localScale = Vector3.one * fit;

            // M3 paper card, M4 ribbon. The old header was a Kenney brown plate with corners of
            // about 6 px, inset 12/8 inside a card rounded to 28 — a box inside a lozenge, which is
            // most of what made every panel look boxy. The ribbon now runs full-bleed inside the
            // card's 3 px edge and shares its corner (28 - 3 = 25), and only its top is rounded.
            var bg = SurfaceLook.Add(holder, Looks.Paper, 28f).Fill;
            bg.raycastTarget = true;                 // taps inside the card must not close it
            _card = holder;

            var head = UIKit.Node("head", holder);
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(3, -75);
            head.offsetMax = new Vector2(-3, -3);
            SurfaceLook.Add(head, Looks.Ribbon, 25f, topOnly: true);
            var title = UIKit.LabelOutlined(head, panel.Title, 30, Looks.Ribbon.ink, TextAnchor.MiddleLeft, Looks.Ribbon.inkLine);
            title.rectTransform.Stretch(30, 0, 96, 4);

            if (!string.IsNullOrEmpty(panel.Subtitle))
            {
                title.rectTransform.Anchor(UIKit.Left, new Vector2(30, 12), new Vector2(560, 38));
                title.rectTransform.pivot = new Vector2(0, 0.5f);
                var sub = UIKit.Label(head, panel.Subtitle, 17, Looks.Ribbon.ink.Alpha(0.82f), TextAnchor.MiddleLeft);
                sub.rectTransform.Anchor(UIKit.Left, new Vector2(30, -17), new Vector2(560, 26));
                sub.rectTransform.pivot = new Vector2(0, 0.5f);
            }

            // Inside the ribbon, centred on its height (3..75 → 39) and 18 px from the card's right
            // edge. It used to overhang the card's top-right corner by 14 px, which read as a button
            // that had slipped off the panel (owner, 15/9).
            var close = UIKit.IconBtn(head, null, Theme.Red, 0.5f, CloseAll);
            close.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-16, 2), new Vector2(52, 52));
            var closeIcon = UIKit.Img(close.transform, Theme.Skin.IconCross, Color.white, "x");
            closeIcon.preserveAspect = true;
            closeIcon.rectTransform.Stretch(16, 16, 16, 16);

            // body
            panel.Card = holder;
            panel.Body = UIKit.Node("body", holder);
            panel.Body.Stretch(22, 88, 22, 20);

            panel.Build();
            Tween.PopIn(holder, 0.24f, 0.88f);
        }

        public void CloseAll()
        {
            if (_scrim != null)
            {
                Sfx.Play(SfxId.PanelClose);
                var dying = _scrim.gameObject;
                Tween.Fade(_modalGroup, 0f, 0.12f, () => { if (dying != null) Destroy(dying); });
                _scrim = null;
                _card = null;
            }
            _panel = null;
            CloseReward();
        }

        public void RefreshPanel() { _panel?.Refresh(); }

        void RainTick()
        {
            var s = GS.Local;
            if (s == null || _farm == null) return;
            bool any = false;
            for (int ii = 0; ii < s.islands.Count; ii++)
            {
                var isl = s.islands[ii];
                if (!isl.unlocked) continue;
                for (int i = 0; i < isl.plots.Count && i < GS.PlotCount; i++)
                {
                    var p = isl.plots[i];
                    // cheap out: nothing to do unless a window is open right now
                    if (!WaterSys.WindowOpen(p, out _)) continue;
                    if (WaterSys.RainWater(s, p, catchUp: false) > 0) { any = true; _farm.ShowRainWater(ii, i); }
                }
            }
            if (any) { MarkDirty(); _hud.Render(); }
        }

        // ============================================================
        // pets
        // ============================================================
        public PetDirector Pets { get; private set; }

        /// <summary>One job from a pet's patrol, through the same view calls a tap uses.</summary>
        public bool PetWork(PetJob j)
        {
            if (_farm == null) return false;
            bool ok;
            if (j.water)
            {
                ok = _farm.WaterOn(j.island, j.plot);
                if (ok) Sfx.Play(SfxId.Water, 0.55f);
            }
            else
            {
                ok = _farm.HarvestOn(j.island, j.plot, out var res);
                if (ok)
                {
                    Sfx.Play(SfxId.Harvest, 0.65f);
                    ShowMutation(res, modalAllowed: false);
                }
            }
            if (ok) { MarkDirty(); _hud.Render(); }
            return ok;
        }

        /// <summary>The pet took a snack. Said once, in a toast, with what it was.</summary>
        public void PetAte(PetDef pet, Seed seed, int variant)
        {
            string what = seed != null ? seed.name + (variant > 0 ? " " + Art.Elem(variant).name : "") : "nông sản";
            Toast(pet.name + " ăn vụng 1 " + what + "!");
            _hud.Render(); RefreshPanel(); GS.Save();
        }

        public void OnPetsChanged() { _hud.Render(); }

        /// <summary>Build views for any island the player has gained. Called after an unlock.</summary>
        public void SyncIslands() { _farm?.Sync(); }

        /// <summary>Move the camera to an island.</summary>
        public void GoToIsland(int index) { _farm?.GoToIsland(index); }

        /// <summary>Redraw everything from current state. Exists for editor tooling that pokes
        /// state directly — see the Dev menu items — and for nothing in the game itself.</summary>
        public void ForceRedraw()
        {
            _farm.RenderAll();
            MarkDirty();
            _hud.Render();
            RefreshPanel();
        }

        // ============================================================
        // plot popup
        // ============================================================
        public void ClosePlotPopup()
        {
            if (_plotPop != null) Destroy(_plotPop.gameObject);
            _plotPop = null;
            _popIndex = -1;
            _popGrow = _popWater = null;
            _popWaterBtn = _popRushBtn = null;
        }

        public void OpenPlot(int i)
        {
            if (_panel != null) return;
            var p = _farm.Plots[i];
            PlotState st = PlotLogic.State(p);

            if (st == PlotState.Ready) { ClosePlotPopup(); DoHarvest(i); return; }

            // A thirsty plot is watered by tapping it, the way a ripe one is harvested. The popup
            // put the one obvious action behind a second tap on a window that can be 8 s long; it
            // is still there on the next tap, for the timer and "Chín ngay".
            if (st == PlotState.Thirsty) { CloseSeedSheet(); DoWater(i); return; }

            // An empty bed opens (or re-targets) the seed sheet instead of a popup.
            if (st == PlotState.Empty) { OpenSeedSheet(i); return; }
            CloseSeedSheet();

            if (_popIndex == i) { ClosePlotPopup(); return; }

            ClosePlotPopup();
            _popIndex = i;

            var pop = UIKit.Node("plotPop", _popupLayer);
            _plotPop = pop;

            float w = st == PlotState.Locked ? 340f : 420f;
            float h = st == PlotState.Locked ? 172f : 222f;
            pop.sizeDelta = new Vector2(w, h);

            var bg = SurfaceLook.Add(pop, Looks.Paper, 24f).Fill;
            bg.raycastTarget = true;

            if (st == PlotState.Locked) BuildLockedPop(pop, i);
            else BuildGrowingPop(pop, i, p);

            // anchor above the plot, clamped to the screen
            Vector3 world = _farm.WorldOfPlot(i);
            Vector2 local = _popupLayer.InverseTransformPoint(world);
            local.y += h * 0.5f + 96f;
            Vector2 half = _popupLayer.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + w * 0.5f + 12f, half.x - w * 0.5f - 12f);
            local.y = Mathf.Clamp(local.y, -half.y + h * 0.5f + 12f, half.y - h * 0.5f - 14f);
            pop.anchoredPosition = local;

            Tween.PopIn(pop, 0.2f, 0.85f);
        }

        void PopTitle(RectTransform pop, string title, string sub)
        {
            var t = UIKit.Label(pop, title, 24, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(UIKit.Top, new Vector2(0, -14), new Vector2(0, 30));
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(14, -44); t.rectTransform.offsetMax = new Vector2(-14, -14);

            if (sub == null) return;
            var s = UIKit.Label(pop, sub, 17, Theme.InkSoft, TextAnchor.MiddleCenter);
            s.rectTransform.anchorMin = new Vector2(0, 1); s.rectTransform.anchorMax = new Vector2(1, 1);
            s.rectTransform.offsetMin = new Vector2(14, -70); s.rectTransform.offsetMax = new Vector2(-14, -44);
        }

        /// <summary>The locked-plot popup is where the plot ladder is taught.
        ///
        /// It quotes the price the ladder is CURRENTLY at rather than a price attached to this
        /// tile, because that is what the player will actually pay — see <c>IslandSys.PlotLevel</c>
        /// for why the ladder is indexed by how many plots are open instead of by which one was
        /// tapped. When the level gate has not been met the price is not shown at all: a number
        /// the player cannot act on reads as a second obstacle rather than a goal.</summary>
        void BuildLockedPop(RectTransform pop, int i)
        {
            int island = _farm != null ? _farm.CurrentIsland : 0;
            var isl = GS.Local.EnsureIsland(island);
            int open = IslandSys.OpenCount(isl);
            int price = IslandSys.PlotPrice(island, open);
            int needLv = IslandSys.PlotLevel(island, open);
            bool levelOk = GS.Local.lv >= needLv;
            bool coinOk = GS.Local.coin >= price;

            if (!levelOk)
            {
                PopTitle(pop, "Ô đất bị khoá", "Mở bán ở cấp " + needLv + " · bạn đang cấp " + GS.Local.lv);

                var only = UIKit.Btn(pop, "Nâng cấp", Theme.Blue, Theme.BlueDeep, 21, 20,
                                     () => { ClosePlotPopup(); Open(new UpgradePanel(this)); });
                only.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 18), new Vector2(190, 52));
                return;
            }

            PopTitle(pop, "Mở ô đất",
                     "Ô thứ " + (open + 1) + "/" + IslandSys.PlotsPerIsland + " · " + IslandSys.NameOf(island));

            var a = UIKit.Btn(pop, "Nâng cấp", Theme.Blue, Theme.BlueDeep, 21, 20,
                              () => { ClosePlotPopup(); Open(new UpgradePanel(this)); });
            a.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-82, 18), new Vector2(150, 52));

            var b = UIKit.Btn(pop, "Mở · " + Fmt.N(price),
                              coinOk ? Theme.Amber : Theme.Cream3,
                              coinOk ? Theme.AmberDeep : Theme.Hex("#B9A98C"), 21, 20,
                              () => BuyPlot(i));
            b.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(82, 18), new Vector2(150, 52));
            if (!coinOk)
            {
                UIKit.BtnLabel(b).color = Theme.InkSoft;
                b.interactable = false;
            }
        }

        // live readouts of the growing-plot popup, refreshed by the one-second tick
        Image _popGrow, _popWater;
        Text _popGrowText, _popWaterText;
        RectTransform _popWaterRow;
        SkinButton _popWaterBtn, _popRushBtn;

        /// <summary>A growing plot: two bars, each with its time written inside it, and two buttons.
        ///
        /// It used to be one subtitle line, "Còn 12m 40s · Băng Giá · 3 quả · lượt tưới sau 4m 10s ·
        /// còn 2 lượt", which ran out of a 340 px card on any long crop; then a bar with a caption
        /// column beside it ("chín sau 9p 39s"). The owner asked (15/9) for the time alone, inside
        /// the bar: the icon in front of the bar already says which clock it is.</summary>
        void BuildGrowingPop(RectTransform pop, int i, Plot p)
        {
            var seed = GameData.Get(p.crop);
            var el = Art.Elem(p.variant);
            int fruits = GS.Viewing.YieldOf(seed, p.variant);
            // how many times it drinks is part of what the crop IS, so it sits with the fruit count
            string drinks = " · tưới " + WaterSys.Windows(p) + " lần";
            string sub = p.variant > 0 ? el.Grade + " " + el.name + " · " + fruits + " quả" + drinks : fruits + " quả mỗi lần thu" + drinks;
            PopTitle(pop, seed.name, sub);

            const float left = 22f, iconW = 30f, barH = 26f;
            float barW = pop.sizeDelta.x - left * 2f - iconW - 4f;

            RectTransform Row(float y, Sprite icon, Color iconTint, Color fill, Color line, out Image bar, out Text caption)
            {
                var row = UIKit.Node("row", pop);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1f);
                row.offsetMin = new Vector2(left, y - barH); row.offsetMax = new Vector2(-left, y);
                var ic = UIKit.Img(row, icon, iconTint, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, Vector2.zero, new Vector2(iconW - 4f, iconW - 4f));
                bar = UIKit.Bar(row, Theme.Hex("#E3D3B3"), fill, 13);
                var track = (RectTransform)bar.transform.parent;
                track.Anchor(UIKit.Left, new Vector2(iconW + 4f, 0), new Vector2(barW, barH));
                // white with the fill's own dark line: reads on the filled part and on the beige track
                caption = UIKit.LabelOutlined(track, "", 17, Color.white, TextAnchor.MiddleCenter, line);
                caption.rectTransform.Stretch(0, 0, 0, 1);
                return row;
            }

            Row(-78f, Art.Icon(seed.art, p.variant), Color.white, Theme.Green, Theme.Hex("#1C7439"), out _popGrow, out _popGrowText);
            _popWaterRow = Row(-112f, Theme.Skin.Droplet, Theme.Blue, Theme.Hex("#5FB8F0"), Theme.BlueDeep, out _popWater, out _popWaterText);

            var water = UIKit.Btn(pop, "Tưới nước", Theme.Blue, Theme.BlueDeep, 21, 20, () => DoWater(i));
            water.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-92, 16), new Vector2(170, 52));
            _popWaterBtn = water.GetComponent<SkinButton>();

            var rush = UIKit.Btn(pop, "Chín ngay", Theme.Amber, Theme.AmberDeep, 20, 20, () => SpeedUp(i));
            rush.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(92, 16), new Vector2(170, 52));
            _popRushBtn = rush.GetComponent<SkinButton>();

            RefreshPlotPop();
        }

        /// <summary>Re-read the open popup's plot. Closes it once there is nothing left to show.</summary>
        void RefreshPlotPop()
        {
            if (_plotPop == null || _popIndex < 0 || _popGrow == null) return;
            var p = _popIndex < _farm.Plots.Count ? _farm.Plots[_popIndex] : null;
            var st = PlotLogic.State(p);
            if (st != PlotState.Growing && st != PlotState.Thirsty) { ClosePlotPopup(); return; }

            _popGrow.fillAmount = Mathf.Clamp01(PlotLogic.Elapsed(p) / Mathf.Max(0.001f, p.dur));
            _popGrowText.text = Fmt.Time(PlotLogic.Remain(p));

            bool open = st == PlotState.Thirsty;
            int windows = WaterSys.Remaining(p);
            if (open)
            {
                _popWater.fillAmount = 1f;
                // no clock to show while the window is open: the bar is full and says so
                _popWaterText.text = "Tưới ngay";
            }
            else if (windows > 0)
            {
                float period = WaterSys.Period(p.dur, WaterSys.Windows(p));
                _popWater.fillAmount = Mathf.Clamp01(1f - WaterSys.NextWindowIn(p) / Mathf.Max(0.001f, period));
                _popWaterText.text = Fmt.Time(Mathf.CeilToInt(WaterSys.NextWindowIn(p)));
            }
            else
            {
                _popWater.fillAmount = 0f;
                _popWaterText.text = "Đủ nước";
            }

            if (_popWaterBtn != null)
            {
                var b = _popWaterBtn.GetComponent<Button>();
                if (b.interactable != open)
                {
                    b.interactable = open;
                    UIKit.Restyle(b, open ? Theme.Blue : Theme.Cream3, open ? (Color?)null : Theme.InkSoft);
                }
                UIKit.BtnLabel(b).text = open ? "Tưới nước" : windows > 0 ? "Chưa tới cữ" : "Đã hết cữ";
            }
            if (_popRushBtn != null)
            {
                int price = ShopSys.RushPrice(GS.Local, p);
                UIKit.BtnLabel(_popRushBtn.GetComponent<Button>()).text = "Chín ngay · " + Fmt.Short(price);
            }
        }

        void DoWater(int i)
        {
            if (_farm.Water(i)) { Sfx.Play(SfxId.Water); MarkDirty(); _hud.Render(); GS.Save(); }
            ClosePlotPopup();
        }

        void SpeedUp(int i)
        {
            var p = _farm.Plots[i];
            int price = ShopSys.RushPrice(GS.Local, p);
            if (GS.Local.coin < price) { Toast("Không đủ xu nông trại"); return; }
            GS.Local.AddCoin(-price);
            _farm.InstantGrow(i);
            MarkDirty(); _hud.Render(); GS.Save();
            ClosePlotPopup();
        }

        void BuyPlot(int i)
        {
            int island = _farm != null ? _farm.CurrentIsland : 0;
            if (!GS.Local.PlotBuyable(island, out int price, out int needLv))
            {
                Toast(GS.Local.lv < needLv ? "Mở bán ở cấp " + needLv : "Không đủ xu nông trại");
                return;
            }
            if (!GS.Local.BuyPlot(island, i)) { ClosePlotPopup(); return; }

            _farm.RenderAll(); MarkDirty(); _hud.Render(); GS.Save();
            Sfx.Play(SfxId.Coins);
            Toast("Đã mở ô đất · " + Fmt.N(price) + " xu");
            ClosePlotPopup();
        }

        /// <summary>Feedback for one harvest, weighted by how rare the result was.
        ///
        /// Escalation is a parameter table, not new code — every effect used here already
        /// existed. The rule that matters is the last row: a modal is reserved for the top tier
        /// alone. Anything more generous and a player harvesting sixteen plots is interrupted
        /// sixteen times, which turns the reward into an obstacle.</summary>
        void DoHarvest(int i)
        {
            if (!_farm.Harvest(i, out var res)) return;
            Sfx.Play(SfxId.Harvest);
            if (res.bonusCoins > 0) Sfx.Play(SfxId.Coins, 0.6f);
            MarkDirty(); _hud.Render(); GS.Save();
            ShowMutation(res, modalAllowed: true);
        }

        void ShowMutation(IslandView.HarvestResult res, bool modalAllowed)
        {
            if (res.variant <= 0) return;
            var el = Art.Elem(res.variant);
            int top = Art.Elements.Length - 1;

            switch (res.variant)
            {
                case 1:  break;                                        // Ngọc Bích: the glow says it
                case 2:  Sfx.Play(SfxId.Mutation); break;             // the ring says it
                case 3:
                    Sfx.Play(SfxId.Mutation);
                    Toast(el.Grade + " · " + el.name + "! " + res.seed.name);
                    Tween.Shake(_root, 5f);
                    break;
                default:
                    Tween.Shake(_root, 9f);
                    if (res.variant == top && modalAllowed)
                        ShowReward(el.name.ToUpper() + "!", new List<RewardItem>
                        {
                            new RewardItem(Art.Icon(res.seed.art, res.variant),
                                           res.seed.name + " " + el.name, el.glow),
                            new RewardItem(CoinIconSprite, "×" + Fmt.N(Mathf.RoundToInt(el.sell)) + " giá trị", Theme.Amber),
                            new RewardItem(Theme.Skin.NavSeeds, res.fruits + " quả", Theme.GreenDeep),
                        }, SfxId.Legendary);
                    else Toast(el.Grade + " · " + el.name + "! " + res.seed.name);
                    break;
            }
        }

        static Sprite CoinIconSprite => Theme.Skin.Coin;

        /// <summary>Sweep-harvest, and the summary that replaces sixteen separate receipts.
        ///
        /// Per-plot popups are suppressed entirely here. One line saying what, how much and why
        /// is both honest and readable; sixteen of them in a row is neither. A legendary found
        /// during the sweep is QUEUED rather than allowed to interrupt — the arcs finish, the
        /// summary lands, and only then does it get its moment.</summary>
        public void HarvestAll()
        {
            int n = 0, fruits = 0, coins = 0, xp = 0, mutations = 0;
            var best = default(IslandView.HarvestResult);
            // why: the weather most of the bonus came from, and the best tag multiplier
            var weatherBonus = new Dictionary<Weather, int>();
            float bestTag = 1f;
            Sprite firstIcon = null;

            int k = 0;
            foreach (int i in _farm.ReadyPlots())
            {
                if (!_farm.Harvest(i, out var res, k)) continue;
                k++;
                n++;
                fruits += res.fruits;
                coins += res.bonusCoins;
                xp += res.xp;
                if (firstIcon == null) firstIcon = Art.Icon(res.seed.art, 0);
                if (res.variant > 0) mutations++;
                if (res.variant > best.variant) best = res;
                IslandView.SplitSell(res.weather, res.sellMul, out float w, out float t);
                if (Mathf.Abs(w - 1f) >= 0.02f)
                {
                    weatherBonus.TryGetValue(res.weather, out int cnt);
                    weatherBonus[res.weather] = cnt + 1;
                }
                if (t > bestTag) bestTag = t;
            }

            if (n == 0) { Toast("Chưa có cây nào chín"); return; }
            MarkDirty(); _hud.Render(); GS.Save();
            Tween.Shake(_root, 4f);
            StartCoroutine(SweepSounds(n, coins > 0));

            Weather mainWeather = Weather.Sunny;
            int most = 0;
            foreach (var kv in weatherBonus) if (kv.Value > most) { most = kv.Value; mainWeather = kv.Key; }
            StartCoroutine(HarvestSummary(n, fruits, coins, xp, firstIcon, most > 0 ? mainWeather : (Weather?)null,
                                          bestTag, mutations, 0.2f + k * 0.04f));

            // the arcs land, the summary appears, and only then does a legendary get its modal
            if (best.variant == Art.Elements.Length - 1) StartCoroutine(QueuedMutation(best, 0.2f + k * 0.04f + 1.2f));
        }

        RectTransform _summary;

        /// <summary>A pluck for each arc as it leaves, up to eight — the sweep's cascade, heard.</summary>
        IEnumerator SweepSounds(int n, bool coins)
        {
            int plucks = Mathf.Min(n, 8);
            for (int i = 0; i < plucks; i++)
            {
                Sfx.Play(SfxId.Harvest, 0.8f);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.04f, n * 0.04f / plucks));
            }
            if (coins) Sfx.Play(SfxId.Coins, 0.7f);
        }

        /// <summary>For the screenshot pass: the receipts, without harvesting anything (the audit
        /// runs on the developer's own save).</summary>
        public void PreviewHarvestReceipts(int plot)
        {
            _farm.PreviewHarvestFx(plot);
            StartCoroutine(HarvestSummary(12, 43, 320, 268, Art.Icon("carrot", 0), Weather.Rain, 1.35f, 2, 0.1f));
        }

        /// <summary>The one receipt for a harvest-all: what, how much, and why.
        ///
        ///     Thu hoạch 16 cây
        ///     [crop] +43 quả      [coin] +320      [star] +268 XP
        ///     [weather] Mưa ×0,95   [star] bonus ×1,35   2 đột biến
        ///
        /// It slides up from the bottom once the arcs have landed, stays 3.2 s and never blocks a
        /// tap. The third line is where the multiplier chain appears, already folded — folding is
        /// the only honest way when sixteen plots are sixteen crops with different tags.</summary>
        IEnumerator HarvestSummary(int n, int fruits, int coins, int xp, Sprite icon, Weather? weather,
                                   float tag, int mutations, float wait)
        {
            yield return new WaitForSecondsRealtime(wait);
            if (_summary != null) Destroy(_summary.gameObject);

            bool why = weather.HasValue || tag >= 1.02f || mutations > 0;
            float h = why ? 132f : 100f;
            var card = UIKit.Node("harvestSummary", _toastLayer);
            card.Anchor(UIKit.Bottom, new Vector2(0, 104), new Vector2(520, h));
            _summary = card;
            SurfaceLook.Add(card, Looks.Paper, 24f);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var title = UIKit.Label(card, "Thu hoạch " + n + " cây", 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(24, -10), new Vector2(472, 32));

            // how much: three amounts in fixed columns (left edges 24, 196, 356)
            void Amount(float x, Sprite sp, Color tint, string text, Color ink)
            {
                var ic = UIKit.Img(card, sp, tint, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, -44), new Vector2(34, 34));
                UIKit.Label(card, text, 24, ink, TextAnchor.MiddleLeft, FontStyle.Bold)
                     .rectTransform.Anchor(UIKit.TopLeft, new Vector2(x + 40, -42), new Vector2(120, 36));
            }
            Amount(24, icon, Color.white, "+" + Fmt.N(fruits) + " quả", Theme.GreenDeep);
            if (coins > 0) Amount(196, Theme.Skin.Coin, Color.white, "+" + Fmt.N(coins), Theme.AmberDeep);
            Amount(coins > 0 ? 356 : 196, Theme.Skin.StarGold, Color.white, "+" + Fmt.N(xp) + " XP", Theme.BlueDeep);

            if (why)
            {
                float x = 24f;
                void Chip(Sprite sp, Color tint, string text, Color bg)
                {
                    var chip = UIKit.Node("why", card);
                    float w = 44f + text.Length * 9.2f;
                    chip.Anchor(UIKit.TopLeft, new Vector2(x, -88), new Vector2(w, 30));
                    var im = UIKit.Img(chip, null, bg.Alpha(0.16f), "bg");
                    im.rectTransform.Stretch();
                    Chrome.Shape(im, 15f);
                    if (sp != null)
                    {
                        var ic = UIKit.Img(chip, sp, tint, "ic");
                        ic.preserveAspect = true;
                        ic.rectTransform.Anchor(UIKit.Left, new Vector2(8, 0), new Vector2(20, 20));
                    }
                    UIKit.Label(chip, text, 16, Color.Lerp(bg, Theme.Ink, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold)
                         .rectTransform.Stretch(sp != null ? 32 : 12, 0, 8, 1);
                    x += w + 8f;
                }
                if (weather.HasValue)
                {
                    var wd = WeatherSys.Def(weather.Value);
                    Chip(Art.WeatherIcon(weather.Value), Theme.Hex(wd.hex), wd.name + " ×" + Fmt.Mul(wd.sell), Theme.Hex(wd.hex));
                }
                if (tag >= 1.02f) Chip(Theme.Skin.StarGold, Color.white, "bonus ×" + Fmt.Mul(tag), Theme.Amber);
                if (mutations > 0) Chip(null, Color.white, mutations + " đột biến", Theme.Purple);
            }

            // slide up and fade in, hold, fade out
            const float rise = 36f;
            var home = card.anchoredPosition;
            for (float e = 0f; e < 0.25f; e += Time.unscaledDeltaTime)
            {
                if (card == null) yield break;
                float q = Tween.EaseOut(e / 0.25f);
                card.anchoredPosition = home - new Vector2(0, rise * (1f - q));
                group.alpha = q;
                yield return null;
            }
            if (card == null) yield break;
            card.anchoredPosition = home; group.alpha = 1f;
            yield return new WaitForSecondsRealtime(3.2f);
            if (card == null) yield break;
            Tween.Fade(group, 0f, 0.3f, () => { if (card != null) Destroy(card.gameObject); });
        }

        IEnumerator QueuedMutation(IslandView.HarvestResult res, float wait = 1.1f)
        {
            yield return new WaitForSecondsRealtime(wait);
            ShowMutation(res, modalAllowed: true);
        }

        // ============================================================
        // seed sheet
        // ============================================================
        SeedSheet _sheet;
        int _sheetPlot = -1;     // -1 while open means "Gieo nhanh": plant every empty bed

        /// <summary>Open the sheet on one empty bed, or (plot = -1) for planting them all.</summary>
        public void OpenSeedSheet(int plot)
        {
            ClosePlotPopup();
            _hud.CloseMenu();
            if (_sheet == null) { _sheet = new SeedSheet(); _sheet.Build(_popupLayer, this); }

            _sheetPlot = plot;
            string isle = IslandSys.NameOf(_farm.CurrentIsland);
            int empty = _farm.EmptyPlots().Count;
            // a big plot takes trees, a small one everything else; "gieo nhanh" follows the island
            bool big = plot >= 0 ? _farm.Plots[plot].big : IslandSys.Def(_farm.CurrentIsland).layout == IslandLayout.Giant;
            _sheet.Big = big;
            if (plot >= 0)
            {
                _sheet.Show(big ? "Chọn cây lớn" : "Chọn hạt giống",
                            (big ? "Ô đất lớn chỉ trồng cây lớn · " : "Gieo vào ô đang sáng · ") + isle + " · còn " + empty + " ô trống");
                _farm.SetSelectedPlot(plot);
                _farm.Camera.SetInsetBottom(SeedSheet.Height, _farm.ContentOfPlot(plot));
            }
            else
            {
                _sheet.Show("Gieo nhanh", "Chọn một loại hạt để gieo kín " + empty + " ô trống · " + isle);
                _farm.SetSelectedPlot(-1);
                _farm.Camera.SetInsetBottom(SeedSheet.Height);
            }
            _hud.SetActionBarVisible(false);
            // The sheet covers the bottom of the rail; a half-cut "$" disc under its edge looked
            // like a rendering fault. The rail comes back with the sheet's slide-out.
            _hud.SetRailVisible(false);
        }

        public void CloseSeedSheet()
        {
            if (_sheet == null || !_sheet.IsOpen) return;
            _sheet.Hide();
            _sheetPlot = -1;
            _farm.SetSelectedPlot(-1);
            _farm.Camera.SetInsetBottom(0f);
            _hud.SetActionBarVisible(true);
            _hud.SetRailVisible(true);
            _hud.Render();
        }

        /// <summary>A seed card was tapped.</summary>
        public void PickSeed(string seedId)
        {
            if (_sheet == null || !_sheet.IsOpen) return;
            if (_sheetPlot < 0) { PlantEveryEmpty(seedId); CloseSeedSheet(); return; }

            var picked = GameData.Get(seedId);
            if (!PlotLogic.Fits(picked, _farm.Plots[_sheetPlot]))
            {
                Toast(picked != null && picked.big ? "Cây lớn chỉ trồng ở ô đất lớn" : "Ô đất lớn chỉ trồng cây lớn");
                return;
            }
            if (!TakeOrBuySeed(seedId)) return;
            if (!_farm.Plant(_sheetPlot, seedId)) return;
            Sfx.Play(SfxId.Plant);
            MarkDirty(); _hud.Render(); GS.Save();

            // Move straight on to the nearest empty bed; slide away when the island is full.
            int next = NearestEmpty(_sheetPlot);
            if (next < 0) { CloseSeedSheet(); return; }       // the sheet sliding away says the island is full
            _sheetPlot = next;
            _sheet.Big = _farm.Plots[next].big;
            _farm.SetSelectedPlot(next);
            _farm.Camera.SetInsetBottom(SeedSheet.Height, _farm.ContentOfPlot(next));
            _sheet.SetSubtitle("Gieo vào ô đang sáng · " + IslandSys.NameOf(_farm.CurrentIsland)
                               + " · còn " + _farm.EmptyPlots().Count + " ô trống");
            _sheet.Rebuild();
        }

        /// <summary>Use a seed from the bag, or buy exactly one. Tapping a seed you do not own is
        /// a purchase — making the player detour through the shop for a 224-coin packet while a
        /// bed sits selected was the friction the sheet exists to remove.</summary>
        bool TakeOrBuySeed(string seedId)
        {
            if (GS.Local.seeds.TryGetValue(seedId, out int have) && have > 0) return true;
            var seed = GameData.Get(seedId);
            if (seed == null || seed.lv > GS.Local.lv) return false;
            if (GS.Local.coin < seed.price) { Toast("Không đủ xu mua " + seed.name); return false; }
            GS.Local.AddCoin(-seed.price);
            GS.Local.AddSeed(seedId, 1);
            return true;
        }

        /// <summary>The empty bed closest to the one just planted, by grid steps — so the
        /// selection walks along the row the player is filling instead of jumping around.</summary>
        int NearestEmpty(int from)
        {
            int best = -1, bestD = int.MaxValue;
            int fr = from / 4, fc = from % 4;
            foreach (int i in _farm.EmptyPlots())
            {
                int d = Mathf.Abs(i / 4 - fr) + Mathf.Abs(i % 4 - fc);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void PlantEveryEmpty(string seedId)
        {
            var seed = GameData.Get(seedId);
            int greenBefore = GS.Local.greenhouse;
            int n = 0;
            foreach (int i in _farm.EmptyPlots())
            {
                if (!PlotLogic.Fits(seed, _farm.Plots[i])) continue;
                if (!TakeOrBuySeed(seedId)) break;
                if (_farm.Plant(i, seedId)) { n++; if (n <= 6) Sfx.Play(SfxId.Plant, 0.8f); } else break;
            }
            if (n == 0) return;
            MarkDirty(); _hud.Render(); GS.Save();

            // the beds filling up are the receipt; only the greenhouse, which spends something
            // the player paid for, is worth a line
            int sheltered = greenBefore - GS.Local.greenhouse;
            if (sheltered > 0)
                Toast("Nhà kính che " + sheltered + " ô" + (GS.Local.greenhouse > 0 ? " · còn " + GS.Local.greenhouse + " lượt" : " · đã hết lượt"));
        }

        /// <summary>"Gieo" on the bottom bar: the seed sheet in plant-everything mode.</summary>
        public void PlantAll()
        {
            if (_farm.EmptyPlots().Count == 0) { Toast("Không còn ô trống"); return; }
            OpenSeedSheet(-1);
        }

        /// <summary>Kept for the dev tools and tests: plant every empty bed with whatever is in
        /// the bag, no sheet.</summary>
        public void PlantAllFromBag()
        {
            // Greenhouse charges are spent inside Plant, one per sheltered planting. A bulk sweep
            // in a storm can burn all six in a single tap, so the count is measured here and said
            // out loud — a consumable that vanishes without a word is the same as a bug.
            int greenBefore = GS.Local.greenhouse;

            int n = 0;
            foreach (int i in _farm.EmptyPlots())
            {
                var plot = _farm.Plots[i];
                var id = GS.Local.seeds.FirstOrDefault(k => k.Value > 0 && PlotLogic.Fits(GameData.Get(k.Key), plot)).Key;
                if (string.IsNullOrEmpty(id)) break;
                if (_farm.Plant(i, id)) n++;
            }
            if (n == 0) { Toast("Không còn hạt giống hoặc ô trống"); return; }

            MarkDirty(); _hud.Render(); GS.Save();

            int sheltered = greenBefore - GS.Local.greenhouse;
            string line = "Đã gieo " + n + " hạt giống";
            if (sheltered > 0)
                line += "  ·  nhà kính che " + sheltered
                      + (GS.Local.greenhouse > 0 ? " (còn " + GS.Local.greenhouse + ")" : " (hết)");
            Toast(line);
        }

        /// <summary>Water every plot whose window is open.
        ///
        /// This is the verb the design was missing. Every watering takes a fixed time off a crop and
        /// there are many plots, so doing it plot by plot across sixteen tiles was work the reward
        /// never justified — which meant the system was, in practice, off. One button makes the
        /// whole timed-window design worth having.</summary>
        public void WaterAll()
        {
            var list = _farm.WaterablePlots();
            if (list.Count == 0) { Toast("Chưa tới cữ tưới"); return; }

            int n = 0;
            foreach (int i in list) if (_farm.Water(i)) n++;
            if (n == 0) { Toast("Chưa tới cữ tưới"); return; }

            Sfx.Play(SfxId.Water);
            MarkDirty(); _hud.Render(); GS.Save();
            // every watered plot already floats its own "Sớm 16p"; a toast on top was the spam
        }

        /// <summary>Move one island along the row, skipping nothing: a locked island is still
        /// somewhere to go, because the tribute board painted on it is the goal the player is
        /// working toward.</summary>
        public void StepIsland(int delta)
        {
            int max = Mathf.Min(IslandSys.Max, _farm.IslandCount) - 1;
            int want = Mathf.Clamp(_farm.CurrentIsland + delta, 0, max);
            if (want == _farm.CurrentIsland) return;
            GoToIsland(want);
            _hud.Render();
        }

        /// <summary>Pull back far enough to see the whole archipelago at once.</summary>
        public void ShowArchipelago()
        {
            if (_farm == null) return;
            _farm.ShowAll();
            _farm.ApplyLod();
        }

        public void DoUpgrade()
        {
            var a = GameData.Level(GS.Local.lv);
            if (GS.Local.xp < a.xpNeed) { Toast("Chưa đủ kinh nghiệm, còn thiếu " + Fmt.N(a.xpNeed - GS.Local.xp) + " XP"); return; }
            if (GS.Local.coin < a.cost) { Toast("Không đủ xu, còn thiếu " + Fmt.N(a.cost - GS.Local.coin) + " xu"); return; }

            if (!GS.Local.LevelUp()) return;

            _farm.RenderAll(); MarkDirty(); _hud.Render(); RefreshPanel(); GS.Save();
            Tween.Shake(_root, 7f);

            var items = new List<RewardItem> { new RewardItem(Theme.Skin.Farmhouse, "Trang trại cấp " + GS.Local.lv, Theme.GreenDeep) };
            var unlocked = GameData.Seeds.FirstOrDefault(s => s.lv == GS.Local.lv);
            if (unlocked != null) items.Add(new RewardItem(Art.Icon(unlocked.art, 0), unlocked.name));
            // Levelling makes the next plot BUYABLE rather than free, so the reward card says
            // that instead of promising a plot the player still has to pay for. Only on the level
            // that actually crosses the gate: "≥ gate" announced it again on every level after.
            if (GS.Local.PlotLevelNow(0) == GS.Local.lv && IslandSys.OpenCount(GS.Local.islands[0]) < IslandSys.PlotsPerIsland)
                items.Add(new RewardItem(Art.TileEmpty, "Mở bán ô đất · " + Fmt.N(GS.Local.PlotPriceNow(0))));
            string quick = QuickActions.AnnouncedAt(GS.Local.lv);
            if (quick != null)
                items.Add(new RewardItem(Theme.Skin.StarGold, "Mở khoá " + quick, Theme.Amber));
            int isle = IslandSys.NextLocked(GS.Local);
            if (isle > 0 && IslandSys.Def(isle).lv == GS.Local.lv)
                items.Add(new RewardItem(Theme.Skin.Farmhouse, "Đủ cấp mở " + IslandSys.NameOf(isle), Theme.Blue));
            ShowReward("Nâng cấp thành công!", items, SfxId.LevelUp);
        }

        public void OpenChests(int tier)
        {
            int n = GS.Local.chests[tier];
            if (n <= 0) { Toast("Bạn chưa có rương loại này"); return; }

            var got = new Dictionary<string, int>();
            long coin = GS.Local.OpenChests(tier, () => UnityEngine.Random.value, got);
            _hud.Render(); RefreshPanel(); GS.Save();

            var items = new List<RewardItem> { new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)) };
            foreach (var kv in got.Take(4))
            {
                var s = GameData.Get(kv.Key);
                items.Add(new RewardItem(Art.Icon(s.art, 0), s.name + " ×" + kv.Value));
            }
            ShowReward("Mở " + n + " " + GameData.Chests[tier].name, items, SfxId.ChestOpen);
        }

        public void SellAll()
        {
            var list = GS.Local.StoreList();
            if (list.Count == 0) { Toast("Kho trống"); return; }

            long total = GS.Local.SellAll(out int count);

            _hud.Render(); RefreshPanel(); GS.Save();
            ShowReward("Bán sỉ thành công", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(total)),
                new RewardItem(Theme.Skin.NavStore, count + " nông sản", Theme.AmberDeep),
            }, SfxId.Coins);
        }

        public void BuySeed(string id, int qty)
        {
            var s = GameData.Get(id);
            if (s == null) return;
            if (s.lv > GS.Local.lv) { Toast("Chưa mở khoá hạt giống này"); return; }
            long cost = (long)s.price * qty;
            if (GS.Local.coin < cost) { Toast("Không đủ xu nông trại"); return; }

            GS.Local.AddCoin(-(int)cost);
            GS.Local.AddSeed(id, qty);
            _hud.Render(); RefreshPanel(); GS.Save();
            Sfx.Play(SfxId.Coins);
            Toast("Đã mua " + qty + " gói " + s.name);
        }

        /// <summary>Shared tail after any mission reward: bank it and refresh the chrome.</summary>
        /// <summary>A tribute payment changes the warehouse, the sign on the island and the coin
        /// count, and the player is looking at all three.</summary>
        public void AfterTribute()
        {
            _farm?.RenderAll();
            MarkDirty(); _hud.Render(); GS.Save();
        }

        /// <summary>An island opened. This is the largest single moment in the game's progression
        /// — sixteen new plots and a farm-wide perk — so it gets the camera, not a toast.</summary>
        public void OnIslandUnlocked(int index)
        {
            GS.Local.SyncPlots();
            _farm?.RenderAll();
            MarkDirty(); _hud.Render(); GS.Save();
            StartCoroutine(BridgeCeremony(index));
        }

        /// <summary>The unlock moment: pull back so both islands are in frame, grow the cloud
        /// bridge from the old island to the new one, then walk the camera across it. The reward
        /// card waits until the player has actually arrived.</summary>
        IEnumerator BridgeCeremony(int index)
        {
            var cam = _farm.Camera;
            Vector2 a = ArchipelagoView.IslandOrigin(Mathf.Max(0, index - 1));
            Vector2 b = ArchipelagoView.IslandOrigin(index);
            float wide = cam.ZBase * 0.59f;
            cam.FlyTo((a + b) * 0.5f, wide, 0.55f);
            yield return new WaitForSecondsRealtime(0.6f);

            _farm.SyncBridges(animateNew: true);
            SyncIslands();
            yield return new WaitForSecondsRealtime(ArchipelagoView.BridgeBuildSeconds);

            GoToIsland(index);
            yield return new WaitForSecondsRealtime(0.45f);
            Tween.Shake(_root, 9f);

            var def = IslandSys.Def(index);
            ShowReward("Đã mở " + def.name + "!", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Farmhouse, def.freePlots + " ô đất sẵn sàng", Theme.GreenDeep),
                new RewardItem(Theme.Skin.StarGold, def.perk, Theme.Amber),
            }, SfxId.IslandUnlock);
        }

        public void AfterClaim()
        {
            MarkDirty();
            _hud.Render();
            GS.Save();
        }

        public void ClaimTask(Task t, bool daily)
        {
            // read before the claim: the card must show what was paid, and the chapter numbers
            // in the table are not what a chapter pays
            int xp = MissionSys.TaskXp(GS.Local, t, daily), coin = MissionSys.TaskCoin(GS.Local, t, daily);
            if (!GS.Local.ClaimTask(t, daily)) return;
            _hud.Render(); RefreshPanel(); GS.Save();
            ShowReward("Hoàn thành nhiệm vụ", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.StarGold, "+" + Fmt.N(xp) + " XP"),
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)),
            });
        }

        public void VisitFriend(Friend f, bool suggest)
        {
            if (suggest) { Toast("Đã gửi lời mời kết bạn"); return; }
            if (GS.Local.visited.Contains(f.id)) { Toast("Hôm nay bạn đã thăm người này rồi"); return; }
            if (GS.Local.stealLeft <= 0) { Toast("Hết lượt thăm nom hôm nay"); return; }

            GS.Local.visited.Add(f.id);
            GS.Local.stealLeft--;

            var pool = GameData.Seeds.Where(s => s.lv <= Mathf.Max(1, f.lv) && !s.big).ToList();
            var pick = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : GameData.Seeds[0];
            int v = UnityEngine.Random.value < 0.25f ? UnityEngine.Random.Range(1, 4) : 0;
            int coin = MissionSys.VisitCoin(GS.Local, UnityEngine.Random.value);

            GS.Local.AddProduce(pick.id, v, 1);
            GS.Local.AddCoin(coin);
            GS.Local.AddEnergy(GS.Local.EnergyGain(4));
            GS.Local.Track("visit", 1);
            _hud.Render(); RefreshPanel(); GS.Save();

            ShowReward("Thăm nom " + f.name, new List<RewardItem>
            {
                new RewardItem(Art.Icon(pick.art, v), pick.name + (v > 0 ? " " + Art.Elem(v).shortName : "")),
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)),
            });
        }

        public void BuyShopItem(ShopItem it)
        {
            if (GS.Local.shopBought.Contains(it.id)) { Toast("Bạn đã mua vật phẩm này"); return; }
            int price = ShopSys.PriceOf(GS.Local, it);
            if (GS.Local.coin < price) { Toast("Không đủ xu nông trại"); return; }
            GS.Local.AddCoin(-price);

            var extra = new List<RewardItem>();
            switch (it.effect)
            {
                case "energy":
                {
                    // Fills the bar, whatever its size. A flat +300 was a whole chest at level 1 and
                    // a fifth of one at level 30, for a price that grows with the economy.
                    int before = GS.Local.chests.Sum();
                    GS.Local.AddEnergy((int)System.Math.Max(1L, GS.Local.EnergyGoal - GS.Local.energy));
                    int got = GS.Local.chests.Sum() - before;
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, "+" + got + " rương", Theme.Purple));
                    break;
                }
                case "xp2":
                    GS.Local.buffXpUntil = System.Math.Max(GS.Local.buffXpUntil, GS.Now) + 600_000L;
                    extra.Add(new RewardItem(Theme.Skin.StarGold, "×2 XP thu hoạch · 10 phút", Theme.Blue));
                    break;
                case "tonic":
                {
                    int n = 0;
                    for (int k = 0; k < GS.PlotCount && k < _farm.Plots.Count; k++) if (_farm.Hasten(k, 0.5f)) n++;
                    if (n == 0) { Toast("Chưa có cây nào đang lớn trên đảo này"); GS.Local.AddCoin(price); return; }
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, n + " cây chín nhanh", Theme.GreenDeep));
                    break;
                }
                case "chest":
                    GS.Local.chests[1]++;
                    extra.Add(new RewardItem(Art.Item("chest_1"), "Vào kho rương", Theme.Purple));
                    break;
                case "seedbest":
                {
                    var top = GameData.Seeds.Where(s => s.lv <= GS.Local.lv && !s.big).OrderByDescending(s => s.lv).FirstOrDefault()
                              ?? GameData.Seeds[0];
                    GS.Local.AddSeed(top.id, 3);
                    extra.Add(new RewardItem(Art.Icon(top.art, 0), "3 hạt " + top.name, Theme.GreenDeep));
                    break;
                }
                case "mutate":
                    // 10 minutes, not 5. The buff is read at PLANT, so what it is worth is the
                    // number of plantings it covers; five minutes covered barely one sweep.
                    GS.Local.buffMutateUntil = GS.Now + 600_000L;
                    extra.Add(new RewardItem(Theme.Skin.StarGold, "+30% đột biến · 10 phút"));
                    break;
                case "forecast":
                    GS.Local.forecastUntil = System.Math.Max(GS.Local.forecastUntil, GS.Now) + ShopSys.ForecastMs;
                    extra.Add(new RewardItem(Art.WeatherIcon(WeatherSys.Next(GS.Local)),
                                             "Dự báo mở trong 12 giờ", Theme.Blue));
                    break;
                case "green":
                    GS.Local.greenhouse += ShopSys.GreenhouseCharges;
                    extra.Add(new RewardItem(Theme.Skin.Farmhouse,
                                             ShopSys.GreenhouseCharges + " lần gieo được che", Theme.GreenDeep));
                    break;
                case "reroll":
                {
                    // Rerolls the LEAST valuable contract on the board. Letting the player pick
                    // would be a second panel for a decision with one obvious answer.
                    int worst = -1; int worstPay = int.MaxValue;
                    for (int k = 0; k < GS.Local.contracts.Count; k++)
                    {
                        var m = GS.Local.contracts[k];
                        if (m.Empty || m.p >= m.need) continue;
                        int pay = MissionSys.CoinReward(GS.Local, m);
                        if (pay < worstPay) { worstPay = pay; worst = k; }
                    }
                    if (worst < 0) { Toast("Không có đơn nào để đổi"); GS.Local.AddCoin(price); return; }
                    var fresh = MissionSys.Generate(GS.Local, worst, MissionSys.CycleOf(GS.Now) + 1);
                    GS.Local.contracts[worst] = fresh;
                    extra.Add(new RewardItem(Theme.Skin.NavQuest, MissionSys.Describe(fresh), Theme.Blue));
                    break;
                }
                case "seedbag":
                {
                    var pool = GameData.Seeds.Where(s => s.lv <= GS.Local.lv && !s.big).ToList();
                    for (int k = 0; k < 5; k++)
                    {
                        var p = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : GameData.Seeds[0];
                        GS.Local.AddSeed(p.id, 1);
                    }
                    extra.Add(new RewardItem(Theme.Skin.NavSeeds, "5 hạt giống", Theme.GreenDeep));
                    break;
                }
                case "instant":
                {
                    // The plot with the LONGEST wait left, on the island being looked at — the
                    // one the player would have chosen.
                    int idx = -1; float worstLeft = 0f;
                    var pls = _farm.Plots;
                    for (int k = 0; k < GS.PlotCount && k < pls.Count; k++)
                    {
                        if (string.IsNullOrEmpty(pls[k].crop) || PlotLogic.State(pls[k]) == PlotState.Ready) continue;
                        float left = PlotLogic.Remain(pls[k]);
                        if (left > worstLeft) { worstLeft = left; idx = k; }
                    }
                    if (idx >= 0) _farm.InstantGrow(idx);
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, "Chín ngay", Theme.Purple));
                    break;
                }
                case "water3":
                {
                    // The watering system changed under this item: windows open and close on a
                    // schedule now, so "tưới nhanh ×3" was three taps saved. Skipping the schedule
                    // entirely on the whole island is what the item is actually worth buying for.
                    int n = 0;
                    for (int k = 0; k < GS.PlotCount; k++) if (_farm.ForceWater(k)) n++;
                    _farm.RenderAll();
                    extra.Add(new RewardItem(Theme.Skin.Droplet, "Tưới " + n + " cây"));
                    break;
                }
                default:
                    GS.Local.shopBought.Add(it.id);
                    break;
            }

            MarkDirty();
            _hud.Render(); RefreshPanel(); GS.Save();

            var items = new List<RewardItem> { new RewardItem(Art.Crop(it.art), it.name) };
            items.AddRange(extra);
            ShowReward("Mua thành công", items, SfxId.Coins);
        }

        /// <summary>Menu ▸ Nhập code. A good code closes the panel and shows what it gave; anything
        /// else says why in a toast and leaves the field for another try.</summary>
        public void RedeemCode(string input)
        {
            switch (GiftCodes.Redeem(GS.Local, input, out var gift, out bool onlyAdded))
            {
                case GiftCodes.Result.Empty:       Toast("Hãy nhập mã quà tặng"); return;
                case GiftCodes.Result.Unknown:     Toast("Mã không đúng hoặc đã hết hạn"); return;
                case GiftCodes.Result.AlreadyUsed: Toast("Mã này đã được dùng trên nông trại của bạn"); return;
            }
            _farm.RenderAll(); MarkDirty(); _hud.Render(true); GS.Save();
            CloseAll();
            var items = new List<RewardItem>();
            if (!onlyAdded && gift.xp > 0) items.Add(new RewardItem(Theme.Skin.StarGold, "+" + Fmt.Short(gift.xp) + " XP"));
            if (!onlyAdded && gift.coin > 0) items.Add(new RewardItem(Theme.Skin.Coin, "+" + Fmt.Short(gift.coin) + " xu"));
            if (gift.eggs > 0) items.Add(new RewardItem(Art.Item("item_egg"), "+" + gift.eggs + " trứng", Theme.Purple));
            ShowReward(string.IsNullOrEmpty(gift.note) ? "Nhận quà thành công" : gift.note, items, SfxId.Legendary);
        }

        /// <summary>A Trang trí card was tapped: buy it, wear it, or take it off.</summary>
        public void TapCosmetic(Cosmetic c)
        {
            var s = GS.Local;
            if (!Cosmetics.Owns(s, c.id))
            {
                if (s.coin < c.price) { Toast("Không đủ xu nông trại"); return; }
                Cosmetics.Buy(s, c.id);
                AfterCosmetic();
                ShowReward("Mua thành công", new List<RewardItem>
                {
                    new RewardItem(Art.Item(c.art), c.name),
                    new RewardItem(Theme.Skin.Check, "Đang dùng · " + c.desc, Theme.GreenDeep),
                }, SfxId.Coins);
                return;
            }
            if (Cosmetics.IsWorn(s, c.id)) { Cosmetics.TakeOff(s, c.slot); Toast("Đã tháo " + c.name); }
            else { Cosmetics.Wear(s, c.id); Toast("Đang dùng " + c.name); }
            Sfx.Play(SfxId.Toggle);
            AfterCosmetic();
        }

        void AfterCosmetic()
        {
            _farm.RenderAll();
            _hud.Render(); RefreshPanel(); GS.Save();
        }

        public void ClaimSet(CollectionSet set)
        {
            int have = set.items.Count(it => GS.Local.collected.Contains(it.Key));
            if (have < set.items.Length) { Toast("Còn thiếu " + (set.items.Length - have) + " vật phẩm"); return; }
            if (GS.Local.claimedSets.Contains(set.id)) { Toast("Bạn đã nhận thưởng bộ này"); return; }

            GS.Local.claimedSets.Add(set.id);
            GS.Local.AddCoin(set.coin);
            GS.Local.AddXp(set.xp);
            _hud.Render(); RefreshPanel(); GS.Save();

            ShowReward("Hoàn thành " + set.name, new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(set.coin)),
                new RewardItem(Theme.Skin.StarGold, "+" + Fmt.N(set.xp) + " XP"),
            });
        }

        public void ClaimAllMilestones()
        {
            int total = GS.Local.CollectedCount;
            int coin = 0, n = 0;

            foreach (int m in GameData.CollectMilestones)
                if (total >= m && GS.Local.claimedMs.Add(m)) { coin += m * 500; n++; }

            foreach (var set in GameData.Collections)
            {
                int have = set.items.Count(it => GS.Local.collected.Contains(it.Key));
                if (have >= set.items.Length && !GS.Local.claimedSets.Contains(set.id))
                {
                    GS.Local.claimedSets.Add(set.id);
                    coin += set.coin;
                    GS.Local.AddXp(set.xp);
                    n++;
                }
            }

            if (n == 0) { Toast("Chưa có phần thưởng nào để nhận"); return; }
            GS.Local.AddCoin(coin);
            _hud.Render(); RefreshPanel(); GS.Save();

            ShowReward("Nhận thưởng", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)),
                new RewardItem(Theme.Skin.Crown, n + " mốc", Theme.AmberDeep),
            });
        }

        // ============================================================
        // reward popup + toast
        // ============================================================
        public struct RewardItem
        {
            public Sprite art; public string label; public Color tint;

            public RewardItem(Sprite art, string label)
            { this.art = art; this.label = label; this.tint = Color.white; }

            /// <summary>Silhouette icons need a tint or they vanish on the cream cell.</summary>
            public RewardItem(Sprite art, string label, Color tint)
            { this.art = art; this.label = label; this.tint = tint; }
        }

        RectTransform _reward;

        public void ShowReward(string title, List<RewardItem> items, SfxId cue = SfxId.Claim)
        {
            Sfx.Play(cue);
            CloseReward();

            var scrim = UIKit.Node("rewardScrim", _overlayLayer);
            scrim.Stretch();
            var bleed = UIKit.Node("bleed", scrim);
            bleed.Stretch();
            FullBleed.On(bleed);
            var im = bleed.gameObject.AddComponent<Image>();
            im.color = new Color(0.03f, 0.07f, 0.05f, 0.55f);
            var btn = scrim.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            var c = btn.colors; c.fadeDuration = 0f; btn.colors = c;
            btn.onClick.AddListener(CloseReward);
            _reward = scrim;

            float w = Mathf.Max(420f, 60f + items.Count * 148f);
            var card = UIKit.Node("card", scrim);
            card.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(w, 330));

            var rays = UIKit.Img(card, Theme.Glow(), new Color(1f, 0.92f, 0.6f, 0.5f), "rays");
            rays.rectTransform.Anchor(UIKit.Center, new Vector2(0, 20), new Vector2(w + 220, 460));

            var bg = SurfaceLook.Add(card, Looks.Paper, 28f).Fill;
            bg.raycastTarget = true;
            // the glow belongs behind the card's shadow, not on top of it
            rays.rectTransform.SetAsFirstSibling();

            var head = UIKit.Node("head", card);
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(3, -69);
            head.offsetMax = new Vector2(-3, -3);
            SurfaceLook.Add(head, Looks.RibbonGreen, 25f, topOnly: true);
            UIKit.LabelOutlined(head, title, 28, Color.white, TextAnchor.MiddleCenter, Looks.RibbonGreen.inkLine)
                 .rectTransform.Stretch(16, 0, 16, 4);

            var row = UIKit.Node("items", card);
            row.Anchor(UIKit.Center, new Vector2(0, 2), new Vector2(w - 40, 160));
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var cell = UIKit.Node("it", row);
                cell.Anchor(UIKit.Center, new Vector2((i - (items.Count - 1) / 2f) * 148f, 0), new Vector2(136, 156));
                SurfaceLook.Add(cell, Looks.Well, 18f);

                var art = UIKit.Node("art", cell);
                art.Anchor(UIKit.Top, new Vector2(0, -10), new Vector2(100, 100));
                UIKit.Img(art, Theme.Glow(), Theme.Amber.Alpha(0.3f), "glow").rectTransform.Stretch(-12, -12, -12, -12);
                if (it.art != null)
                {
                    var ai = UIKit.Img(art, it.art, it.tint, "im");
                    ai.preserveAspect = true;
                    ai.rectTransform.Stretch(6, 6, 6, 6);
                }

                var lab = UIKit.Label(cell, it.label, 17, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                lab.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 22), new Vector2(126, 40));
                lab.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            var ok = UIKit.Btn(card, "Tuyệt vời!", Theme.Green, Theme.GreenDark, 24, 24, CloseReward);
            ok.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 18), new Vector2(240, 58));

            Tween.PopIn(card, 0.28f, 0.7f);
            Tween.Stagger(row, 0.09f);
            Tween.Shake(_root, 3f);
        }

        public void CloseReward()
        {
            if (_reward != null) Destroy(_reward.gameObject);
            _reward = null;
        }

        // ============================================================
        // toast: one line at a time
        // ============================================================
        // Three stacked dark glass bars used to pile up whenever the player tapped quickly ("Đã gieo
        // kín đảo này", "Chưa tới cữ tưới" ×3). Now there is one light pill: the same message again
        // counts up ("×3") instead of stacking, a different one replaces the text in place.
        RectTransform _toast;
        Text _toastLabel;
        CanvasGroup _toastGroup;
        string _toastMsg;
        int _toastCount;
        float _toastUntil, _toastSoundAt;
        Coroutine _toastLife;

        const float ToastSeconds = 1.8f;

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            // held until the arrival cinematic is over; the latest one is what is shown
            if (_cinematic != null) { _heldToast = message; return; }
            bool refusal = Sfx.LooksLikeRefusal(message);
            bool same = _toast != null && _toast.gameObject.activeSelf && message == _toastMsg;
            // a refusal repeated by a quick double tap says so once
            if (refusal && (!same || Time.unscaledTime - _toastSoundAt > 0.8f)) { Sfx.Play(SfxId.Error); _toastSoundAt = Time.unscaledTime; }

            // the worn frame (Trang trí ▸ Thông báo): rebuilt when it changes
            string skin = Cosmetics.Worn(GS.Local, CosmeticSlot.Toast);
            if (_toast != null && skin != _toastSkin) { Destroy(_toast.gameObject); _toast = null; }
            if (_toast == null) BuildToast(skin);
            _toastCount = same ? _toastCount + 1 : 1;
            _toastMsg = message;
            _toastLabel.text = _toastCount > 1 ? message + "  ×" + _toastCount : message;
            _toastLabel.color = refusal ? _toastRefusalInk : _toastInk;

            float w = Mathf.Clamp(_toastLabel.preferredWidth + 56f, 200f, 820f);
            _toast.sizeDelta = new Vector2(w, 44f);
            bool wasHidden = !_toast.gameObject.activeSelf || _toastGroup.alpha < 0.99f;
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            _toastGroup.alpha = 1f;
            if (wasHidden) Tween.PopIn(_toast, 0.16f, 0.88f);
            else Tween.PopIn(_toast, 0.12f, 0.96f);           // a small nudge: the text changed

            _toastUntil = Time.unscaledTime + ToastSeconds;
            if (_toastLife == null) _toastLife = StartCoroutine(ToastLife());
        }

        string _toastSkin;
        Color _toastInk = Theme.Ink, _toastRefusalInk = Theme.Hex("#9A3A22");

        void BuildToast(string skin)
        {
            _toastSkin = skin;
            _toast = UIKit.Node("toast", _toastLayer);
            _toast.Anchor(UIKit.Bottom, new Vector2(0, 108), new Vector2(360, 44));
            var look = Looks.Paper;
            look.shadow = new Color(0f, 0f, 0f, 0.16f); look.blur = 12f; look.drop = new Vector2(0f, -3f);
            look.edgeW = 2f;
            _toastInk = Theme.Ink; _toastRefusalInk = Theme.Hex("#9A3A22");
            bool stars = false;
            switch (skin)
            {
                case "ts_wood":
                    look.top = Theme.Hex("#CF955A"); look.bottom = Theme.Hex("#A56C3B"); look.edge = Theme.Hex("#5A3719"); look.edgeW = 3f;
                    look.rim = new Color(1f, 0.9f, 0.7f, 0.6f);
                    _toastInk = Theme.Hex("#FFF4E2"); _toastRefusalInk = Theme.Hex("#FFD2B8");
                    break;
                case "ts_candy":
                    look.top = Theme.Hex("#FFDDEB"); look.bottom = Theme.Hex("#FFB5D3"); look.edge = Theme.Hex("#E2609A"); look.edgeW = 3f;
                    _toastInk = Theme.Hex("#7E1B47"); _toastRefusalInk = Theme.Hex("#B3122F");
                    break;
                case "ts_night":
                    look.top = Theme.Hex("#3C4C90"); look.bottom = Theme.Hex("#1F285A"); look.edge = Theme.Hex("#8FA3FF"); look.edgeW = 2.5f;
                    look.rim = new Color(0.7f, 0.8f, 1f, 0.5f);
                    _toastInk = Theme.Hex("#EEF2FF"); _toastRefusalInk = Theme.Hex("#FFB4A0");
                    stars = true;
                    break;
                case "ts_gold":
                    look.top = Theme.Hex("#FFF0AE"); look.bottom = Theme.Hex("#F0BE48"); look.edge = Theme.Hex("#B9831A"); look.edgeW = 3f;
                    _toastInk = Theme.Hex("#5E3A02"); _toastRefusalInk = Theme.Hex("#9A2A12");
                    stars = true;
                    break;
            }
            SurfaceLook.Add(_toast, look, SurfaceLook.Pill);
            if (stars)
            {
                var spark = Art.Load("Art/fx/mut_spark");
                foreach (var (x, y, sz) in new[] { (-6f, 14f, 18f), (1f, -12f, 12f) })
                {
                    var st = UIKit.Img(_toast, spark, skin == "ts_gold" ? Color.white : Theme.Hex("#FFF3A0"), "star");
                    st.raycastTarget = false;
                    st.rectTransform.anchorMin = st.rectTransform.anchorMax = new Vector2(x < 0 ? 0f : 1f, 0.5f);
                    st.rectTransform.sizeDelta = new Vector2(sz, sz);
                    st.rectTransform.anchoredPosition = new Vector2(x < 0 ? 14f : -12f, y);
                }
            }
            _toastLabel = UIKit.Label(_toast, "", 19, _toastInk);
            _toastLabel.rectTransform.Stretch(24, 0, 24, 1);
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
        }

        IEnumerator ToastLife()
        {
            while (true)
            {
                if (Time.unscaledTime < _toastUntil) { yield return null; continue; }
                // fade, unless a new message arrives part-way
                float a = 1f;
                while (a > 0f && Time.unscaledTime >= _toastUntil)
                {
                    a -= Time.unscaledDeltaTime / 0.22f;
                    if (_toastGroup != null) _toastGroup.alpha = Mathf.Max(0f, a);
                    yield return null;
                }
                if (Time.unscaledTime < _toastUntil) continue;
                if (_toast != null) _toast.gameObject.SetActive(false);
                _toastMsg = null; _toastCount = 0;
                _toastLife = null;
                yield break;
            }
        }
    }

    /// <summary>Fires when the canvas changes size — rotation, resize, or a new device.</summary>
    public class ResizeWatcher : MonoBehaviour
    {
        public Action onResize;
        void OnRectTransformDimensionsChange() { onResize?.Invoke(); }
    }

    /// <summary>Lets one backdrop inside a safe-area layer reach the real screen edges.
    ///
    /// The popup and overlay layers are inset to the safe area so their cards and buttons clear a
    /// notch, and that took the dark scrim behind every panel with them: on a phone with a camera
    /// cut-out the farm showed, undimmed, in a strip down each side. A scrim or a sheet's paper
    /// carries this; what sits on it stays where the safe area put it.</summary>
    public class FullBleed : MonoBehaviour
    {
        public bool left = true, right = true, bottom = true, top = true;
        RectTransform _rt, _root;
        Vector2 _baseMin, _baseMax;
        bool _based;
        static readonly Vector3[] Corners = new Vector3[4];

        public static FullBleed On(RectTransform rt, bool left = true, bool right = true, bool bottom = true, bool top = true)
        {
            var fb = rt.gameObject.AddComponent<FullBleed>();
            fb.left = left; fb.right = right; fb.bottom = bottom; fb.top = top;
            fb.Apply();
            return fb;
        }

        void LateUpdate() { Apply(); }

        void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            if (!_based) { _baseMin = _rt.offsetMin; _baseMax = _rt.offsetMax; _based = true; }
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            if (_root == null)
            {
                var c = GetComponentInParent<Canvas>();
                if (c == null) return;
                _root = c.rootCanvas.transform as RectTransform;
            }
            _root.GetWorldCorners(Corners);
            Vector2 lo = parent.InverseTransformPoint(Corners[0]);
            Vector2 hi = parent.InverseTransformPoint(Corners[2]);
            var pr = parent.rect;
            var min = new Vector2(left ? lo.x - pr.xMin : _baseMin.x, bottom ? lo.y - pr.yMin + _baseMin.y : _baseMin.y);
            var max = new Vector2(right ? hi.x - pr.xMax : _baseMax.x, top ? hi.y - pr.yMax : _baseMax.y);
            if (_rt.offsetMin != min) _rt.offsetMin = min;
            if (_rt.offsetMax != max) _rt.offsetMax = max;
        }
    }

    /// <summary>Insets a layer to the device safe area, so no control hides under a notch,
    /// a rounded corner or the gesture bar.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _applied;

        /// <summary>Editor-only stand-in for a phone's cut-outs, as shares of the screen: the
        /// Editor always reports the whole screen as safe, so without this the notch path is never
        /// exercised before a device build. Null means the real <c>Screen.safeArea</c>.</summary>
        public static Rect? Simulated;

        public static Rect SafeArea
        {
            get
            {
                if (!Simulated.HasValue) return Screen.safeArea;
                var r = Simulated.Value;
                return new Rect(r.x * Screen.width, r.y * Screen.height, r.width * Screen.width, r.height * Screen.height);
            }
        }

        void Start() { _rt = (RectTransform)transform; Apply(); }

        void Update() { if (SafeArea != _applied) Apply(); }

        void Apply()
        {
            if (_rt == null) return;
            var sa = SafeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _applied = sa;

            Vector2 min = sa.position;
            Vector2 max = sa.position + sa.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }

}
