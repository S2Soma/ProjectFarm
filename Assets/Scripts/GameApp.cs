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
        Canvas _canvas;
        FarmView _farm;
        Hud _hud;

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

            _farm = gameObject.AddComponent<FarmView>();
            _farm.Build(_world);
            _farm.onPlotTapped = OpenPlot;

            _hud = new Hud();
            _hud.Build(Layer("hud", 3, true, true), this);
            _farm.storeAnchorWorld = () => _hud.WarehouseWorld;

            _popupLayer = Layer("popups", 4, true, true);
            _overlayLayer = Layer("overlay", 5, true, true);
            _toastLayer = Layer("toasts", 6, false, true);

            _farm.RenderAll();
            _hud.Render(true);

            var watcher = _root.gameObject.AddComponent<ResizeWatcher>();
            watcher.onResize = FitFarm;
            FitFarm();

            if (GS.stats.plant == 0 && GS.stats.harvest == 0)
                StartCoroutine(FirstHint());
        }

        /// <summary>Scales the field to whatever room is left between the HUD rails and the
        /// action bar, so the farm fills a 20:9 phone and a 4:3 tablet equally well.</summary>
        void FitFarm()
        {
            if (_farm == null || _farm.Field == null) return;

            Vector2 canvas = _root.rect.size;
            if (canvas.x <= 0f || canvas.y <= 0f) return;

            // Fit the GRID, not the island: the island is meant to bleed under the HUD bars
            // like a backdrop. Grid half-extents are 489.4 x 255.7 at the current spacing.
            const float FieldW = 979f, FieldH = 504f;
            float room = Mathf.Min((canvas.x - 210f) / FieldW, (canvas.y - 190f) / FieldH);
            _farm.Field.localScale = Vector3.one * Mathf.Clamp(room, 0.55f, 1.5f);
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
            var sky = UIKit.Img(bg, Theme.Sky(), Color.white, "sky");
            sky.type = Image.Type.Sliced;
            sky.rectTransform.Stretch();

            // ---- sun ----
            var glow = UIKit.Img(bg, Theme.Glow(), new Color(1f, 0.95f, 0.72f, 0.60f), "sunGlow");
            glow.rectTransform.Anchor(UIKit.TopRight, new Vector2(-190, -70), new Vector2(520, 520));
            var sun = UIKit.Img(bg, Art.Load("Art/bg/sun"), new Color(1f, 0.97f, 0.80f, 0.95f), "sun");
            sun.preserveAspect = true;
            sun.rectTransform.Anchor(UIKit.TopRight, new Vector2(-190, -70), new Vector2(120, 120));

            // ---- distant hills ----
            // The source art is a solid block whose top third carries the wavy crest, so the
            // body has to sit BELOW the waterline (the sea is drawn after and hides it) with
            // only the crest breaking the horizon. Tint multiplies, so a pale tint on pale
            // art stays invisible — these have to be genuinely dark to register.
            var farHills = UIKit.Img(bg, Art.Load("Art/bg/hills_large"), Theme.Hex("#7FB4D6"), "hillsFar");
            farHills.rectTransform.anchorMin = new Vector2(0, 0);
            farHills.rectTransform.anchorMax = new Vector2(1, 0);
            farHills.rectTransform.pivot = new Vector2(0.5f, 0);
            farHills.rectTransform.offsetMin = new Vector2(-70, Horizon - 200);
            farHills.rectTransform.offsetMax = new Vector2(70, Horizon + 58);

            var nearHills = UIKit.Img(bg, Art.Load("Art/bg/hills"), Theme.Hex("#5E97BE"), "hillsNear");
            nearHills.rectTransform.anchorMin = new Vector2(0, 0);
            nearHills.rectTransform.anchorMax = new Vector2(1, 0);
            nearHills.rectTransform.pivot = new Vector2(0.5f, 0);
            nearHills.rectTransform.offsetMin = new Vector2(-30, Horizon - 200);
            nearHills.rectTransform.offsetMax = new Vector2(30, Horizon + 30);

            // ---- clouds, three depths drifting at their own pace ----
            var clouds = UIKit.Node("clouds", bg);
            clouds.Stretch();
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
        }

        /// <summary>Petals drifting across the scene. Cheap, and the farm feels alive.</summary>
        void BuildPetals(RectTransform parent)
        {
            var layer = UIKit.Node("petals", parent);
            layer.Stretch();
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

            // Ready badges bob themselves (see Bobber); the loop here only has to advance
            // the countdowns, once a second. It used to re-render every ready plot and
            // allocate a fresh list every frame.
            if (now >= _nextTick)
            {
                _nextTick = now + 1f;
                if (_tickingDirty) { _ticking = _farm.TickingPlots(); _tickingDirty = false; }

                bool finished = false;
                foreach (int i in _ticking)
                {
                    _farm.RenderPlot(i);
                    if (FarmView.PlotState(GS.plots[i]) != "growing") finished = true;
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

        void OnApplicationPause(bool paused)
        {
            if (paused) GS.Save();
            else { _tickingDirty = true; _farm.RenderAll(); }
        }

        void OnApplicationQuit() { GS.Save(); }

        void MarkDirty() { _tickingDirty = true; }

        // ============================================================
        // modal frame
        // ============================================================
        public void Open(PanelBase panel)
        {
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
            var p = GS.plots[i];
            string st = FarmView.PlotState(p);

            if (st == "ready") { ClosePlotPopup(); DoHarvest(i); return; }
            if (_popIndex == i) { ClosePlotPopup(); return; }

            ClosePlotPopup();
            _popIndex = i;

            var pop = UIKit.Node("plotPop", _popupLayer);
            _plotPop = pop;

            float w = st == "empty" ? 420f : 340f;
            float h = st == "empty" ? 232f : 172f;
            pop.sizeDelta = new Vector2(w, h);

            var shadow = UIKit.Img(pop, Theme.Shadow(20, 20), new Color(0, 0, 0, 0.32f), "shadow");
            shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-14, -10, -14, -18);
            var bg = UIKit.Round(pop, Theme.Cream, 22, "bg");
            bg.rectTransform.Stretch();
            bg.raycastTarget = true;

            if (st == "locked") BuildLockedPop(pop, i);
            else if (st == "growing") BuildGrowingPop(pop, i, p);
            else BuildPlantPop(pop, i);

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

        void BuildLockedPop(RectTransform pop, int i)
        {
            PopTitle(pop, "Ô đất bị khoá", "Mở rộng trang trại để canh tác thêm");

            var a = UIKit.Btn(pop, "Nâng cấp", Theme.Blue, Theme.BlueDeep, 21, 20,
                              () => { ClosePlotPopup(); Open(new UpgradePanel(this)); });
            a.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-82, 18), new Vector2(150, 52));

            var b = UIKit.Btn(pop, "Mở · " + Fmt.N(12000), Theme.Amber, Theme.AmberDeep, 21, 20,
                              () => BuyPlot(i));
            b.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(82, 18), new Vector2(150, 52));
        }

        void BuildGrowingPop(RectTransform pop, int i, Plot p)
        {
            var seed = GameData.Get(p.crop);
            string name = seed.name + (p.variant > 0 ? "  \u2022 " + Art.Elem(p.variant).name : "");
            PopTitle(pop, name, "Còn " + Fmt.Time(FarmView.Remain(p)) +
                                "  ·  giai đoạn " + (FarmView.StageOf(p) + 1) + "/4");

            var water = UIKit.Btn(pop, p.watered ? "Đã tưới" : "Tưới nước",
                                  p.watered ? Theme.Cream3 : Theme.Blue,
                                  p.watered ? Theme.Hex("#B9A98C") : Theme.BlueDeep, 21, 20,
                                  () => DoWater(i));
            water.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-82, 18), new Vector2(150, 52));
            if (p.watered) UIKit.BtnLabel(water).color = Theme.InkSoft;

            var speed = UIKit.Btn(pop, "Chín ngay · 800", Theme.Amber, Theme.AmberDeep, 19, 20,
                                  () => SpeedUp(i));
            speed.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(82, 18), new Vector2(150, 52));
        }

        void BuildPlantPop(RectTransform pop, int i)
        {
            var owned = GS.seeds.Where(kv => kv.Value > 0).ToList();
            if (owned.Count == 0)
            {
                PopTitle(pop, "Chưa có hạt giống", "Ghé cửa hàng để mua thêm");
                var b = UIKit.Btn(pop, "Mua hạt giống", Theme.Green, Theme.GreenDark, 21, 20,
                                  () => { ClosePlotPopup(); Open(new SeedShopPanel(this)); });
                b.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 18), new Vector2(240, 52));
                return;
            }

            PopTitle(pop, "Chọn hạt giống", "Chạm để gieo trồng");

            var strip = UIKit.Node("seeds", pop);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.offsetMin = new Vector2(14, -184); strip.offsetMax = new Vector2(-14, -74);

            var scroll = strip.gameObject.AddComponent<ScrollRect>();
            strip.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.Node("content", strip);
            content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, 0.5f);
            content.anchoredPosition = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(86, 96);
            grid.spacing = new Vector2(8, 8);
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = strip;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            foreach (var kv in owned.OrderBy(k => GameData.Get(k.Key)?.lv ?? 0))
            {
                var s = GameData.Get(kv.Key);
                if (s == null) continue;
                string id = s.id;
                var cell = UIKit.Node("seed", content);
                var face = UIKit.Round(cell, Theme.Cream2, 14, "face");
                face.rectTransform.Stretch();
                face.raycastTarget = true;
                var frame = UIKit.Img(cell, Theme.Round(14), Theme.Rarity[s.r], "frame");
                frame.type = Image.Type.Sliced;
                frame.rectTransform.Stretch(-2, -2, -2, -2);
                frame.transform.SetAsFirstSibling();

                var art = UIKit.Node("art", cell);
                art.Stretch(8, 6, 8, 24);
                var im = UIKit.Img(art, Art.Icon(s.art, 0), Color.white, "im");
                im.preserveAspect = true;
                im.rectTransform.Stretch();

                var n = UIKit.Node("n", cell);
                n.Anchor(UIKit.Bottom, new Vector2(0, 6), new Vector2(72, 20));
                UIKit.Label(n, s.name + " ×" + kv.Value, 13, Theme.InkSoft, TextAnchor.MiddleCenter)
                     .rectTransform.Stretch();

                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.onClick.AddListener(() => DoPlant(i, id));
                cell.gameObject.AddComponent<PressFx>();
            }

            var shop = UIKit.Btn(pop, "Cửa hàng", Theme.Cream3, Theme.Hex("#B9A98C"), 19, 20,
                                 () => { ClosePlotPopup(); Open(new SeedShopPanel(this)); });
            shop.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(-98, 14), new Vector2(180, 48));
            UIKit.BtnLabel(shop).color = Theme.Ink;

            var all = UIKit.Btn(pop, "Gieo tất cả", Theme.Green, Theme.GreenDark, 19, 20,
                                () => { ClosePlotPopup(); PlantAll(); });
            all.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(98, 14), new Vector2(180, 48));
        }

        // ============================================================
        // actions
        // ============================================================
        void DoPlant(int i, string seedId)
        {
            if (_farm.Plant(i, seedId)) { MarkDirty(); _hud.Render(); GS.Save(); }
            ClosePlotPopup();
        }

        void DoWater(int i)
        {
            if (_farm.Water(i)) { _hud.Render(); GS.Save(); }
            ClosePlotPopup();
        }

        void SpeedUp(int i)
        {
            if (GS.coin < 800) { Toast("Không đủ xu nông trại"); return; }
            GS.AddCoin(-800);
            _farm.InstantGrow(i);
            MarkDirty(); _hud.Render(); GS.Save();
            ClosePlotPopup();
            Toast("Cây đã chín ngay lập tức!");
        }

        void BuyPlot(int i)
        {
            if (GS.coin < 12000) { Toast("Không đủ xu nông trại"); return; }
            if (GS.UnlockExtraPlot(i))
            {
                GS.AddCoin(-12000);
                _farm.RenderAll(); MarkDirty(); _hud.Render(); GS.Save();
                Toast("Đã mở khoá ô đất này!");
            }
            ClosePlotPopup();
        }

        void DoHarvest(int i)
        {
            if (!_farm.Harvest(i, out var res)) return;
            MarkDirty(); _hud.Render(); GS.Save();
            if (res.variant > 0)
            {
                Toast("Đột biến! " + res.seed.name + " " + Art.Elem(res.variant).name);
                Tween.Shake(_root, 5f);
            }
        }

        public void HarvestAll()
        {
            int n = 0;
            foreach (int i in _farm.ReadyPlots())
                if (_farm.Harvest(i, out _)) n++;

            if (n == 0) { Toast("Chưa có cây nào chín"); return; }
            MarkDirty(); _hud.Render(); GS.Save();
            Tween.Shake(_root, 4f);
            Toast("Đã thu hoạch " + n + " cây trồng");
        }

        public void PlantAll()
        {
            int n = 0;
            for (int i = 0; i < GS.PlotCount; i++)
            {
                if (FarmView.PlotState(GS.plots[i]) != "empty") continue;
                var id = GS.seeds.FirstOrDefault(k => k.Value > 0).Key;
                if (string.IsNullOrEmpty(id)) break;
                if (_farm.Plant(i, id)) n++;
            }
            if (n > 0)
            {
                MarkDirty(); _hud.Render(); GS.Save();
                Toast("Đã gieo " + n + " hạt giống");
            }
            else Toast("Không còn hạt giống hoặc ô trống");
        }

        public void DoUpgrade()
        {
            var a = GameData.Level(GS.lv);
            if (GS.xp < a.xpNeed) { Toast("Chưa đủ kinh nghiệm — hãy thu hoạch thêm!"); return; }
            if (GS.coin < a.cost) { Toast("Không đủ xu nông trại"); return; }

            int before = GS.MaxPlots;
            if (!GS.LevelUp()) return;

            _farm.RenderAll(); MarkDirty(); _hud.Render(); RefreshPanel(); GS.Save();
            Tween.Shake(_root, 7f);

            var items = new List<RewardItem> { new RewardItem(Theme.Skin.Farmhouse, "Trang trại cấp " + GS.lv, Theme.GreenDeep) };
            var unlocked = GameData.Seeds.FirstOrDefault(s => s.lv == GS.lv);
            if (unlocked != null) items.Add(new RewardItem(Art.Icon(unlocked.art, 0), unlocked.name));
            if (GS.MaxPlots > before) items.Add(new RewardItem(Art.TileEmpty, "+1 ô đất"));
            ShowReward("Nâng cấp thành công!", items);
        }

        public void OpenChests(int tier)
        {
            int n = GS.chests[tier];
            if (n <= 0) { Toast("Bạn chưa có rương loại này"); return; }

            float[] mul = { 1f, 2f, 3.5f, 7f };
            float[] rareChance = { 0.10f, 0.20f, 0.25f, 1f };
            int coin = 0;
            var got = new Dictionary<string, int>();

            for (int k = 0; k < n; k++)
            {
                coin += Mathf.RoundToInt((600f + UnityEngine.Random.value * 1800f) * mul[tier]);
                bool rare = UnityEngine.Random.value < rareChance[tier];
                var pool = GameData.Seeds.Where(s => s.lv <= GS.lv + (rare ? 4 : 0) && (rare ? s.r >= 2 : s.r <= 1)).ToList();
                if (pool.Count == 0) pool = GameData.Seeds.ToList();
                var pick = pool[UnityEngine.Random.Range(0, pool.Count)];
                int qty = rare ? 2 : 3;
                GS.AddSeed(pick.id, qty);
                got.TryGetValue(pick.id, out int had);
                got[pick.id] = had + qty;
            }

            GS.chests[tier] = 0;
            GS.AddCoin(coin);
            GS.Track("chest", n);
            GS.AddEnergy(GS.EnergyGain(n * 3));
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
            var list = GS.StoreList();
            if (list.Count == 0) { Toast("Kho trống"); return; }

            long total = 0;
            int count = 0;
            foreach (var it in list)
            {
                total += (long)it.price * it.n;
                count += it.n;
                GS.TrackCrop("sellCrop", it.crop, it.n);
            }
            GS.store.Clear();
            GS.AddCoin((int)Mathf.Min(total, int.MaxValue));
            GS.Track("sell", count);

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
            if (s.lv > GS.lv) { Toast("Chưa mở khoá hạt giống này"); return; }
            long cost = (long)s.price * qty;
            if (GS.coin < cost) { Toast("Không đủ xu nông trại"); return; }

            GS.AddCoin(-(int)cost);
            GS.AddSeed(id, qty);
            _hud.Render(); RefreshPanel(); GS.Save();
            Toast("Đã mua " + qty + " gói " + s.name);
        }

        public void ClaimTask(Task t, bool daily)
        {
            if (!GS.ClaimTask(t, daily)) return;
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
            if (GS.visited.Contains(f.id)) { Toast("Hôm nay bạn đã thăm người này rồi"); return; }
            if (GS.stealLeft <= 0) { Toast("Hết lượt thăm nom hôm nay"); return; }

            GS.visited.Add(f.id);
            GS.stealLeft--;

            var pool = GameData.Seeds.Where(s => s.lv <= Mathf.Max(1, f.lv)).ToList();
            var pick = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : GameData.Seeds[0];
            int v = UnityEngine.Random.value < 0.25f ? UnityEngine.Random.Range(1, 4) : 0;
            int coin = 400 + UnityEngine.Random.Range(0, 900);

            GS.AddProduce(pick.id, v, 1);
            GS.AddCoin(coin);
            GS.AddEnergy(GS.EnergyGain(4));
            GS.Track("visit", 1);
            _hud.Render(); RefreshPanel(); GS.Save();

            ShowReward("Thăm nom " + f.name, new List<RewardItem>
            {
                new RewardItem(Art.Icon(pick.art, v), pick.name + (v > 0 ? " " + Art.Elem(v).shortName : "")),
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(coin)),
            });
        }

        public void BuyShopItem(ShopItem it)
        {
            if (GS.shopBought.Contains(it.id)) { Toast("Bạn đã mua vật phẩm này"); return; }
            if (GS.coin < it.price) { Toast("Không đủ xu nông trại"); return; }
            GS.AddCoin(-it.price);

            var extra = new List<RewardItem>();
            switch (it.effect)
            {
                case "energy":
                    GS.AddEnergy(300);
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, "+300 năng lượng", Theme.Purple));
                    break;
                case "plot":
                    GS.UnlockExtraPlot();
                    _farm.RenderAll();
                    extra.Add(new RewardItem(Art.TileEmpty, "+1 ô đất"));
                    break;
                case "mutate":
                    GS.buffMutateUntil = GS.Now + 300000;
                    extra.Add(new RewardItem(Theme.Skin.StarGold, "+30% đột biến"));
                    break;
                case "seedbag":
                {
                    var pool = GameData.Seeds.Where(s => s.lv <= GS.lv).ToList();
                    for (int k = 0; k < 5; k++)
                    {
                        var p = pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : GameData.Seeds[0];
                        GS.AddSeed(p.id, 1);
                    }
                    extra.Add(new RewardItem(Theme.Skin.NavSeeds, "5 hạt giống", Theme.GreenDeep));
                    break;
                }
                case "instant":
                {
                    int idx = -1;
                    for (int k = 0; k < GS.PlotCount; k++)
                        if (!string.IsNullOrEmpty(GS.plots[k].crop) && FarmView.PlotState(GS.plots[k]) != "ready") { idx = k; break; }
                    if (idx >= 0) _farm.InstantGrow(idx);
                    extra.Add(new RewardItem(Theme.Skin.NavMagic, "Chín ngay", Theme.Purple));
                    break;
                }
                case "water3":
                {
                    int n = 0;
                    for (int k = 0; k < GS.PlotCount && n < 3; k++) if (_farm.Water(k)) n++;
                    extra.Add(new RewardItem(Art.Ui("ic_can"), "Tưới ×" + n));
                    break;
                }
                default:
                    GS.shopBought.Add(it.id);
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
            int have = set.items.Count(it => GS.collected.Contains(it.Key));
            if (have < set.items.Length) { Toast("Còn thiếu " + (set.items.Length - have) + " vật phẩm"); return; }
            if (GS.claimedSets.Contains(set.id)) { Toast("Bạn đã nhận thưởng bộ này"); return; }

            GS.claimedSets.Add(set.id);
            GS.AddCoin(set.coin);
            GS.AddXp(set.xp);
            _hud.Render(); RefreshPanel(); GS.Save();

            ShowReward("Hoàn thành " + set.name, new List<RewardItem>
            {
                new RewardItem(Theme.Skin.Coin, "+" + Fmt.N(set.coin)),
                new RewardItem(Theme.Skin.StarGold, "+" + Fmt.N(set.xp) + " XP"),
            });
        }

        public void ClaimAllMilestones()
        {
            int total = GS.CollectedCount;
            int coin = 0, n = 0;

            foreach (int m in GameData.CollectMilestones)
                if (total >= m && GS.claimedMs.Add(m)) { coin += m * 500; n++; }

            foreach (var set in GameData.Collections)
            {
                int have = set.items.Count(it => GS.collected.Contains(it.Key));
                if (have >= set.items.Length && !GS.claimedSets.Contains(set.id))
                {
                    GS.claimedSets.Add(set.id);
                    coin += set.coin;
                    GS.AddXp(set.xp);
                    n++;
                }
            }

            if (n == 0) { Toast("Chưa có phần thưởng nào để nhận"); return; }
            GS.AddCoin(coin);
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
