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

        /// <summary>Sky, hills, sea and clouds. Shifted against the camera so the world reads as
        /// something the player moves through rather than islands sliding over a painted
        /// backdrop — see <see cref="Parallax"/>.</summary>
        RectTransform _bgLayer;
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

            GS.Load();
            BuildCanvas();

            // Each layer is its own nested Canvas. With everything on one canvas, a single
            // moving cloud or bobbing badge forced a rebuild of every graphic in the game
            // once per frame — that was the lag.
            BuildBackground(Layer("background", 1, false));

            _world = Layer("world", 2, true);

            _farm = gameObject.AddComponent<ArchipelagoView>();
            _farm.Build(_world);
            _farm.onPlotTapped = OpenPlot;
            _farm.onIslandTapped = i => Open(new IslandPanel(this, i));

            // Weather falls across the islands but never across a button: its own layer, above
            // the world and below the HUD, and not interactive.
            _weather = gameObject.AddComponent<WeatherFx>();
            _weather.Init(_scene, Layer("weather", 3, false), _farm);

            _hud = new Hud();
            _hud.Build(Layer("hud", 4, true, true), this);
            _farm.storeAnchorWorld = () => _hud.WarehouseWorld;

            _popupLayer = Layer("popups", 5, true, true);
            _overlayLayer = Layer("overlay", 6, true, true);
            _toastLayer = Layer("toasts", 7, false, true);

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

            if (GS.Local.stats.plant == 0 && GS.Local.stats.harvest == 0)
                StartCoroutine(FirstHint());
        }

        RectTransform _pxFar, _pxNear, _pxClouds;
        WeatherFx.Scene _scene;
        WeatherFx _weather;
        public WeatherFx WeatherView => _weather;
        Weather _shownWeather = (Weather)255;
        float _pxFarBase, _pxNearBase;

        /// <summary>Slide the distant layers against the camera, so the world reads as somewhere
        /// the player moves through rather than islands sliding over a painted picture.
        ///
        /// Only the hills and the clouds move. The sky is a flat gradient with no features, so
        /// moving it would achieve nothing, and the SEA must not move at all — its horizon is the
        /// waterline every island is drawn to sit in, and the moment that drifts the islands stop
        /// looking like they are floating in it.
        ///
        /// Both offsets are clamped to the margin each layer was built with. An unclamped
        /// parallax over a 6500-unit archipelago would drag a hill's own edge into frame, which
        /// looks far worse than no parallax at all.</summary>
        void LateUpdate()
        {
            if (_farm == null || _farm.Camera == null) return;
            float camX = _farm.Camera.Camera.x;

            if (_pxFar != null)  Shift(_pxFar, ref _pxFarBase, -camX * 0.030f, 210f);
            if (_pxNear != null) Shift(_pxNear, ref _pxNearBase, -camX * 0.055f, 130f);
            if (_pxClouds != null) _pxClouds.anchoredPosition = new Vector2(Mathf.Clamp(-camX * 0.085f, -170f, 170f), 0f);
        }

        static void Shift(RectTransform rt, ref float applied, float want, float limit)
        {
            float next = Mathf.Clamp(want, -limit, limit);
            float delta = next - applied;
            if (Mathf.Abs(delta) < 0.01f) return;
            rt.offsetMin += new Vector2(delta, 0f);
            rt.offsetMax += new Vector2(delta, 0f);
            applied = next;
        }

        /// <summary>Re-fit the world to the current canvas.
        ///
        /// The fitting formula itself now lives in <see cref="MapCamera.Recompute"/> — it became
        /// the base zoom level rather than a one-off scale assignment, because the field is no
        /// longer a thing with one fixed size but a world the player can move around in.</summary>
        void FitFarm()
        {
            if (_farm != null) _farm.Refit();
        }

        IEnumerator FirstHint()
        {
            yield return new WaitForSecondsRealtime(0.9f);
            Toast("Chạm vào ô đất để gieo hạt giống!");
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

            if (FindObjectOfType<EventSystem>() == null)
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

        /// <summary>Horizon line, measured up from the bottom edge.</summary>
        const float Horizon = 430f;

        /// <summary>Layered sky, a hazy range of hills, drifting clouds, the sea the island
        /// sits in, and a vignette to settle the eye on the farm.</summary>
        void BuildBackground(RectTransform bg)
        {
            // ---- sky ----
            _scene.clouds = new List<Image>();
            _scene.swell = new List<Image>();
            var sky = UIKit.Img(bg, Theme.Sky(), Color.white, "sky");
            _scene.sky = sky;
            sky.type = Image.Type.Sliced;
            sky.rectTransform.Stretch();

            // ---- sun ----
            var glow = UIKit.Img(bg, Theme.Glow(), new Color(1f, 0.95f, 0.72f, 0.60f), "sunGlow");
            glow.rectTransform.Anchor(UIKit.TopRight, new Vector2(-190, -70), new Vector2(520, 520));
            var sun = UIKit.Img(bg, Art.Load("Art/bg/sun"), new Color(1f, 0.97f, 0.80f, 0.95f), "sun");
            sun.preserveAspect = true;
            sun.rectTransform.Anchor(UIKit.TopRight, new Vector2(-190, -70), new Vector2(120, 120));
            _scene.sun = sun; _scene.sunGlow = glow;

            // ---- distant hills ----
            // The source art is a solid block whose top third carries the wavy crest, so the
            // body has to sit BELOW the waterline (the sea is drawn after and hides it) with
            // only the crest breaking the horizon. Tint multiplies, so a pale tint on pale
            // art stays invisible — these have to be genuinely dark to register.
            var farHills = UIKit.Img(bg, Art.Load("Art/bg/hills_large"), Theme.Hex("#7FB4D6"), "hillsFar");
            farHills.rectTransform.anchorMin = new Vector2(0, 0);
            farHills.rectTransform.anchorMax = new Vector2(1, 0);
            farHills.rectTransform.pivot = new Vector2(0.5f, 0);
            // the horizontal margin is travel for the parallax; without it a shifted hill
            // would pull its own edge into frame
            farHills.rectTransform.offsetMin = new Vector2(-240, Horizon - 200);
            farHills.rectTransform.offsetMax = new Vector2(240, Horizon + 58);

            var nearHills = UIKit.Img(bg, Art.Load("Art/bg/hills"), Theme.Hex("#5E97BE"), "hillsNear");
            nearHills.rectTransform.anchorMin = new Vector2(0, 0);
            nearHills.rectTransform.anchorMax = new Vector2(1, 0);
            nearHills.rectTransform.pivot = new Vector2(0.5f, 0);
            nearHills.rectTransform.offsetMin = new Vector2(-160, Horizon - 200);
            nearHills.rectTransform.offsetMax = new Vector2(160, Horizon + 30);
            _scene.farHills = farHills; _scene.nearHills = nearHills;

            // ---- clouds, three depths drifting at their own pace ----
            var clouds = UIKit.Node("clouds", bg);
            clouds.Stretch();
            _pxFar = farHills.rectTransform; _pxNear = nearHills.rectTransform; _pxClouds = clouds;
            var specs = new (int sprite, float y, float w, float alpha, float speed)[]
            {
                // kept below the HUD band — clouds were drifting across the player card
                (1, -168f, 240f, 0.95f, 26f), (4, -244f, 300f, 0.80f, 18f),
                (2, -196f, 190f, 0.70f, 34f), (6, -300f, 250f, 0.55f, 13f),
                (3, -150f, 170f, 0.85f, 22f), (5, -272f, 210f, 0.45f, 16f),
            };
            for (int i = 0; i < specs.Length; i++)
            {
                var c = specs[i];
                var im = UIKit.Img(clouds, Art.Load("Art/bg/cloud" + c.sprite), new Color(1, 1, 1, c.alpha), "cloud");
                im.preserveAspect = true;
                float h = c.w * 0.62f;
                im.rectTransform.Anchor(UIKit.TopLeft, new Vector2(0, c.y), new Vector2(c.w, h));
                _scene.clouds.Add(im);
                var d = im.gameObject.AddComponent<Drifter>();
                d.speed = c.speed;
                d.startX = -c.w - i * 190f;
            }

            // ---- sea ----
            var water = UIKit.Img(bg, Theme.Sea(), Color.white, "water");
            water.type = Image.Type.Sliced;
            water.rectTransform.anchorMin = new Vector2(0, 0);
            water.rectTransform.anchorMax = new Vector2(1, 0);
            water.rectTransform.pivot = new Vector2(0.5f, 0);
            water.rectTransform.offsetMin = new Vector2(-40, -60);
            water.rectTransform.offsetMax = new Vector2(40, Horizon);
            _scene.water = water;

            // ---- swell ----
            float[] bands = { 0.30f, 0.62f, 0.86f };
            for (int i = 0; i < bands.Length; i++)
            {
                var band = UIKit.Round(bg, new Color(1, 1, 1, 0.10f - i * 0.02f), 6, "swell");
                float y = Horizon * (1f - bands[i]);
                band.rectTransform.anchorMin = new Vector2(0, 0);
                band.rectTransform.anchorMax = new Vector2(1, 0);
                band.rectTransform.pivot = new Vector2(0.5f, 0);
                band.rectTransform.offsetMin = new Vector2(40 + i * 60, y);
                band.rectTransform.offsetMax = new Vector2(-40 - i * 40, y + 5 + i * 2);
            }

            BuildPetals(bg);

            // ---- vignette, last so it sits over the whole scene ----
            var vig = UIKit.Img(bg, Theme.Vignette(), Color.white, "vignette");
            vig.rectTransform.Stretch(-80, -80, -80, -80);
            _scene.vignette = vig;
        }

        /// <summary>Petals drifting across the scene. Cheap, and the farm feels alive.</summary>
        void BuildPetals(RectTransform parent)
        {
            var layer = UIKit.Node("petals", parent);
            layer.Stretch();
            _scene.petals = layer;
            var tints = new[]
            {
                new Color(1f, 0.92f, 0.96f, 0.75f), new Color(1f, 0.98f, 0.84f, 0.70f),
                new Color(0.93f, 1f, 0.90f, 0.65f),
            };
            for (int i = 0; i < 14; i++)
            {
                float size = UnityEngine.Random.Range(7f, 14f);
                var im = UIKit.Img(layer, Theme.Circle(), tints[i % tints.Length], "petal");
                im.rectTransform.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(size, size * 0.66f));
                var f = im.gameObject.AddComponent<Faller>();
                f.speed = UnityEngine.Random.Range(14f, 34f);
                f.swing = UnityEngine.Random.Range(18f, 48f);
                f.phase = UnityEngine.Random.value * 6.28f;
                f.startX = UnityEngine.Random.value;
                f.startY = UnityEngine.Random.value;
            }
        }

        // ============================================================
        // main loop
        // ============================================================
        void Update()
        {
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

                _hud.Render();
            }

            if (now >= _nextSave) { _nextSave = now + 5f; GS.Save(); }

            if (EscapePressed())
            {
                if (_panel != null) CloseAll();
                else ClosePlotPopup();
            }
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
            _hud?.HideTray();
            CloseSeedSheet();
            CloseAll();
            ClosePlotPopup();
            _panel = panel;

            _scrim = UIKit.Node("scrim", _overlayLayer);
            _scrim.Stretch();
            var scrimIm = _scrim.gameObject.AddComponent<Image>();
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

            var shadow = UIKit.Img(holder, Theme.Shadow(28, 26), new Color(0, 0, 0, 0.38f), "shadow");
            shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-22, -16, -22, -28);

            var bg = UIKit.Round(holder, Theme.Cream, 28, "card");
            bg.rectTransform.Stretch();
            bg.raycastTarget = true;                 // taps inside the card must not close it
            _card = holder;

            // header band
            var head = UIKit.Img(holder, Theme.Skin.Header, Color.white, "head");
            head.type = Image.Type.Sliced;
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(12, -78);
            head.rectTransform.offsetMax = new Vector2(-12, -8);
            var title = UIKit.LabelOutlined(head.transform, panel.Title, 28, Color.white, TextAnchor.MiddleLeft);
            title.rectTransform.Stretch(30, 0, 120, 0);

            if (!string.IsNullOrEmpty(panel.Subtitle))
            {
                title.rectTransform.Anchor(UIKit.Left, new Vector2(30, 10), new Vector2(500, 30));
                title.rectTransform.pivot = new Vector2(0, 0.5f);
                var sub = UIKit.Label(head.transform, panel.Subtitle, 17, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft);
                sub.rectTransform.Anchor(UIKit.Left, new Vector2(30, -14), new Vector2(500, 22));
                sub.rectTransform.pivot = new Vector2(0, 0.5f);
            }

            var close = UIKit.IconBtn(head.transform, null, Theme.Red, 0.5f, CloseAll);
            close.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, -2), new Vector2(52, 52));
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
                var dying = _scrim.gameObject;
                Tween.Fade(_modalGroup, 0f, 0.12f, () => { if (dying != null) Destroy(dying); });
                _scrim = null;
                _card = null;
            }
            _panel = null;
            CloseReward();
        }

        public void RefreshPanel() { _panel?.Refresh(); }

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
        }

        public void OpenPlot(int i)
        {
            if (_panel != null) return;
            var p = _farm.Plots[i];
            PlotState st = PlotLogic.State(p);

            if (st == PlotState.Ready) { ClosePlotPopup(); DoHarvest(i); return; }

            // An empty bed opens (or re-targets) the seed sheet instead of a popup.
            if (st == PlotState.Empty) { OpenSeedSheet(i); return; }
            CloseSeedSheet();

            if (_popIndex == i) { ClosePlotPopup(); return; }

            ClosePlotPopup();
            _popIndex = i;

            var pop = UIKit.Node("plotPop", _popupLayer);
            _plotPop = pop;

            float w = 340f;
            float h = 172f;
            pop.sizeDelta = new Vector2(w, h);

            var shadow = UIKit.Img(pop, Theme.Shadow(20, 20), new Color(0, 0, 0, 0.32f), "shadow");
            shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-14, -10, -14, -18);
            var bg = UIKit.Round(pop, Theme.Cream, 22, "bg");
            bg.rectTransform.Stretch();
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

        void BuildGrowingPop(RectTransform pop, int i, Plot p)
        {
            var seed = GameData.Get(p.crop);
            var el = Art.Elem(p.variant);
            // The tier was rolled at plant and the plot has been glowing since stage one, so the
            // popup confirms rather than reveals. It also promises the fruit count, which is the
            // part the glow cannot say.
            string name = seed.name + (p.variant > 0 ? "  \u2022 " + el.name : "");
            int fruits = GS.Viewing.YieldOf(seed, p.variant);
            // The subtitle is where the watering rhythm is taught. A player who never opens this
            // popup can still learn it from the pulsing ring, but the numbers only live here.
            bool open = WaterSys.WindowOpen(p, out _);
            int left = WaterSys.Remaining(p);
            string hint;
            if (open)          hint = "tưới được ngay, giảm " + WaterSys.CutPercent(p) + "%";
            else if (left > 0) hint = "lượt tưới sau " + Fmt.Time(Mathf.CeilToInt(WaterSys.NextWindowIn(p)))
                                      + " · còn " + left + " lượt";
            else               hint = "hết lượt tưới";

            string yieldNote = fruits + " quả";
            if (p.variant > 0) yieldNote = el.Grade + " · " + yieldNote;
            PopTitle(pop, name, "Còn " + Fmt.Time(PlotLogic.Remain(p)) + "  ·  " + yieldNote + "  ·  " + hint);

            var water = UIKit.Btn(pop, open ? "Tưới nước" : "Chưa tới cữ",
                                  open ? Theme.Blue : Theme.Cream3,
                                  open ? Theme.BlueDeep : Theme.Hex("#B9A98C"), 21, 20,
                                  () => DoWater(i));
            water.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-82, 18), new Vector2(150, 52));
            if (!open)
            {
                UIKit.BtnLabel(water).color = Theme.InkSoft;
                water.interactable = false;
            }

            var speed = UIKit.Btn(pop, "Chín ngay · 800", Theme.Amber, Theme.AmberDeep, 19, 20,
                                  () => SpeedUp(i));
            speed.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(82, 18), new Vector2(150, 52));
        }

        void DoWater(int i)
        {
            if (_farm.Water(i)) { _hud.Render(); GS.Save(); }
            ClosePlotPopup();
        }

        void SpeedUp(int i)
        {
            if (GS.Local.coin < 800) { Toast("Không đủ xu nông trại"); return; }
            GS.Local.AddCoin(-800);
            _farm.InstantGrow(i);
            MarkDirty(); _hud.Render(); GS.Save();
            ClosePlotPopup();
            Toast("Cây đã chín ngay lập tức!");
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
                case 2:  Toast(el.name + "! " + res.seed.name); break;
                case 3:
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
                        });
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
            int n = 0, fruits = 0, coins = 0, mutations = 0;
            var best = default(IslandView.HarvestResult);

            foreach (int i in _farm.ReadyPlots())
            {
                if (!_farm.Harvest(i, out var res)) continue;
                n++;
                fruits += res.fruits;
                coins += res.bonusCoins;
                if (res.variant > 0) mutations++;
                if (res.variant > best.variant) best = res;
            }

            if (n == 0) { Toast("Chưa có cây nào chín"); return; }
            MarkDirty(); _hud.Render(); GS.Save();
            Tween.Shake(_root, 4f);

            // what · how much · why
            string line = "Thu hoạch " + n + " cây · " + fruits + " quả";
            if (coins > 0) line += "  +" + Fmt.N(coins);
            // No emoji: legacy Text renders from the OS font and has no colour-glyph path, so
            // on Android "✨" is a tofu box in the middle of the one line that celebrates luck.
            if (mutations > 0) line += "  ·  " + mutations + " đột biến";
            Toast(line);

            if (best.variant == Art.Elements.Length - 1) StartCoroutine(QueuedMutation(best));
        }

        IEnumerator QueuedMutation(IslandView.HarvestResult res)
        {
            yield return new WaitForSecondsRealtime(1.1f);
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
            _hud.HideTray();
            if (_sheet == null) { _sheet = new SeedSheet(); _sheet.Build(_popupLayer, this); }

            _sheetPlot = plot;
            string isle = IslandSys.NameOf(_farm.CurrentIsland);
            int empty = _farm.EmptyPlots().Count;
            if (plot >= 0)
            {
                _sheet.Show("Chọn hạt giống", "Gieo vào ô đang sáng · " + isle + " · còn " + empty + " ô trống");
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
        }

        public void CloseSeedSheet()
        {
            if (_sheet == null || !_sheet.IsOpen) return;
            _sheet.Hide();
            _sheetPlot = -1;
            _farm.SetSelectedPlot(-1);
            _farm.Camera.SetInsetBottom(0f);
            _hud.SetActionBarVisible(true);
            _hud.Render();
        }

        /// <summary>A seed card was tapped.</summary>
        public void PickSeed(string seedId)
        {
            if (_sheet == null || !_sheet.IsOpen) return;
            if (_sheetPlot < 0) { PlantEveryEmpty(seedId); CloseSeedSheet(); return; }

            if (!TakeOrBuySeed(seedId)) return;
            if (!_farm.Plant(_sheetPlot, seedId)) return;
            MarkDirty(); _hud.Render(); GS.Save();

            // Move straight on to the nearest empty bed; slide away when the island is full.
            int next = NearestEmpty(_sheetPlot);
            if (next < 0) { CloseSeedSheet(); Toast("Đã gieo kín đảo này"); return; }
            _sheetPlot = next;
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
                if (!TakeOrBuySeed(seedId)) break;
                if (_farm.Plant(i, seedId)) n++; else break;
            }
            if (n == 0) return;
            MarkDirty(); _hud.Render(); GS.Save();

            int sheltered = greenBefore - GS.Local.greenhouse;
            string line = "Đã gieo " + n + " " + (seed != null ? seed.name : "hạt");
            if (sheltered > 0)
                line += "  ·  nhà kính che " + sheltered + (GS.Local.greenhouse > 0 ? " (còn " + GS.Local.greenhouse + ")" : " (hết)");
            Toast(line);
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
                var id = GS.Local.seeds.FirstOrDefault(k => k.Value > 0).Key;
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
        /// This is the verb the design was missing. Watering cuts 20% off a crop's time and the
        /// windows are short, so doing it plot by plot across sixteen tiles was work the reward
        /// never justified — which meant the system was, in practice, off. One button makes the
        /// whole timed-window design worth having.</summary>
        public void WaterAll()
        {
            var list = _farm.WaterablePlots();
            if (list.Count == 0) { Toast("Chưa tới cữ tưới"); return; }

            int n = 0;
            float saved = 0f;
            foreach (int i in list)
            {
                var p = _farm.Plots[i];
                float before = p.cut;
                if (!_farm.Water(i)) continue;
                n++; saved += p.cut - before;
            }
            if (n == 0) { Toast("Chưa tới cữ tưới"); return; }

            MarkDirty(); _hud.Render(); GS.Save();
            Toast("Đã tưới " + n + " cây · sớm hơn " + Fmt.Time(Mathf.RoundToInt(saved)));
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
            if (GS.Local.xp < a.xpNeed) { Toast("Chưa đủ kinh nghiệm — hãy thu hoạch thêm!"); return; }
            if (GS.Local.coin < a.cost) { Toast("Không đủ xu nông trại"); return; }

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
            string quick = QuickActions.UnlockedAt(GS.Local.lv);
            if (quick != null)
                items.Add(new RewardItem(Theme.Skin.StarGold, "Mở khoá " + quick, Theme.Amber));
            int isle = IslandSys.NextLocked(GS.Local);
            if (isle > 0 && IslandSys.Def(isle).lv == GS.Local.lv)
                items.Add(new RewardItem(Theme.Skin.Farmhouse, "Đủ cấp mở " + IslandSys.NameOf(isle), Theme.Blue));
            ShowReward("Nâng cấp thành công!", items);
        }

        public void OpenChests(int tier)
        {
            int n = GS.Local.chests[tier];
            if (n <= 0) { Toast("Bạn chưa có rương loại này"); return; }

            float[] mul = { 1f, 2f, 3.5f, 7f };
            float[] rareChance = { 0.10f, 0.20f, 0.25f, 1f };
            int coin = 0;
            var got = new Dictionary<string, int>();

            for (int k = 0; k < n; k++)
            {
                coin += Mathf.RoundToInt((600f + UnityEngine.Random.value * 1800f) * mul[tier]);
                bool rare = UnityEngine.Random.value < rareChance[tier];
                var pool = GameData.Seeds.Where(s => s.lv <= GS.Local.lv + (rare ? 4 : 0) && (rare ? s.r >= 2 : s.r <= 1)).ToList();
                if (pool.Count == 0) pool = GameData.Seeds.ToList();
                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                int qty = rare ? 2 : 3;
                GS.Local.AddSeed(pick.id, qty);
                got.TryGetValue(pick.id, out int had);
                got[pick.id] = had + qty;
            }

            GS.Local.chests[tier] = 0;
            GS.Local.AddCoin(coin);
            GS.Local.Track("chest", n);
            GS.Local.AddEnergy(GS.Local.EnergyGain(n * 3));
            _hud.Render(); RefreshPanel(); GS.Save();

            var items = new List<RewardItem> { new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)) };
            foreach (var kv in got.Take(4))
            {
                var s = GameData.Get(kv.Key);
                items.Add(new RewardItem(Art.Icon(s.art, 0), s.name + " ×" + kv.Value));
            }
            ShowReward("Mở " + n + " " + GameData.Chests[tier].name, items);
        }

        public void SellAll()
        {
            var list = GS.Local.StoreList();
            if (list.Count == 0) { Toast("Kho trống"); return; }

            long total = 0;
            int count = 0;
            foreach (var it in list)
            {
                total += (long)it.price * it.n;
                count += it.n;
                GS.Local.TrackCrop("sellCrop", it.crop, it.n);
            }
            GS.Local.store.Clear();
            GS.Local.AddCoin((int)Mathf.Min(total, int.MaxValue));
            GS.Local.Track("sell", count);

            _hud.Render(); RefreshPanel(); GS.Save();
            ShowReward("Bán sỉ thành công", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(total)),
                new RewardItem(Theme.Skin.NavStore, count + " nông sản", Theme.AmberDeep),
            });
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
            });
        }

        public void AfterClaim()
        {
            MarkDirty();
            _hud.Render();
            GS.Save();
        }

        public void ClaimTask(Task t, bool daily)
        {
            if (!GS.Local.ClaimTask(t, daily)) return;
            _hud.Render(); RefreshPanel(); GS.Save();
            ShowReward("Hoàn thành nhiệm vụ", new List<RewardItem>
            {
                new RewardItem(Theme.Skin.StarGold, "+" + Fmt.N(t.xp) + " XP"),
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(t.coin)),
            });
        }

        public void VisitFriend(Friend f, bool suggest)
        {
            if (suggest) { Toast("Đã gửi lời mời kết bạn"); return; }
            if (GS.Local.visited.Contains(f.id)) { Toast("Hôm nay bạn đã thăm người này rồi"); return; }
            if (GS.Local.stealLeft <= 0) { Toast("Hết lượt thăm nom hôm nay"); return; }

            GS.Local.visited.Add(f.id);
            GS.Local.stealLeft--;

            var pool = GameData.Seeds.Where(s => s.lv <= Mathf.Max(1, f.lv)).ToList();
            var pick = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : GameData.Seeds[0];
            int v = UnityEngine.Random.value < 0.25f ? UnityEngine.Random.Range(1, 4) : 0;
            int coin = 400 + UnityEngine.Random.Range(0, 900);

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
                    GS.Local.AddEnergy(300);
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, "+300 năng lượng", Theme.Purple));
                    break;
                case "mutate":
                    // 10 minutes, not 5. The buff is read at PLANT, so what it is worth is the
                    // number of plantings it covers; five minutes covered barely one sweep.
                    GS.Local.buffMutateUntil = GS.Now + 600_000L;
                    extra.Add(new RewardItem(Theme.Skin.StarGold, "+30% đột biến · 10 phút"));
                    break;
                case "forecast":
                    GS.Local.forecastUntil = System.Math.Max(GS.Local.forecastUntil, GS.Now) + ShopSys.ForecastMs;
                    extra.Add(new RewardItem(Art.WeatherIcon(WeatherSys.Next(GS.Local)),
                                             "Xem trước 12 giờ", Theme.Blue));
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
                    var pool = GameData.Seeds.Where(s => s.lv <= GS.Local.lv).ToList();
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
            ShowReward("Mua thành công", items);
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

        public void ShowReward(string title, List<RewardItem> items)
        {
            CloseReward();

            var scrim = UIKit.Node("rewardScrim", _overlayLayer);
            scrim.Stretch();
            var im = scrim.gameObject.AddComponent<Image>();
            im.color = new Color(0.03f, 0.07f, 0.05f, 0.55f);
            var btn = scrim.gameObject.AddComponent<Button>();
            btn.targetGraphic = im;
            var c = btn.colors; c.fadeDuration = 0f; btn.colors = c;
            btn.onClick.AddListener(CloseReward);
            _reward = scrim;

            float w = Mathf.Max(420f, 60f + items.Count * 148f);
            var card = UIKit.Node("card", scrim);
            card.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(w, 330));

            var shadow = UIKit.Img(card, Theme.Shadow(26, 24), new Color(0, 0, 0, 0.4f), "shadow");
            shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-20, -14, -20, -26);

            var rays = UIKit.Img(card, Theme.Glow(), new Color(1f, 0.92f, 0.6f, 0.5f), "rays");
            rays.rectTransform.Anchor(UIKit.Center, new Vector2(0, 20), new Vector2(w + 220, 460));

            var bg = UIKit.Round(card, Theme.Cream, 26, "bg");
            bg.rectTransform.Stretch();
            bg.raycastTarget = true;

            var head = UIKit.Round(card, Theme.GreenDeep, 26, "head");
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(0, -66);
            head.rectTransform.offsetMax = Vector2.zero;
            var flat = UIKit.Round(head.transform, Theme.GreenDeep, 6, "flat");
            flat.rectTransform.anchorMin = new Vector2(0, 0);
            flat.rectTransform.anchorMax = new Vector2(1, 0);
            flat.rectTransform.pivot = new Vector2(0.5f, 0);
            flat.rectTransform.sizeDelta = new Vector2(0, 26);
            UIKit.LabelOutlined(head.transform, title, 26, Color.white).rectTransform.Stretch(16, 0, 16, 0);

            var row = UIKit.Node("items", card);
            row.Anchor(UIKit.Center, new Vector2(0, 2), new Vector2(w - 40, 160));
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var cell = UIKit.Node("it", row);
                cell.Anchor(UIKit.Center, new Vector2((i - (items.Count - 1) / 2f) * 148f, 0), new Vector2(136, 156));
                UIKit.Round(cell, Theme.Cream2, 18, "bg").rectTransform.Stretch();

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

        readonly List<RectTransform> _toasts = new List<RectTransform>();

        public void Toast(string message)
        {
            while (_toasts.Count >= 3)
            {
                var old = _toasts[0];
                _toasts.RemoveAt(0);
                if (old != null) Destroy(old.gameObject);
            }

            var t = UIKit.Node("toast", _toastLayer);
            t.Anchor(UIKit.Bottom, new Vector2(0, 104 + _toasts.Count * 52), new Vector2(520, 46));
            var bg = UIKit.Round(t, new Color(0.08f, 0.16f, 0.13f, 0.92f), 23, "bg");
            bg.rectTransform.Stretch();
            var label = UIKit.Label(t, message, 20, Color.white);
            label.rectTransform.Stretch(20, 0, 20, 0);

            _toasts.Add(t);
            Tween.PopIn(t, 0.18f, 0.8f);
            StartCoroutine(KillToast(t, 2.1f));
        }

        IEnumerator KillToast(RectTransform t, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _toasts.Remove(t);
            if (t == null) yield break;
            var g = t.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(g, 0f, 0.2f, () => { if (t != null) Destroy(t.gameObject); });
        }
    }

    /// <summary>Fires when the canvas changes size — rotation, resize, or a new device.</summary>
    public class ResizeWatcher : MonoBehaviour
    {
        public Action onResize;
        void OnRectTransformDimensionsChange() { onResize?.Invoke(); }
    }

    /// <summary>Insets a layer to the device safe area, so no control hides under a notch,
    /// a rounded corner or the gesture bar.</summary>
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _applied;

        void Start() { _rt = (RectTransform)transform; Apply(); }

        void Update() { if (Screen.safeArea != _applied) Apply(); }

        void Apply()
        {
            if (_rt == null) return;
            var sa = Screen.safeArea;
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

    /// <summary>Drifts a cloud sideways, wrapping round once it leaves the screen.</summary>
    public class Drifter : MonoBehaviour
    {
        public float speed = 20f, startX;

        RectTransform _rt, _parent;
        float _x;

        void Start()
        {
            _rt = (RectTransform)transform;
            _parent = (RectTransform)transform.parent;
            _x = startX;
        }

        void Update()
        {
            if (_rt == null || _parent == null) return;
            float span = _parent.rect.width + _rt.rect.width * 2f;
            _x += speed * Time.unscaledDeltaTime;
            if (_x > span) _x -= span;
            var p = _rt.anchoredPosition;
            p.x = _x - _rt.rect.width;
            _rt.anchoredPosition = p;
        }
    }

    /// <summary>A petal: falls slowly, sways as it goes, restarts at the top.</summary>
    public class Faller : MonoBehaviour
    {
        public float speed = 20f, swing = 30f, phase, startX, startY;

        RectTransform _rt, _parent;
        float _y, _baseX;

        void Start()
        {
            _rt = (RectTransform)transform;
            _parent = (RectTransform)transform.parent;
            _baseX = startX * Mathf.Max(1f, _parent.rect.width);
            _y = startY * Mathf.Max(1f, _parent.rect.height);
        }

        void Update()
        {
            if (_rt == null || _parent == null) return;
            float h = _parent.rect.height;
            _y += speed * Time.unscaledDeltaTime;
            if (_y > h + 40f) { _y = -40f; _baseX = UnityEngine.Random.value * _parent.rect.width; }
            float t = Time.unscaledTime + phase;
            _rt.anchoredPosition = new Vector2(_baseX + Mathf.Sin(t * 0.8f) * swing, -_y);
            _rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t) * 35f);
        }
    }
}
