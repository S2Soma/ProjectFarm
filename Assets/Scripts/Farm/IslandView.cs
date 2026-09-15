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
        public const float StepX = HW * PlotScale, StepY = HH * PlotScale;

        // ---- island shape, in grid cells from the field centre (the field spans ±2) ----
        // The island is the field's own grid grown by a yard and rounded: a rounded square in
        // cells, which the isometric view draws as a diamond with edges parallel to the beds.
        // Tools/gen_islands.py paints it from the same numbers — change both together.
        public const float RimCells = 3.0f, RimRound = 0.45f;
        /// <summary>The fence runs round all four sides this far out, leaving a gate at the left
        /// and right corners where the cloud bridges come in.</summary>
        public const float FenceCells = 2.72f, GateCells = 0.7f;
        /// <summary>The painted sprite's rect: 2000x1278 px at 0.72 world units per px, its centre
        /// 94 below the field centre.</summary>
        static readonly Vector2 IslandSpriteSize = new Vector2(1440f, 920.16f);
        const float IslandSpriteY = -94f;
        /// <summary>The painting's rect, for layers drawn over it (IslandLife's light masks).</summary>
        public static Vector2 IslandSpriteRect => IslandSpriteSize;
        public const float IslandSpriteCentreY = IslandSpriteY;

        public static Vector2 GridPoint(float u, float v) { return new Vector2((u - v) * StepX, -(u + v) * StepY); }
        /// <summary>How far out the left and right tips of the grass reach.</summary>
        public static float TipX => (2f * (RimCells - RimRound) + 1.41421f * RimRound) * StepX;
        /// <summary>The x of the lantern posts either side of a gate.</summary>
        public static float GateX => (2f * FenceCells - GateCells) * StepX;
        /// <summary>World units per source pixel of the fence art, from the run length.</summary>
        public static float FenceScale => (2f * FenceCells - GateCells) * StepX / (FenceSegments * PostPitchPx);
        /// <summary>How far out the gate post's lantern arm reaches (the sprite's outer edge).</summary>
        public static float LanternOuterX => GateX + (97f - 19.5f) * FenceScale;

        /// <summary>Signed distance, in cells, from a field-space point to the island's rim
        /// (negative on the grass). The same rounded square Tools/gen_islands.py paints.</summary>
        public static float RimDistance(Vector2 p)
        {
            float u = (p.x / StepX - p.y / StepY) * 0.5f;
            float v = (-p.x / StepX - p.y / StepY) * 0.5f;
            float qx = Mathf.Abs(u) - (RimCells - RimRound);
            float qy = Mathf.Abs(v) - (RimCells - RimRound);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - RimRound;
        }

        /// <summary>Where a cloud bridge lands: on the grass between the gate and the tip.</summary>
        public static float BridgeLandX => (GateX + TipX) * 0.5f;
        const float TileArtW = TW * 390f / 380f;
        const float TileArtH = TileArtW * 320f / 390f;
        const float TileAxis = 0.475f;               // diamond axis, measured from the art's bottom edge

        public RectTransform Field { get; private set; }
        RectTransform _plotLayer, _labelLayer, _fxLayer, _decoBack, _decoFront;
        /// <summary>The island's ambient life (see <see cref="IslandLife"/>): its ground layer sits on the
        /// painting under the snow; its creatures live on two small canvases, one under the plots and
        /// one over the front fence, so a butterfly moving rebuilds only them.</summary>
        RectTransform _ground, _behind, _lifeBack, _lifeFront;
        IslandLife _life;
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
            public MutationFx fx;
            /// <summary>The worn plot skin's border (Trang trí ▸ Ô đất), over the bed, under everything else.</summary>
            public Image skin;
            /// <summary>Frost on the rim and snow in the furrow (Art/beds/bed_frost), shown while snow lies.</summary>
            public Image frost;
            public MutationTint cropTint, fruitTint;
            /// <summary>The thirsty signal (owner, 15/9): a water drop hopping on the ground at the front-left of
            /// the crop, in the label layer so the crops of the row in front never cover it, and a brighter rim
            /// with light running round it on the bed. Both animate in shaders (MiT/UI Thirst, Thirst Rim).</summary>
            public LifeQuads thirst;
            public Image thirstRim;
            public bool sThirst;
            public string sCrop, sTile, sRim = "";
            public int sStage = -1, sVariant = -1;
            public PlotState sState = (PlotState)255;
            public float sway;
            public Color cropBase = Color.white;
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
            _lifeBack = UIKit.Node("lifeBack", Field); _lifeBack.Stretch();
            _lifeBack.gameObject.AddComponent<Canvas>();
            _plotLayer = UIKit.Node("plots", Field);   _plotLayer.Stretch();
            // Its own canvas + raycaster, so zooming out can disable plot hit-testing for the
            // whole island in one assignment instead of touching sixteen colliders.
            var plotCanvas = _plotLayer.gameObject.AddComponent<Canvas>();
            plotCanvas.overrideSorting = false;
            _plotRaycaster = _plotLayer.gameObject.AddComponent<GraphicRaycaster>();
            _decoFront = UIKit.Node("decoFront", Field); _decoFront.Stretch();
            _lifeFront = UIKit.Node("lifeFront", Field); _lifeFront.Stretch();
            _lifeFront.gameObject.AddComponent<Canvas>();
            // ABOVE every plot: the selection outline (the bed being planted stays visible over the crops
            // of the row in front), the "Cấp N" chip on the next plot, and the thirsty drops — with beds
            // touching, a crop two rows in front stands up far enough to cover anything drawn on its bed.
            _labelLayer = UIKit.Node("labels", Field); _labelLayer.Stretch();
            _thirstLayer = UIKit.Node("thirst", _labelLayer); _thirstLayer.Stretch();
            _fxLayer = UIKit.Node("fx", Field);        _fxLayer.Stretch();

            _life = new IslandLife(this, IslandSys.Def(islandIndex).style);
            BuildIsland();
            BuildPlots();
            BuildScenery();
            _life.Build(_ground, _behind, _lifeBack, _lifeFront);
            BuildLockSign();
            RenderLock();
            Recolour();
        }

        /// <summary>Advance this island's creatures (ArchipelagoView calls it once a frame).
        /// <paramref name="visible"/>: on screen and close enough to see them. A locked island under its
        /// veil stays still.</summary>
        public void StepLife(float t, float dt, bool visible)
        {
            _life?.Step(t, dt, visible && !Locked);
            // the drops are shader-animated, but an island off screen or a map pulled far back has no use for them
            if (_thirstLayer != null && _thirstLayer.gameObject.activeSelf != visible) _thirstLayer.gameObject.SetActive(visible);
        }

        RectTransform _thirstLayer;

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
            veil.rectTransform.Anchor(UIKit.Center, new Vector2(0, IslandSpriteY), IslandSpriteSize);
            veil.raycastTarget = false;

            var def = IslandSys.Def(islandIndex);
            int rows = def.tribute.Length;

            _lockSign = UIKit.Node("lockSign", _decoFront);
            _lockSign.Anchor(UIKit.Center, new Vector2(0, 34), new Vector2(430, 88f + rows * 40f));
            SurfaceLook.Add(_lockSign, Looks.Glass, 26f);

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
            holder.Anchor(UIKit.Center, Vector2.zero, new Vector2(1, 1));

            // The island carries a soil body, so its base sits far below the field — the shadow
            // and foam belong down at that waterline, and wider than the island, or they hide
            // behind it. A soft oval under a diamond is right: it is a shadow, not an outline.
            // A faint shadow on the cloud sea far below. No foam ring: the islands float, and a
            // white oval under each one said they were sitting in water.
            var shade = UIKit.Img(holder, Theme.Glow(), Theme.Hex("#1A2550").Alpha(0.10f), "cloudShade");
            shade.rectTransform.Anchor(UIKit.Center, new Vector2(0, -540), new Vector2(1640, 380));

            // the sky right behind this island (Đảo Băng's aurora): the painting covers its foot
            _behind = UIKit.Node("behind", holder);
            _behind.Anchor(UIKit.Center, Vector2.zero, Vector2.one);

            // Each island has its own painting (Tools/gen_islands.py): outline, ground, cliff and
            // details all differ, so six islands in a row read as six places. The offset puts
            // the island's top surface — not the sprite's centre — on the field origin.
            var island = UIKit.Img(holder, IslandSprite(islandIndex), Color.white, "island");
            island.rectTransform.Anchor(UIKit.Center, new Vector2(0, IslandSpriteY), IslandSpriteSize);
            _land = island;

            // the island's moving ground (a river, lava light, glints) — on the painting, under the snow
            _ground = UIKit.Node("ground", holder);
            _ground.Anchor(UIKit.Center, Vector2.zero, Vector2.one);

            // Snow cover for snowy hours, cut from this island's own top surface.
            var snowSprite = Art.Load("Art/islands/island_" + IslandSys.Def(islandIndex).style + "_snow");
            if (snowSprite != null)
            {
                _snowCap = UIKit.Img(holder, snowSprite, new Color(1, 1, 1, 0), "snow");
                _snowCap.rectTransform.Anchor(UIKit.Center, new Vector2(0, IslandSpriteY), IslandSpriteSize);
                _snowCap.raycastTarget = false;
            }
        }

        static Sprite IslandSprite(int index) { return Art.Load("Art/islands/island_" + IslandSys.Def(index).style); }

        /// <summary>Props take a little of the island's colour, so a haystack on the snowfield
        /// is frosted and a rock on the volcano is basalt, instead of six islands sharing one
        /// set of sunny-meadow props.</summary>
        static Color PropTint(int index)
        {
            switch (IslandSys.Def(index).style)
            {
                case 1:  return new Color(1.00f, 0.98f, 0.90f);   // wind: sun-bleached
                case 2:  return new Color(0.86f, 0.93f, 1.00f);   // ice: frosted
                case 3:  return new Color(0.78f, 0.68f, 0.64f);   // fire: scorched
                case 4:  return new Color(0.86f, 0.86f, 1.00f);   // storm: cool
                case 5:  return new Color(1.00f, 0.95f, 0.82f);   // gold: warm
                default: return Color.white;
            }
        }

        /// <summary>The painted decor props carry their island's ground in their own art (a snow, ash, slate or sand
        /// skirt), so they take only a third of the island's identity tint — the full scorch on Đảo Hoả turned
        /// them to mud.</summary>
        static Color DecorTint(Color islandTint) { var c = Color.Lerp(Color.white, islandTint, 0.35f); c.a = 1f; return c; }

        UnityEngine.UI.Image _land, _snowCap;

        /// <summary>Weather on the land: the island's own identity tint multiplied by the hour's,
        /// plus the snow cover. Beds get a lighter version of the same tint, so a storm darkens
        /// the soil too instead of leaving sixteen sunlit squares on a grey island.</summary>
        public void ApplyWeatherLook(Color tint, float snow)
        {
            _weatherTint = tint;
            _snow = snow;
            Recolour();
        }

        /// <summary>The time of day's light on this island (see DayCycle): multiplied over the
        /// weather tint on the land, the beds, the crops and every prop. The lantern glow, the bed
        /// state lights and the thirsty drop are NOT tinted — they are light sources and readouts, and
        /// dimming them at night would hide exactly what night needs to show.</summary>
        public void SetAmbient(Color land, Color crop)
        {
            if (land == _ambient && crop == _cropAmbient) return;
            _ambient = land;
            _cropAmbient = crop;
            Recolour();
        }

        Color _weatherTint = Color.white, _ambient = Color.white, _cropAmbient = Color.white;
        float _snow;

        void Recolour()
        {
            var a = _ambient; a.a = 1f;
            var landLit = _weatherTint * a; landLit.a = 1f;
            if (_land != null) _land.color = landLit;

            // Snow is painted opaque where it lies (gen_islands.py paint_snow, gen_snow.py), so the
            // weather only fades it in and out: alpha IS the amount of snow. It takes the hour's light,
            // cooled toward moonlight as the light falls, never the weather's own tint.
            bool snowing = _snow > 0.01f;
            var snowCol = SnowLight(a); snowCol.a = _snow;
            if (_snowCap != null)
            {
                _snowCap.color = snowCol;
                if (_snowCap.gameObject.activeSelf != snowing) _snowCap.gameObject.SetActive(snowing);
            }
            foreach (var cap in _snowCaps)
            {
                if (cap == null) continue;
                cap.color = snowCol;
                if (cap.gameObject.activeSelf != snowing) cap.gameObject.SetActive(snowing);
            }

            // The soil stays soil under snow: dark and readable, with frost only on the rims and
            // snow in the furrows (bed_frost). Whitening the soil itself washed the crops out.
            var soil = Color.Lerp(Color.white, _weatherTint, 0.6f);
            soil = Color.Lerp(soil, new Color(0.90f, 0.93f, 1f), _snow * 0.08f);
            soil *= a;
            soil.a = 1f;
            _soilTint = soil;
            for (int i = 0; i < _views.Length; i++)
            {
                var v = _views[i];
                if (v == null) continue;
                if (v.tile != null) v.tile.color = soil;
                if (v.skin != null) v.skin.color = a;
                // crops take less of the dark than the land (DayCycle.FloorAmbient)
                if (v.crop != null) v.crop.color = v.cropBase * _cropAmbient.Alpha(1f);
                if (v.fruit != null) v.fruit.color = v.cropBase * _cropAmbient.Alpha(1f);
                RenderFrost(v);
            }
            var prop = PropTint(islandIndex) * a;
            prop.a = 1f;
            foreach (var im in _sceneryImages) if (im != null) im.color = prop;
            var decor = DecorTint(PropTint(islandIndex)) * a;
            decor.a = 1f;
            foreach (var im in _decorImages) if (im != null) im.color = decor;
            _life?.Recolour(landLit, prop, _snow);
        }

        /// <summary>Snow under the hour's light: white by day, a cool moonlit blue as the light falls.</summary>
        static Color SnowLight(Color ambient)
        {
            float lum = 0.299f * ambient.r + 0.587f * ambient.g + 0.114f * ambient.b;
            float dusk = Mathf.Clamp01((1f - lum) * 1.8f);
            var c = ambient * Color.Lerp(Color.white, new Color(0.80f, 0.87f, 1.0f), dusk);
            c.a = 1f;
            return c;
        }

        /// <summary>Frost on an open bed while snow lies; never on a locked patch of lawn.</summary>
        void RenderFrost(PlotView v)
        {
            if (v.frost == null) return;
            bool on = _snow > 0.01f && v.sState != PlotState.Locked;
            if (v.frost.gameObject.activeSelf != on) v.frost.gameObject.SetActive(on);
            if (on) v.frost.color = SnowLight(_ambient).Alpha(_snow);
        }

        readonly List<Image> _snowCaps = new List<Image>();

        Color _soilTint = Color.white;
        readonly List<Image> _sceneryImages = new List<Image>();
        readonly List<Image> _decorImages = new List<Image>();

        void BuildPlots()
        {
            var anim = FieldAnimator.Ensure(gameObject);

            // back rows first so the ones nearer the camera draw over them
            var order = new List<int>();
            for (int i = 0; i < GS.PlotCount; i++) order.Add(i);
            order.Sort((a, b) => Depth(a).CompareTo(Depth(b)));

            foreach (int i in order)
            {
                // the river and the cells under a big plot are not land
                if (IslandSys.KindOf(islandIndex, i) == PlotKind.None) continue;
                int idx = i;
                var pos = SlotPos(i);
                float size = SlotSize(i);
                var root = UIKit.Node("plot" + i, _plotLayer);
                root.Anchor(UIKit.Center, pos, new Vector2(TW, TH) * size);

                // PlotPress scales the root on touch, so the fixed PlotScale lives one level down.
                // A big plot is the same bed drawn twice the size: its crop, rim, thirsty drop and
                // tap area all grow with it.
                var body = UIKit.Node("body", root);
                body.Anchor(UIKit.Center, Vector2.zero, new Vector2(TW, TH));
                body.localScale = Vector3.one * PlotScale * size;


                var v = new PlotView { root = root };
                v.sway = (i * 137 % 1000) / 1000f * 2.8f;

                v.tile = UIKit.Img(body, Art.Bed("empty", i), Color.white, "tile");
                v.tile.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                v.skin = UIKit.Img(body, null, Color.white, "skin");
                v.skin.raycastTarget = false;
                v.skin.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                v.skin.enabled = false;
                v.frost = UIKit.Img(body, Art.Load("Art/beds/bed_frost"), new Color(1, 1, 1, 0), "frost");
                v.frost.raycastTarget = false;
                v.frost.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                v.frost.gameObject.SetActive(false);

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

                // Thirsty: its own rim light, stronger than the ripe one and animated in its shader (light runs
                // round the bed), so it is not on FieldAnimator and never touches the canvas while it plays.
                v.thirstRim = UIKit.Img(body, Art.Load("Art/beds/bed_glow_thirst"), Color.white, "thirstRim");
                v.thirstRim.raycastTarget = false;
                v.thirstRim.material = ThirstRimMat;
                v.thirstRim.rectTransform.Anchor(UIKit.Center,
                    new Vector2(0, (0.5f - TileAxis) * TileArtH), new Vector2(TileArtW, TileArtH));
                v.thirstRim.gameObject.SetActive(false);

                v.thirst = BuildThirstBadge(i, pos, size);

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

                v.fx = MutationFx.Build(body, v.glow, v.fruit, Vector2.zero, new Vector2(TW * 0.86f, TH * 0.86f), v.sway);
                v.cropTint = v.crop.gameObject.AddComponent<MutationTint>();
                v.fruitTint = v.fruit.gameObject.AddComponent<MutationTint>();


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

        /// <summary>One piece of scenery: a fence post, rail, gate post or prop, placed by its
        /// ground-contact point so every piece on the island can be depth-sorted together.</summary>
        struct Piece
        {
            public Sprite sprite;
            public Vector2 pivot;     // normalised; may lie outside 0..1 (rails hang off a post)
            public Vector2 at;        // ground contact, field-local
            public Vector2 size;
            public bool mirror;
            public float depth;       // screen y of the ground contact; larger = farther back
            public string name;
            public bool painted;      // a decor-sheet prop: takes a milder share of the island's tint
        }

        // Measured on Art/gen/fence_iso.png and fence_lamp_iso.png by Tools/gen_fences.py:
        // the fence's left post stands at column 22.5 with its base on row 160 of 167, and the
        // next post along is 108 px right. The pieces are cropped from those sprites.
        const float PostPitchPx = 108f;
        static readonly Vector2 PostPivot = new Vector2(22.5f / 44f, 7f / 167f);
        static readonly Vector2 RailPivot = new Vector2((22.5f - 42f) / 72f, 7f / 167f);
        static readonly Vector2 GatePivot = new Vector2((115.5f - 96f) / 97f, 16f / 182f);
        /// <summary>The lantern's flame, measured on fence_gate.png: 56.5 px out from the post
        /// base and 75 px up (column 76, row 91 against a pivot at 19.5, 166).</summary>
        static readonly Vector2 LanternFromPostPx = new Vector2(56.5f, 75.2f);
        /// <summary>Every flame on the island that lights up at night: the gate lanterns, and the lamps and fires of
        /// the painted props (DecorCatalog.lights). Size is the glow's width; fires burn more orange.</summary>
        readonly List<(Vector2 at, float size, bool fire)> _lanterns = new List<(Vector2, float, bool)>();
        readonly List<Image> _lanternGlows = new List<Image>();
        readonly List<Color> _lanternCols = new List<Color>();
        static readonly Color LampGlow = new Color(1f, 0.78f, 0.38f), FireGlow = new Color(1f, 0.56f, 0.22f);
        const int FenceSegments = 8;

        /// <summary>A fence round all four sides of the island, with a lantern gate at the left
        /// and right corners.
        ///
        /// Built from single posts and rails, one post per node. It used to chain whole painted
        /// segments, each with a post at both ends; the two posts meeting at every joint never
        /// quite coincided, so the whole fence read as pairs of posts. It also only ran along the
        /// back two edges of an oval island, which left the haystack and rocks standing behind it
        /// — outside the farm, on the cliff edge.
        ///
        /// Every run is the same length (a corner to a gate), so one segment count and one scale
        /// fit all four and each node lands exactly on the edge line. The gates face the bridges:
        /// a bridge arrives at a corner, so the fence opens there.</summary>
        void AddFence(List<Piece> pieces)
        {
            var post = Art.Load("Art/gen/fence_post");
            var rail = Art.Load("Art/gen/fence_rail");
            var gate = Art.Load("Art/gen/fence_gate");
            if (post == null || rail == null || gate == null) return;

            float F = FenceCells, g = GateCells;
            float runX = (2f * F - g) * StepX;                 // horizontal span of one run
            float scale = runX / (FenceSegments * PostPitchPx);

            // Each run is listed from its LOWER end, the way its rails hang: an unmirrored rail
            // rises to the right, a mirrored one rises to the left.
            var runs = new (Vector2 from, Vector2 to, bool mirror, bool gateAtStart)[]
            {
                (GridPoint(-F, F - g), GridPoint(-F, -F), false, true),   // back-left:  left gate -> top
                (GridPoint(F - g, -F), GridPoint(-F, -F), true,  true),   // back-right: right gate -> top
                (GridPoint(F, F),      GridPoint(-F + g, F), true,  false), // front-left: bottom -> left gate
                (GridPoint(F, F),      GridPoint(F, -F + g), false, false), // front-right: bottom -> right gate
            };

            var placed = new HashSet<Vector2Int>();
            bool river = IslandSys.Def(islandIndex).layout == IslandLayout.River;
            foreach (var r in runs)
            {
                for (int k = 0; k <= FenceSegments; k++)
                {
                    var at = Vector2.Lerp(r.from, r.to, k / (float)FenceSegments);
                    bool isGate = r.gateAtStart ? k == 0 : k == FenceSegments;

                    // Đảo Nước: the river leaves through the front-right fence, so that run opens
                    // where the water crosses it — no post standing in the waterfall
                    if (river && !isGate)
                    {
                        var mid = k < FenceSegments ? Vector2.Lerp(r.from, r.to, (k + 0.5f) / FenceSegments) : at;
                        bool railInRiver = k < FenceSegments && InRiverGap(mid);
                        bool postInRiver = InRiverGap(at);
                        if (railInRiver && postInRiver) continue;
                        if (postInRiver)
                        {
                            if (k < FenceSegments && !railInRiver)
                                pieces.Add(new Piece
                                {
                                    sprite = rail, pivot = RailPivot, at = at, mirror = r.mirror, name = "rail",
                                    size = new Vector2(rail.rect.width, rail.rect.height) * scale, depth = at.y + 0.5f,
                                });
                            continue;
                        }
                        if (railInRiver)
                        {
                            var key0 = new Vector2Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y));
                            if (placed.Add(key0))
                                pieces.Add(new Piece
                                {
                                    sprite = post, pivot = PostPivot, at = at, mirror = r.mirror, name = "post",
                                    size = new Vector2(post.rect.width, post.rect.height) * scale, depth = at.y,
                                });
                            continue;
                        }
                    }

                    if (k < FenceSegments)
                        pieces.Add(new Piece
                        {
                            sprite = rail, pivot = RailPivot, at = at, mirror = r.mirror, name = "rail",
                            size = new Vector2(rail.rect.width, rail.rect.height) * scale,
                            // a hair farther back than its own post, so the post covers its end
                            depth = at.y + 0.5f,
                        });

                    // The top and bottom corners belong to two runs: one post, not two.
                    var key = new Vector2Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y));
                    if (!isGate && !placed.Add(key)) continue;

                    if (isGate)
                    {
                        bool outLeft = at.x < 0f;
                        pieces.Add(new Piece
                        {
                            sprite = gate, pivot = GatePivot, at = at, name = "gate",
                            size = new Vector2(gate.rect.width, gate.rect.height) * scale,
                            // lanterns hang outward, over the path the bridge arrives on
                            mirror = outLeft, depth = at.y,
                        });
                        var flame = new Vector2(LanternFromPostPx.x * (outLeft ? -1f : 1f), LanternFromPostPx.y) * scale;
                        _lanterns.Add((at + flame, 170f, false));
                    }
                    else
                        pieces.Add(new Piece
                        {
                            sprite = post, pivot = PostPivot, at = at, mirror = r.mirror, name = "post",
                            size = new Vector2(post.rect.width, post.rect.height) * scale,
                            depth = at.y,
                        });
                }
            }
        }

        /// <summary>Grid cells (u, v) of a field-space point: the inverse of <see cref="GridPoint"/>.</summary>
        public static Vector2 CellOf(Vector2 p)
        {
            return new Vector2((p.x / StepX - p.y / StepY) * 0.5f, (-p.x / StepX - p.y / StepY) * 0.5f);
        }

        /// <summary>Whether a point on the front-right fence line stands in the river's mouth.</summary>
        static bool InRiverGap(Vector2 p)
        {
            var c = CellOf(p);
            return c.x > 0f && Mathf.Abs(c.y) < IslandSys.RiverHalf + 0.18f;
        }

        /// <summary>Cosmetic yard plantings (see <see cref="Cosmetics"/>): id, art, grid spot, width.
        /// Both stand in the back yard, where nothing they are drawn over is a crop.</summary>
        public static readonly (string id, string art, float u, float v, float w)[] Decor =
        {
            ("dc_roses",      "decor_roses",      -2.34f,  0.22f, 104f),
            ("dc_sunflowers", "decor_sunflowers", -1.05f, -2.36f, 96f),
        };

        readonly Dictionary<string, Image> _decor = new Dictionary<string, Image>();

        /// <summary>Show the owner's worn decoration and hide the rest.</summary>
        public void RenderDecor()
        {
            string worn = Cosmetics.Worn(Ctx.owner, CosmeticSlot.Decor);
            foreach (var kv in _decor)
            {
                bool on = kv.Key == worn;
                if (kv.Value.gameObject.activeSelf != on) kv.Value.gameObject.SetActive(on);
            }
        }

        /// <summary>Every island's yard, one table (owner, 15/9: "decor phải giống với đảo" — each island's props
        /// belong to that island). Keyed by the island's painting (<see cref="IslandDef.style"/>), in grid cells
        /// (beds span ±2, the fence runs at ±<see cref="FenceCells"/>), and mirrored with the rest of the scenery
        /// on odd islands (<see cref="Mirrored"/>).
        ///
        /// <c>art</c>: a painted prop cut from the owner's decor sheets (Resources/Art/decor, Tools/slice_decor.py,
        /// measured in <see cref="DecorCatalog"/>) — on an island whose ground is not grass, its recoloured
        /// skirt (<see cref="GroundOf"/>) is used when the prop has one; <c>farm:</c> the older painted props
        /// (Art/farm); <c>life:</c> the props with a moving part drawn by Tools/gen_life.py (vents, lightning rods,
        /// the flag, pines, the treasure) — kept only where no painted prop does the job.
        /// <c>w</c>: drawn width. <c>low</c>: short enough (≤ 80 tall) for the front yard, where anything stands in
        /// front of the beds.
        ///
        /// The yard rules (IslandTest "vật riêng từng đảo"): inside the fence and off the beds, clear of the
        /// gates the bridges come in by, nothing tall in the front yard or in the top corner (a 16:9 screen cuts
        /// it at farm zoom), no two props on one island standing on each other, not in Đảo Nước's river or on its
        /// bank beds, and not on Vườn Nhà's two shop decorations (<see cref="Decor"/>).</summary>
        public static readonly (int style, string art, float u, float v, float w, bool low)[] Places =
        {
            // ---- 0 Vườn Nhà: a home garden — the sign and the hay by the left gate, a well greeting the right one,
            //      a birdhouse, and small garden things along the front ----
            (0, "farm:signpost",  -2.38f,  1.45f,  86f, false),
            (0, "farm:haystack",  -2.36f, -1.05f, 128f, false),
            (0, "watering_can",   -2.40f, -2.02f,  58f, true),
            (0, "birdhouse_post",  0.00f, -2.40f,  58f, false),
            (0, "well",            1.15f, -2.38f, 112f, false),
            (0, "farm:rock",      -0.95f,  2.40f,  62f, true),
            (0, "pumpkin_crate",   0.75f,  2.40f,  70f, true),
            (0, "flower_barrow",   2.40f, -0.70f,  92f, true),
            (0, "mailbox",         2.40f,  1.30f,  50f, true),
            // ---- 6 Đảo Nước: the river is the show — a lantern tub by the spring, a lily pond and a bird bath
            //      by the falls; the beds reach the fence elsewhere ----
            // (the front yard's props hide behind the front fence here, so the lily pond takes the sign's place at the back)
            (6, "lily_pond",      -2.38f,  1.40f, 100f, false),
            (6, "water_tub",      -2.38f, -1.35f,  84f, false),
            // not nearer the beds than 2.42: the pets' path from the right gate runs between the beds and this bird bath
            (6, "bird_bath_g",     2.42f, -1.35f,  64f, true),
            // ---- 7 Khổng Lồ: a giant toadstool, a blueberry bush, a glowing pod, mushrooms on a stump ----
            (7, "farm:signpost",  -2.38f,  1.45f,  86f, false),
            (7, "giant_shroom",   -2.38f, -0.30f, 120f, false),
            (7, "stump_shrooms",  -2.40f, -1.75f,  84f, false),
            (7, "blueberry",      -0.55f, -2.38f,  92f, false),
            (7, "glow_pod",        0.75f, -2.38f,  70f, false),
            (7, "farm:banner",     1.60f, -2.38f,  64f, false),
            (7, "berry_basket",    0.20f,  2.40f,  62f, true),
            (7, "farm:rock",       2.40f, -0.60f,  62f, true),
            // ---- 1 Đảo Gió: the windmill, hay, a scarecrow, a signpost pointing the way, the flag flying ----
            (1, "arrow_sign",     -2.38f,  1.45f,  64f, false),
            (1, "mill",           -2.38f,  0.15f, 132f, false),
            (1, "hay_barrel",     -2.38f, -1.25f, 118f, false),
            (1, "life:flagpole",  -0.40f, -2.40f,  30f, false),
            (1, "scarecrow",       0.75f, -2.38f,  84f, false),
            (1, "farm:banner",     1.65f, -2.38f,  64f, false),
            (1, "sunflower_fence", 2.40f,  0.50f,  96f, true),
            (1, "farm:rock",      -0.90f,  2.40f,  62f, true),
            // ---- 2 Đảo Băng: snowed-in pines, a well, crystals turned to ice, lamps for the long nights ----
            (2, "sign_leaf",      -2.38f,  1.45f,  80f, false),
            (2, "life:pine",      -2.38f,  0.35f,  92f, false),
            (2, "well",           -2.38f, -0.95f, 112f, false),
            (2, "life:pine",      -1.45f, -2.40f,  78f, false),
            (2, "crystals",       -0.20f, -2.38f,  96f, false),
            (2, "banner_lamp",     1.45f, -2.38f,  72f, false),
            (2, "mossy_rock",      0.45f,  2.40f,  66f, true),
            (2, "crystals",        2.40f, -0.60f,  58f, true),
            // ---- 3 Đảo Hoả: vents breathing smoke, a clay oven and a campfire, all on ash ----
            (3, "sign_leaf",      -2.38f,  1.45f,  80f, false),
            (3, "life:vent",      -2.40f,  0.20f,  88f, true),
            (3, "clay_oven",      -2.38f, -1.10f, 118f, false),
            (3, "life:vent",      -0.30f, -2.40f,  84f, true),
            (3, "campfire",        0.75f, -2.38f,  92f, false),
            (3, "banner_lamp",     1.60f, -2.38f,  72f, false),
            (3, "life:vent",       2.40f, -0.90f,  70f, true),
            (3, "mossy_rock",      0.40f,  2.40f,  62f, true),
            (3, "barrel",         -1.00f,  2.40f,  46f, true),
            // ---- 4 Đảo Lôi: two lightning rods arcing, charged crystals, lamps on slate ----
            (4, "sign_leaf",      -2.38f,  1.45f,  80f, false),
            (4, "life:rod",       -2.40f,  0.60f,  50f, false),
            (4, "life:rod",       -2.40f, -0.20f,  50f, false),
            (4, "rock_lamp",      -2.38f, -1.35f,  96f, false),
            (4, "crystals",       -0.35f, -2.38f, 100f, false),
            (4, "lamp_iron",       0.55f, -2.38f,  50f, false),
            (4, "banner_lamp",     1.50f, -2.38f,  72f, false),
            (4, "crystals",        2.40f,  0.55f,  62f, true),
            (4, "mossy_rock",      0.30f,  2.40f,  62f, true),
            // ---- 5 Đảo Vàng: a market stall and a cart on gold sand, the treasure, spilled coins ----
            (5, "sign_leaf",      -2.38f,  1.45f,  80f, false),
            (5, "market",         -2.38f,  0.10f, 118f, false),
            (5, "veg_cart",       -2.38f, -1.20f, 104f, false),
            (5, "life:chest",     -0.30f, -2.40f, 100f, false),
            (5, "lamp_iron",       0.85f, -2.38f,  50f, false),
            (5, "banner_lamp",     1.65f, -2.38f,  72f, false),
            (5, "life:coins",      0.50f,  2.40f,  66f, true),
            (5, "life:coins",      2.40f, -1.00f,  58f, true),
            (5, "barrel",          2.40f,  0.60f,  46f, true),
        };

        /// <summary>For readers written against the two tables Places replaced (PetPaths): there are no props shared by
        /// every island any more, so this is empty, and <see cref="StyleProps"/> is all of Places with the art's
        /// source prefix dropped ("life:rod" → "rod"). New code should read Places or <see cref="PropsOn"/>.</summary>
        public static readonly (string art, float u, float v, float w)[] Props = new (string, float, float, float)[0];
        public static readonly (int style, string art, float u, float v, float w, bool low)[] StyleProps = StylePropsView();

        static (int, string, float, float, float, bool)[] StylePropsView()
        {
            var list = new (int, string, float, float, float, bool)[Places.Length];
            for (int i = 0; i < Places.Length; i++)
            {
                var p = Places[i];
                SplitArt(p.art, out _, out var name);
                list[i] = (p.style, name, p.u, p.v, p.w, p.low);
            }
            return list;
        }

        /// <summary>The ground a prop's skirt is recoloured to on an island painting, or null for grass.</summary>
        public static string GroundOf(int style)
        {
            switch (style)
            {
                case 2: return "snow";
                case 3: return "ash";
                case 4: return "slate";
                case 5: return "sand";
                default: return null;
            }
        }

        /// <summary>A prop's source and name: "farm:rock" → (Art/farm/, rock), "vent" → (Art/decor/, vent).</summary>
        public static void SplitArt(string art, out string source, out string name)
        {
            int c = art.IndexOf(':');
            source = c < 0 ? "decor" : art.Substring(0, c);
            name = c < 0 ? art : art.Substring(c + 1);
        }

        /// <summary>The sprite path a place draws on an island of <paramref name="style"/>, ground variant included.</summary>
        public static string SpritePath(string art, int style)
        {
            SplitArt(art, out var source, out var name);
            if (source == "farm") return "Art/farm/" + name;
            if (source == "life") return "Art/life/prop_" + name;
            string ground = GroundOf(style);
            if (ground != null && DecorCatalog.All.TryGetValue(name, out var e) && e.grounds != null)
            {
                // Đảo Băng's crystals are ice, not violet
                if (ground == "snow" && System.Array.IndexOf(e.grounds, "ice") >= 0) return "Art/decor/" + name + "_ice";
                if (System.Array.IndexOf(e.grounds, ground) >= 0) return "Art/decor/" + name + "_" + ground;
            }
            return "Art/decor/" + name;
        }

        /// <summary>The props standing on island <paramref name="islandIndex"/>, in grid cells with this island's mirror
        /// applied, each with the rough radius of its footprint in cells — for anything that has to walk round
        /// them (pets) or keep off them (IslandLife's plants).</summary>
        public static List<(string art, Vector2 cell, float radius)> PropsOn(int islandIndex)
        {
            var list = new List<(string, Vector2, float)>();
            var def = IslandSys.Def(islandIndex);
            bool mirror = (islandIndex & 1) == 1 && def.layout != IslandLayout.River;
            foreach (var p in Places)
            {
                if (p.style != def.style) continue;
                SplitArt(p.art, out _, out var name);
                float foot = DecorCatalog.All.TryGetValue(name, out var e) ? e.footW : 0.8f;
                // mirroring x in the iso view swaps the two grid axes
                var cell = mirror ? new Vector2(p.v, p.u) : new Vector2(p.u, p.v);
                list.Add((p.art, cell, foot * p.w * 0.5f / (1.414f * StepX)));
            }
            if (islandIndex == 0)
                foreach (var d in Decor) list.Add(("decor_" + d.id, new Vector2(d.u, d.v), d.w * 0.4f / (1.414f * StepX)));
            return list;
        }

        /// <summary>Odd islands (except the river) draw their scenery mirrored left to right.</summary>
        public bool Mirrored => (islandIndex & 1) == 1 && IslandSys.Def(islandIndex).layout != IslandLayout.River;

        /// <summary>A yard spot in field space, mirrored the way this island's scenery is.</summary>
        public Vector2 YardPoint(float u, float v)
        {
            var at = GridPoint(u, v);
            if (Mirrored) at.x = -at.x;
            return at;
        }

        void BuildScenery()
        {
            var layout = IslandSys.Def(islandIndex).layout;
            // the river's banks run along one axis: mirroring would lay the props across it
            bool mirror = (islandIndex & 1) == 1 && layout != IslandLayout.River;
            int style = IslandSys.Def(islandIndex).style;

            var pieces = new List<Piece>();
            foreach (var p in Places)
            {
                if (p.style != style) continue;
                SplitArt(p.art, out var source, out var name);
                var sp = Art.Load(SpritePath(p.art, style));
                if (sp == null) continue;
                var at = GridPoint(p.u, p.v);
                if (mirror) at.x = -at.x;
                bool painted = source == "decor" && DecorCatalog.All.TryGetValue(name, out _);
                var entry = painted ? DecorCatalog.All[name] : default;
                var size = new Vector2(p.w, p.w * sp.rect.height / sp.rect.width);
                pieces.Add(new Piece
                {
                    sprite = sp, pivot = new Vector2(0.5f, painted ? entry.pivotY : 0f), at = at,
                    name = "prop_" + name, size = size, depth = at.y, painted = painted,
                });
                if (painted && entry.lights != null)
                    foreach (var l in entry.lights)
                    {
                        var lit = at + new Vector2((l.at.x - 0.5f) * size.x, (l.at.y - entry.pivotY) * size.y);
                        _lanterns.Add((lit, l.fire ? size.x * 1.5f : Mathf.Max(90f, size.x * 1.25f), l.fire));
                    }
            }

            // Yard decorations bought in the shop, on the home island only. Built always and
            // shown by the owner's worn item, so wearing one does not rebuild the scenery.
            if (islandIndex == 0)
                foreach (var d in Decor)
                {
                    var sp = Art.Item(d.art);
                    if (sp == null) continue;
                    var at = GridPoint(d.u, d.v);
                    pieces.Add(new Piece
                    {
                        sprite = sp, pivot = new Vector2(0.5f, 0.06f), at = at, name = "decor_" + d.id,
                        size = new Vector2(d.w, d.w * sp.rect.height / sp.rect.width), depth = at.y,
                    });
                }

            AddFence(pieces);

            // Farther back first. Anything whose ground contact is behind the field's centre line
            // is drawn under the beds; everything nearer is drawn over them.
            pieces.Sort((a, b) => b.depth.CompareTo(a.depth));
            var tint = PropTint(islandIndex);
            foreach (var pc in pieces)
            {
                var layer = pc.depth < 0f ? _decoFront : _decoBack;
                var im = UIKit.Img(layer, pc.sprite, pc.painted ? DecorTint(tint) : tint, pc.name);
                (pc.painted ? _decorImages : _sceneryImages).Add(im);
                if (pc.name.StartsWith("decor_")) _decor[pc.name.Substring(6)] = im;
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = UIKit.Center;
                rt.pivot = pc.pivot;
                rt.sizeDelta = pc.size;
                rt.anchoredPosition = pc.at;
                if (pc.mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
                AddSnowCap(im, pc.name);
                // a windmill's blades, a vent's smoke: moving parts go in right after their prop
                _life?.AttachToProp(pc.name, im, layer);
            }

            // swaying plants: behind the beds after every back piece, in front before every front piece
            _life?.BuildPlants(_decoBack, _decoFront);

            // Lantern light for the evening: a warm pool round each flame, invisible by day. All of
            // them on one nested canvas over the island, so the flicker rewrites only these.
            _lights = UIKit.Node("lights", Field);
            _lights.Stretch();
            _lights.gameObject.AddComponent<Canvas>();
            foreach (var l in _lanterns)
            {
                var col = l.fire ? FireGlow : LampGlow;
                var glow = UIKit.Img(_lights, Theme.Glow(), col.Alpha(0f), l.fire ? "fireGlow" : "lanternGlow");
                glow.rectTransform.Anchor(UIKit.Center, l.at, new Vector2(l.size, l.size * (150f / 170f)));
                glow.raycastTarget = false;
                _lanternGlows.Add(glow);
                _lanternCols.Add(col);
            }
            _lights.gameObject.SetActive(false);
        }

        /// <summary>The snow that settles on a piece of scenery (Art/snow/*_snow, Tools/gen_snow.py): the
        /// same rect as the piece, grown upward by the art's padding, because a mound rises above a
        /// sprite cropped to its own silhouette. A child of the piece, so it keeps its mirror and its
        /// place in the depth order. Hidden until snow falls.</summary>
        void AddSnowCap(Image piece, string name)
        {
            string key = name == "post" ? "fence_post" : name == "rail" ? "fence_rail" : name == "gate" ? "fence_gate"
                       : name.StartsWith("prop_") ? name.Substring(5) : null;
            if (key == null || piece.sprite == null) return;
            // A painted prop's grass skirt goes under the snow too (Art/snow/<name>_skirt, Tools/slice_decor.py) — unless the
            // prop already stands on a snow skirt (Đảo Băng's variants).
            var skirt = Art.Load("Art/snow/" + key + "_skirt");
            string spName = piece.sprite.name;
            if (skirt != null && !spName.EndsWith("_snow") && !spName.EndsWith("_ice"))
            {
                var sk = UIKit.Img(piece.rectTransform, skirt, new Color(1, 1, 1, 0), "snowSkirt");
                sk.raycastTarget = false;
                sk.rectTransform.anchorMin = Vector2.zero;
                sk.rectTransform.anchorMax = Vector2.one;
                sk.rectTransform.offsetMin = sk.rectTransform.offsetMax = Vector2.zero;
                sk.gameObject.SetActive(false);
                _snowCaps.Add(sk);
            }
            var cap = Art.Load("Art/snow/" + key + "_snow");
            if (cap == null) return;
            float grow = cap.rect.height / Mathf.Max(1f, piece.sprite.rect.height) - 1f;
            var im = UIKit.Img(piece.rectTransform, cap, new Color(1, 1, 1, 0), "snow");
            im.raycastTarget = false;
            var rt = im.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 1f + grow);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            im.gameObject.SetActive(false);
            _snowCaps.Add(im);
        }

        /// <summary>How lit the lanterns are, 0 by day to 1 at night. Presentation only.</summary>
        public void SetLanternLight(float k)
        {
            _lanternK = k;
            bool on = k > 0.01f;
            if (_lights != null && _lights.gameObject.activeSelf != on) _lights.gameObject.SetActive(on);
            FlickerLanterns(Time.unscaledTime);
        }

        float _lanternK;

        /// <summary>The slow uneven flicker, once per frame while the lanterns are lit. The glows are
        /// on their own nested canvas, so this rebuilds nothing but them.</summary>
        public void FlickerLanterns(float t)
        {
            if (_lanternK <= 0.01f) return;
            for (int i = 0; i < _lanternGlows.Count; i++)
            {
                float f = ArchipelagoView.Flicker(t, i * 1.7f + islandIndex * 0.9f);
                _lanternGlows[i].color = _lanternCols[i].Alpha(0.75f * _lanternK * f);
            }
        }

        RectTransform _lights;

        // ============================================================
        // geometry
        // ============================================================
        public static Vector2 CellPos(int i)
        {
            int r = i / 4, c = i % 4;
            return new Vector2((c - r) * StepX, (3 - (c + r)) * StepY);
        }
        /// <summary>Where slot <paramref name="i"/> of THIS island sits: the grid cell, the middle of a big
        /// plot's block, or a cell pushed away from the river (<see cref="IslandSys.SlotCell"/>).</summary>
        public Vector2 SlotPos(int i) { var c = IslandSys.SlotCell(islandIndex, i); return GridPoint(c.x, c.y); }
        /// <summary>1 for a small plot, 2 for a big one.</summary>
        public float SlotSize(int i) { return IslandSys.SizeOf(islandIndex, i); }
        float Depth(int i) { var c = IslandSys.SlotCell(islandIndex, i); return c.x + c.y; }

        // ============================================================
        // rendering
        // ============================================================
        public void RenderAll()
        {
            RenderLock();
            RenderDecor();
            for (int i = 0; i < GS.PlotCount; i++) RenderPlot(i);
            RenderNextPlotHint();
        }

        public void RenderPlot(int i)
        {
            var v = _views[i];
            if (v == null) return;
            var p = i < Plots.Count ? Plots[i] : null;
            PlotState st = PlotLogic.State(p);

            // The bed carries the whole state now, in priority order:
            //   ripe     warm soil, gold rim, glints          — collect me
            //   thirsty  parched cracked soil, running blue rim, and a hopping water drop — a window is open
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
                RenderFrost(v);
            }

            // the worn plot skin lies on every open bed of the owner's farm
            var skinSprite = st == PlotState.Locked ? null : CosmeticFx.PlotSkin(Cosmetics.Worn(Ctx.owner, CosmeticSlot.Plot));
            if (v.skin.sprite != skinSprite) v.skin.sprite = skinSprite;
            if (v.skin.enabled != (skinSprite != null)) v.skin.enabled = skinSprite != null;

            SetThirst(v, st == PlotState.Thirsty);

            if (st == PlotState.Locked || st == PlotState.Empty)
            {
                SetRim(v, "");
                if (v.sCrop != null || v.sStage != -1)
                {
                    v.crop.enabled = false; v.crop.sprite = null;
                    v.fruit.enabled = false; v.fruit.sprite = null;
                    v.fx.Set(0, 0f, Color.white);
                    SetRim(v, "");
                    v.sCrop = null; v.sStage = -1; v.sVariant = -1;
                }
                return;
            }

            var seed = GameData.Get(p.crop);
            if (seed == null) return;
            int stage = PlotLogic.StageOf(p);
            // the tier shows from the first sprout: it was rolled at plant (see MutationFx)
            int variant = p.variant;

            if (v.sCrop != p.crop || v.sStage != stage || v.sVariant != variant)
            {
                var sp = Art.Plant(seed.art, stage, variant);
                v.crop.enabled = sp != null;
                v.crop.sprite = sp;
                // A mutation is recoloured and lit by MiT/UI Mutation rather than multiplied by a tint:
                // a multiply can only darken, and every tier looked like a crop going bad.
                var elT = Art.Elem(variant);
                float strength = MutationTint.StrengthFor(seed.art, variant);
                v.cropBase = Color.white;
                v.cropTint.Set(elT.glow, strength);
                v.fruitTint.Set(elT.glow, strength);
                v.crop.color = v.cropBase * _cropAmbient.Alpha(1f);
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
                    v.fruit.color = v.cropBase * _cropAmbient.Alpha(1f);
                    v.fruit.rectTransform.anchoredPosition = new Vector2(0, Art.StageHeight[stage] * 0.52f);
                }

                if (v.sStage != -1 && v.sStage != stage) Tween.PopIn(v.crop.transform, 0.3f, 0.7f);

                var el = Art.Elem(variant);
                v.fx.Set(el.hasGlow ? variant : 0, Art.StageHeight[stage], el.glow);

                v.sCrop = p.crop; v.sStage = stage; v.sVariant = variant;
            }

            SetRim(v, st == PlotState.Ready ? "ready" : "");
        }

        /// <summary>Redraw every plot whose state has moved on since it was last drawn.
        ///
        /// The countdown loop only redraws the plots in its ticking list, and that list is rebuilt
        /// whenever anything on the field changes — a harvest, a planting. A plot that ripened in
        /// the second between its last redraw and such a rebuild was dropped from the list already
        /// Ready, never drawn Ready, and sat there showing "1s" with no gold rim until something
        /// else touched it. Comparing the drawn state with the real one each tick cannot miss.</summary>
        public void RefreshStale()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                var v = _views[i];
                if (v == null) continue;
                var p = i < Plots.Count ? Plots[i] : null;
                if (PlotLogic.State(p) != v.sState) RenderPlot(i);
            }
            RenderNextPlotHint();
        }

        // ============================================================
        // the next plot to open
        // ============================================================
        RectTransform _nextHint;
        Text _nextHintText;
        int _hintSlot = -2;
        string _hintLine;

        /// <summary>A small "Cấp N" under the padlock of the locked plot the player will open next,
        /// while the level is still short. Once the level is there the plot shows nothing extra: the
        /// price is in the popup a tap opens (owner's call — the field is not a price list). Only one
        /// plot is ever labelled; labelling every lock turned the field into a table.</summary>
        void RenderNextPlotHint()
        {
            int slot = -1;
            string line = null;
            if (!Locked && Ctx.IsOwn && islandIndex < Ctx.owner.islands.Count)
            {
                var isl = Ctx.owner.islands[islandIndex];
                slot = IslandSys.NextPlotSlot(isl, islandIndex);
                if (slot >= 0)
                {
                    int open = IslandSys.OpenCount(isl);
                    int needLv = IslandSys.PlotLevel(islandIndex, open);
                    line = Ctx.owner.lv < needLv ? "Cấp " + needLv : null;
                }
            }
            if (slot == _hintSlot && line == _hintLine) return;
            _hintSlot = slot; _hintLine = line;

            if (slot < 0 || line == null)
            {
                if (_nextHint != null) _nextHint.gameObject.SetActive(false);
                return;
            }
            if (_nextHint == null)
            {
                _nextHint = UIKit.Node("nextPlot", _labelLayer);
                var look = Looks.Paper;
                look.shadow = new Color(0f, 0f, 0f, 0.3f); look.blur = 6f; look.drop = new Vector2(0f, -2f);
                look.edge = Theme.Hex("#8A6A3E"); look.edgeW = 2f;
                SurfaceLook.Add(_nextHint, look, SurfaceLook.Pill);
                _nextHintText = UIKit.Label(_nextHint, "", 19, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                _nextHintText.rectTransform.Stretch(12, 0, 12, 1);
                var cg = _nextHint.gameObject.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
            }
            _nextHint.gameObject.SetActive(true);
            _nextHintText.text = line;
            float w = Mathf.Max(72f, _nextHintText.preferredWidth + 26f);
            // the padlock is baked into the middle of bed_locked; the chip hangs just under it
            _nextHint.Anchor(UIKit.Center, SlotPos(slot) + new Vector2(0, -24f) * PlotScale * SlotSize(slot), new Vector2(w, 32f));
            Tween.PopIn(_nextHint, 0.22f, 0.7f);
        }

        static void SetRim(PlotView v, string kind)
        {
            if (v.sRim == kind) return;
            v.sRim = kind;
            bool on = kind.Length > 0;
            v.rim.gameObject.SetActive(on);
            v.sparkle.gameObject.SetActive(kind == "ready");
        }

        static void SetThirst(PlotView v, bool on)
        {
            if (v.sThirst == on) return;
            v.sThirst = on;
            if (v.thirst != null) v.thirst.gameObject.SetActive(on);
            if (v.thirstRim != null) v.thirstRim.gameObject.SetActive(on);
        }

        // ============================================================
        // the thirsty drop
        // ============================================================
        /// <summary>Where the drop stands, in the plot's 168 x 84 space: on the ground at the front-left of the
        /// crop's base, between it and the two plots in front — off this crop, and clear of theirs.</summary>
        public static readonly Vector2 ThirstFoot = new Vector2(-38f, -16f);
        /// <summary>The drop's cell, in world units (the painted drop fills 62% x 80% of it).</summary>
        const float ThirstCell = 42f, ThirstHop = 0.18f;
        /// <summary>The drop's cell in the plot's own units, for pictures of a thirsty bed drawn elsewhere.</summary>
        public const float ThirstCellUnits = ThirstCell / PlotScale;

        static Material _thirstMat, _thirstRimMat;
        static Material ThirstMat
        {
            get
            {
                if (_thirstMat != null) return _thirstMat;
                var sh = Resources.Load<Shader>("Shaders/UIThirst");
                if (sh == null) return null;
                _thirstMat = new Material(sh) { name = "UIThirst (plots)" };
                _thirstMat.SetVector("_Hop", new Vector4(ThirstHop, 1.15f, 0.08f, 1.7f));
                return _thirstMat;
            }
        }
        static Material ThirstRimMat
        {
            get
            {
                if (_thirstRimMat != null) return _thirstRimMat;
                var sh = Resources.Load<Shader>("Shaders/UIThirstRim");
                if (sh != null) _thirstRimMat = new Material(sh) { name = "UIThirstRim (plots)" };
                return _thirstRimMat;
            }
        }

        /// <summary>One plot's drop: shadow, ripple ring and drop as three quads of one mesh (see UIThirst.shader),
        /// built once and switched on and off with the plot's state.</summary>
        LifeQuads BuildThirstBadge(int i, Vector2 pos, float size)
        {
            var tex = Art.Load("Art/beds/thirst_badge");
            if (tex == null || ThirstMat == null) return null;
            var q = LifeQuads.Create(_thirstLayer, "thirst" + i, ThirstMat, tex.texture);
            float k = size > 1.5f ? 1.35f : 1f;
            var foot = pos + ThirstFoot * PlotScale * size;
            float c = ThirstCell * k;
            float phase = i * 0.23f;
            void Quad(Vector2 bl, Vector2 wh, int part)
            {
                q.Add(bl, bl + new Vector2(0, wh.y), bl + wh, bl + new Vector2(wh.x, 0),
                      new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
                      new Vector4(part, phase, 0, 0));
            }
            Quad(foot - new Vector2(c * 0.45f, c * 0.45f), new Vector2(c * 0.9f, c * 0.9f), 2);          // shadow
            Quad(foot - new Vector2(c * 0.8f, c * 0.8f), new Vector2(c * 1.6f, c * 1.6f), 0);            // ring
            // the painted drop's bottom is 5% above its cell's: the quad starts that far below the foot
            Quad(foot - new Vector2(c * 0.5f, c * 0.05f), new Vector2(c, c * (1f + ThirstHop)), 1);      // drop
            q.gameObject.SetActive(false);
            return q;
        }

        /// <summary>Farm level: plots are tappable. Anything below it: not. See ArchipelagoView.ApplyLod for why this is a hard switch and not a fade.</summary>
        public void SetLod(bool farmLevel)
        {
            if (_lodFarm == farmLevel) return;
            _lodFarm = farmLevel;
            // never re-arm taps on an island the player does not own
            if (_plotRaycaster != null) _plotRaycaster.enabled = farmLevel && !Locked;
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
            if (!PlotLogic.Plant(Ctx.owner, Ctx.actor, Plots[i], seedId)) return false;
            RenderPlot(i);
            Puff(i);
            EmitCosmetic(i, Cosmetics.Worn(Ctx.actor, CosmeticSlot.Plant));
            return true;
        }

        /// <summary>The one action a visitor is allowed to perform, so it is also the one place
        /// the owner/actor split is not hypothetical: the time comes off the owner's crop and the
        /// energy goes to the owner, but the mission credit belongs to whoever tapped.</summary>
        public bool Water(int i)
        {
            if (!Ctx.Can(FarmPerm.Water)) return false;
            int sec = PlotLogic.Water(Ctx.owner, Ctx.actor, Plots[i], byVisitor: !Ctx.IsOwn);
            if (sec < 0) return false;
            RenderPlot(i);
            WaterFx(i);
            Burst(i, WaterLabel(sec), Theme.Hex("#8FD8FF"));
            return true;
        }

        /// <summary>The floating label after a watering: the time it took off, "Sớm 16p".</summary>
        public static string WaterLabel(int seconds) { return seconds > 0 ? "Sớm " + Fmt.Time(seconds) : "Đã tưới"; }

        /// <summary>Water without waiting for the window to open — what the golden watering can
        /// sells. It still consumes a real window (<see cref="PlotLogic.WaterAhead"/>), so the floor is
        /// untouched: the item buys convenience, never extra growth.</summary>
        public bool ForceWater(int i)
        {
            if (!Ctx.Can(FarmPerm.Water)) return false;
            int sec = PlotLogic.WaterAhead(Ctx.owner, Ctx.actor, Plots[i], byVisitor: !Ctx.IsOwn);
            if (sec < 0) return false;
            RenderPlot(i);
            WaterFx(i);
            Burst(i, WaterLabel(sec), Theme.Hex("#8FD8FF"));
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
            /// <summary>The planting's locked weather × tag sell multiplier.</summary>
            public float sellMul;
        }

        /// <summary>Harvest one plot.
        ///
        /// <paramref name="sweepIndex"/> is this plot's place in a harvest-all (-1 for a single
        /// tap). A sweep turns off the per-plot readouts — sixteen floating numbers at once are a
        /// smear, and the summary card says it once — and staggers the arcs 0.04 s apart in plot
        /// order, so sixteen of them read as a cascade.</summary>
        public bool Harvest(int i, out HarvestResult res, int sweepIndex = -1)
        {
            res = default;
            if (!Ctx.Can(FarmPerm.Harvest)) return false;
            if (!PlotLogic.Harvest(Ctx.owner, Ctx.actor, Plots[i], out var o)) return false;
            var seed = o.seed;
            int v = o.variant, xp = o.xp, fruits = o.fruits, bonus = o.bonusCoins;
            float lockedSell = o.sellMul;
            var plantedIn = o.weather;
            var tag = o.tag;

            var el = Art.Elem(v);
            bool sweep = sweepIndex >= 0;
            FlyToStore(i, Art.Icon(seed.art, v), Color.white, sweep ? sweepIndex * 0.04f : -1f, seed.art, v);
            if (!sweep)
            {
                // level 1: what it earned, in the currency the timing paid in
                Burst(i, bonus > 0 ? "+" + Fmt.N(bonus) : "+" + xp + " XP",
                      bonus > 0 ? Theme.Hex("#FFD45E") : (el.hasGlow ? el.glow : Theme.Hex("#FFE9A8")));
                // level 2: why — only when the planting was timed well enough to be worth saying
                if (lockedSell >= 1.25f) MultiplierLine(i, plantedIn, lockedSell);
            }
            Sparkle(i, v > 0 ? 18 : 9, el.hasGlow ? el.glow : Theme.Hex("#FFE9A8"));
            if (v >= 2) MutationRing(i, v);
            EmitCosmetic(i, Cosmetics.Worn(Ctx.actor, CosmeticSlot.Harvest));

            RenderPlot(i);

            res = new HarvestResult
            {
                seed = seed, variant = v, xp = xp, fruits = fruits,
                bonusCoins = bonus, weather = plantedIn, tag = tag, sellMul = lockedSell,
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

        /// <summary>Thuốc lớn nhanh: take <paramref name="frac"/> of what is left off one growing plot.
        /// It adds to the cut, like watering, so the watering windows keep their schedule.</summary>
        public bool Hasten(int i, float frac)
        {
            var p = Plots[i];
            if (p == null || string.IsNullOrEmpty(p.crop) || PlotLogic.State(p) == PlotState.Ready) return false;
            p.cut += Mathf.Max(0f, p.dur - PlotLogic.Elapsed(p)) * Mathf.Clamp01(frac);
            RenderPlot(i);
            Sparkle(i, 8, Theme.Hex("#9BF07A"));
            return true;
        }

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
            rt.sizeDelta = new Vector2(TileArtW, TileArtH) * PlotScale * SlotSize(i);
            rt.anchoredPosition = SlotPos(i);
            _select.gameObject.SetActive(true);
        }

        /// <summary>The plot's root, unscaled (its art is drawn at <see cref="PlotScale"/> inside it).
        /// For the tutorial, which needs to know where on screen a plot is.</summary>
        public RectTransform PlotRoot(int i) { return i >= 0 && i < _views.Length ? _views[i]?.root : null; }

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
            node.Anchor(UIKit.Center, SlotPos(i) + new Vector2(0, 60) * PlotScale * SlotSize(i), new Vector2(200, 40));
            var t = UIKit.LabelOutlined(node, text, 28, color);
            t.rectTransform.Stretch();
            Tween.FloatText(node, 66f, 1.05f);
        }

        /// <summary>For the screenshot pass: a single harvest's readouts on a plot, touching nothing.</summary>
        public void PreviewHarvestFx(int i)
        {
            Burst(i, "+318", Theme.Hex("#FFD45E"));
            MultiplierLine(i, Weather.Wind, 1.2f * 1.35f);
            MutationRing(i, 3);
            Sparkle(i, 18, Theme.Rarity[2]);
        }

        /// <summary>Splits a planting's locked sell multiplier into its weather and tag parts. The
        /// weather part is the planting hour's own; the rest is the tag's. Sheltered plantings had
        /// their weather floored, so the split can put a little of it on the tag — harmless for a
        /// readout that exists to say "you timed this well".</summary>
        public static void SplitSell(Weather plantedIn, float sellMul, out float weather, out float tag)
        {
            weather = Mathf.Max(0.01f, WeatherSys.Def(plantedIn).sell);
            tag = sellMul / weather;
        }

        /// <summary>Two chips under the harvest number: <c>×1,2 [weather]  ×1,35 [star]</c>, never
        /// more — a player learns the multipliers from these, and a third term is noise.</summary>
        void MultiplierLine(int i, Weather plantedIn, float sellMul)
        {
            SplitSell(plantedIn, sellMul, out float w, out float t);
            var node = UIKit.Node("why", _fxLayer);
            node.Anchor(UIKit.Center, SlotPos(i) + new Vector2(0, 30) * PlotScale * SlotSize(i), new Vector2(180, 30));
            float x = 0f;
            void Chip(Sprite icon, Color tint, float mul)
            {
                if (Mathf.Abs(mul - 1f) < 0.02f) return;
                var chip = UIKit.Node("chip", node);
                chip.Anchor(UIKit.Left, new Vector2(x, 0), new Vector2(84, 28));
                var look = Looks.Glass;
                look.shadow = Color.clear;
                SurfaceLook.Add(chip, look);
                var ic = UIKit.Img(chip, icon, tint, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, new Vector2(6, 0), new Vector2(20, 20));
                UIKit.LabelOutlined(chip, "×" + Fmt.Mul(mul), 16,
                                    mul >= 1f ? Theme.Hex("#FFE27A") : Theme.Hex("#FF9C8A"), TextAnchor.MiddleLeft)
                     .rectTransform.Stretch(30, 0, 6, 1);
                x += 90f;
            }
            var wd = WeatherSys.Def(plantedIn);
            Chip(Art.WeatherIcon(plantedIn), Theme.Hex(wd.hex), w);
            Chip(Theme.Skin.StarGold, Color.white, t);
            if (x <= 0f) { Destroy(node.gameObject); return; }
            // centre the chips that were drawn
            foreach (RectTransform c in node) c.anchoredPosition += new Vector2((180f - (x - 6f)) * 0.5f, 0f);
            Tween.FloatText(node, 40f, 2.0f);
        }

        /// <summary>The escalation for a mutation, read by rarity before any text: an expanding
        /// ring in the tier's colour and a spray of rays — Băng Giá 2 px and 18 rays, Viêm Hoả
        /// 3 px and 30, Lôi Điện 4 px and 48. Ngọc Bích stays silent: its glow already says it.</summary>
        public void MutationRing(int i, int tier)
        {
            int[] rays = { 0, 0, 18, 30, 48 };
            float[] width = { 0f, 0f, 0.10f, 0.14f, 0.17f };
            tier = Mathf.Clamp(tier, 2, 4);
            var col = Theme.Rarity[Mathf.Clamp(tier - 1, 0, 3)];
            var at = SlotPos(i) + new Vector2(0, 36) * PlotScale * SlotSize(i);

            var ring = UIKit.Node("ring", _fxLayer);
            ring.Anchor(UIKit.Center, at, new Vector2(120, 120));
            var im = UIKit.Img(ring, Theme.Ring(width[tier]), col, "r");
            im.rectTransform.Stretch();
            StartCoroutine(RingRoutine(ring, im, 0.7f + tier * 0.08f));

            for (int k = 0; k < rays[tier]; k++)
            {
                float a = (k / (float)rays[tier]) * Mathf.PI * 2f + UnityEngine.Random.value * 0.2f;
                float len = 90f + UnityEngine.Random.value * 70f + tier * 20f;
                var node = UIKit.Node("ray", _fxLayer);
                node.Anchor(UIKit.Center, at, new Vector2(18, 6));
                node.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                UIKit.Img(node, Theme.Glow(), col.Alpha(0.95f), "g").rectTransform.Stretch(-6, -4, -6, -4);
                Tween.Spark(node, new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * len, 0.75f);
            }
        }

        System.Collections.IEnumerator RingRoutine(RectTransform ring, Image im, float time)
        {
            var c0 = im.color;
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                if (ring == null) yield break;
                float k = Mathf.Clamp01(e / time);
                float s = Mathf.Lerp(0.3f, 2.4f, Tween.EaseOut(k));
                ring.localScale = new Vector3(s, s * 0.62f, 1f);          // lies on the ground: squashed like the beds
                im.color = c0.Alpha(c0.a * (1f - k * k));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        /// <summary>A cosmetic trail: small sprites thrown up from a plot, tumbling as they fall away.</summary>
        public void Sparkle(int i, int n, Color color)
        {
            var at = SlotPos(i) + new Vector2(0, 40) * PlotScale * SlotSize(i);
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
            var at = SlotPos(i) + new Vector2(0, 8) * PlotScale * SlotSize(i);
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

        /// <summary>For the screenshot pass: an effect cosmetic's burst on a plot, touching nothing.</summary>
        public void PreviewCosmeticFx(int i, string id) { EmitCosmetic(i, id); }

        /// <summary>A worn effect cosmetic's burst on plot <paramref name="i"/>; nothing for null.</summary>
        void EmitCosmetic(int i, string id)
        {
            if (id == null) return;
            CosmeticFx.Emit(_fxLayer, SlotPos(i), id, PlotScale * SlotSize(i));
        }

        /// <summary>The watering splash: the worn "Hiệu ứng tưới", or the plain droplets.</summary>
        void WaterFx(int i)
        {
            var id = Cosmetics.Worn(Ctx.actor, CosmeticSlot.Water);
            if (id != null && CosmeticFx.For(id) != null) EmitCosmetic(i, id);
            else Droplets(i);
        }

        public void Droplets(int i)
        {
            var at = SlotPos(i) + new Vector2(0, 96) * PlotScale * SlotSize(i);
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

        void FlyToStore(int i, Sprite icon, Color tint, float delay = -1f, string art = null, int variant = 0)
        {
            if (icon == null) return;
            var node = UIKit.Node("fly", _fxLayer);
            var from = SlotPos(i) + new Vector2(0, 50) * PlotScale * SlotSize(i);
            node.Anchor(UIKit.Center, from, new Vector2(58, 58));
            var im = UIKit.Img(node, icon, tint, "ic");
            im.preserveAspect = true;
            im.rectTransform.Stretch();
            if (variant > 0) MutationTint.Apply(im, art, variant);

            Vector2 to = from + new Vector2(0, 130f);
            if (storeAnchorWorld != null)
                to = (Vector2)_fxLayer.InverseTransformPoint(storeAnchorWorld());

            Tween.FlyArc(node, from, to, 0.62f, 90f, null, delay);
        }
    }
}
