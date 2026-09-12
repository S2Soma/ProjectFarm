using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The isometric field: sixteen painted plots, the island they sit on,
    /// the scenery ring around them, and every bit of feedback a tap produces.</summary>
    public class FarmView : MonoBehaviour
    {
        // ---- isometric metrics (reference pixels) ----
        public const float TW = 168f, TH = 84f;
        const float HW = TW / 2f, HH = TH / 2f;

        /// <summary>Spacing is the whole ball game here, so the numbers are load-bearing.
        ///
        /// Each sprite carries its own stone border, a post at all four vertices, and ~25px
        /// of soil skirt hanging below the diamond. The perpendicular grass in a diagonal
        /// channel works out to 37.57*(GapX+GapY-2), and the far plot's skirt eats 22.54 of
        /// it — so below Gap 1.30 on both axes the skirt physically crosses into the bed in
        /// front. The old 1.12 was not a tight gap, it was a 13.5px interpenetration, which
        /// is why the field read as a pile rather than a grid.
        ///
        /// GapY additionally has to clear the 32.3px post pillar: 84*GapY - 42 >= 74.3, so
        /// GapY >= 1.385. GapX has no such floor (side posts bulge only 2.2px) and is set by
        /// channel width against the screen budget.</summary>
        const float GapX = 1.60f, GapY = 1.44f;
        const float StepX = HW * GapX, StepY = HH * GapY;
        const float TileArtW = TW * 390f / 380f;
        const float TileArtH = TileArtW * 320f / 390f;
        const float TileAxis = 0.475f;               // diamond axis, measured from the art's bottom edge

        public RectTransform Field { get; private set; }
        RectTransform _plotLayer, _fxLayer, _decoBack, _decoFront;
        public Action<int> onPlotTapped;

        class PlotView
        {
            public RectTransform root;
            public Image tile, crop, fruit, glow;
            public RectTransform badge, timerBox, waterMark;
            public Text timer;
            public string sCrop, sTile, sTimer;
            public int sStage = -1, sVariant = -1;
            public string sState = "";
            public bool? sWatered;
            public float sway;
        }

        readonly PlotView[] _views = new PlotView[GS.PlotCount];

        // ============================================================
        // build
        // ============================================================
        public void Build(RectTransform parent)
        {
            Field = UIKit.Node("field", parent);
            Field.Anchor(UIKit.Center, new Vector2(0, -26), new Vector2(760, 460));

            _decoBack = UIKit.Node("decoBack", Field); _decoBack.Stretch();
            _plotLayer = UIKit.Node("plots", Field);   _plotLayer.Stretch();
            _decoFront = UIKit.Node("decoFront", Field); _decoFront.Stretch();
            _fxLayer = UIKit.Node("fx", Field);        _fxLayer.Stretch();

            BuildIsland();
            BuildPlots();
            BuildScenery();
        }

        /// <summary>Grass island under the field, with a soil cliff and a water shadow.</summary>
        void BuildIsland()
        {
            var holder = UIKit.Node("island", _decoBack);
            holder.Anchor(UIKit.Center, new Vector2(0, -6), new Vector2(1, 1));

            // The island now carries a soil body, so its base sits far lower than the old
            // flat disc did — the shadow and foam belong down at that waterline, and wider
            // than the island, or they hide behind it.
            var shade = UIKit.Img(holder, Theme.Glow(), new Color(0.04f, 0.22f, 0.33f, 0.45f), "waterShade");
            shade.rectTransform.Anchor(UIKit.Center, new Vector2(0, -484), new Vector2(1300, 338));

            var foamOuter = UIKit.Img(holder, Theme.Circle(), new Color(1f, 1f, 1f, 0.14f), "foamOuter");
            foamOuter.rectTransform.Anchor(UIKit.Center, new Vector2(0, -437), new Vector2(1300, 273));
            var foam = UIKit.Img(holder, Theme.Circle(), new Color(1f, 1f, 1f, 0.28f), "foam");
            foam.rectTransform.Anchor(UIKit.Center, new Vector2(0, -442), new Vector2(1209, 219));

            // one sculpted sprite instead of four stacked ellipses; the offset puts the
            // grass surface — not the sprite's centre — on the field origin
            var island = UIKit.Img(holder, Theme.Island(), Color.white, "island");
            island.rectTransform.Anchor(UIKit.Center, new Vector2(0, -87), new Vector2(1144, 832));
        }

        void BuildPlots()
        {
            // back rows first so the ones nearer the camera draw over them
            var order = new List<int>();
            for (int i = 0; i < GS.PlotCount; i++) order.Add(i);
            order.Sort((a, b) => Depth(a).CompareTo(Depth(b)));

            foreach (int i in order)
            {
                int idx = i;
                var pos = CellPos(i);
                var root = UIKit.Node("plot" + i, _plotLayer);
                root.Anchor(UIKit.Center, pos, new Vector2(TW, TH));

                var v = new PlotView { root = root };
                v.sway = (i * 137 % 1000) / 1000f * 2.8f;

                v.tile = UIKit.Img(root, Art.TileEmpty, Color.white, "tile");
                v.tile.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));

                v.glow = UIKit.Img(root, Theme.Glow(), new Color(1, 1, 1, 0), "glow");
                v.glow.rectTransform.Anchor(UIKit.Center, new Vector2(0, 46), new Vector2(150, 150));

                // crop art stands on the tile axis and grows upward
                v.crop = UIKit.Img(root, null, Color.white, "crop");
                v.crop.preserveAspect = true;
                v.crop.rectTransform.anchorMin = v.crop.rectTransform.anchorMax = UIKit.Center;
                v.crop.rectTransform.pivot = new Vector2(0.5f, 0f);
                v.crop.rectTransform.anchoredPosition = new Vector2(0, -8);
                v.crop.enabled = false;
                var sway = v.crop.gameObject.AddComponent<Swayer>();
                sway.phase = v.sway;

                v.fruit = UIKit.Img(root, null, Color.white, "fruit");
                v.fruit.preserveAspect = true;
                v.fruit.rectTransform.Anchor(UIKit.Center, new Vector2(0, 34), new Vector2(46, 46));
                v.fruit.enabled = false;

                // ready badge — a bobbing sickle chip above the plant
                v.badge = UIKit.Node("ready", root);
                v.badge.Anchor(UIKit.Center, new Vector2(0, 28), new Vector2(40, 40));
                var bShadow = UIKit.Img(v.badge, Theme.Circle(), new Color(0, 0, 0, 0.25f), "sh");
                bShadow.rectTransform.Stretch(-2, 2, -2, -6);
                var bFace = UIKit.Img(v.badge, Theme.Circle(), Theme.Amber, "face");
                bFace.rectTransform.Stretch();
                UIKit.Img(v.badge, Theme.Ring(0.12f), new Color(1, 1, 1, 0.8f), "ring").rectTransform.Stretch(3, 3, 3, 3);
                var bIcon = UIKit.Img(v.badge, Art.Ui("ic_leaf"), Color.white, "ic");
                bIcon.preserveAspect = true;
                bIcon.rectTransform.Stretch(8, 8, 8, 8);
                var bob = v.badge.gameObject.AddComponent<Bobber>();
                bob.phase = v.sway;
                v.badge.gameObject.SetActive(false);

                // countdown chip under the plant
                v.timerBox = UIKit.Node("timer", root);
                v.timerBox.Anchor(UIKit.Center, new Vector2(0, -8), new Vector2(96, 30));
                var tb = UIKit.Round(v.timerBox, new Color(0.09f, 0.16f, 0.13f, 0.72f), 15, "bg");
                tb.rectTransform.Stretch();
                v.timer = UIKit.Label(v.timerBox, "", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                v.timer.rectTransform.Stretch();
                v.timerBox.gameObject.SetActive(false);

                // "needs water" hint
                v.waterMark = UIKit.Node("dry", root);
                v.waterMark.Anchor(UIKit.Center, new Vector2(76, -8), new Vector2(34, 34));
                var wIcon = UIKit.Img(v.waterMark, Art.Ui("ic_can"), new Color(1, 1, 1, 0.95f), "ic");
                wIcon.preserveAspect = true;
                wIcon.rectTransform.Stretch();
                v.waterMark.gameObject.SetActive(false);

                // tap target sized to the diamond's bounding box
                var hit = UIKit.Node("hit", root);
                hit.Anchor(UIKit.Center, Vector2.zero, new Vector2(184, 134));
                var hitIm = hit.gameObject.AddComponent<Image>();
                hitIm.color = new Color(0, 0, 0, 0);
                var btn = hit.gameObject.AddComponent<Button>();
                btn.targetGraphic = hitIm;
                var cols = btn.colors; cols.fadeDuration = 0f; btn.colors = cols;
                btn.onClick.AddListener(() => onPlotTapped?.Invoke(idx));
                // the Button consumes the pointer event, so the press handler has to live
                // on the same object and drive the plot root itself
                var press = hit.gameObject.AddComponent<PlotPress>();
                press.target = root;
                // rect hit areas are 172x141 but the step is only 134x60, so they overlap
                // badly and a tap lands on whichever rect happens to win
                hit.gameObject.AddComponent<DiamondHit>();

                _views[i] = v;
            }
        }

        /// <summary>Scenery, placed as a composition rather than by formula.
        ///
        /// The old version walked a parametric ellipse and dropped nine props at hand-picked
        /// angles — which is exactly why it read as randomly generated. The scene's grammar
        /// is the isometric grid, not a circle, so everything now sits on two lines parallel
        /// to the grid's own axes: a back fence line at y = 317.8 - 0.5225|x| and a front
        /// accent line at y = -346.0 + 0.5225|x|.
        ///
        /// Composition rules this encodes:
        ///   - closed back, open front: a continuous fence wall behind, a bare apron in front
        ///   - mass behind the horizon, only low/thin props in front, so no plot is occluded
        ///   - each fence run steps 72px with ~30px overlap and TERMINATES on a lantern;
        ///     a repeating unit ending on a different unit is what reads as authored
        ///   - one focal point only: the banner at the front-left gate, nearest to camera
        ///   - two deliberate voids: the back-centre gap (island wears no crown) and the
        ///     front apron (the entrance, and where fingers reach the nearest beds)</summary>
        void BuildScenery()
        {
            // name, x, y (ground-contact anchor, island centre origin), drawn width
            var props = new (string art, float x, float y, float w)[]
            {
                ("fence_lamp", -465, 75, 134),
                ("fence",      -393, 112, 102),
                ("fence",      -321, 150, 102),
                ("fence",      -249, 188, 102),
                ("fence",      -177, 225, 102),
                ("fence_lamp",  465, 75, 134),
                ("fence",       393, 112, 102),
                ("fence",       321, 150, 102),
                ("fence",       249, 188, 102),
                ("fence",       177, 225, 102),
                ("haystack",    406, 66, 144),   // the one large mass, stored yield
                ("rock",        313, 110, 112),  // clusters into the haystack's base
                ("signpost",   -359, -158, 96),  // outer gatepost
                ("banner",     -173, -256, 72),  // focal point: nearest, tallest, only cloth
                ("rock",        213, -235, 110), // counterweight, keeps the apron balanced
            };

            // farther back draws first within each layer
            System.Array.Sort(props, (p, q) => q.y.CompareTo(p.y));

            foreach (var p in props)
            {
                var sp = Art.Farm(p.art);
                if (sp == null) continue;
                float h = p.w * sp.rect.height / sp.rect.width;

                // anything anchored below the horizon stands in front of the plots
                var layer = p.y < 0f ? _decoFront : _decoBack;
                var im = UIKit.Img(layer, sp, Color.white, "prop_" + p.art);
                im.preserveAspect = true;

                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = UIKit.Center;
                rt.pivot = new Vector2(0.5f, 0f);            // ground contact, not centre
                rt.anchoredPosition = new Vector2(p.x, p.y);
                rt.sizeDelta = new Vector2(p.w, h);
            }
        }

        // ============================================================
        // geometry
        // ============================================================
        public static Vector2 CellPos(int i)
        {
            int r = i / 4, c = i % 4;
            return new Vector2((c - r) * StepX, (3 - (c + r)) * StepY);
        }
        static int Depth(int i) { return (i / 4) + (i % 4); }

        // ============================================================
        // plot logic
        // ============================================================
        public static string PlotState(Plot p)
        {
            if (p == null || p.locked) return "locked";
            if (string.IsNullOrEmpty(p.crop)) return "empty";
            return Elapsed(p) >= p.dur ? "ready" : "growing";
        }

        public static float Elapsed(Plot p) { return (float)((GS.Now - p.plantedAt) / 1000.0) + p.bonus; }
        public static int Remain(Plot p)    { return Mathf.Max(0, Mathf.CeilToInt(p.dur - Elapsed(p))); }

        public static int StageOf(Plot p)
        {
            float t = Elapsed(p) / Mathf.Max(0.001f, p.dur);
            if (t >= 1f)    return 3;
            if (t >= 0.62f) return 2;
            if (t >= 0.28f) return 1;
            return 0;
        }

        // ============================================================
        // rendering
        // ============================================================
        public void RenderAll()
        {
            for (int i = 0; i < GS.PlotCount; i++) RenderPlot(i);
        }

        public void RenderPlot(int i)
        {
            var v = _views[i];
            if (v == null) return;
            var p = i < GS.plots.Count ? GS.plots[i] : null;
            string st = PlotState(p);

            string tileKey = st == "locked" ? "locked"
                           : st == "ready" ? "ready"
                           : (p != null && p.watered) ? "watered" : "empty";

            if (v.sState != st || v.sTile != tileKey)
            {
                v.tile.sprite = Art.Tile(tileKey);
                // A locked plot should read as "not yours yet", not as dead ground. The
                // sprite is already dark stone and tint only multiplies, so fading it lets
                // the grass show through instead of piling grey on grey.
                v.tile.color = st == "locked" ? new Color(1f, 0.98f, 0.95f, 0.72f) : Color.white;
                v.sState = st; v.sTile = tileKey;
            }

            if (st == "locked" || st == "empty")
            {
                if (v.sCrop != null || v.sStage != -1)
                {
                    v.crop.enabled = false; v.crop.sprite = null;
                    v.fruit.enabled = false; v.fruit.sprite = null;
                    v.glow.color = new Color(1, 1, 1, 0);
                    v.badge.gameObject.SetActive(false);
                    v.timerBox.gameObject.SetActive(false);
                    v.waterMark.gameObject.SetActive(false);
                    v.sCrop = null; v.sStage = -1; v.sVariant = -1; v.sTimer = null; v.sWatered = null;
                }
                return;
            }

            var seed = GameData.Get(p.crop);
            if (seed == null) return;
            int stage = StageOf(p);
            int variant = stage == 3 ? p.variant : 0;

            if (v.sCrop != p.crop || v.sStage != stage || v.sVariant != variant)
            {
                var sp = Art.Plant(seed.art, stage, variant);
                v.crop.enabled = sp != null;
                v.crop.sprite = sp;
                v.crop.color = Art.VariantTint(seed.art, variant);
                if (sp != null)
                {
                    float h = Art.StageHeight[stage];
                    float w = h * sp.rect.width / sp.rect.height;
                    v.crop.rectTransform.sizeDelta = new Vector2(w, h);
                }

                var fr = stage == 3 ? Art.Fruit(seed.art) : null;
                v.fruit.enabled = fr != null;
                v.fruit.sprite = fr;
                if (fr != null)
                {
                    v.fruit.color = Art.VariantTint(seed.art, variant);
                    v.fruit.rectTransform.anchoredPosition = new Vector2(0, Art.StageHeight[stage] * 0.52f);
                }

                if (v.sStage != -1 && v.sStage != stage) Tween.PopIn(v.crop.transform, 0.3f, 0.7f);

                var el = Art.Elem(variant);
                v.glow.color = el.hasGlow ? new Color(el.glow.r, el.glow.g, el.glow.b, 0.55f)
                                          : new Color(1, 1, 1, 0);

                v.sCrop = p.crop; v.sStage = stage; v.sVariant = variant;
            }

            if (st == "ready")
            {
                if (!v.badge.gameObject.activeSelf)
                {
                    v.badge.gameObject.SetActive(true);
                    Tween.PopIn(v.badge, 0.3f, 0.4f);
                    v.timerBox.gameObject.SetActive(false);
                    v.waterMark.gameObject.SetActive(false);
                    v.sTimer = null;
                }
            }
            else
            {
                if (v.badge.gameObject.activeSelf) v.badge.gameObject.SetActive(false);
                if (!v.timerBox.gameObject.activeSelf) v.timerBox.gameObject.SetActive(true);

                string txt = Fmt.Time(Remain(p));
                if (txt != v.sTimer) { v.timer.text = txt; v.sTimer = txt; }

                bool wet = p.watered;
                if (v.sWatered != wet)
                {
                    v.waterMark.gameObject.SetActive(!wet);
                    v.sWatered = wet;
                }
            }
        }

        public List<int> TickingPlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < GS.plots.Count; i++)
            {
                var p = GS.plots[i];
                if (p != null && !string.IsNullOrEmpty(p.crop) && PlotState(p) == "growing") outp.Add(i);
            }
            return outp;
        }

        public List<int> ReadyPlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < GS.plots.Count; i++)
                if (PlotState(GS.plots[i]) == "ready") outp.Add(i);
            return outp;
        }

        // ============================================================
        // actions
        // ============================================================
        public bool Plant(int i, string seedId)
        {
            var p = GS.plots[i];
            if (p == null || p.locked || !string.IsNullOrEmpty(p.crop)) return false;
            var seed = GameData.Get(seedId);
            if (seed == null || !GS.TakeSeed(seedId, 1)) return false;

            p.crop = seedId;
            p.plantedAt = GS.Now;
            p.dur = GS.GrowTime(seed);
            p.bonus = 0;
            p.watered = false;
            p.variant = GS.RollVariant();

            GS.TrackCrop("plant", seedId, 1);
            GS.AddEnergy(GS.EnergyGain(1));
            RenderPlot(i);
            Puff(i);
            return true;
        }

        public bool Water(int i)
        {
            var p = GS.plots[i];
            if (p == null || string.IsNullOrEmpty(p.crop) || p.watered || PlotState(p) == "ready") return false;
            p.watered = true;
            p.bonus += p.dur * 0.25f;
            GS.Track("water", 1);
            GS.AddEnergy(GS.EnergyGain(2));
            RenderPlot(i);
            Droplets(i);
            Burst(i, "-25%", Theme.Hex("#8FD8FF"));
            return true;
        }

        public struct HarvestResult { public Seed seed; public int variant; public int xp; }

        public bool Harvest(int i, out HarvestResult res)
        {
            res = default;
            var p = GS.plots[i];
            if (p == null || PlotState(p) != "ready") return false;
            var seed = GameData.Get(p.crop);
            if (seed == null) return false;
            int v = p.variant;

            int xp = GS.XpFor(seed, v);
            GS.AddProduce(p.crop, v, 1);
            GS.AddXp(xp);
            GS.AddEnergy(GS.EnergyFor(seed, v));
            GS.TrackCrop("harvest", p.crop, 1);
            if (v > 0) GS.Track("mutate", 1);

            var el = Art.Elem(v);
            FlyToStore(i, Art.Icon(seed.art, v), Art.VariantTint(seed.art, v));
            Burst(i, "+" + xp + " XP", el.hasGlow ? el.glow : Theme.Hex("#FFE9A8"));
            Sparkle(i, v > 0 ? 18 : 9, el.hasGlow ? el.glow : Theme.Hex("#FFE9A8"));

            p.crop = null; p.plantedAt = 0; p.variant = 0; p.watered = false; p.bonus = 0;
            RenderPlot(i);

            res = new HarvestResult { seed = seed, variant = v, xp = xp };
            return true;
        }

        public bool InstantGrow(int i)
        {
            var p = GS.plots[i];
            if (p == null || string.IsNullOrEmpty(p.crop)) return false;
            p.bonus = p.dur;
            RenderPlot(i);
            Sparkle(i, 12, Theme.Hex("#FFD45E"));
            return true;
        }

        /// <summary>Screen position of a plot, for flying rewards toward the HUD.</summary>
        public Vector2 WorldOfPlot(int i)
        {
            var v = _views[i];
            return v != null ? (Vector2)v.root.position : (Vector2)Field.position;
        }

        // ============================================================
        // effects
        // ============================================================
        public void Burst(int i, string text, Color color)
        {
            var node = UIKit.Node("burst", _fxLayer);
            node.Anchor(UIKit.Center, CellPos(i) + new Vector2(0, 60), new Vector2(200, 40));
            var t = UIKit.LabelOutlined(node, text, 28, color);
            t.rectTransform.Stretch();
            Tween.FloatText(node, 66f, 1.05f);
        }

        public void Sparkle(int i, int n, Color color)
        {
            var at = CellPos(i) + new Vector2(0, 40);
            for (int k = 0; k < n; k++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float r = 30f + UnityEngine.Random.value * 60f;
                var node = UIKit.Node("spark", _fxLayer);
                node.Anchor(UIKit.Center, at, new Vector2(12, 12));
                var im = UIKit.Img(node, Theme.Glow(), color, "s");
                im.rectTransform.Stretch(-4, -4, -4, -4);
                Tween.Spark(node, new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f + 40f), 0.85f);
            }
        }

        public void Puff(int i)
        {
            var at = CellPos(i) + new Vector2(0, 8);
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7f * Mathf.PI * 2f;
                var node = UIKit.Node("puff", _fxLayer);
                node.Anchor(UIKit.Center, at, new Vector2(26, 26));
                UIKit.Img(node, Theme.Circle(), new Color(0.86f, 0.78f, 0.62f, 0.85f), "p")
                     .rectTransform.Stretch();
                Tween.Spark(node, new Vector2(Mathf.Cos(a) * 34f, Mathf.Sin(a) * 18f), 0.6f);
            }
        }

        public void Droplets(int i)
        {
            var at = CellPos(i) + new Vector2(0, 96);
            for (int k = 0; k < 9; k++)
            {
                var node = UIKit.Node("drop", _fxLayer);
                node.Anchor(UIKit.Center, at + new Vector2(UnityEngine.Random.Range(-34f, 34f), 0), new Vector2(9, 14));
                UIKit.Img(node, Theme.Circle(), Theme.Hex("#7FD4FF"), "d").rectTransform.Stretch();
                Tween.Spark(node, new Vector2(0, -84f), 0.55f);
            }
        }

        /// <summary>Harvested produce arcs toward the warehouse button.</summary>
        public Func<Vector3> storeAnchorWorld;

        void FlyToStore(int i, Sprite icon, Color tint)
        {
            if (icon == null) return;
            var node = UIKit.Node("fly", _fxLayer);
            var from = CellPos(i) + new Vector2(0, 50);
            node.Anchor(UIKit.Center, from, new Vector2(58, 58));
            var im = UIKit.Img(node, icon, tint, "ic");
            im.preserveAspect = true;
            im.rectTransform.Stretch();

            Vector2 to = from + new Vector2(0, 130f);
            if (storeAnchorWorld != null)
                to = (Vector2)_fxLayer.InverseTransformPoint(storeAnchorWorld());

            Tween.FlyArc(node, from, to, 0.62f, 90f);
        }
    }

    /// <summary>Restricts a plot's raycast to its own diamond (plus its soil skirt), so the
    /// overlapping rectangles stop stealing each other's taps. Nearer plots already sit later
    /// in the hierarchy, so the topmost hit is the nearest one — which is what the eye picks.</summary>
    public class DiamondHit : MonoBehaviour, ICanvasRaycastFilter
    {
        public float halfW = 90f, halfH = 45f, skirt = 25f;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera cam)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, cam, out var p))
                return false;
            // the band directly under the bed is this plot's own skirt; nothing else claims it
            float y = (p.y < 0f && p.y > -skirt) ? 0f : (p.y < 0f ? p.y + skirt : p.y);
            return Mathf.Abs(p.x) / halfW + Mathf.Abs(y) / halfH <= 1f;
        }
    }

    /// <summary>Leans a plant back and forth, as if in a breeze. Pivoted at the base, so it
    /// bends from the soil rather than sliding.</summary>
    public class Swayer : MonoBehaviour
    {
        public float amplitude = 2.4f, speed = 1.5f, phase;

        RectTransform _rt;
        Image _img;

        void Awake() { _rt = (RectTransform)transform; _img = GetComponent<Image>(); }

        void Update()
        {
            if (_img == null || !_img.enabled) return;       // empty plot: nothing to move
            _rt.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
        }
    }

    /// <summary>Squashes the whole plot on touch, so a tap feels like it landed.</summary>
    public class PlotPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform target;

        Coroutine _co;

        public void OnPointerDown(PointerEventData e) { Go(0.93f, 0.07f); }
        public void OnPointerUp(PointerEventData e)   { Go(1f, 0.14f); }

        void Go(float to, float time)
        {
            if (target == null || !gameObject.activeInHierarchy) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(to, time));
        }

        System.Collections.IEnumerator Run(float to, float time)
        {
            Vector3 from = target.localScale;
            Vector3 dst = Vector3.one * to;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.Lerp(from, dst, t / time);
                yield return null;
            }
            if (target != null) target.localScale = dst;
            _co = null;
        }
    }

    /// <summary>Bobs a node up and down. Lives on the handful of "ready" badges so the plot
    /// itself never has to be re-rendered just to animate them.</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 5f, speed = 3f, phase;

        RectTransform _rt;
        Vector2 _home;

        void OnEnable()
        {
            if (_rt == null)
            {
                _rt = (RectTransform)transform;
                _home = _rt.anchoredPosition;
            }
        }

        void Update()
        {
            _rt.anchoredPosition = _home + new Vector2(0f, Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude);
        }
    }
}
