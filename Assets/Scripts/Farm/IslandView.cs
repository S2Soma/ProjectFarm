using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>ONE island: sixteen painted plots, the land they sit on, the scenery around
    /// them, and every bit of feedback a tap produces.
    ///
    /// Every spacing constant below is tuned for a single field at a single scale, which is
    /// exactly why this is one island and not the world — <see cref="ArchipelagoView"/> owns the
    /// list and the camera, and none of the numbers here have to know that more than one exists.</summary>
    public class IslandView : MonoBehaviour
    {
        /// <summary>Which of the owner's islands this view draws.</summary>
        public int islandIndex;

        // ---- isometric metrics (reference pixels) ----
        public const float TW = 168f, TH = 84f;
        const float HW = TW / 2f, HH = TH / 2f;

        /// <summary>The field is one tilled patch: beds touch, and the rows run straight.
        ///
        /// It used to be a lattice of separate squares with grass paths between them, because
        /// the painted tiles carried a fence post at every vertex and a rail round the edge —
        /// pushed together they doubled posts at every shared corner and cut into each other.
        /// The spread-out grid that forced read as a scatter, not a farm. The beds from
        /// Tools/gen_beds.py are drawn to touch instead: the gap between two beds is now part
        /// of the art (a dark furrow), identical everywhere, so every row is one straight line.
        ///
        /// Everything inside a plot is still authored in the old 168x84 space (tile, crop,
        /// badge, hit area) and scaled by PlotScale. 1.42 is chosen so the whole 4x4 field keeps
        /// the footprint the island, the scenery and the camera fit were composed around
        /// (half-extents 477x239 against the old 489x256) — so the beds grew instead of the
        /// field shrinking, which is also a bigger tap target.</summary>
        public const float PlotScale = 1.42f;
        const float StepX = HW * PlotScale, StepY = HH * PlotScale;
        const float TileArtW = TW * 390f / 380f;
        const float TileArtH = TileArtW * 320f / 390f;
        const float TileAxis = 0.475f;               // diamond axis, measured from the art's bottom edge

        public RectTransform Field { get; private set; }
        RectTransform _plotLayer, _labelLayer, _fxLayer, _decoBack, _decoFront;
        GraphicRaycaster _plotRaycaster;
        bool _lodFarm = true;
        public Action<int> onPlotTapped;
        /// <summary>Tapping a locked island opens its tribute board.</summary>
        public Action<int> onIslandTapped;

        /// <summary>Whose farm this view draws, who is acting on it, and what that is allowed
        /// to do. Held on the view rather than passed to every call because a view IS a view of
        /// exactly one farm — but the owner/actor split still happens inside each action, which
        /// is the part that matters once a friend can water your crops.</summary>
        public FarmContext Ctx { get; set; } = FarmContext.Own;

        /// <summary>The plots this view draws — always the context owner's, never the actor's,
        /// and always this view's own island.</summary>
        public List<Plot> Plots => Ctx.owner.islands[islandIndex].plots;

        class PlotView
        {
            public RectTransform root;
            public Image tile, crop, fruit, glow;
            /// <summary>The state light on the bed's rim — gold when ripe, blue when a watering
            /// window is open — plus glints over a ripe bed. Both lie UNDER the crop: the plot's
            /// state is painted on the ground, never pinned in front of the plant.</summary>
            public Image rim, sparkle;
            public RectTransform timerBox;
            public Text timer;
            public string sCrop, sTile, sTimer, sRim = "";
            public int sStage = -1, sVariant = -1;
            public PlotState sState = (PlotState)255;
            public float sway;
        }

        readonly PlotView[] _views = new PlotView[GS.PlotCount];

        // ============================================================
        // build
        // ============================================================
        /// <summary>The camera every plot forwards its drags to, so a drag that begins on a
        /// plot pans the world instead of being swallowed by the plot.</summary>
        public MapCamera map;

        public void Build(RectTransform parent)
        {
            Field = UIKit.Node("field", parent);
            Field.Anchor(UIKit.Center, new Vector2(0, -26), new Vector2(760, 460));

            _decoBack = UIKit.Node("decoBack", Field); _decoBack.Stretch();
            _plotLayer = UIKit.Node("plots", Field);   _plotLayer.Stretch();
            // Its own canvas + raycaster, so zooming out can disable plot hit-testing for the
            // whole island in one assignment instead of touching sixteen colliders.
            var plotCanvas = _plotLayer.gameObject.AddComponent<Canvas>();
            plotCanvas.overrideSorting = false;
            _plotRaycaster = _plotLayer.gameObject.AddComponent<GraphicRaycaster>();
            _decoFront = UIKit.Node("decoFront", Field); _decoFront.Stretch();
            // The selection outline lives ABOVE every plot, so the bed being planted stays visible
            // over the crops of the row in front.
            // With beds touching, the crop two rows in front stands up far enough to cover the
            // countdown of the plot behind it — the one number the player is reading.
            _labelLayer = UIKit.Node("labels", Field); _labelLayer.Stretch();
            _fxLayer = UIKit.Node("fx", Field);        _fxLayer.Stretch();

            BuildIsland();
            BuildPlots();
            BuildScenery();
            BuildLockSign();
            RenderLock();
        }

        // ============================================================
        // locked islands
        // ============================================================
        RectTransform _lockSign, _lockVeil;
        Text _lockTitle, _lockLine;
        readonly List<(Image bar, Text label)> _lockRows = new List<(Image, Text)>();
        /// <summary>Tri-state on purpose. A plain bool seeded to true matched the real state of
        /// every locked island on the first render, so the "did it change?" guard skipped the
        /// first paint entirely — the veil never went on and, worse, the plot layer never went
        /// OFF, leaving sixteen tappable plots on an island the player does not own.</summary>
        int _sLocked = -1;
        string _sLockText = "";

        public bool Locked => islandIndex > 0
                           && (islandIndex >= Ctx.owner.islands.Count || !Ctx.owner.islands[islandIndex].unlocked);

        /// <summary>The tribute board, painted on the locked island itself.
        ///
        /// This is the single best thing the shared canvas buys. A locked island in a menu is a
        /// row with a price on it; a locked island the player can pan to, with "Nho Tím 412/660"
        /// standing on it, is a PLACE they are working toward — and the number updates while they
        /// farm somewhere else. It turns the map into the goal screen, which is worth more than
        /// any menu the game could have instead.</summary>
        void BuildLockSign()
        {
            if (islandIndex == 0) return;

            // Desaturating veil. Deliberately a cool wash rather than greyscale: a grey island
            // reads as broken art, a blue-grey one reads as distance.
            _lockVeil = UIKit.Node("veil", _decoFront);
            _lockVeil.Stretch();
            var veil = UIKit.Img(_lockVeil, IslandSprite(islandIndex), new Color(0.32f, 0.45f, 0.58f, 0.55f), "veil");
            veil.rectTransform.Anchor(UIKit.Center, new Vector2(0, -93), new Vector2(1144, 832));
            veil.raycastTarget = false;

            var def = IslandSys.Def(islandIndex);
            int rows = def.tribute.Length;

            _lockSign = UIKit.Node("lockSign", _decoFront);
            _lockSign.Anchor(UIKit.Center, new Vector2(0, 34), new Vector2(430, 88f + rows * 40f));
            UIKit.Round(_lockSign, new Color(0.07f, 0.16f, 0.13f, 0.88f), 22, "bg").rectTransform.Stretch();

            // TapOrDrag, not Button: the sign sits on a map the player pans with the same finger,
            // and a Button fires on release however far that finger travelled in between.
            var hit = _lockSign.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0.001f);
            hit.raycastTarget = true;
            var tap = _lockSign.gameObject.AddComponent<TapOrDrag>();
            tap.map = map;
            tap.onTap = () => onIslandTapped?.Invoke(islandIndex);

            _lockTitle = UIKit.LabelOutlined(_lockSign, def.name, 26, Color.white);
            _lockTitle.rectTransform.Anchor(UIKit.Top, new Vector2(0, -12), new Vector2(410, 34));

            _lockLine = UIKit.Label(_lockSign, "", 16, Theme.Cream3, TextAnchor.MiddleCenter);
            _lockLine.rectTransform.Anchor(UIKit.Top, new Vector2(0, -46), new Vector2(410, 24));

            for (int i = 0; i < rows; i++)
            {
                var row = UIKit.Node("t" + i, _lockSign);
                row.Anchor(UIKit.Top, new Vector2(0, -74 - i * 40), new Vector2(394, 38));

                var seed = GameData.Get(def.tribute[i].crop);
                var ic = UIKit.Img(row, Art.Icon(seed != null ? seed.art : null, 0), Color.white, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, new Vector2(18, 0), new Vector2(30, 30));

                // green = progress toward something you get, same as every other goal bar
                // Anchor(Left) takes the LEFT EDGE: the icon spans 18..48, so text at 42 ran
                // into it. Everything to its right starts at 58.
                var track = UIKit.Node("bar", row);
                track.Anchor(UIKit.Left, new Vector2(58, -12), new Vector2(322, 12));
                var fill = UIKit.Bar(track, Theme.TrackGlass, Theme.Green, 6);
                fill.transform.parent.GetComponent<RectTransform>().Stretch();

                var lab = UIKit.LabelOutlined(row, "", 16, Color.white, TextAnchor.MiddleLeft);
                lab.rectTransform.Anchor(UIKit.Left, new Vector2(58, 9), new Vector2(322, 22));

                _lockRows.Add((fill, lab));
            }
        }

        /// <summary>Repaint the lock state. Cheap enough to call from RenderAll, which is what
        /// makes the counter on a locked island move while the player farms a different one.</summary>
        public void RenderLock()
        {
            if (islandIndex == 0) return;
            bool locked = Locked;
            if (_sLocked != (locked ? 1 : 0))
            {
                _sLocked = locked ? 1 : 0;
                if (_lockVeil != null) _lockVeil.gameObject.SetActive(locked);
                if (_lockSign != null) _lockSign.gameObject.SetActive(locked);
                // The field stays visible under the veil — an island with its future beds staked
                // out reads as a place to work toward; hiding them left a bare green plate. It is
                // just not touchable: no plot on an island you do not own may open a popup.
                if (_plotRaycaster != null) _plotRaycaster.enabled = !locked && _lodFarm;
                _labelLayer.gameObject.SetActive(!locked);
            }
            if (!locked || _lockSign == null) return;

            var s = Ctx.owner;
            var def = IslandSys.Def(islandIndex);

            string head = s.lv < def.lv
                ? "Mở ở cấp " + def.lv + "  ·  " + Fmt.N(def.coin) + " xu"
                : Fmt.N(def.coin) + " xu  ·  " + def.perk;
            if (_sLockText != head) { _lockLine.text = head; _sLockText = head; }

            for (int i = 0; i < _lockRows.Count && i < def.tribute.Length; i++)
            {
                var t = def.tribute[i];
                int paid = IslandSys.Paid(s, islandIndex, t.crop);
                var seed = GameData.Get(t.crop);
                _lockRows[i].bar.fillAmount = Mathf.Clamp01(paid / (float)t.need);
                string line = (seed != null ? seed.name : t.crop) + "  " + Fmt.N(paid) + " / " + Fmt.N(t.need);
                if (_lockRows[i].label.text != line) _lockRows[i].label.text = line;
            }
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

            // Each island has its own painting (Tools/gen_islands.py): outline, ground, cliff and
            // details all differ, so six islands in a row read as six places. The offset puts
            // the island's top surface — not the sprite's centre — on the field origin.
            var island = UIKit.Img(holder, IslandSprite(islandIndex), Color.white, "island");
            island.rectTransform.Anchor(UIKit.Center, new Vector2(0, -87), new Vector2(1144, 832));
            _land = island;

            // Snow cover for snowy hours, cut from this island's own top surface.
            var snowSprite = Art.Load("Art/islands/island_" + (islandIndex % IslandStyles) + "_snow");
            if (snowSprite != null)
            {
                _snowCap = UIKit.Img(holder, snowSprite, new Color(1, 1, 1, 0), "snow");
                _snowCap.rectTransform.Anchor(UIKit.Center, new Vector2(0, -87), new Vector2(1144, 832));
                _snowCap.raycastTarget = false;
            }
        }

        const int IslandStyles = 6;
        static Sprite IslandSprite(int index) { return Art.Load("Art/islands/island_" + (index % IslandStyles)); }

        /// <summary>Props take a little of the island's colour, so a haystack on the snowfield
        /// is frosted and a rock on the volcano is basalt, instead of six islands sharing one
        /// set of sunny-meadow props.</summary>
        static Color PropTint(int index)
        {
            switch (index % IslandStyles)
            {
                case 1:  return new Color(1.00f, 0.98f, 0.90f);   // wind: sun-bleached
                case 2:  return new Color(0.86f, 0.93f, 1.00f);   // ice: frosted
                case 3:  return new Color(0.78f, 0.68f, 0.64f);   // fire: scorched
                case 4:  return new Color(0.86f, 0.86f, 1.00f);   // storm: cool
                case 5:  return new Color(1.00f, 0.95f, 0.82f);   // gold: warm
                default: return Color.white;
            }
        }

        UnityEngine.UI.Image _land, _snowCap;

        /// <summary>Weather on the land: the island's own identity tint multiplied by the hour's,
        /// plus the snow cover. Beds get a lighter version of the same tint, so a storm darkens
        /// the soil too instead of leaving sixteen sunlit squares on a grey island.</summary>
        public void ApplyWeatherLook(Color tint, float snow)
        {
            if (_land != null) _land.color = tint;
            if (_snowCap != null)
            {
                var c = _snowCap.color; c.a = snow * 0.92f; _snowCap.color = c;
                if (_snowCap.gameObject.activeSelf != (snow > 0.01f)) _snowCap.gameObject.SetActive(snow > 0.01f);
            }
            var soil = Color.Lerp(Color.white, tint, 0.6f);
            soil = Color.Lerp(soil, new Color(0.93f, 0.96f, 1f), snow * 0.5f);
            soil.a = 1f;
            for (int i = 0; i < _views.Length; i++)
                if (_views[i] != null && _views[i].tile != null) _views[i].tile.color = soil;
            _soilTint = soil;
        }

        Color _soilTint = Color.white;

        void BuildPlots()
        {
            var anim = FieldAnimator.Ensure(gameObject);

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

                // PlotPress scales the root on touch, so the fixed PlotScale lives one level down.
                var body = UIKit.Node("body", root);
                body.Anchor(UIKit.Center, Vector2.zero, new Vector2(TW, TH));
                body.localScale = Vector3.one * PlotScale;


                var v = new PlotView { root = root };
                v.sway = (i * 137 % 1000) / 1000f * 2.8f;

                v.tile = UIKit.Img(body, Art.Bed("empty", i), Color.white, "tile");
                v.tile.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));

                // State on the ground. Ripe and thirsty used to be a 44 px badge floating over the
                // plant — a check mark or a droplet standing exactly where the crop is, on every
                // plot that needed anything, so a busy field was a wall of icons hiding its own
                // harvest. The bed now says it: parched, cracked soil with a blue rim when a window
                // opens; warm soil, a gold rim and glints when ripe. Both lights breathe slowly,
                // drawn on the same canvas as the tile and below the crop.
                v.rim = UIKit.Img(body, Art.Load("Art/beds/bed_glow_ready"), Color.white, "rim");
                v.rim.raycastTarget = false;
                v.rim.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                anim.Add(v.rim.rectTransform, FieldAnimator.Motion.Breathe, v.sway, v.rim);
                v.rim.gameObject.SetActive(false);

                // glints on a ripe bed's soil, also under the crop
                v.sparkle = UIKit.Img(body, Art.Load("Art/beds/bed_sparkle"), Color.white, "sparkle");
                v.sparkle.raycastTarget = false;
                v.sparkle.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                anim.Add(v.sparkle.rectTransform, FieldAnimator.Motion.Breathe, v.sway + 1.3f, v.sparkle);
                v.sparkle.gameObject.SetActive(false);

                // Countdown at the bed's FRONT CORNER, small, and UNDER the crops: drawn in the plot's
                // own body before the plant, so a crop — this plot's or the one in front — always
                // covers the chip and never the other way round. Centred above everything, it sat
                // across the stems of the field.
                v.timerBox = UIKit.Node("timer", body);
                v.timerBox.Anchor(UIKit.Center, new Vector2(0, -HH * 0.72f), new Vector2(78, 24));
                var tb = UIKit.Round(v.timerBox, new Color(0.09f, 0.16f, 0.13f, 0.66f), 12, "bg");
                tb.rectTransform.Stretch();
                v.timer = UIKit.Label(v.timerBox, "", 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                v.timer.rectTransform.Stretch();
                v.timerBox.gameObject.SetActive(false);
                v.glow = UIKit.Img(body, Theme.Glow(), new Color(1, 1, 1, 0), "glow");
                v.glow.rectTransform.Anchor(UIKit.Center, new Vector2(0, 46), new Vector2(150, 150));

                // crop art stands on the tile axis and grows upward
                v.crop = UIKit.Img(body, null, Color.white, "crop");
                v.crop.preserveAspect = true;
                v.crop.rectTransform.anchorMin = v.crop.rectTransform.anchorMax = UIKit.Center;
                v.crop.rectTransform.pivot = new Vector2(0.5f, 0f);
                v.crop.rectTransform.anchoredPosition = new Vector2(0, -8);
                v.crop.enabled = false;
                anim.Add(v.crop.rectTransform, FieldAnimator.Motion.Sway, v.sway, v.crop);

                v.fruit = UIKit.Img(body, null, Color.white, "fruit");
                v.fruit.preserveAspect = true;
                v.fruit.rectTransform.Anchor(UIKit.Center, new Vector2(0, 34), new Vector2(46, 46));
                v.fruit.enabled = false;


                // tap target sized to the diamond's bounding box
                var hit = UIKit.Node("hit", body);
                hit.Anchor(UIKit.Center, Vector2.zero, new Vector2(184, 134));
                var hitIm = hit.gameObject.AddComponent<Image>();
                hitIm.color = new Color(0, 0, 0, 0);

                // NOT a Button: it would fire its click after a drag of any length, so panning
                // the map across a plot would plant on it. See TapOrDrag.
                var tap = hit.gameObject.AddComponent<TapOrDrag>();
                tap.onTap = () => onPlotTapped?.Invoke(idx);
                tap.map = map;

                // the press handler has to live on the same object as the pointer handlers,
                // and drive the plot root itself
                var press = hit.gameObject.AddComponent<PlotPress>();
                press.target = root;
                // Rect hit areas overlap their neighbours heavily on a touching grid; the
                // diamond filter makes a tap land on the bed the finger is actually on. The
                // bounds are the cell exactly now, and the skirt is the field rim's few pixels.
                var dh = hit.gameObject.AddComponent<DiamondHit>();
                dh.halfW = HW; dh.halfH = HH; dh.skirt = 6f;

                _views[i] = v;
            }
        }

        /// <summary>The two back fence runs, chained post to post along lines parallel to the
        /// field's back edges.
        ///
        /// The segments come from Tools/gen_fences.py, sheared so their rails run at the field's
        /// own 0.5 slope; each segment is pivoted on its near post's base and the next one starts
        /// exactly where the previous one's far post stands, so the posts coincide instead of
        /// landing a hand-tuned ~20 px apart. The right-hand run is the left-hand run MIRRORED —
        /// using the same unflipped sprite on both sides is what made the right fence zigzag
        /// against its own direction. Lanterns end each run at the outer post.</summary>
        void BuildFences()
        {
            var fence = Art.Load("Art/gen/fence_iso");
            var lamp = Art.Load("Art/gen/fence_lamp_iso");
            if (fence == null || lamp == null) return;

            // measured by gen_fences.py, in source pixels (x right, y up from the base)
            var fencePivot = new Vector2(0.1299f, 0.0838f);
            var fenceStep = new Vector2(111f, 55.5f);
            var lampPivot = new Vector2(0.0622f, 0.4451f);
            var lampStep = new Vector2(100f, -50f);
            const float Scale = 0.68f;                    // world units per source pixel
            const int Segments = 4;

            // back edges of the field, pushed out onto the grass
            float halfW = HW * PlotScale * 4f, halfH = HH * PlotScale * 4f;
            // OuterX 390: at 420 the lantern post hung past the grass rim, over the sea
            const float Gap = 34f, OuterX = 390f;

            for (int side = -1; side <= 1; side += 2)
            {
                // the outer post of this run, on the line y = halfH + Gap - |x|/2
                var outer = new Vector2(side * OuterX, halfH + Gap - OuterX * (halfH / halfW));
                bool mirror = side > 0;

                // far segments first, so nearer ones draw over them
                for (int k = Segments - 1; k >= 0; k--)
                {
                    var step = new Vector2(fenceStep.x * (mirror ? -1f : 1f), fenceStep.y) * Scale;
                    PlaceSegment(fence, fencePivot, outer + step * k, Scale, mirror);
                }
                // lantern hangs off the outer end, running on outward and down
                PlaceSegment(lamp, lampPivot, outer, Scale, !mirror);
            }
        }

        void PlaceSegment(Sprite sp, Vector2 pivot, Vector2 at, float scale, bool mirror)
        {
            var im = UIKit.Img(_decoBack, sp, PropTint(islandIndex), "fence");
            im.preserveAspect = true;
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = UIKit.Center;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(sp.rect.width, sp.rect.height) * scale;
            rt.anchoredPosition = at;
            if (mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
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
            // Fences are placed by BuildFences, after these — the haystack stands BEHIND the
            // fence, so it has to be drawn first. These are the loose props. The haystack sits
            // BEHIND the right-hand fence now — at its old spot it stood on the fence line itself.
            var props = new (string art, float x, float y, float w)[]
            {
                ("haystack",    300, 196, 144),  // the one large mass, stored yield
                ("rock",        392, 150, 104),  // clusters into the haystack's base
                ("signpost",   -359, -158, 96),  // outer gatepost
                ("banner",     -173, -256, 72),  // focal point: nearest, tallest, only cloth
                ("rock",        213, -235, 110), // counterweight, keeps the apron balanced
            };

            // farther back draws first within each layer
            System.Array.Sort(props, (p, q) => q.y.CompareTo(p.y));

            // Every island drew this exact arrangement, so a row of them read as a tiling bug
            // rather than an archipelago — the single loudest "cheaply generated" signal in the
            // whole map view. The composition is good and stays; what varies is which side the
            // large masses fall on, and which of the small props are present.
            //
            // Seeded on the island index, so an island looks the same every time it is drawn.
            var rng = new System.Random(9173 + islandIndex * 31);
            bool mirror = (islandIndex & 1) == 1;

            foreach (var p in props)
            {
                var sp = Art.Farm(p.art);
                if (sp == null) continue;

                float x = mirror ? -p.x : p.x;

                // thin out the small scatter, never the fence line or the focal banner
                bool optional = p.art == "rock" || p.art == "signpost";
                if (optional && islandIndex > 0 && rng.Next(100) < 35) continue;

                float w = p.w * (0.92f + (float)rng.NextDouble() * 0.16f);
                float h = w * sp.rect.height / sp.rect.width;

                // anything anchored below the horizon stands in front of the plots
                var layer = p.y < 0f ? _decoFront : _decoBack;
                var im = UIKit.Img(layer, sp, PropTint(islandIndex), "prop_" + p.art);
                im.preserveAspect = true;

                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = UIKit.Center;
                rt.pivot = new Vector2(0.5f, 0f);            // ground contact, not centre
                rt.anchoredPosition = new Vector2(x, p.y);
                rt.sizeDelta = new Vector2(w, h);
            }

            BuildFences();
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
        // rendering
        // ============================================================
        public void RenderAll()
        {
            RenderLock();
            for (int i = 0; i < GS.PlotCount; i++) RenderPlot(i);
        }

        public void RenderPlot(int i)
        {
            var v = _views[i];
            if (v == null) return;
            var p = i < Plots.Count ? Plots[i] : null;
            PlotState st = PlotLogic.State(p);

            // The bed carries the whole state now, in priority order:
            //   ripe     warm soil, gold rim, glints          — collect me
            //   thirsty  parched cracked soil, blue rim       — a watering window is open
            //   watered  dark wet soil                        — watered at least once
            //   growing  plain tilled soil
            // "Watered" still means "has been watered", not "is growing": used for every growing
            // plot, untended crops looked permanently wet and watering seemed to do nothing. And
            // thirsty is DRY soil with a blue light, not blue soil — blue ground reads as wet on
            // the one state that means dry.
            string tileKey = st == PlotState.Locked  ? (Locked ? "unclaimed" : "locked")
                           : st == PlotState.Ready   ? "ready"
                           : st == PlotState.Thirsty ? "thirsty"
                           : (p != null && p.cut > 0f) ? "watered"
                           : "empty";

            if (v.sState != st || v.sTile != tileKey)
            {
                v.tile.sprite = Art.Bed(tileKey, i);
                // The locked bed is already a mostly-transparent patch over the island's own
                // grass, so it needs no extra fade (the old stone tile did).
                v.tile.color = _soilTint;
                v.sState = st; v.sTile = tileKey;
            }

            if (st == PlotState.Locked || st == PlotState.Empty)
            {
                SetRim(v, "");
                if (v.sCrop != null || v.sStage != -1)
                {
                    v.crop.enabled = false; v.crop.sprite = null;
                    v.fruit.enabled = false; v.fruit.sprite = null;
                    v.glow.color = new Color(1, 1, 1, 0);
                    SetRim(v, "");
                    v.timerBox.gameObject.SetActive(false);
                    v.sCrop = null; v.sStage = -1; v.sVariant = -1; v.sTimer = null;
                }
                return;
            }

            var seed = GameData.Get(p.crop);
            if (seed == null) return;
            int stage = PlotLogic.StageOf(p);
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

            SetRim(v, st == PlotState.Ready ? "ready" : st == PlotState.Thirsty ? "water" : "");

            // ---- countdown chip ----
            if (st == PlotState.Ready)
            {
                if (v.timerBox.gameObject.activeSelf) { v.timerBox.gameObject.SetActive(false); v.sTimer = null; }
            }
            else
            {
                if (v.timerBox.gameObject.activeSelf != _lodFarm) v.timerBox.gameObject.SetActive(_lodFarm);
                string txt = Fmt.Time(PlotLogic.Remain(p));
                if (txt != v.sTimer) { v.timer.text = txt; v.sTimer = txt; }
            }
        }

        static void SetRim(PlotView v, string kind)
        {
            if (v.sRim == kind) return;
            v.sRim = kind;
            bool on = kind.Length > 0;
            v.rim.gameObject.SetActive(on);
            v.sparkle.gameObject.SetActive(kind == "ready");
            if (on) v.rim.sprite = Art.Load(kind == "ready" ? "Art/beds/bed_glow_ready" : "Art/beds/bed_glow_water");
        }

        /// <summary>Farm level: plots are tappable and their chips are readable. Anything below
        /// it: neither. See ArchipelagoView.ApplyLod for why this is a hard switch and not a fade.</summary>
        public void SetLod(bool farmLevel)
        {
            if (_lodFarm == farmLevel) return;
            _lodFarm = farmLevel;
            // never re-arm taps on an island the player does not own
            if (_plotRaycaster != null) _plotRaycaster.enabled = farmLevel && !Locked;
            for (int i = 0; i < _views.Length; i++)
            {
                var v = _views[i];
                if (v == null) continue;
                // chips carry the only text on the field; the bed art carries the state
                if (v.timerBox != null && v.timerBox.gameObject.activeSelf != (farmLevel && v.sTimer != null))
                    v.timerBox.gameObject.SetActive(farmLevel && v.sTimer != null);
            }
        }

        public List<int> TickingPlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < Plots.Count; i++)
            {
                var p = Plots[i];
                if (p != null && !string.IsNullOrEmpty(p.crop) && PlotLogic.State(p) != PlotState.Ready) outp.Add(i);
            }
            return outp;
        }

        public List<int> ReadyPlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < Plots.Count; i++)
                if (PlotLogic.State(Plots[i]) == PlotState.Ready) outp.Add(i);
            return outp;
        }

        /// <summary>Plots whose watering window is open right now. Windows close on their own, so
        /// this is genuinely a "do it in the next forty seconds" list rather than a backlog.</summary>
        public List<int> WaterablePlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < Plots.Count; i++)
            {
                var p = Plots[i];
                if (p == null || p.locked || string.IsNullOrEmpty(p.crop)) continue;
                if (PlotLogic.State(p) == PlotState.Ready) continue;
                if (WaterSys.WindowOpen(p, out _)) outp.Add(i);
            }
            return outp;
        }

        /// <summary>Unlocked, empty, ready to take a seed.</summary>
        public List<int> EmptyPlots()
        {
            var outp = new List<int>();
            for (int i = 0; i < GS.PlotCount && i < Plots.Count; i++)
                if (Plots[i] != null && !Plots[i].locked && PlotLogic.State(Plots[i]) == PlotState.Empty)
                    outp.Add(i);
            return outp;
        }

        // ============================================================
        // actions
        // ============================================================
        public bool Plant(int i, string seedId)
        {
            if (!Ctx.Can(FarmPerm.Plant)) return false;
            var p = Plots[i];
            if (p == null || p.locked || !string.IsNullOrEmpty(p.crop)) return false;
            var seed = GameData.Get(seedId);
            // the seed leaves the actor's bag; everything else is a property of the land
            if (seed == null || !Ctx.actor.TakeSeed(seedId, 1)) return false;

            // Everything situational is resolved HERE and stamped on the plot. After this the
            // crop's value is fixed: the weather may turn and the tag set may rotate, and neither
            // can reach back and change what this planting is worth.
            var w = WeatherSys.Now(Ctx.owner);

            // A greenhouse charge is spent only when the weather would actually hurt, and it is
            // spent HERE — before anything is stamped. The hour itself is kept: shelter floors
            // each axis in the player's favour rather than swapping the weather for Nắng, so a
            // sheltered planting during a storm still gets the storm's +60% mutation odds.
            bool sheltered = ShopSys.UseGreenhouse(Ctx.owner, w);

            // Order matters: the tier is rolled first, because it stretches the duration, and
            // the duration decides how many watering windows the crop gets.
            int variant = Ctx.owner.RollVariantFor(seedId, w, sheltered);

            p.crop = seedId;
            p.plantedAt = GS.Now;
            p.variant = variant;
            p.dur = Ctx.owner.GrowTimeIn(seed, w, variant, sheltered);
            p.cut = 0;
            p.waterMask = 0;
            p.friendMask = 0;
            p.windowCount = (byte)WaterSys.WindowsFor(p.dur);
            p.plantWeather = (byte)w;
            Ctx.owner.ResolveSituational(seedId, w, out p.sellMul, out p.xpMul, sheltered);

            // Rain waters the field for you: the first window is granted at planting. It saves
            // sixteen taps and is the one weather that rewards a player for simply being there.
            if (w == Weather.Rain)
            {
                int k = WaterSys.NextUnusedWindow(p);
                if (k > 0) WaterSys.Consume(p, k, byVisitor: false);
            }

            Ctx.actor.TrackCrop("plant", seedId, 1);
            Ctx.owner.AddEnergy(Ctx.owner.EnergyGain(1));
            RenderPlot(i);
            Puff(i);
            return true;
        }

        /// <summary>The one action a visitor is allowed to perform, so it is also the one place
        /// the owner/actor split is not hypothetical: the time comes off the owner's crop and the
        /// energy goes to the owner, but the mission credit belongs to whoever tapped.</summary>
        public bool Water(int i)
        {
            if (!Ctx.Can(FarmPerm.Water)) return false;
            var p = Plots[i];
            if (p == null || PlotLogic.State(p) != PlotState.Thirsty) return false;
            if (!WaterSys.WindowOpen(p, out int window)) return false;

            int pct = WaterSys.CutPercent(p);
            WaterSys.Consume(p, window, byVisitor: !Ctx.IsOwn);

            Ctx.actor.Track("water", 1);
            Ctx.owner.AddEnergy(Ctx.owner.EnergyGain(2));
            RenderPlot(i);
            Droplets(i);
            Burst(i, "-" + pct + "%", Theme.Hex("#8FD8FF"));
            return true;
        }

        /// <summary>Water without waiting for the window to open — what the golden watering can
        /// sells. It still consumes a real window, so the 20% cap is untouched: the item buys
        /// convenience, never extra growth.</summary>
        public bool ForceWater(int i)
        {
            if (!Ctx.Can(FarmPerm.Water)) return false;
            var p = Plots[i];
            if (p == null || PlotLogic.State(p) == PlotState.Ready) return false;
            int k = WaterSys.NextUnusedWindow(p);
            if (k == 0) return false;

            int pct = WaterSys.CutPercent(p);
            WaterSys.Consume(p, k, byVisitor: !Ctx.IsOwn);
            Ctx.actor.Track("water", 1);
            Ctx.owner.AddEnergy(Ctx.owner.EnergyGain(2));
            RenderPlot(i);
            Droplets(i);
            Burst(i, "-" + pct + "%", Theme.Hex("#8FD8FF"));
            return true;
        }

        public struct HarvestResult
        {
            public Seed seed;
            public int variant;
            public int xp;
            public int fruits;
            /// <summary>Coins paid on the spot for weather and tag. Produce pools in the
            /// warehouse under one price per crop, so a per-plot multiplier cannot survive into
            /// it — and it should not have to. The bonus is paid where it was earned, at the
            /// moment the player sees the plot they timed well come up.</summary>
            public int bonusCoins;
            public Weather weather;
            public CropTag tag;
        }

        public bool Harvest(int i, out HarvestResult res)
        {
            res = default;
            if (!Ctx.Can(FarmPerm.Harvest)) return false;
            var p = Plots[i];
            if (p == null || PlotLogic.State(p) != PlotState.Ready) return false;
            var seed = GameData.Get(p.crop);
            if (seed == null) return false;
            int v = p.variant;

            // harvesting is owner-only (a visitor gets Steal, not Harvest), so the goods and the
            // xp go to the owner; only the mission credit follows the actor
            int xp = Mathf.RoundToInt(Ctx.owner.XpFor(seed, v) * Mathf.Max(0.01f, p.xpMul));
            int fruits = Ctx.owner.YieldOf(seed, v);
            Ctx.owner.AddProduce(p.crop, v, fruits);
            Ctx.owner.AddXp(xp);
            Ctx.owner.AddEnergy(Ctx.owner.EnergyFor(seed, v));
            Ctx.actor.TrackCrop("harvest", p.crop, 1);
            if (v > 0) Ctx.actor.Track("mutate", 1);

            int bonus = Mathf.RoundToInt(Ctx.owner.HarvestValue(p.crop, v) * (Mathf.Max(1f, p.sellMul) - 1f));
            if (bonus > 0) Ctx.owner.AddCoin(bonus);

            var plantedIn = (Weather)Mathf.Clamp(p.plantWeather, 0, WeatherSys.All.Length - 1);
            var tag = TagSys.TagOf(Ctx.owner, p.crop, p.plantedAt);

            var el = Art.Elem(v);
            FlyToStore(i, Art.Icon(seed.art, v), Art.VariantTint(seed.art, v));
            Burst(i, bonus > 0 ? "+" + Fmt.N(bonus) : "+" + xp + " XP",
                  bonus > 0 ? Theme.Hex("#FFD45E") : (el.hasGlow ? el.glow : Theme.Hex("#FFE9A8")));
            Sparkle(i, v > 0 ? 18 : 9, el.hasGlow ? el.glow : Theme.Hex("#FFE9A8"));

            p.crop = null; p.plantedAt = 0; p.variant = 0;
            p.cut = 0; p.waterMask = 0; p.friendMask = 0; p.windowCount = 0;
            p.plantWeather = 0; p.sellMul = 1f; p.xpMul = 1f;
            RenderPlot(i);

            res = new HarvestResult
            {
                seed = seed, variant = v, xp = xp, fruits = fruits,
                bonusCoins = bonus, weather = plantedIn, tag = tag,
            };
            return true;
        }

        public bool InstantGrow(int i)
        {
            var p = Plots[i];
            if (p == null || string.IsNullOrEmpty(p.crop)) return false;
            p.cut = p.dur;
            RenderPlot(i);
            Sparkle(i, 12, Theme.Hex("#FFD45E"));
            return true;
        }

        /// <summary>Screen position of a plot, for flying rewards toward the HUD.</summary>
        // ============================================================
        // selection — the plot the seed sheet is planting into
        // ============================================================
        UnityEngine.UI.Image _select;

        /// <summary>Outline one plot, or pass -1 to clear. Lives in the label layer so it stays
        /// visible over the crops of the row in front — it is the thing the player is looking
        /// for after every planting.</summary>
        public void SetSelected(int i)
        {
            if (i < 0)
            {
                if (_select != null) _select.gameObject.SetActive(false);
                return;
            }
            if (_select == null)
            {
                _select = UIKit.Img(_labelLayer, Art.Load("Art/beds/bed_select"), Color.white, "select");
                _select.raycastTarget = false;
                _select.rectTransform.SetAsFirstSibling();
                FieldAnimator.Ensure(gameObject).Add(_select.rectTransform, FieldAnimator.Motion.Breathe, 0f, _select);
            }
            var rt = _select.rectTransform;
            rt.anchorMin = rt.anchorMax = UIKit.Center;
            rt.pivot = new Vector2(0.5f, TileAxis);
            rt.sizeDelta = new Vector2(TileArtW, TileArtH) * PlotScale;
            rt.anchoredPosition = CellPos(i);
            _select.gameObject.SetActive(true);
        }

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
            node.Anchor(UIKit.Center, CellPos(i) + new Vector2(0, 60) * PlotScale, new Vector2(200, 40));
            var t = UIKit.LabelOutlined(node, text, 28, color);
            t.rectTransform.Stretch();
            Tween.FloatText(node, 66f, 1.05f);
        }

        public void Sparkle(int i, int n, Color color)
        {
            var at = CellPos(i) + new Vector2(0, 40) * PlotScale;
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
            var at = CellPos(i) + new Vector2(0, 8) * PlotScale;
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
            var at = CellPos(i) + new Vector2(0, 96) * PlotScale;
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
            var from = CellPos(i) + new Vector2(0, 50) * PlotScale;
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
}
